using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Independent, scoped-down manual rollback for one targeted
// consumer-authoritative repair apply.
//
// This is deliberately a single-step manual undo, not the legacy
// two-phase RollbackRequested/RolledBack crash-safe rollback recovery.
//
// Apply publishes by renaming the source's own inode to the
// correctly-cased destination name (see
// DataRelativePathTargetedConsumerCaseRepairApplyExecutor), so rollback
// is the same rename in reverse rather than a delete. Safety comes from
// two independent checks before that reverse rename is attempted: the
// file currently at the destination must have the exact incarnation
// identity this apply actually published (so a file that was since
// replaced, re-deployed over, or is no longer CaseCompat's exact
// publication is never touched), and the original name must currently
// be vacant (enforced atomically by RENAME_NOREPLACE, so something that
// has since re-populated the old name - a mod update re-deploying its
// loose file, for example - is never clobbered).
public enum DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
{
    RolledBack,

    AppliedJournalUnavailable,
    AlreadyRolledBack,

    DestinationParentOpenFailed,
    DestinationOpenFailed,
    DestinationIdentityUnavailable,
    DestinationIdentityMismatch,

    SourceParentOpenFailed,
    OriginalLocationOccupied,
    RenameBackFailed,

    RestoredIdentityUnavailable,
    RestoredIdentityMismatch,

    SourceParentSyncFailed,
    DestinationParentSyncFailed,

    RollbackJournalInvalid,
    RollbackJournalWriteFailed
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult(
        DataRelativePathTargetedConsumerCaseRepairApplyRollbackState State,
        Guid PlanId,
        string? DestinationPath,
        string? RolledBackJournalChildName,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
            .RolledBack;
}

public static class DataRelativePathTargetedConsumerCaseRepairApplyRollback
{
    public static
        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
        Rollback(
            LinuxNoFollowPathHandle journalDirectory,
            Guid planId,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        string rolledBackChildName =
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .RolledBackChildName(
                    planId
                );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalReaderResult
            existingRollback =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReader
                    .Read(
                        journalDirectory,
                        rolledBackChildName
                    );

        if (existingRollback.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .AlreadyRolledBack,
                planId,
                error:
                    "This plan has already been rolled back."
            );
        }

        string appliedChildName =
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .AppliedChildName(
                    planId
                );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalReaderResult
            appliedRead =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReader
                    .Read(
                        journalDirectory,
                        appliedChildName
                    );

        if (!appliedRead.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .AppliedJournalUnavailable,
                planId,
                error:
                    appliedRead.Error ??
                    appliedRead.State.ToString()
            );
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord applied =
            appliedRead.Phase!;

        string destinationPath =
            applied.Operation.DestinationPath;

        string sourcePath =
            applied.SourceSnapshot.PhysicalPath;

        string? destinationParentPath =
            Path.GetDirectoryName(
                destinationPath
            );

