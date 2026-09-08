using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Independent, scoped-down manual rollback for one targeted
// consumer-authoritative repair apply.
//
// This is deliberately a single-step manual undo, not the legacy
// two-phase RollbackRequested/RolledBack crash-safe rollback recovery.
// Safety comes entirely from LinuxRemoveOwnedFileAt: the destination is
// removed only if its current complete file incarnation exactly equals
// the incarnation this apply actually published, so a file that was
// since replaced, re-deployed over, or is no longer CaseCompat's exact
// publication is never touched.
public enum DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
{
    RolledBack,

    AppliedJournalUnavailable,
    AlreadyRolledBack,

    DestinationParentOpenFailed,
    DestinationIdentityMismatch,
    RemoveFailed,

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

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                destinationParent,
                destinationChildName,
                applied.AppliedFileIncarnationIdentity!
            );

        if (!remove.Success)
        {
            return Result(
                remove.State ==
                LinuxRemoveOwnedFileAtState.IdentityMismatch
                    ? DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                        .DestinationIdentityMismatch
                    : DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                        .RemoveFailed,
                planId,
                destinationPath:
                    destinationPath,
                error:
                    remove.Error ??
                    remove.State.ToString()
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
             * The destination has already been safely removed. A
             * failure to record the RolledBack journal here does not
             * leave the Data tree mutated in an untracked way; it only
             * means the Applied journal remains the last durable
             * checkpoint for manual inspection.
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
