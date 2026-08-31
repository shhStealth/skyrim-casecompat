namespace CaseCompat.Filesystem.Linux;

public sealed class LinuxPreparedOwnedDirectoryLease
    : IDisposable
{
    internal LinuxPreparedOwnedDirectoryLease(
        string stagingChildName,
        LinuxFileIdentityResult identity,
        LinuxOpenedChildHandle openedDirectory)
    {
        StagingChildName =
            stagingChildName;

        Identity =
            identity;

        OpenedDirectory =
            openedDirectory;
    }

    public string StagingChildName { get; }

    public LinuxFileIdentityResult Identity { get; }

    public LinuxOpenedChildHandle OpenedDirectory { get; }

    public void Dispose()
    {
        OpenedDirectory.Dispose();
    }
}