        if (destinationParentPath is null)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .DestinationParentOpenFailed,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    "The applied destination path has no parent directory."
            );
        }

        LinuxNoFollowPathOpenResult destinationParentOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                destinationParentPath
            );

        if (!destinationParentOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .DestinationParentOpenFailed,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    destinationParentOpen.Error ??
                    destinationParentOpen.State.ToString()
            );
        }

        using LinuxNoFollowPathHandle destinationParent =
            destinationParentOpen.OpenedPath!;

        string destinationChildName =
            Path.GetFileName(
                destinationPath
            );

        LinuxOpenChildRegularFileReadOnlyAtResult destinationOpen =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                destinationParent,
                destinationChildName
            );

        if (!destinationOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .DestinationOpenFailed,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    destinationOpen.Error ??
                    destinationOpen.State.ToString()
            );
        }

        using LinuxOpenedChildHandle destinationChild =
            destinationOpen.OpenedFile!;

        LinuxOpenedFileIncarnationResult currentIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                destinationChild
            );

        if (!currentIncarnation.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .DestinationIdentityUnavailable,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    currentIncarnation.Error ??
                    currentIncarnation.State.ToString()
            );
        }

        if (
            !currentIncarnation.Identity!.SameIncarnationAs(
                applied.AppliedFileIncarnationIdentity!
            ))
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .DestinationIdentityMismatch,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    "The current destination does not have the exact " +
                    "file incarnation this apply published. Rollback " +
                    "refuses to move a file it cannot prove it created."
            );
        }

        string? sourceParentPath =
            Path.GetDirectoryName(
                sourcePath
            );

        if (sourceParentPath is null)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .SourceParentOpenFailed,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    "The original source path has no parent directory."
            );
        }

        LinuxNoFollowPathOpenResult sourceParentOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                sourceParentPath
            );

        if (!sourceParentOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .SourceParentOpenFailed,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    sourceParentOpen.Error ??
                    sourceParentOpen.State.ToString()
            );
        }

        using LinuxNoFollowPathHandle sourceParent =
            sourceParentOpen.OpenedPath!;

        string sourceChildName =
            Path.GetFileName(
                sourcePath
            );

        LinuxRenameChildAtResult renameBack =
            LinuxRenameChildAt.Rename(
                destinationParent,
                destinationChildName,
                sourceParent,
                sourceChildName
            );

        if (!renameBack.Success)
        {
            return Result(
                renameBack.State ==
                LinuxRenameChildAtState.DestinationExists
                    ? DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                        .OriginalLocationOccupied
                    : DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                        .RenameBackFailed,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    renameBack.Error ??
                    renameBack.State.ToString()
            );
        }

        bool sourceAndDestinationParentDiffer =
            !string.Equals(
                sourceParent.FullPath,
                destinationParent.FullPath,
                StringComparison.Ordinal
            );

        if (sourceAndDestinationParentDiffer)
        {
            LinuxFsyncResult destinationParentSync =
                LinuxFsync.Sync(
                    destinationParent
                );

            if (!destinationParentSync.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                        .DestinationParentSyncFailed,
                    planId,
                    destinationPath:
                        destinationPath,
                    error:
                        destinationParentSync.Error ??
                        destinationParentSync.State.ToString()
                );
            }
        }

        LinuxFsyncResult sourceParentSync =
            LinuxFsync.Sync(
                sourceParent
            );

        if (!sourceParentSync.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .SourceParentSyncFailed,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    sourceParentSync.Error ??
                    sourceParentSync.State.ToString()
            );
        }

        LinuxOpenChildRegularFileReadOnlyAtResult restoredOpen =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                sourceParent,
                sourceChildName
            );

        if (!restoredOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .RestoredIdentityUnavailable,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    restoredOpen.Error ??
                    restoredOpen.State.ToString()
            );
        }

        using LinuxOpenedChildHandle restored =
            restoredOpen.OpenedFile!;

        LinuxOpenedFileIncarnationResult restoredIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                restored
            );

        if (!restoredIncarnation.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .RestoredIdentityUnavailable,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    restoredIncarnation.Error ??
                    restoredIncarnation.State.ToString()
            );
        }

        if (
            !restoredIncarnation.Identity!.SameIncarnationAs(
                applied.AppliedFileIncarnationIdentity!
            ))
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .RestoredIdentityMismatch,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    "The restored original-name file's physical identity " +
                    "did not equal the identity this apply published."
            );
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            rollbackTransition =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkRolledBack(
                        applied,
                        nowUtc
                    );

        if (!rollbackTransition.Success)
        {
            /*
             * The rename-back has already happened. A failure to record
             * the RolledBack journal here does not leave the Data tree
             * mutated in an untracked way; it only means the Applied
             * journal remains the last durable checkpoint for manual
             * inspection.
             */
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .RollbackJournalInvalid,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    rollbackTransition.Error ??
                    rollbackTransition.State.ToString()
            );
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            rollbackWrite =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        journalDirectory,
                        rolledBackChildName,
                        rollbackTransition.Record!
                    );

        if (!rollbackWrite.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .RollbackJournalWriteFailed,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    rollbackWrite.Error ??
                    rollbackWrite.State.ToString()
            );
        }

        return Result(
            DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                .RolledBack,
            planId,
            destinationPath:
                destinationPath,
            rolledBackJournalChildName:
                rolledBackChildName
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
        Result(
            DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                state,
            Guid planId,
            string? destinationPath = null,
            string? rolledBackJournalChildName = null,
            string? error = null)
    {
        return new(
            State:
                state,
            PlanId:
                planId,
            DestinationPath:
                destinationPath,
            RolledBackJournalChildName:
                rolledBackJournalChildName,
            Error:
                error
        );
    }
}
