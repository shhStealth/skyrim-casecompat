using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum
    DataRelativePathRepairDestinationParentValidationState
{
    Matched,

    InvalidExpectedSnapshot,
    ParentOutsideDataRoot,

    ParentOpenFailed,
    OpenedSnapshotFailed,

    IdentityChanged,
    CasefoldChanged
}

public sealed record
    DataRelativePathRepairDestinationParentValidation(
        DataRelativePathRepairDestinationParentValidationState State,
        string DataRoot,
        DataRelativePathRepairDestinationParentSnapshot
            ExpectedSnapshot,
        LinuxNoFollowPathOpenState? OpenState,
        LinuxOpenedDirectorySnapshotResult? ActualSnapshot,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathRepairDestinationParentValidationState
                .Matched;
}
