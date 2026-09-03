namespace CaseCompat.Filesystem.Linux;

/*
 * Read-only content evidence obtained from one already retained readable
 * regular-file descriptor.
 *
 * StableContentEvidence means:
 *
 * - the pre-observation stamp was complete;
 * - descriptor-backed SHA-256 capture succeeded;
 * - the post-observation stamp was complete;
 * - no difference was observed in physical identity, size, ctime, or mtime.
 *
 * This is observational evidence. It is not write exclusion and does not
 * claim that concurrent mutation is impossible.
 */
public enum LinuxOpenedFileContentObservationState
{
    StableContentEvidence,
    ChangedDuringObservation,
    IncompleteEvidence
}

public sealed record LinuxOpenedFileContentObservationResult(
    LinuxOpenedFileContentObservationState State,
    string DisplayPath,
    LinuxOpenedFileObservationStampResult? Before,
    LinuxOpenedFileSnapshotResult? Snapshot,
    LinuxOpenedFileObservationStampResult? After,
    long? Size,
    string? Sha256,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxOpenedFileContentObservationState
                .StableContentEvidence &&
        Before is not null &&
        Before.Success &&
        Snapshot is not null &&
        Snapshot.Success &&
        After is not null &&
        After.Success &&
        Size is not null &&
        Sha256 is not null;
}
