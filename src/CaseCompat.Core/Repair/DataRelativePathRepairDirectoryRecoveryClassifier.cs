using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairDirectoryRecoveryClassifier
{
    private enum ChildObservationState
    {
        Missing,
        Directory,
        Conflict,
        InspectionFailed
    }

    private sealed record ChildObservation(
        ChildObservationState State,
        LinuxOpenChildReadOnlyAtState? OpenState,
        LinuxOpenedDirectorySnapshotResult? Snapshot,
        string? Error
    );

    public static
        DataRelativePathRepairDirectoryRecoveryClassification
        Classify(
            DataRelativePathRepairDirectoryJournalRecord journal)
    {
        ArgumentNullException.ThrowIfNull(
            journal
        );

        string? validationError =
            DataRelativePathRepairDirectoryJournal.Validate(
                journal
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .InvalidRecord,
                journal,
                error:
                    validationError
            );
        }

        /*
         * RecoveryConflict is already a durable terminal
         * conclusion. Do not reinterpret it against the live
         * filesystem.
         */
        if (
            journal.State ==
            DataRelativePathRepairDirectoryJournalState
                .RecoveryConflict)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .RecoveryConflictTerminal,
                journal,
                error:
                    journal.RecoveryConflictReason
            );
        }

        DataRelativePathRepairDestinationParentLeaseAcquisition
            parentAcquisition =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        journal.DataRoot,
                        journal.DestinationParentSnapshot
                    );

        if (!parentAcquisition.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .DestinationParentValidationFailed,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                error:
                    parentAcquisition.Validation.Error ??
                    parentAcquisition.Validation.State.ToString()
            );
        }

        using DataRelativePathRepairValidatedDestinationParentLease
            parent =
                parentAcquisition.Lease!;

        /*
         * Directory journals deliberately require MountId in
         * their ownership evidence.
         *
         * The shared destination-parent validator predates that
         * stronger requirement and its LinuxFileIdentityResult
         * SameObjectAs() comparison does not include MountId.
         *
         * Do not change the proven file-repair validator here.
         * Instead add the stronger directory-specific check after
         * successful lease acquisition.
         */
        LinuxFileIdentityResult expectedParentIdentity =
            journal.DestinationParentSnapshot.Identity;

        LinuxFileIdentityResult? actualParentIdentity =
            parent.ActualSnapshot.Identity;

        if (
            actualParentIdentity is null ||
            !SameDirectoryObject(
                expectedParentIdentity,
                actualParentIdentity
            ))
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .DestinationParentValidationFailed,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                error:
                    "The destination parent does not match the " +
                    "complete mount-aware physical identity " +
                    "recorded by the directory journal."
            );
        }

        string finalChildName =
            Path.GetFileName(
                journal.Operation.DestinationPath
            );

        string parentPath =
            journal.DestinationParentSnapshot.PhysicalPath;

        ChildObservation final =
            InspectChild(
                parent.OpenedPath,
                finalChildName,
                Path.Combine(
                    parentPath,
                    finalChildName
                )
            );

        if (
            final.State ==
            ChildObservationState.InspectionFailed)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .DestinationInspectionFailed,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot,
                error:
                    final.Error
            );
        }

        /*
         * IntentRecorded has no prepared staging name yet.
         * Only the final destination namespace entry is relevant.
         */
        if (
            journal.State ==
            DataRelativePathRepairDirectoryJournalState
                .IntentRecorded)
        {
            return Result(
                final.State ==
                ChildObservationState.Missing
                    ? DataRelativePathRepairDirectoryRecoveryState
                        .IntentFinalMissing
                    : DataRelativePathRepairDirectoryRecoveryState
                        .IntentFinalConflict,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot,
                error:
                    final.State ==
                    ChildObservationState.Missing
                        ? null
                        : final.Error ??
                            "The final destination name is occupied " +
                            "although the durable directory journal " +
                            "has not reached Prepared state."
            );
        }

        string stagingChildName =
            journal.PreparedStagingChildName!;

        ChildObservation staging =
            InspectChild(
                parent.OpenedPath,
                stagingChildName,
                Path.Combine(
                    parentPath,
                    stagingChildName
                )
            );

        if (
            staging.State ==
            ChildObservationState.InspectionFailed)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .DestinationInspectionFailed,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    staging.OpenState,
                stagingSnapshot:
                    staging.Snapshot,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot,
                error:
                    staging.Error
            );
        }

        LinuxFileIdentityResult preparedIdentity =
            journal.PreparedDirectoryIdentity!;

        bool stagingMatches =
            staging.State ==
                ChildObservationState.Directory &&
            staging.Snapshot?.Identity is not null &&
            SameDirectoryObject(
                preparedIdentity,
                staging.Snapshot.Identity
            );

        bool finalMatches =
            final.State ==
                ChildObservationState.Directory &&
            final.Snapshot?.Identity is not null &&
            SameDirectoryObject(
                preparedIdentity,
                final.Snapshot.Identity
            );

        return journal.State switch
        {
            DataRelativePathRepairDirectoryJournalState
                .Prepared =>
                    ClassifyPrepared(
                        journal,
                        parentAcquisition.Validation,
                        staging,
                        final,
                        stagingMatches,
                        finalMatches
                    ),

            DataRelativePathRepairDirectoryJournalState
                .Applied =>
                    ClassifyApplied(
                        journal,
                        parentAcquisition.Validation,
                        staging,
                        final,
                        finalMatches
                    ),

            DataRelativePathRepairDirectoryJournalState
                .RollbackRequested =>
                    ClassifyRollbackRequested(
                        journal,
                        parentAcquisition.Validation,
                        staging,
                        final,
                        finalMatches
                    ),

            DataRelativePathRepairDirectoryJournalState
                .RolledBack =>
                    ClassifyRolledBack(
                        journal,
                        parentAcquisition.Validation,
                        staging,
                        final
                    ),

            _ =>
                Result(
                    DataRelativePathRepairDirectoryRecoveryState
                        .InvalidRecord,
                    journal,
                    parentValidation:
                        parentAcquisition.Validation,
                    error:
                        $"Unsupported directory journal state " +
                        $"{journal.State}."
                )
        };
    }

    private static
        DataRelativePathRepairDirectoryRecoveryClassification
        ClassifyPrepared(
            DataRelativePathRepairDirectoryJournalRecord journal,
            DataRelativePathRepairDestinationParentValidation
                parentValidation,
            ChildObservation staging,
            ChildObservation final,
            bool stagingMatches,
            bool finalMatches)
    {
        if (
            staging.State ==
                ChildObservationState.Missing &&
            final.State ==
                ChildObservationState.Missing)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .PreparedBothMissing,
                journal,
                parentValidation:
                    parentValidation,
                stagingOpenState:
                    staging.OpenState,
                finalOpenState:
                    final.OpenState
            );
        }

        if (
            stagingMatches &&
            final.State ==
                ChildObservationState.Missing)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .PreparedStagingMatchesFinalMissing,
                journal,
                parentValidation:
                    parentValidation,
                stagingOpenState:
                    staging.OpenState,
                stagingSnapshot:
                    staging.Snapshot,
                finalOpenState:
                    final.OpenState
            );
        }

        if (
            staging.State ==
                ChildObservationState.Missing &&
            finalMatches)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .PreparedFinalMatchesStagingMissing,
                journal,
                parentValidation:
                    parentValidation,
                stagingOpenState:
                    staging.OpenState,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot
            );
        }

        return Result(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedConflict,
            journal,
            parentValidation:
                parentValidation,
            stagingOpenState:
                staging.OpenState,
            stagingSnapshot:
                staging.Snapshot,
            finalOpenState:
                final.OpenState,
            finalSnapshot:
                final.Snapshot,
            error:
                PreparedConflictReason(
                    staging,
                    final,
                    stagingMatches,
                    finalMatches
                )
        );
    }

    private static
        DataRelativePathRepairDirectoryRecoveryClassification
        ClassifyApplied(
            DataRelativePathRepairDirectoryJournalRecord journal,
            DataRelativePathRepairDestinationParentValidation
                parentValidation,
            ChildObservation staging,
            ChildObservation final,
            bool finalMatches)
    {
        if (
            staging.State ==
                ChildObservationState.Missing &&
            final.State ==
                ChildObservationState.Missing)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .AppliedFinalMissing,
                journal,
                parentValidation:
                    parentValidation,
                stagingOpenState:
                    staging.OpenState,
                finalOpenState:
                    final.OpenState
            );
        }

        if (
            staging.State ==
                ChildObservationState.Missing &&
            finalMatches)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .AppliedFinalMatches,
                journal,
                parentValidation:
                    parentValidation,
                stagingOpenState:
                    staging.OpenState,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot
            );
        }

        return Result(
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedConflict,
            journal,
            parentValidation:
                parentValidation,
            stagingOpenState:
                staging.OpenState,
            stagingSnapshot:
                staging.Snapshot,
            finalOpenState:
                final.OpenState,
            finalSnapshot:
                final.Snapshot,
            error:
                "Applied state requires the recorded staging name " +
                "to be absent and the final destination to identify " +
                "the prepared directory inode."
        );
    }

    private static
        DataRelativePathRepairDirectoryRecoveryClassification
        ClassifyRollbackRequested(
            DataRelativePathRepairDirectoryJournalRecord journal,
            DataRelativePathRepairDestinationParentValidation
                parentValidation,
            ChildObservation staging,
            ChildObservation final,
            bool finalMatches)
    {
        if (
            staging.State ==
                ChildObservationState.Missing &&
            final.State ==
                ChildObservationState.Missing)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .RollbackRequestedFinalMissing,
                journal,
                parentValidation:
                    parentValidation,
                stagingOpenState:
                    staging.OpenState,
                finalOpenState:
                    final.OpenState
            );
        }

        if (
            staging.State ==
                ChildObservationState.Missing &&
            finalMatches)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .RollbackRequestedFinalMatches,
                journal,
                parentValidation:
                    parentValidation,
                stagingOpenState:
                    staging.OpenState,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot
            );
        }

        return Result(
            DataRelativePathRepairDirectoryRecoveryState
                .RollbackRequestedConflict,
            journal,
            parentValidation:
                parentValidation,
            stagingOpenState:
                staging.OpenState,
            stagingSnapshot:
                staging.Snapshot,
            finalOpenState:
                final.OpenState,
            finalSnapshot:
                final.Snapshot,
            error:
                "RollbackRequested requires the staging name to " +
                "be absent and the final destination either to be " +
                "missing or to identify the prepared directory."
        );
    }

    private static
        DataRelativePathRepairDirectoryRecoveryClassification
        ClassifyRolledBack(
            DataRelativePathRepairDirectoryJournalRecord journal,
            DataRelativePathRepairDestinationParentValidation
                parentValidation,
            ChildObservation staging,
            ChildObservation final)
    {
        if (
            staging.State ==
                ChildObservationState.Missing &&
            final.State ==
                ChildObservationState.Missing)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryState
                    .RolledBackBothMissing,
                journal,
                parentValidation:
                    parentValidation,
                stagingOpenState:
                    staging.OpenState,
                finalOpenState:
                    final.OpenState
            );
        }

        return Result(
            DataRelativePathRepairDirectoryRecoveryState
                .RolledBackConflict,
            journal,
            parentValidation:
                parentValidation,
            stagingOpenState:
                staging.OpenState,
            stagingSnapshot:
                staging.Snapshot,
            finalOpenState:
                final.OpenState,
            finalSnapshot:
                final.Snapshot,
            error:
                "RolledBack requires both the recorded staging " +
                "name and final destination name to be absent."
        );
    }

    private static ChildObservation InspectChild(
        ILinuxOpenedHandle parent,
        string childName,
        string displayPath)
    {
        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                childName
            );

        if (!opened.Success)
        {
            if (
                opened.State ==
                LinuxOpenChildReadOnlyAtState.ChildUnavailable)
            {
                return new(
                    State:
                        ChildObservationState.Missing,
                    OpenState:
                        opened.State,
                    Snapshot:
                        null,
                    Error:
                        null
                );
            }

            if (
                opened.State ==
                LinuxOpenChildReadOnlyAtState
                    .ChildSymbolicLinkRejected)
            {
                return new(
                    State:
                        ChildObservationState.Conflict,
                    OpenState:
                        opened.State,
                    Snapshot:
                        null,
                    Error:
                        "The namespace entry is occupied by a " +
                        "symbolic link."
                );
            }

            return new(
                State:
                    ChildObservationState.InspectionFailed,
                OpenState:
                    opened.State,
                Snapshot:
                    null,
                Error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        using LinuxOpenedChildHandle child =
            opened.OpenedChild!;

        LinuxOpenedDirectorySnapshotResult snapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                child,
                displayPath
            );

        if (
            snapshot.State ==
            LinuxOpenedDirectorySnapshotState.NotDirectory)
        {
            return new(
                State:
                    ChildObservationState.Conflict,
                OpenState:
                    opened.State,
                Snapshot:
                    snapshot,
                Error:
                    "The namespace entry is occupied by a " +
                    "non-directory object."
            );
        }

        bool usableDirectoryIdentity =
            snapshot.Identity is not null &&
            HasCompleteIdentity(
                snapshot.Identity
            ) &&
            (
                snapshot.State ==
                    LinuxOpenedDirectorySnapshotState.Captured ||
                snapshot.State ==
                    LinuxOpenedDirectorySnapshotState.FlagsUnavailable
            );

        if (!usableDirectoryIdentity)
        {
            return new(
                State:
                    ChildObservationState.InspectionFailed,
                OpenState:
                    opened.State,
                Snapshot:
                    snapshot,
                Error:
                    snapshot.Error ??
                    snapshot.State.ToString()
            );
        }

        return new(
            State:
                ChildObservationState.Directory,
            OpenState:
                opened.State,
            Snapshot:
                snapshot,
            Error:
                null
        );
    }

    private static string PreparedConflictReason(
        ChildObservation staging,
        ChildObservation final,
        bool stagingMatches,
        bool finalMatches)
    {
        if (
            staging.State !=
                ChildObservationState.Missing &&
            final.State !=
                ChildObservationState.Missing)
        {
            return
                "Both the recorded staging name and final " +
                "destination name are occupied.";
        }

        if (
            staging.State ==
                ChildObservationState.Directory &&
            !stagingMatches)
        {
            return
                "The staging name identifies a directory other " +
                "than the inode recorded while Prepared.";
        }

        if (
            final.State ==
                ChildObservationState.Directory &&
            !finalMatches)
        {
            return
                "The final destination identifies a directory " +
                "other than the inode recorded while Prepared.";
        }

        return
            staging.Error ??
            final.Error ??
            "The live namespace does not match a valid Prepared " +
            "directory transaction state.";
    }

    private static bool SameDirectoryObject(
        LinuxFileIdentityResult left,
        LinuxFileIdentityResult right)
    {
        return
            HasCompleteIdentity(left) &&
            HasCompleteIdentity(right) &&
            left.DeviceMajor ==
                right.DeviceMajor &&
            left.DeviceMinor ==
                right.DeviceMinor &&
            left.Inode ==
                right.Inode &&
            left.MountId ==
                right.MountId;
    }

    private static bool HasCompleteIdentity(
        LinuxFileIdentityResult identity)
    {
        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null &&
            identity.MountId is not null;
    }

    private static
        DataRelativePathRepairDirectoryRecoveryClassification
        Result(
            DataRelativePathRepairDirectoryRecoveryState state,
            DataRelativePathRepairDirectoryJournalRecord journal,
            DataRelativePathRepairDestinationParentValidation?
                parentValidation = null,
            LinuxOpenChildReadOnlyAtState?
                stagingOpenState = null,
            LinuxOpenedDirectorySnapshotResult?
                stagingSnapshot = null,
            LinuxOpenChildReadOnlyAtState?
                finalOpenState = null,
            LinuxOpenedDirectorySnapshotResult?
                finalSnapshot = null,
            string? error = null)
    {
        return new
            DataRelativePathRepairDirectoryRecoveryClassification(
                State:
                    state,
                Journal:
                    journal,
                ParentValidation:
                    parentValidation,
                StagingOpenState:
                    stagingOpenState,
                StagingSnapshot:
                    stagingSnapshot,
                FinalOpenState:
                    finalOpenState,
                FinalSnapshot:
                    finalSnapshot,
                Error:
                    error
            );
    }
}
