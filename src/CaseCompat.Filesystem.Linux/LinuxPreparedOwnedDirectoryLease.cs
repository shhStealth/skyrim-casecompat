namespace CaseCompat.Filesystem.Linux;

public sealed class LinuxPreparedOwnedDirectoryLease
    : IDisposable
{
    internal LinuxPreparedOwnedDirectoryLease(
        string stagingChildName,
        LinuxDirectoryIncarnationIdentity incarnationIdentity,
        LinuxOpenedChildHandle openedDirectory)
    {
        StagingChildName =
            stagingChildName;

        IncarnationIdentity =
            incarnationIdentity;

        OpenedDirectory =
            openedDirectory;
    }

    public string StagingChildName { get; }

    /*
     * Compatibility view for code that still needs the traditional
     * physical identity while directory-journal authority is being
     * upgraded to incarnation-aware evidence.
     */
    public LinuxFileIdentityResult Identity =>
        IncarnationIdentity.PhysicalIdentity;

    public LinuxDirectoryIncarnationIdentity
        IncarnationIdentity { get; }

    public LinuxOpenedChildHandle OpenedDirectory { get; }

    public void Dispose()
    {
        OpenedDirectory.Dispose();
    }
}
