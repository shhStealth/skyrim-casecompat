namespace CaseCompat.Filesystem.Linux;

/*
 * Observe the contents of one already retained readable regular-file
 * descriptor without consulting its pathname for filesystem authority.
 *
 * Evidence sequence:
 *
 * 1. capture descriptor-level identity/size/ctime/mtime;
 * 2. hash the same retained readable descriptor;
 * 3. capture descriptor-level identity/size/ctime/mtime again;
 * 4. reject the hash as stable evidence if any observed state changed.
 *
 * LinuxOpenedFileSnapshot is intentionally reused unchanged. This primitive
 * adds the stronger pre/post observational envelope needed by namespace
 * content analysis without altering existing Repair semantics.
 */
public static class LinuxOpenedFileContentObservation
{
    public static LinuxOpenedFileContentObservationResult Observe(
        ILinuxOpenedHandle openedFile,
        string displayPath)
    {
        ArgumentNullException.ThrowIfNull(
            openedFile
        );

        ArgumentNullException.ThrowIfNull(
            displayPath
        );

        LinuxOpenedFileObservationStampResult before =
            LinuxOpenedFileObservationStamp.Capture(
                openedFile
            );

        if (!before.Success)
        {
            return Result(
                LinuxOpenedFileContentObservationState
                    .IncompleteEvidence,
                displayPath,
                before:
                    before,
                error:
                    "The pre-hash file observation is incomplete: " +
                    (
                        before.Error ??
                        before.State.ToString()
                    )
            );
        }

        LinuxOpenedFileSnapshotResult snapshot =
            LinuxOpenedFileSnapshot.Capture(
                openedFile,
                displayPath
            );

        /*
         * Capture the post-observation even when hashing failed.
         *
         * It can provide useful evidence to callers and, in particular,
         * preserves the complete observation envelope around a direct
         * SizeChangedDuringHash result.
         */
        LinuxOpenedFileObservationStampResult after =
            LinuxOpenedFileObservationStamp.Capture(
                openedFile
            );

        if (
            snapshot.State ==
            LinuxOpenedFileSnapshotState
                .SizeChangedDuringHash)
        {
            return Result(
                LinuxOpenedFileContentObservationState
                    .ChangedDuringObservation,
                displayPath,
                before:
                    before,
                snapshot:
                    snapshot,
                after:
                    after,
                error:
                    snapshot.Error ??
                    "The opened file size changed while " +
                    "its contents were being hashed."
            );
        }

        if (!snapshot.Success)
        {
            return Result(
                LinuxOpenedFileContentObservationState
                    .IncompleteEvidence,
                displayPath,
                before:
                    before,
                snapshot:
                    snapshot,
                after:
                    after,
                error:
                    "Descriptor-backed content hashing is incomplete: " +
                    (
                        snapshot.Error ??
                        snapshot.State.ToString()
                    )
            );
        }

        if (!after.Success)
        {
            return Result(
                LinuxOpenedFileContentObservationState
                    .IncompleteEvidence,
                displayPath,
                before:
                    before,
                snapshot:
                    snapshot,
                after:
                    after,
                error:
                    "The post-hash file observation is incomplete: " +
                    (
                        after.Error ??
                        after.State.ToString()
                    )
            );
        }

        if (
            !before.SameObservedStateAs(
                after
            ))
        {
            return Result(
                LinuxOpenedFileContentObservationState
                    .ChangedDuringObservation,
                displayPath,
                before:
                    before,
                snapshot:
                    snapshot,
                after:
                    after,
                error:
                    "The opened file's physical identity, size, " +
                    "ctime, or mtime changed during content " +
                    "observation."
            );
        }

        /*
         * LinuxOpenedFileSnapshot performs its own size capture around the
         * hash. Require that evidence to agree with both outer stamps too.
         */
        if (
            snapshot.Size != before.Size ||
            snapshot.Size != after.Size)
        {
            return Result(
                LinuxOpenedFileContentObservationState
                    .ChangedDuringObservation,
                displayPath,
                before:
                    before,
                snapshot:
                    snapshot,
                after:
                    after,
                error:
                    "The descriptor-backed hash size does not " +
                    "match the stable outer observation size."
            );
        }

        return new LinuxOpenedFileContentObservationResult(
            State:
                LinuxOpenedFileContentObservationState
                    .StableContentEvidence,
            DisplayPath:
                displayPath,
            Before:
                before,
            Snapshot:
                snapshot,
            After:
                after,
            Size:
                snapshot.Size,
            Sha256:
                snapshot.Sha256,
            Error:
                null
        );
    }

    private static LinuxOpenedFileContentObservationResult Result(
        LinuxOpenedFileContentObservationState state,
        string displayPath,
        LinuxOpenedFileObservationStampResult? before = null,
        LinuxOpenedFileSnapshotResult? snapshot = null,
        LinuxOpenedFileObservationStampResult? after = null,
        string? error = null)
    {
        return new LinuxOpenedFileContentObservationResult(
            State:
                state,
            DisplayPath:
                displayPath,
            Before:
                before,
            Snapshot:
                snapshot,
            After:
                after,
            Size:
                null,
            Sha256:
                null,
            Error:
                error
        );
    }
}
