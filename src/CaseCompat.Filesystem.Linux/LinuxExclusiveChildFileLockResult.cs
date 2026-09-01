namespace CaseCompat.Filesystem.Linux;

public enum LinuxExclusiveChildFileLockState
{
    Locked,

    UnsupportedPlatform,
    InvalidName,
    InvalidParentHandle,
    ParentNotDirectory,

    ChildSymbolicLinkRejected,
    ChildNotRegularFile,
    ChildIdentityUnavailable,
    ChildOpenFailed,

    AlreadyLocked,
    LockTableUnavailable,
    LockFailed
}

public sealed record LinuxExclusiveChildFileLockResult(
    LinuxExclusiveChildFileLockState State,
    string ChildName,
    LinuxOpenedFileIdentityResult? OpenedIdentity,
    LinuxExclusiveChildFileLockLease? Lease,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxExclusiveChildFileLockState.Locked &&
        Lease is not null &&
        Lease.IsHeld;
}

public sealed class LinuxExclusiveChildFileLockLease
    : IDisposable
{
    private LinuxOpenedChildHandle? _openedChild;

    internal LinuxExclusiveChildFileLockLease(
        string childName,
        LinuxOpenedChildHandle openedChild)
    {
        ArgumentNullException.ThrowIfNull(
            openedChild
        );

        ChildName =
            childName;

        _openedChild =
            openedChild;
    }

    public string ChildName { get; }

    public bool IsHeld
    {
        get
        {
            LinuxOpenedChildHandle? opened =
                _openedChild;

            return
                opened is not null &&
                !opened.Handle.IsInvalid &&
                !opened.Handle.IsClosed;
        }
    }

    public void Dispose()
    {
        LinuxOpenedChildHandle? opened =
            System.Threading.Interlocked.Exchange(
                ref _openedChild,
                null
            );

        opened?.Dispose();
    }
}
