using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Independent, scoped-down forward executor for one targeted
// consumer-authoritative repair plan.
//
// This is intentionally not a variant of the legacy
// DataRelativePathRepairFileExecutor. It supports exactly one CreateFile
// operation (the shape every durable plan produced by this session's
// pipeline currently has) and re-derives fresh filesystem proof - source
// identity, source inode generation, and destination absence - immediately
// before mutating, exactly as every other boundary in this pipeline
// (admission, durable-plan creation) re-derives fresh proof rather than
// trusting an earlier snapshot.
//
// There is no automated crash-forward-recovery layer here. The durable
// apply journal still proves exactly which phase execution reached if the
// process is interrupted, but resuming from a partial apply is a manual
// concern, not an automated reconciler, by deliberate scope decision.
public enum DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
{
    AppliedDurably,

    InvalidPlan,
    UnsupportedOperationShape,
    DataRootMismatch,

    SourceOpenFailed,
    SourceIdentityUnavailable,
    SourceIdentityMismatch,
    SourceGenerationMismatch,

    DestinationParentOpenFailed,
    DestinationInspectionFailed,
    DestinationExists,

    InitialJournalInvalid,
    InitialJournalWriteFailed,

    TemporaryFileCreateFailed,
    CopyFailed,
    TemporaryFileSyncFailed,
    PreparedIdentityFailed,
    PreparedJournalInvalid,
    PreparedJournalWriteFailed,

    PublicationFailed,
    DestinationParentSyncFailed,
    AppliedIdentityFailed,
    AppliedJournalInvalid,
    AppliedJournalWriteFailed
}

public sealed record DataRelativePathTargetedConsumerCaseRepairApplyExecution(
    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState State,
    Guid PlanId,
    string? DestinationPath,
    string? IntentJournalChildName,
    string? PreparedJournalChildName,
    string? AppliedJournalChildName,
    LinuxFileIncarnationIdentity? AppliedFileIncarnationIdentity,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
            .AppliedDurably;
}

