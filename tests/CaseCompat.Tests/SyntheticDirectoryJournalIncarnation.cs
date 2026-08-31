using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

/*
 * TEST-ONLY helper for pure directory-journal model/persistence tests.
 *
 * These tests intentionally use synthetic physical identities rather
 * than identities captured from live filesystem objects. Give those
 * synthetic identities an explicit inode generation so schema-v2
 * journal validation can exercise strong incarnation semantics.
 *
 * Do NOT use this helper in filesystem recovery/action fixtures.
 * Those fixtures must capture the real inode generation from the
 * retained descriptor.
 */
internal static class SyntheticDirectoryJournalIncarnation
{
    public static LinuxDirectoryIncarnationIdentity FromPhysical(
        LinuxFileIdentityResult physicalIdentity,
        uint inodeGeneration = 1U)
    {
        ArgumentNullException.ThrowIfNull(
            physicalIdentity
        );

        return new(
            PhysicalIdentity:
                physicalIdentity,
            InodeGeneration:
                inodeGeneration
        );
    }
}
