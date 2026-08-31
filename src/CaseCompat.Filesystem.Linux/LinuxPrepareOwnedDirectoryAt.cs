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
        /*
         * Capture ownership evidence from the exact opened staging
         * descriptor.
         *
         * Physical identity alone is insufficient because ext4 may
         * immediately reuse an inode number after deletion.
         *
         * Incarnation identity therefore binds:
         *
         *   device
         *   inode
         *   mount ID
         *   inode generation
         */
        LinuxOpenedDirectoryIncarnationResult incarnation =
            LinuxOpenedDirectoryIncarnation.Capture(
                staging,
                displayPath
            );

        LinuxOpenedDirectorySnapshotResult? snapshot =
            incarnation.Snapshot;

        if (!incarnation.Success)
        {
            staging.Dispose();

            LinuxPrepareOwnedDirectoryAtState state =
                incarnation.State switch
                {
                    LinuxOpenedDirectoryIncarnationState
                        .UnsupportedPlatform =>
                            LinuxPrepareOwnedDirectoryAtState
                                .UnsupportedPlatform,

                    LinuxOpenedDirectoryIncarnationState
                        .NotDirectory =>
                            LinuxPrepareOwnedDirectoryAtState
                                .StagingNotDirectory,

                    LinuxOpenedDirectoryIncarnationState
                        .GenerationUnavailable =>
                            LinuxPrepareOwnedDirectoryAtState
                                .StagingGenerationUnavailable,

                    _ =>
                        LinuxPrepareOwnedDirectoryAtState
                            .StagingSnapshotFailed
                };

            return Result(
                state,
                stagingChildName,
                createResult:
                    create,
                openResult:
                    opened,
                snapshot:
                    snapshot,
                incarnation:
                    incarnation,
                stagingEntryChanged:
                    true,
                stagingEntryMayRemain:
                    true,
                error:
                    incarnation.Error ??
                    incarnation.State.ToString()
            );
        }

        LinuxDirectoryIncarnationIdentity incarnationIdentity =
            incarnation.Identity!;

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
                incarnation:
                    incarnation,
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
                incarnationIdentity,
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
            incarnation:
                incarnation,
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
        LinuxOpenedDirectoryIncarnationResult? incarnation = null,
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
            Incarnation:
                incarnation,
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
