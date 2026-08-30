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
        string? Error
    )
{
    public bool ClassificationAvailable =>
        State is not
            DataRelativePathRepairFileRecoveryState
                .InvalidRecord and not
            DataRelativePathRepairFileRecoveryState
                .DestinationParentValidationFailed and not
            DataRelativePathRepairFileRecoveryState
                .DestinationInspectionFailed;

    public bool DestinationMatchesPreparedIdentity =>
        Journal.PreparedFileIdentity is not null &&
        DestinationIdentity is not null &&
        Journal.PreparedFileIdentity.SameObjectAs(
            DestinationIdentity
        );
}
