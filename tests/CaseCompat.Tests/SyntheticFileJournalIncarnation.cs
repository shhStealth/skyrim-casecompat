using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

/*
 * TEST-ONLY helper for pure file-journal model/persistence tests.
 *
 * These tests intentionally use synthetic opened-file physical
 * identities rather than identities captured from live filesystem
 * objects. Give those synthetic identities an explicit inode
 * generation so schema-v2 journal validation can exercise strong
 * file-incarnation semantics.
 *
 * Use this helper when a test deliberately models synthetic
 * historical journal authority. Tests whose purpose depends on
 * the live incarnation of a filesystem object should capture the
 * real inode generation from that object's retained descriptor.
 */
internal static class SyntheticFileJournalIncarnation
{
    public static LinuxFileIncarnationIdentity FromPhysical(
        LinuxOpenedFileIdentityResult physicalIdentity,
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
