using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public sealed class
    DataRelativePathRepairValidatedDestinationParentLease
    : IDisposable
{
    internal
        DataRelativePathRepairValidatedDestinationParentLease(
            DataRelativePathRepairDestinationParentSnapshot
                expectedSnapshot,
            LinuxOpenedDirectorySnapshotResult
                actualSnapshot,
            LinuxOpenedDirectoryIncarnationResult
                actualIncarnation,
            LinuxNoFollowPathHandle
                openedPath)
    {
        ExpectedSnapshot =
            expectedSnapshot;

        ActualSnapshot =
            actualSnapshot;

        ActualIncarnation =
            actualIncarnation;

        OpenedPath =
            openedPath;
    }

    public DataRelativePathRepairDestinationParentSnapshot
        ExpectedSnapshot { get; }

    public LinuxOpenedDirectorySnapshotResult
        ActualSnapshot { get; }

    /*
     * Strong directory-incarnation evidence captured from the exact
     * descriptor retained by this lease.
     *
     * The lease itself remains usable when generation capture is
     * unavailable because this shared parent validator is also used
     * by file repair. Directory-journal v2 will explicitly require
     * ActualIncarnation.Success before accepting destructive
     * directory authority.
     */
    public LinuxOpenedDirectoryIncarnationResult
        ActualIncarnation { get; }

    public LinuxDirectoryIncarnationIdentity?
        IncarnationIdentity =>
            ActualIncarnation.Identity;

    public LinuxNoFollowPathHandle
        OpenedPath { get; }

    public void Dispose()
    {
        OpenedPath.Dispose();
    }
}

public sealed record
    DataRelativePathRepairDestinationParentLeaseAcquisition(
        DataRelativePathRepairDestinationParentValidation
            Validation,
        DataRelativePathRepairValidatedDestinationParentLease?
            Lease
    )
{
    public bool Success =>
        Validation.Success &&
        Lease is not null;
}
