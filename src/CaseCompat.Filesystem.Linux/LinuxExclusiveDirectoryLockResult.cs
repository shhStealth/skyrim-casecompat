using Microsoft.Win32.SafeHandles;

namespace CaseCompat.Filesystem.Linux;

public enum LinuxExclusiveDirectoryLockState
{
    Acquired,

    UnsupportedPlatform,
    InvalidParentHandle,
    ParentNotDirectory,
    LockDescriptorOpenFailed,

    AlreadyLocked,
    LockInterrupted,
    LockResourceUnavailable,
    LockFailed
}

public sealed class LinuxExclusiveDirectoryLockLease
    : IDisposable
{
    internal LinuxExclusiveDirectoryLockLease(
        SafeFileHandle lockHandle)
    {
        LockHandle =
            lockHandle;
    }

    internal SafeFileHandle LockHandle { get; }

    public bool IsHeld =>
        !LockHandle.IsClosed &&
        !LockHandle.IsInvalid;

    public void Dispose()
    {
        /*
         * flock() locks are associated with the open file
         * description. This lease owns a dedicated directory
         * descriptor, so closing it releases the lock.
         */
        LockHandle.Dispose();
    }
}

public sealed record LinuxExclusiveDirectoryLockResult(
    LinuxExclusiveDirectoryLockState State,
    LinuxExclusiveDirectoryLockLease? Lease,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxExclusiveDirectoryLockState.Acquired &&
        Lease is not null;
}
