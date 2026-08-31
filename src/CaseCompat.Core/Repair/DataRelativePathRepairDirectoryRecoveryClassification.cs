using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryRecoveryState
{
    InvalidRecord,
    DestinationParentValidationFailed,
    DestinationInspectionFailed,

    IntentFinalMissing,
    IntentFinalConflict,

    PreparedBothMissing,
    PreparedStagingMatchesFinalMissing,
    PreparedFinalMatchesStagingMissing,
    PreparedConflict,

    AppliedFinalMissing,
    AppliedFinalMatches,
    AppliedConflict,

    RollbackRequestedFinalMissing,
    RollbackRequestedFinalMatches,
    RollbackRequestedConflict,

    RolledBackBothMissing,
    RolledBackConflict,

    RecoveryConflictTerminal
}

public sealed record
    DataRelativePathRepairDirectoryRecoveryClassification(
        DataRelativePathRepairDirectoryRecoveryState State,
        DataRelativePathRepairDirectoryJournalRecord Journal,
        DataRelativePathRepairDestinationParentValidation?
            ParentValidation,
        LinuxOpenChildReadOnlyAtState?
            StagingOpenState,
        LinuxOpenedDirectorySnapshotResult?
            StagingSnapshot,
        LinuxOpenChildReadOnlyAtState?
            FinalOpenState,
        LinuxOpenedDirectorySnapshotResult?
            FinalSnapshot,
        string? Error
    )
{
    public LinuxOpenedDirectoryIncarnationResult?
        StagingIncarnation { get; init; }

    public LinuxOpenedDirectoryIncarnationResult?
        FinalIncarnation { get; init; }

    public bool ClassificationAvailable =>
        State is not
            DataRelativePathRepairDirectoryRecoveryState
                .InvalidRecord and not
            DataRelativePathRepairDirectoryRecoveryState
                .DestinationParentValidationFailed and not
            DataRelativePathRepairDirectoryRecoveryState
                .DestinationInspectionFailed;

    /*
     * Compatibility views for callers that still need physical
     * statx identity. Ownership matching itself is generation-aware.
     */
    public LinuxFileIdentityResult? StagingIdentity =>
        StagingSnapshot?.Identity;

    public LinuxFileIdentityResult? FinalIdentity =>
        FinalSnapshot?.Identity;

    public LinuxDirectoryIncarnationIdentity?
        StagingIncarnationIdentity =>
            StagingIncarnation?.Identity;

    public LinuxDirectoryIncarnationIdentity?
        FinalIncarnationIdentity =>
            FinalIncarnation?.Identity;

    public bool StagingMatchesPreparedIdentity =>
        Journal.PreparedDirectoryIncarnationIdentity is not null &&
        StagingIncarnationIdentity is not null &&
        Journal.PreparedDirectoryIncarnationIdentity
            .SameIncarnationAs(
                StagingIncarnationIdentity
            );

    public bool FinalMatchesPreparedIdentity =>
        Journal.PreparedDirectoryIncarnationIdentity is not null &&
        FinalIncarnationIdentity is not null &&
        Journal.PreparedDirectoryIncarnationIdentity
            .SameIncarnationAs(
                FinalIncarnationIdentity
            );
}
