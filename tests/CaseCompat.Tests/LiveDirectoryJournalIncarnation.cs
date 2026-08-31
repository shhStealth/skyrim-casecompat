using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

/*
 * TEST helper for recovery/action fixtures that operate on real
 * filesystem directories.
 *
 * Unlike SyntheticDirectoryJournalIncarnation, this helper obtains
 * inode generation from the exact opened descriptor.
 */
internal static class LiveDirectoryJournalIncarnation
{
    public static LinuxDirectoryIncarnationIdentity Capture(
        LinuxNoFollowPathHandle openedDirectory)
    {
        ArgumentNullException.ThrowIfNull(
            openedDirectory
        );

        LinuxOpenedDirectoryIncarnationResult result =
            LinuxOpenedDirectoryIncarnation.Capture(
                openedDirectory
            );

        Assert.True(
            result.Success,
            result.Error ??
            result.State.ToString()
        );

        return Assert.IsType<
            LinuxDirectoryIncarnationIdentity
        >(
            result.Identity
        );
    }

    public static LinuxDirectoryIncarnationIdentity Capture(
        LinuxOpenedChildHandle openedDirectory,
        string displayPath)
    {
        ArgumentNullException.ThrowIfNull(
            openedDirectory
        );

        LinuxOpenedDirectoryIncarnationResult result =
            LinuxOpenedDirectoryIncarnation.Capture(
                openedDirectory,
                displayPath
            );

        Assert.True(
            result.Success,
            result.Error ??
            result.State.ToString()
        );

        return Assert.IsType<
            LinuxDirectoryIncarnationIdentity
        >(
            result.Identity
        );
    }
}
