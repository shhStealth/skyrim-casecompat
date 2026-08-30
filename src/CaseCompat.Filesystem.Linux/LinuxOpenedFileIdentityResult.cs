namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenedFileIdentityState
{
    Captured,

    UnsupportedPlatform,
    InvalidHandle,
    MetadataUnavailable,
    NotRegularFile
}

public sealed record LinuxOpenedFileIdentityResult(
    LinuxOpenedFileIdentityState State,
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
            LinuxOpenedFileIdentityState.Captured &&
        DeviceMajor is not null &&
        DeviceMinor is not null &&
        Inode is not null;

    public bool SameObjectAs(
        LinuxOpenedFileIdentityResult other)
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
