namespace CaseCompat.Filesystem.Linux;

/*
 * Durable identity for one incarnation of a Linux regular file.
 *
 * Device + inode + mount ID identify the current inode location,
 * but an inode number may be reused after deletion.
 *
 * InodeGeneration distinguishes separate incarnations of that
 * reused inode on filesystems such as ext4.
 */
public sealed record LinuxFileIncarnationIdentity(
    LinuxOpenedFileIdentityResult PhysicalIdentity,
    uint InodeGeneration
)
{
    public bool Success =>
        HasCompletePhysicalIdentity(
            PhysicalIdentity
        );

    public bool SameIncarnationAs(
        LinuxFileIncarnationIdentity other)
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
            PhysicalIdentity.DeviceMajor ==
                other.PhysicalIdentity.DeviceMajor &&
            PhysicalIdentity.DeviceMinor ==
                other.PhysicalIdentity.DeviceMinor &&
            PhysicalIdentity.Inode ==
                other.PhysicalIdentity.Inode &&
            PhysicalIdentity.MountId ==
                other.PhysicalIdentity.MountId &&
            InodeGeneration ==
                other.InodeGeneration;
    }

    private static bool HasCompletePhysicalIdentity(
        LinuxOpenedFileIdentityResult identity)
    {
        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null &&
            identity.MountId is not null;
    }
}
