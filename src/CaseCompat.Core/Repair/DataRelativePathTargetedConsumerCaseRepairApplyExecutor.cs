using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Independent, scoped-down forward executor for one targeted
// consumer-authoritative repair plan.
//
// This is intentionally not a variant of the legacy
// DataRelativePathRepairFileExecutor. It re-derives fresh filesystem proof
// - source identity, source inode generation, destination absence -
// immediately before mutating, exactly as every other boundary in this
// pipeline (admission, durable-plan creation) re-derives fresh proof
// rather than trusting an earlier snapshot.
//
// A durable Intent -> Prepared -> Applied journal covers only the final
// CreateFile operation, matching every plan this session's pipeline
// produces (zero or more CreateDirectory operations followed by exactly
// one CreateFile). Directory creation has no comparable torn-write risk
// - mkdir either succeeds or it doesn't, atomically, with no partial
// "content copied but not yet visible" state the way file publication
// has - so CreateDirectory steps use the existing no-overwrite
// LinuxCreateDirectoryAt primitive directly rather than a parallel
// per-directory journal apparatus. Every directory actually created is
// still reported in the execution result for diagnostics.
//
// There is no automated crash-forward-recovery layer here. The durable
// apply journal still proves exactly which phase the final CreateFile
// reached if the process is interrupted, but resuming from a partial
// apply is a manual concern, not an automated reconciler, by deliberate
// scope decision. Rollback undoes only the published file; any
// directories created along the way are intentionally left in place -
// an empty, correctly-cased directory is harmless.
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
    DirectoryInspectionFailed,
    DirectoryAlreadyExists,
    DirectoryCreateFailed,
    DirectoryReopenFailed,

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
    IReadOnlyList<string> CreatedDirectoryPaths,
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
            plan.Operations.Count == 0 ||
            plan.Operations[^1].Kind !=
                DataRelativePathRepairPlanOperationKind.CreateFile ||
            plan.Operations
                .Take(
                    plan.Operations.Count - 1
                )
                .Any(
                    op =>
                        op.Kind !=
                        DataRelativePathRepairPlanOperationKind
                            .CreateDirectory
                ))
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .UnsupportedOperationShape,
                plan.PlanId,
                error:
                    "This executor supports plans whose operations are " +
                    "zero or more CreateDirectory steps followed by " +
                    "exactly one final CreateFile step."
            );
        }

        DataRelativePathRepairPlanOperation[] directoryOperations =
            plan.Operations
                .Take(
                    plan.Operations.Count - 1
                )
                .ToArray();

        DataRelativePathRepairPlanOperation fileOperation =
            plan.Operations[^1];

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
                    fileOperation.DestinationPath,
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
                    fileOperation.DestinationPath,
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
                    fileOperation.DestinationPath,
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
                    fileOperation.DestinationPath,
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
                    fileOperation.DestinationPath,
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
                    fileOperation.DestinationPath,
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
                    fileOperation.DestinationPath,
                error:
                    "The freshly reacquired source inode generation does " +
                    "not equal the durable plan's recorded generation."
            );
        }

        LinuxNoFollowPathOpenResult startingParentOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                plan.InitialDestinationParentSnapshot.PhysicalPath
            );

        if (!startingParentOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DestinationParentOpenFailed,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                error:
                    startingParentOpen.Error ??
                    startingParentOpen.State.ToString()
            );
        }

        var createdDirectoryPaths =
            new List<string>();

        LinuxNoFollowPathHandle? currentParent =
            startingParentOpen.OpenedPath!;

        try
        {
            foreach (
                DataRelativePathRepairPlanOperation directoryOperation
                in directoryOperations)
            {
                string? directoryParentPath =
                    Path.GetDirectoryName(
                        directoryOperation.DestinationPath
                    );

                if (
                    directoryParentPath is null ||
                    !string.Equals(
                        directoryParentPath,
                        currentParent.FullPath,
                        StringComparison.Ordinal))
                {
                    return Result(
                        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .UnsupportedOperationShape,
                        plan.PlanId,
                        destinationPath:
                            fileOperation.DestinationPath,
                        createdDirectoryPaths:
                            createdDirectoryPaths,
                        error:
                            "A CreateDirectory operation's parent does " +
                            "not match the previous step's exact " +
                            "destination."
                    );
                }

                string directoryChildName =
                    Path.GetFileName(
                        directoryOperation.DestinationPath
                    );

                LinuxInspectChildAtResult directoryPreflight =
                    LinuxInspectChildAt.Inspect(
                        currentParent,
                        directoryChildName
                    );

                if (directoryPreflight.Success)
                {
                    return Result(
                        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .DirectoryAlreadyExists,
                        plan.PlanId,
                        destinationPath:
                            fileOperation.DestinationPath,
                        createdDirectoryPaths:
                            createdDirectoryPaths,
                        error:
                            "The requested directory already exists. " +
                            "Forward apply never overwrites it."
                    );
                }

                if (
                    directoryPreflight.State !=
                    LinuxInspectChildAtState.ChildUnavailable)
                {
                    return Result(
                        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .DirectoryInspectionFailed,
                        plan.PlanId,
                        destinationPath:
                            fileOperation.DestinationPath,
                        createdDirectoryPaths:
                            createdDirectoryPaths,
                        error:
                            directoryPreflight.Error ??
                            directoryPreflight.State.ToString()
                    );
                }

                LinuxCreateDirectoryAtResult directoryCreate =
                    LinuxCreateDirectoryAt.Create(
                        currentParent,
                        directoryChildName
                    );

                if (!directoryCreate.Success)
                {
                    return Result(
                        directoryCreate.State ==
                        LinuxCreateDirectoryAtState.DestinationExists
                            ? DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                .DirectoryAlreadyExists
                            : DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                .DirectoryCreateFailed,
                        plan.PlanId,
                        destinationPath:
                            fileOperation.DestinationPath,
                        createdDirectoryPaths:
                            createdDirectoryPaths,
                        error:
                            directoryCreate.Error ??
                            directoryCreate.State.ToString()
                    );
                }

                createdDirectoryPaths.Add(
                    directoryOperation.DestinationPath
                );

                LinuxNoFollowPathOpenResult reopen =
                    LinuxNoFollowPath.OpenRootReadOnly(
                        directoryOperation.DestinationPath
                    );

                if (!reopen.Success)
                {
                    return Result(
                        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .DirectoryReopenFailed,
                        plan.PlanId,
                        destinationPath:
                            fileOperation.DestinationPath,
                        createdDirectoryPaths:
                            createdDirectoryPaths,
                        error:
                            reopen.Error ??
                            reopen.State.ToString()
                    );
                }

                currentParent.Dispose();

                currentParent =
                    reopen.OpenedPath!;
            }

            LinuxNoFollowPathHandle destinationParent =
                currentParent;

            string? finalParentPath =
                Path.GetDirectoryName(
                    fileOperation.DestinationPath
                );

            if (
                finalParentPath is null ||
                !string.Equals(
                    finalParentPath,
                    destinationParent.FullPath,
                    StringComparison.Ordinal))
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .UnsupportedOperationShape,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    error:
                        "The final CreateFile operation's parent does " +
                        "not match the last created directory (or the " +
                        "plan's initial destination-parent snapshot when " +
                        "no directories were needed)."
                );
            }

            string destinationChildName =
                Path.GetFileName(
                    fileOperation.DestinationPath
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    error:
                        "The destination already exists. Forward apply " +
                        "never overwrites it."
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                            fileOperation,
                            plan.SourceSnapshot
                        );

            if (!intentTransition.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .InitialJournalInvalid,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                 * inspectable checkpoint if this final journal write
                 * fails.
                 */
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .AppliedJournalWriteFailed,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
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
                    fileOperation.DestinationPath,
                createdDirectoryPaths:
                    createdDirectoryPaths,
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
        finally
        {
            currentParent?.Dispose();
        }
    }

    private static DataRelativePathTargetedConsumerCaseRepairApplyExecution
        Result(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                state,
            Guid planId,
            string? destinationPath = null,
            IReadOnlyList<string>? createdDirectoryPaths = null,
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
            CreatedDirectoryPaths:
                createdDirectoryPaths ??
                Array.Empty<string>(),
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