public static class DataRelativePathTargetedConsumerCaseRepairApplyExecutor
{
    public static DataRelativePathTargetedConsumerCaseRepairApplyExecution
        Execute(
            LinuxNoFollowPathHandle trustedDataRoot,
            LinuxNoFollowPathHandle journalDirectory,
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            trustedDataRoot
        );

        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            plan
        );

        string? planValidationError =
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                plan
            );

        if (planValidationError is not null)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .InvalidPlan,
                plan.PlanId,
                error:
                    planValidationError
            );
        }

        if (
            plan.Operations.Count != 1 ||
            plan.Operations[0].Kind !=
                DataRelativePathRepairPlanOperationKind.CreateFile)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .UnsupportedOperationShape,
                plan.PlanId,
                error:
                    "This executor supports plans with exactly one " +
                    "CreateFile operation only."
            );
        }

        DataRelativePathRepairPlanOperation operation =
            plan.Operations[0];

        string? destinationParentPath =
            Path.GetDirectoryName(
                operation.DestinationPath
            );

        if (
            destinationParentPath is null ||
            !string.Equals(
                destinationParentPath,
                plan.InitialDestinationParentSnapshot.PhysicalPath,
                StringComparison.Ordinal))
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .UnsupportedOperationShape,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    "The single CreateFile operation's parent does not " +
                    "match the plan's initial destination-parent snapshot."
            );
        }

        if (
            !string.Equals(
                trustedDataRoot.FullPath,
                plan.DataRoot,
                StringComparison.Ordinal))
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DataRootMismatch,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    "The caller-authorized Data root does not match the " +
                    "plan's Data root."
            );
        }

        string? sourceParentPath =
            Path.GetDirectoryName(
                plan.SourceSnapshot.PhysicalPath
            );

        string sourceChildName =
            Path.GetFileName(
                plan.SourceSnapshot.PhysicalPath
            );

        if (sourceParentPath is null)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceOpenFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    "The source physical path has no parent directory."
            );
        }

        LinuxNoFollowPathOpenResult sourceParentOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                sourceParentPath
            );

        if (!sourceParentOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceOpenFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    sourceParentOpen.Error ??
                    sourceParentOpen.State.ToString()
            );
        }

        using LinuxNoFollowPathHandle sourceParent =
            sourceParentOpen.OpenedPath!;

        LinuxOpenChildRegularFileReadOnlyAtResult sourceOpen =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                sourceParent,
                sourceChildName
            );

        if (!sourceOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceOpenFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    sourceOpen.Error ??
                    sourceOpen.State.ToString()
            );
        }

        using LinuxOpenedChildHandle source =
            sourceOpen.OpenedFile!;

        LinuxOpenedFileIncarnationResult sourceIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                source
            );

        if (!sourceIncarnation.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceIdentityUnavailable,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    sourceIncarnation.Error ??
                    sourceIncarnation.State.ToString()
            );
        }

        LinuxFileIncarnationIdentity sourceIdentity =
            sourceIncarnation.Identity!;

        LinuxFileIdentityResult expectedSourceIdentity =
            plan.SourceSnapshot.Identity;

        if (
            sourceIdentity.PhysicalIdentity.DeviceMajor !=
                expectedSourceIdentity.DeviceMajor ||
            sourceIdentity.PhysicalIdentity.DeviceMinor !=
                expectedSourceIdentity.DeviceMinor ||
            sourceIdentity.PhysicalIdentity.Inode !=
                expectedSourceIdentity.Inode ||
            sourceIdentity.PhysicalIdentity.MountId !=
                expectedSourceIdentity.MountId)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceIdentityMismatch,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    "The freshly reacquired source physical identity does " +
                    "not equal the durable plan's source snapshot identity."
            );
        }

        if (sourceIdentity.InodeGeneration != plan.SourceInodeGeneration)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceGenerationMismatch,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    "The freshly reacquired source inode generation does " +
                    "not equal the durable plan's recorded generation."
            );
        }

        LinuxNoFollowPathOpenResult destinationParentOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                destinationParentPath
            );

        if (!destinationParentOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DestinationParentOpenFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    destinationParentOpen.Error ??
                    destinationParentOpen.State.ToString()
            );
        }

        using LinuxNoFollowPathHandle destinationParent =
            destinationParentOpen.OpenedPath!;

        string destinationChildName =
            Path.GetFileName(
                operation.DestinationPath
            );

        LinuxInspectChildAtResult destinationPreflight =
            LinuxInspectChildAt.Inspect(
                destinationParent,
                destinationChildName
            );

        if (destinationPreflight.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DestinationExists,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    "The destination already exists. Forward apply never " +
                    "overwrites it."
            );
        }

        if (
            destinationPreflight.State !=
            LinuxInspectChildAtState.ChildUnavailable)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DestinationInspectionFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    destinationPreflight.Error ??
                    destinationPreflight.State.ToString()
            );
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            intentTransition =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .CreateIntent(
                        plan.PlanId,
                        nowUtc,
                        plan.DataRoot,
                        operation,
                        plan.SourceSnapshot
                    );

        if (!intentTransition.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .InitialJournalInvalid,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    intentTransition.Error ??
                    intentTransition.State.ToString()
            );
        }

        string intentChildName =
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .IntentChildName(
                    plan.PlanId
                );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            intentWrite =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        journalDirectory,
                        intentChildName,
                        intentTransition.Record!
                    );

        if (!intentWrite.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .InitialJournalWriteFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                error:
                    intentWrite.Error ??
                    intentWrite.State.ToString()
            );
        }

        LinuxCreateUnnamedFileAtResult temporaryCreate =
            LinuxCreateUnnamedFileAt.Create(
                destinationParent
            );

        if (!temporaryCreate.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .TemporaryFileCreateFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                error:
                    temporaryCreate.Error ??
                    temporaryCreate.State.ToString()
            );
        }

        using LinuxUnnamedFileHandle temporary =
            temporaryCreate.OpenedFile!;

        LinuxCopyFileContentsResult copy =
            LinuxCopyFileContents.CopyAndVerify(
                source,
                temporary,
                plan.SourceSnapshot.Size,
                plan.SourceSnapshot.Sha256
            );

        if (!copy.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .CopyFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                error:
                    copy.Error ??
                    copy.State.ToString()
            );
        }

        LinuxFsyncResult temporarySync =
            LinuxFsync.Sync(
                temporary
            );

        if (!temporarySync.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .TemporaryFileSyncFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                error:
                    temporarySync.Error ??
                    temporarySync.State.ToString()
            );
        }

        LinuxOpenedFileIncarnationResult preparedIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                temporary
            );

        if (!preparedIncarnation.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .PreparedIdentityFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                error:
                    preparedIncarnation.Error ??
                    preparedIncarnation.State.ToString()
            );
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            preparedTransition =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkPrepared(
                        intentTransition.Record!,
                        preparedIncarnation.Identity!,
                        nowUtc
                    );

        if (!preparedTransition.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .PreparedJournalInvalid,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                error:
                    preparedTransition.Error ??
                    preparedTransition.State.ToString()
            );
        }

        string preparedChildName =
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .PreparedChildName(
                    plan.PlanId
                );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            preparedWrite =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        journalDirectory,
                        preparedChildName,
                        preparedTransition.Record!
                    );

        if (!preparedWrite.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .PreparedJournalWriteFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                error:
                    preparedWrite.Error ??
                    preparedWrite.State.ToString()
            );
        }

        LinuxPublishUnnamedFileAtResult publication =
            LinuxPublishUnnamedFileAt.Publish(
                temporary,
                destinationParent,
                destinationChildName
            );

        if (!publication.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .PublicationFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                preparedJournalChildName:
                    preparedChildName,
                error:
                    publication.Error ??
                    publication.State.ToString()
            );
        }

        LinuxFsyncResult destinationParentSync =
            LinuxFsync.Sync(
                destinationParent
            );

        if (!destinationParentSync.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DestinationParentSyncFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                preparedJournalChildName:
                    preparedChildName,
                error:
                    destinationParentSync.Error ??
                    destinationParentSync.State.ToString()
            );
        }

        LinuxOpenChildRegularFileReadOnlyAtResult publishedOpen =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                destinationParent,
                destinationChildName
            );

        if (!publishedOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AppliedIdentityFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                preparedJournalChildName:
                    preparedChildName,
                error:
                    publishedOpen.Error ??
                    publishedOpen.State.ToString()
            );
        }

        using LinuxOpenedChildHandle published =
            publishedOpen.OpenedFile!;

        LinuxOpenedFileIncarnationResult appliedIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                published
            );

        if (!appliedIncarnation.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AppliedIdentityFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                preparedJournalChildName:
                    preparedChildName,
                error:
                    appliedIncarnation.Error ??
                    appliedIncarnation.State.ToString()
            );
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            appliedTransition =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkApplied(
                        preparedTransition.Record!,
                        appliedIncarnation.Identity!,
                        nowUtc
                    );

        if (!appliedTransition.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AppliedJournalInvalid,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                preparedJournalChildName:
                    preparedChildName,
                appliedFileIncarnationIdentity:
                    appliedIncarnation.Identity,
                error:
                    appliedTransition.Error ??
                    appliedTransition.State.ToString()
            );
        }

        string appliedChildName =
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .AppliedChildName(
                    plan.PlanId
                );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            appliedWrite =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        journalDirectory,
                        appliedChildName,
                        appliedTransition.Record!
                    );

        if (!appliedWrite.Success)
        {
            /*
             * The destination is already published and its parent
             * directory already synced. Prepared remains a durable,
             * inspectable checkpoint if this final journal write fails.
             */
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AppliedJournalWriteFailed,
                plan.PlanId,
                destinationPath:
                    operation.DestinationPath,
                intentJournalChildName:
                    intentChildName,
                preparedJournalChildName:
                    preparedChildName,
                appliedFileIncarnationIdentity:
                    appliedIncarnation.Identity,
                error:
                    appliedWrite.Error ??
                    appliedWrite.State.ToString()
            );
        }

        return Result(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                .AppliedDurably,
            plan.PlanId,
            destinationPath:
                operation.DestinationPath,
            intentJournalChildName:
                intentChildName,
            preparedJournalChildName:
                preparedChildName,
            appliedJournalChildName:
                appliedChildName,
            appliedFileIncarnationIdentity:
                appliedIncarnation.Identity
        );
    }

    private static DataRelativePathTargetedConsumerCaseRepairApplyExecution
        Result(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                state,
            Guid planId,
            string? destinationPath = null,
            string? intentJournalChildName = null,
            string? preparedJournalChildName = null,
            string? appliedJournalChildName = null,
            LinuxFileIncarnationIdentity?
                appliedFileIncarnationIdentity = null,
            string? error = null)
    {
        return new(
            State:
                state,
            PlanId:
                planId,
            DestinationPath:
                destinationPath,
            IntentJournalChildName:
                intentJournalChildName,
            PreparedJournalChildName:
                preparedJournalChildName,
            AppliedJournalChildName:
                appliedJournalChildName,
            AppliedFileIncarnationIdentity:
                appliedFileIncarnationIdentity,
            Error:
                error
        );
    }
}
