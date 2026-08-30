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
            LinuxNoFollowPathHandle
                openedPath)
    {
        ExpectedSnapshot =
            expectedSnapshot;

        ActualSnapshot =
            actualSnapshot;

        OpenedPath =
            openedPath;
    }

    public DataRelativePathRepairDestinationParentSnapshot
        ExpectedSnapshot { get; }

    public LinuxOpenedDirectorySnapshotResult
        ActualSnapshot { get; }

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
