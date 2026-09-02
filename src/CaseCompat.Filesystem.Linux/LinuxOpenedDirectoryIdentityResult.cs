namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenedDirectoryIdentityState
{
    Captured,

    UnsupportedPlatform,
    InvalidHandle,
    MetadataUnavailable,
    NotDirectory
}

public sealed record LinuxOpenedDirectoryIdentityResult(
    LinuxOpenedDirectoryIdentityState State,
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
            LinuxOpenedDirectoryIdentityState.Captured &&
        DeviceMajor is not null &&
        DeviceMinor is not null &&
        Inode is not null &&
        MountId is not null;

    public bool SameObjectAs(
        LinuxOpenedDirectoryIdentityResult other)
    {
        ArgumentNullException.ThrowIfNull(
            other
        );

        if (
            !Success ||
            !other.Success)
        {
            return false;
        }

        return
            DeviceMajor == other.DeviceMajor &&
            DeviceMinor == other.DeviceMinor &&
            Inode == other.Inode &&
            MountId == other.MountId;
    }
}
