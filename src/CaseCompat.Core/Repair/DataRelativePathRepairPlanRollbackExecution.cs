namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairPlanRollbackExecutionState
{
    RolledBackDurably,

    ManifestReadFailed,
    ManifestDataRootMismatch,

    PlanExecutionLockUnavailable,
    ManifestRevalidationFailed,
    ExpectedManifestMismatch,

    PreflightFailed,
    OperationFailed
}

public enum DataRelativePathRepairPlanRollbackOperationExecutionState
{
    RolledBackDurably,
    NotStartedSkipped,

    JournalReadFailed,
    JournalMismatch,
    JournalGap,
    CausalHistoryConflict,

    DirectoryRollbackRequestFailed,
    DirectoryRollbackRecoveryFailed,
    DirectoryReconciliationFailed,
    DirectoryRecoveryStateNotRollbackSafe,

    FileRollbackRequestFailed,
    FileRollbackRecoveryFailed,
    FileReconciliationFailed,
    FileRecoveryStateNotRollbackSafe,

    ProgressLimitExceeded
}

public sealed record
    DataRelativePathRepairPlanRollbackOperationExecution(
        int Index,
        DataRelativePathRepairPlanOperationKind Kind,
        string JournalChildName,
        DataRelativePathRepairPlanRollbackOperationExecutionState
            State,

        DataRelativePathRepairDirectoryJournalReaderResult?
            DirectoryJournalRead,
        DataRelativePathRepairDirectoryRecoveryClassification?
            DirectoryClassification,
        DataRelativePathRepairDirectoryRollbackRequest?
            DirectoryRollbackRequest,
        DataRelativePathRepairDirectoryRollbackRecovery?
            DirectoryRollbackRecovery,
        DataRelativePathRepairDirectoryRecoveryReconciliation?
            DirectoryReconciliation,

        DataRelativePathRepairFileJournalReaderResult?
            FileJournalRead,
        DataRelativePathRepairFileRecoveryClassification?
            FileClassification,
        DataRelativePathRepairFileRollbackRequest?
            FileRollbackRequest,
        DataRelativePathRepairFileRollbackRecovery?
            FileRollbackRecovery,
        DataRelativePathRepairFileRecoveryReconciliation?
            FileReconciliation,

        string? Error
    )
{
    public bool Success =>
        State is
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .RolledBackDurably or
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .NotStartedSkipped;
}

public sealed record DataRelativePathRepairPlanRollbackExecution(
    DataRelativePathRepairPlanRollbackExecutionState State,
    DataRelativePathRepairPlanManifestReaderResult? ManifestRead,
    IReadOnlyList<
        DataRelativePathRepairPlanRollbackOperationExecution
    > OperationResults,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairPlanRollbackExecutionState
            .RolledBackDurably;
}
