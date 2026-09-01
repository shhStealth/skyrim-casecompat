namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairPlanForwardExecutionState
{
    AppliedDurably,

    ManifestReadFailed,
    ManifestDataRootMismatch,

    PlanExecutionLockUnavailable,
    ManifestRevalidationFailed,

    PreflightFailed,
    OperationFailed
}

public enum DataRelativePathRepairPlanForwardOperationExecutionState
{
    AppliedDurably,

    JournalReadFailed,
    JournalMismatch,
    JournalGap,

    DestinationParentSnapshotCaptureFailed,

    DirectoryExecutionFailed,
    DirectoryIntentRecoveryFailed,
    DirectoryReprepareRecoveryFailed,
    DirectoryForwardRecoveryFailed,
    DirectoryReconciliationFailed,
    DirectoryRecoveryStateNotForwardSafe,

    FileIntentCreationFailed,
    FileExecutionFailed,
    FileForwardRecoveryFailed,
    FileReconciliationFailed,
    FileRecoveryStateNotForwardSafe,

    ProgressLimitExceeded
}

public sealed record
    DataRelativePathRepairPlanForwardOperationExecution(
        int Index,
        DataRelativePathRepairPlanOperationKind Kind,
        string JournalChildName,
        DataRelativePathRepairPlanForwardOperationExecutionState
            State,

        DataRelativePathRepairDestinationParentSnapshotCaptureResult?
            ParentSnapshotCapture,

        DataRelativePathRepairDirectoryJournalReaderResult?
            DirectoryJournalRead,
        DataRelativePathRepairDirectoryRecoveryClassification?
            DirectoryClassification,
        DataRelativePathRepairDirectoryExecution?
            DirectoryExecution,
        DataRelativePathRepairDirectoryIntentRecovery?
            DirectoryIntentRecovery,
        DataRelativePathRepairDirectoryReprepareRecovery?
            DirectoryReprepareRecovery,
        DataRelativePathRepairDirectoryForwardRecovery?
            DirectoryForwardRecovery,
        DataRelativePathRepairDirectoryRecoveryReconciliation?
            DirectoryReconciliation,

        DataRelativePathRepairFileJournalReaderResult?
            FileJournalRead,
        DataRelativePathRepairFileRecoveryClassification?
            FileClassification,
        DataRelativePathRepairFileJournalTransitionResult?
            FileIntentTransition,
        DataRelativePathRepairFileExecution?
            FileExecution,
        DataRelativePathRepairFileForwardRecovery?
            FileForwardRecovery,
        DataRelativePathRepairFileRecoveryReconciliation?
            FileReconciliation,

        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathRepairPlanForwardOperationExecutionState
            .AppliedDurably;
}

public sealed record DataRelativePathRepairPlanForwardExecution(
    DataRelativePathRepairPlanForwardExecutionState State,
    DataRelativePathRepairPlanManifestReaderResult? ManifestRead,
    IReadOnlyList<DataRelativePathRepairPlanForwardOperationExecution>
        OperationResults,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairPlanForwardExecutionState
            .AppliedDurably;
}
