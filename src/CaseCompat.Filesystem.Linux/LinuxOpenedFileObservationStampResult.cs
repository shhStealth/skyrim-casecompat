namespace CaseCompat.Filesystem.Linux;

/*
 * Descriptor-level observational metadata for one regular file.
 *
 * Equality of two successful stamps means that no change was observed
 * through the available physical identity, size, ctime, and mtime fields.
 *
 * It does not provide write exclusion and does not claim that concurrent
 * mutation is impossible.
 */
public enum LinuxOpenedFileObservationStampState
{
    Captured,

    UnsupportedPlatform,
    InvalidHandle,
    NotRegularFile,
    IdentityUnavailable,

    MetadataUnavailable,
    SizeUnavailable
}

public sealed record LinuxOpenedFileObservationStampResult(
    LinuxOpenedFileObservationStampState State,
    LinuxOpenedFileIdentityResult? Identity,
    long? Size,
    long? ChangeTimeSeconds,
    uint? ChangeTimeNanoseconds,
    long? ModificationTimeSeconds,
    uint? ModificationTimeNanoseconds,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxOpenedFileObservationStampState.Captured &&
        Identity is not null &&
        Identity.Success &&
        Identity.MountId is not null &&
        Size is not null &&
        ChangeTimeSeconds is not null &&
        ChangeTimeNanoseconds is not null &&
        ModificationTimeSeconds is not null &&
        ModificationTimeNanoseconds is not null;

    public bool SameObservedStateAs(
        LinuxOpenedFileObservationStampResult other)
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
            Identity!.SameObjectAs(
                other.Identity!
            ) &&
            Size == other.Size &&
            ChangeTimeSeconds ==
                other.ChangeTimeSeconds &&
            ChangeTimeNanoseconds ==
                other.ChangeTimeNanoseconds &&
            ModificationTimeSeconds ==
                other.ModificationTimeSeconds &&
            ModificationTimeNanoseconds ==
                other.ModificationTimeNanoseconds;
    }
}
