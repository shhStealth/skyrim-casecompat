using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public sealed class DataRelativePathRepairValidatedSourceLease
    : IDisposable
{
    internal DataRelativePathRepairValidatedSourceLease(
        DataRelativePathRepairSourceSnapshot expectedSnapshot,
        LinuxOpenedFileSnapshotResult actualSnapshot,
        LinuxNoFollowPathHandle openedPath)
    {
        ExpectedSnapshot =
            expectedSnapshot;

        ActualSnapshot =
            actualSnapshot;

        OpenedPath =
            openedPath;
    }

    public DataRelativePathRepairSourceSnapshot
        ExpectedSnapshot { get; }

    public LinuxOpenedFileSnapshotResult
        ActualSnapshot { get; }

    public LinuxNoFollowPathHandle
        OpenedPath { get; }

    public void Dispose()
    {
        OpenedPath.Dispose();
    }
}

public sealed record DataRelativePathRepairSourceLeaseAcquisition(
    DataRelativePathRepairSourceValidation Validation,
    DataRelativePathRepairValidatedSourceLease? Lease
)
{
    public bool Success =>
        Validation.Success &&
        Lease is not null;
}
