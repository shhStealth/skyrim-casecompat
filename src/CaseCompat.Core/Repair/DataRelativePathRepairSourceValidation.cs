using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairSourceValidationState
{
    Matched,

    InvalidExpectedSnapshot,
    SourceOutsideDataRoot,

    SourceOpenFailed,
    OpenedSnapshotFailed,

    IdentityChanged,
    SizeChanged,
    HashChanged
}

public sealed record DataRelativePathRepairSourceValidation(
    DataRelativePathRepairSourceValidationState State,
    string DataRoot,
    DataRelativePathRepairSourceSnapshot ExpectedSnapshot,
    LinuxNoFollowPathOpenState? OpenState,
    LinuxOpenedFileSnapshotResult? ActualSnapshot,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairSourceValidationState
                .Matched;
}
