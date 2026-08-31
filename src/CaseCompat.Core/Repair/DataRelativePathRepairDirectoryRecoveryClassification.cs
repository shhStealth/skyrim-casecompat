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
    public bool ClassificationAvailable =>
        State is not
            DataRelativePathRepairDirectoryRecoveryState
                .InvalidRecord and not
            DataRelativePathRepairDirectoryRecoveryState
                .DestinationParentValidationFailed and not
            DataRelativePathRepairDirectoryRecoveryState
                .DestinationInspectionFailed;

    public LinuxFileIdentityResult? StagingIdentity =>
        StagingSnapshot?.Identity;

    public LinuxFileIdentityResult? FinalIdentity =>
        FinalSnapshot?.Identity;

    public bool StagingMatchesPreparedIdentity =>
        Journal.PreparedDirectoryIdentity is not null &&
        StagingIdentity is not null &&
        SameDirectoryObject(
            Journal.PreparedDirectoryIdentity,
            StagingIdentity
        );

    public bool FinalMatchesPreparedIdentity =>
        Journal.PreparedDirectoryIdentity is not null &&
        FinalIdentity is not null &&
        SameDirectoryObject(
            Journal.PreparedDirectoryIdentity,
            FinalIdentity
        );

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
}
