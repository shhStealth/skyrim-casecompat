namespace CaseCompat.Filesystem.Linux;

public static class LinuxPrepareOwnedDirectoryAt
{
    public static LinuxPrepareOwnedDirectoryAtResult Prepare(
        ILinuxOpenedHandle parentDirectory,
        string stagingChildName,
        string displayPath)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (string.IsNullOrWhiteSpace(displayPath))
        {
            throw new ArgumentException(
                "A diagnostic display path is required.",
                nameof(displayPath)
            );
        }

        /*
         * mkdirat() itself provides the no-overwrite gate.
         *
         * If the staging name already exists we stop immediately;
         * this primitive never adopts or merges with an existing
         * directory.
         */
        LinuxCreateDirectoryAtResult create =
            LinuxCreateDirectoryAt.Create(
                parentDirectory,
                stagingChildName
            );

        if (!create.Success)
        {
            LinuxPrepareOwnedDirectoryAtState state =
                create.State switch
                {
                    LinuxCreateDirectoryAtState
                        .UnsupportedPlatform =>
                            LinuxPrepareOwnedDirectoryAtState
                                .UnsupportedPlatform,

                    LinuxCreateDirectoryAtState
                        .InvalidName =>
                            LinuxPrepareOwnedDirectoryAtState
                                .InvalidName,

                    LinuxCreateDirectoryAtState
                        .InvalidParentHandle =>
                            LinuxPrepareOwnedDirectoryAtState
                                .InvalidParentHandle,

                    LinuxCreateDirectoryAtState
                        .ParentNotDirectory =>
                            LinuxPrepareOwnedDirectoryAtState
                                .ParentNotDirectory,

                    LinuxCreateDirectoryAtState
                        .DestinationExists =>
                            LinuxPrepareOwnedDirectoryAtState
                                .StagingAlreadyExists,

                    _ =>
                        LinuxPrepareOwnedDirectoryAtState
                            .CreateFailed
                };

            return Result(
                state,
                stagingChildName,
                createResult:
                    create,
                error:
                    create.Error ??
                    create.State.ToString()
            );
        }

        /*
         * From this point onward a new namespace entry has been
         * created. If a later step fails, do not guess at cleanup.
         * Report that a staging entry may remain so higher-level
         * durable recovery can inspect it explicitly.
         */
        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parentDirectory,
                stagingChildName
            );

        if (!opened.Success)
        {
            LinuxPrepareOwnedDirectoryAtState state =
                opened.State switch
                {
                    LinuxOpenChildReadOnlyAtState
                        .InvalidParentHandle =>
                            LinuxPrepareOwnedDirectoryAtState
                                .InvalidParentHandle,

                    LinuxOpenChildReadOnlyAtState
                        .ParentNotDirectory =>
                            LinuxPrepareOwnedDirectoryAtState
                                .ParentNotDirectory,

                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable =>
                            LinuxPrepareOwnedDirectoryAtState
                                .StagingUnavailableAfterCreate,

                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected =>
                            LinuxPrepareOwnedDirectoryAtState
                                .StagingSymbolicLinkRejected,

                    LinuxOpenChildReadOnlyAtState
                        .UnsupportedPlatform =>
                            LinuxPrepareOwnedDirectoryAtState
                                .UnsupportedPlatform,

                    _ =>
                        LinuxPrepareOwnedDirectoryAtState
                            .StagingOpenFailed
                };

            return Result(
                state,
                stagingChildName,
                createResult:
                    create,
                openResult:
                    opened,
                stagingEntryChanged:
                    true,
                stagingEntryMayRemain:
                    true,
                error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        LinuxOpenedChildHandle staging =
            opened.OpenedChild!;

        /*
         * Keep the exact opened directory descriptor. On success
         * ownership transfers into LinuxPreparedOwnedDirectoryLease.
         */
        LinuxOpenedDirectorySnapshotResult snapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                staging,
                displayPath
            );

        if (!snapshot.Success)
        {
            staging.Dispose();

            LinuxPrepareOwnedDirectoryAtState state =
                snapshot.State ==
                LinuxOpenedDirectorySnapshotState.NotDirectory
                    ? LinuxPrepareOwnedDirectoryAtState
                        .StagingNotDirectory
                    : LinuxPrepareOwnedDirectoryAtState
                        .StagingSnapshotFailed;

            return Result(
                state,
                stagingChildName,
                createResult:
                    create,
                openResult:
                    opened,
                snapshot:
                    snapshot,
                stagingEntryChanged:
                    true,
                stagingEntryMayRemain:
                    true,
                error:
                    snapshot.Error ??
                    snapshot.State.ToString()
            );
        }

        LinuxFileIdentityResult identity =
            snapshot.Identity!;

        if (!HasCompleteIdentity(identity))
        {
            staging.Dispose();

            return Result(
                LinuxPrepareOwnedDirectoryAtState
                    .StagingSnapshotFailed,
                stagingChildName,
                createResult:
                    create,
                openResult:
                    opened,
                snapshot:
                    snapshot,
                stagingEntryChanged:
                    true,
                stagingEntryMayRemain:
                    true,
                error:
                    "The prepared staging directory did not " +
                    "produce a complete physical identity " +
                    "including device, inode, and mount ID."
            );
        }

        LinuxFsyncResult parentSync =
            LinuxFsync.Sync(
                parentDirectory
            );

        if (!parentSync.Success)
        {
            staging.Dispose();

            return Result(
                LinuxPrepareOwnedDirectoryAtState
                    .ParentSyncFailed,
                stagingChildName,
                createResult:
                    create,
                openResult:
                    opened,
                snapshot:
                    snapshot,
                parentSync:
                    parentSync,
                stagingEntryChanged:
                    true,
                stagingEntryMayRemain:
                    true,
                error:
                    parentSync.Error ??
                    parentSync.State.ToString()
            );
        }

        var lease =
            new LinuxPreparedOwnedDirectoryLease(
                stagingChildName,
                identity,
                staging
            );

        return Result(
            LinuxPrepareOwnedDirectoryAtState
                .PreparedDurably,
            stagingChildName,
            createResult:
                create,
            openResult:
                opened,
            snapshot:
                snapshot,
            parentSync:
                parentSync,
            lease:
                lease,
            stagingEntryChanged:
                true,
            stagingEntryMayRemain:
                false
        );
    }

    private static bool HasCompleteIdentity(
        LinuxFileIdentityResult identity)
    {
        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null &&
            identity.MountId is not null;
    }

    private static LinuxPrepareOwnedDirectoryAtResult Result(
        LinuxPrepareOwnedDirectoryAtState state,
        string? stagingChildName,
        LinuxCreateDirectoryAtResult? createResult = null,
        LinuxOpenChildReadOnlyAtResult? openResult = null,
        LinuxOpenedDirectorySnapshotResult? snapshot = null,
        LinuxFsyncResult? parentSync = null,
        LinuxPreparedOwnedDirectoryLease? lease = null,
        bool stagingEntryChanged = false,
        bool stagingEntryMayRemain = false,
        string? error = null)
    {
        return new LinuxPrepareOwnedDirectoryAtResult(
            State:
                state,
            StagingChildName:
                stagingChildName ?? string.Empty,
            CreateResult:
                createResult,
            OpenResult:
                openResult,
            Snapshot:
                snapshot,
            ParentSync:
                parentSync,
            Lease:
                lease,
            StagingEntryChanged:
                stagingEntryChanged,
            StagingEntryMayRemain:
                stagingEntryMayRemain,
            Error:
                error
        );
    }
}
