using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairFileRecoveryState
{
    InvalidRecord,
    DestinationParentValidationFailed,
    DestinationInspectionFailed,

    IntentDestinationMissing,
    IntentDestinationConflict,

    PreparedDestinationMissing,
    PreparedDestinationMatches,
    PreparedDestinationConflict,

    AppliedDestinationMissing,
    AppliedDestinationMatches,
    AppliedDestinationConflict,

    RollbackRequestedDestinationMissing,
    RollbackRequestedDestinationMatches,
    RollbackRequestedDestinationConflict,

    RolledBackDestinationMissing,
    RolledBackDestinationConflict,

    RecoveryConflictTerminal
}

public sealed record
    DataRelativePathRepairFileRecoveryClassification(
        DataRelativePathRepairFileRecoveryState State,
        DataRelativePathRepairFileJournalRecord Journal,
        DataRelativePathRepairDestinationParentValidation?
            ParentValidation,
        LinuxOpenChildReadOnlyAtState? DestinationOpenState,
        LinuxOpenedFileIdentityResult? DestinationIdentity,
        LinuxOpenedFileSnapshotResult? DestinationSnapshot,
        string? Error
    )
{
    public LinuxOpenedFileIncarnationResult?
        DestinationIncarnation { get; init; }

    /*
     * Generation-aware identity view of the opened destination.
     * DestinationIdentity remains available separately for callers
     * that still need the physical opened-file identity.
     */
    public LinuxFileIncarnationIdentity?
        DestinationIncarnationIdentity =>
            DestinationIncarnation?.Identity;

    public bool ClassificationAvailable =>
        State is not
            DataRelativePathRepairFileRecoveryState
                .InvalidRecord and not
            DataRelativePathRepairFileRecoveryState
                .DestinationParentValidationFailed and not
            DataRelativePathRepairFileRecoveryState
                .DestinationInspectionFailed;

    public bool DestinationMatchesPreparedIdentity =>
        Journal.PreparedFileIncarnationIdentity is not null &&
        DestinationIncarnationIdentity is not null &&
        Journal.PreparedFileIncarnationIdentity
            .SameIncarnationAs(
                DestinationIncarnationIdentity
            );

    public bool DestinationContentMatchesSourceSnapshot =>
        DestinationSnapshot is not null &&
        DestinationSnapshot.Success &&
        DestinationSnapshot.Size ==
            Journal.SourceSnapshot.Size &&
        string.Equals(
            DestinationSnapshot.Sha256,
            Journal.SourceSnapshot.Sha256,
            StringComparison.OrdinalIgnoreCase
        );
}
