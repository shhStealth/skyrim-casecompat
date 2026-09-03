namespace CaseCompat.Filesystem.Linux;

public enum LinuxChildObjectKind
{
    RegularFile,
    Directory,
    SymbolicLink,
    Other
}

public enum LinuxInspectChildAtState
{
    Inspected,

    UnsupportedPlatform,
    InvalidName,
    InvalidParentHandle,
    ParentNotDirectory,

    ChildUnavailable,
    MetadataUnavailable
}

public sealed record LinuxInspectChildAtResult(
    LinuxInspectChildAtState State,
    string ChildName,
    LinuxChildObjectKind? Kind,
    uint? DeviceMajor,
    uint? DeviceMinor,
    ulong? Inode,
    uint? LinkCount,
    ulong? MountId,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxInspectChildAtState.Inspected &&
        Kind is not null &&
        DeviceMajor is not null &&
        DeviceMinor is not null &&
        Inode is not null &&
        MountId is not null;
}
