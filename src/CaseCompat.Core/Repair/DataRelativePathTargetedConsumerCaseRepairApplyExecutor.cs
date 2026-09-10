using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Independent, scoped-down forward executor for one targeted
// consumer-authoritative repair plan.
//
// This is intentionally not a variant of the legacy
// DataRelativePathRepairFileExecutor. It re-derives fresh filesystem proof
// - source identity, source inode generation, destination absence -
// immediately before mutating, exactly as every other boundary in this
// pipeline (admission, durable-plan creation) re-derives fresh proof
// rather than trusting an earlier snapshot.
//
// The final CreateFile step is published by RENAMING the source's
// existing inode to the correctly-cased destination name, not by copying
// its bytes into a new inode. This is a deliberate correctness property,
// not just an optimization: after a successful apply there is exactly
// one directory entry for this asset - the correctly-cased one - the
// same end state the original Windows/NTFS install had. A copy-based
// publish leaves both the original mismatched-case name and the new
// correctly-cased name resolvable side by side, a directory shape that
// cannot exist on the source platform and that at least one in-game
// subsystem (Skyrim's FaceGen morph-asset resolution, used by SKEE64/
// RaceMenu) has been observed to handle incorrectly when it occurs.
// Because this only renames a single directory entry, the candidate
// projector's guarantee that every candidate carries exactly one
// agreed-upon winning consumer spelling (see
// SkyrimWinningTargetedConsumerCaseRepairCandidateProjector) is what
// makes this safe: nothing else in the load order is still looking for
// the old name.
//
// A durable Intent -> Prepared -> Applied journal still covers the
// final rename, matching every plan this session's pipeline produces
// (zero or more CreateDirectory operations followed by exactly one
// CreateFile). Because a rename is a single atomic metadata operation
// with no separate "materialized but not yet visible" state, Prepared
// and Applied necessarily carry the same physical incarnation identity
// here - the source's own inode, never a new one. Prepared is recorded
// immediately before the rename syscall anyway, so an interrupted apply
// still leaves a durable record of exactly which identity was about to
// move and where.
//
// Intermediate CreateDirectory operations come in two flavors. When no
// case-insensitive match exists for a path segment, a brand-new,
// correctly-cased directory is created (SourcePath is null). When a
// case-insensitive match already exists, the plan instead renames that
// existing directory - and everything still inside it, including sibling
// assets this repair run never touched - in place to the correctly-cased
// name (SourcePath is the matched physical path; see
// RenameExistingDirectoryIntoPlace). This is the same "rename, don't
// duplicate" correctness property as the file case above, and for the
// same reason: splitting one populated mod directory into an old,
// still-populated tree and a new, sparsely-populated correctly-cased
// tree is exactly the shape that was observed to break FaceGen
// resolution in-game, since a directory-enumerating consumer only sees
// whichever tree it happens to resolve into.
//
// There is no automated crash-forward-recovery layer here. Resuming
// from a partial apply is a manual concern, not an automated reconciler,
// by deliberate scope decision. Rollback undoes only the renamed file
// (by renaming it back); directory-level operations are never rolled
// back - a brand-new empty directory is harmless to leave behind, and a
// renamed-in-place existing directory cannot be safely reversed without
// re-deriving the same ambiguity the forward rename resolved, so
// directories are left exactly where the apply put them.
public enum DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
{
    AppliedDurably,

    InvalidPlan,
    UnsupportedOperationShape,
    DataRootMismatch,

    SourceOpenFailed,
    SourceIdentityUnavailable,
    SourceIdentityMismatch,
    SourceGenerationMismatch,

    DestinationParentOpenFailed,
    DirectoryInspectionFailed,
    DirectoryAlreadyExists,
    DirectoryCreateFailed,
    DirectoryReopenFailed,

    DirectoryRenameSourceMissing,
    DirectorySourceOpenFailed,
    DirectorySourceIdentityUnavailable,
    DirectorySourceIdentityMismatch,
    DirectorySourceGenerationMismatch,
    DirectoryRenameFailed,
    DirectoryDestinationParentSyncFailed,

    AliasesDirectoryRequired,
    AliasSourceMissing,
    AliasInspectionFailed,
    AliasTargetOpenFailed,
    AliasTargetIdentityUnavailable,
    AliasTargetIdentityMismatch,
    AliasTargetGenerationMismatch,
    AliasAlreadyExists,
    AliasCreateFailed,
    AliasParentSyncFailed,
    AliasRegistrationFailed,

    VerifyExistingDirectoryMissing,
    VerifyExistingDirectoryInspectionFailed,

    DestinationInspectionFailed,
    DestinationExists,

    InitialJournalInvalid,
    InitialJournalWriteFailed,

    PreparedJournalInvalid,
    PreparedJournalWriteFailed,

    RenameFailed,
    CrossDeviceRenameNotSupported,

    SourceParentSyncFailed,
    DestinationParentSyncFailed,
    AppliedIdentityFailed,
    AppliedIdentityMismatch,
    AppliedJournalInvalid,
    AppliedJournalWriteFailed
}

public sealed record DataRelativePathTargetedConsumerCaseRepairApplyExecution(
    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState State,
    Guid PlanId,
    string? DestinationPath,
    IReadOnlyList<string> CreatedDirectoryPaths,
    string? IntentJournalChildName,
    string? PreparedJournalChildName,
    string? AppliedJournalChildName,
    LinuxFileIncarnationIdentity? AppliedFileIncarnationIdentity,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
            .AppliedDurably;
}

public static class DataRelativePathTargetedConsumerCaseRepairApplyExecutor
{
    public static DataRelativePathTargetedConsumerCaseRepairApplyExecution
        Execute(
            LinuxNoFollowPathHandle trustedDataRoot,
            LinuxNoFollowPathHandle journalDirectory,
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan,
            DateTimeOffset nowUtc,
            LinuxNoFollowPathHandle? aliasesDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(
            trustedDataRoot
        );

        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            plan
        );

        string? planValidationError =
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                plan
            );

        if (planValidationError is not null)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .InvalidPlan,
                plan.PlanId,
                error:
                    planValidationError
            );
        }

        if (
            plan.Operations.Count == 0 ||
            plan.Operations[^1].Kind !=
                DataRelativePathRepairPlanOperationKind.CreateFile ||
            plan.Operations
                .Take(
                    plan.Operations.Count - 1
                )
                .Any(
                    op =>
                        op.Kind is not (
                            DataRelativePathRepairPlanOperationKind
                                .CreateDirectory or
                            DataRelativePathRepairPlanOperationKind
                                .CreateAliasSymlink or
                            DataRelativePathRepairPlanOperationKind
                                .VerifyExistingDirectory
                        )
                ))
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .UnsupportedOperationShape,
                plan.PlanId,
                error:
                    "This executor supports plans whose operations are " +
                    "zero or more CreateDirectory/CreateAliasSymlink/" +
                    "VerifyExistingDirectory steps followed by exactly " +
                    "one final CreateFile step."
            );
        }

        DataRelativePathRepairPlanOperation[] directoryOperations =
            plan.Operations
                .Take(
                    plan.Operations.Count - 1
                )
                .ToArray();

        DataRelativePathRepairPlanOperation fileOperation =
            plan.Operations[^1];

        if (
            !string.Equals(
                trustedDataRoot.FullPath,
                plan.DataRoot,
                StringComparison.Ordinal))
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DataRootMismatch,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                error:
                    "The caller-authorized Data root does not match the " +
                    "plan's Data root."
            );
        }

        string? sourceParentPath =
            Path.GetDirectoryName(
                plan.SourceSnapshot.PhysicalPath
            );

        string sourceChildName =
            Path.GetFileName(
                plan.SourceSnapshot.PhysicalPath
            );

        if (sourceParentPath is null)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceOpenFailed,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                error:
                    "The source physical path has no parent directory."
            );
        }

        LinuxNoFollowPathOpenResult sourceParentOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                sourceParentPath
            );

        if (!sourceParentOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceOpenFailed,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                error:
                    sourceParentOpen.Error ??
                    sourceParentOpen.State.ToString()
            );
        }

        using LinuxNoFollowPathHandle sourceParent =
            sourceParentOpen.OpenedPath!;

        LinuxOpenChildRegularFileReadOnlyAtResult sourceOpen =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                sourceParent,
                sourceChildName
            );

        if (!sourceOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceOpenFailed,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                error:
                    sourceOpen.Error ??
                    sourceOpen.State.ToString()
            );
        }

        using LinuxOpenedChildHandle source =
            sourceOpen.OpenedFile!;

        LinuxOpenedFileIncarnationResult sourceIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                source
            );

        if (!sourceIncarnation.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceIdentityUnavailable,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                error:
                    sourceIncarnation.Error ??
                    sourceIncarnation.State.ToString()
            );
        }

        LinuxFileIncarnationIdentity sourceIdentity =
            sourceIncarnation.Identity!;

        LinuxFileIdentityResult expectedSourceIdentity =
            plan.SourceSnapshot.Identity;

        if (
            sourceIdentity.PhysicalIdentity.DeviceMajor !=
                expectedSourceIdentity.DeviceMajor ||
            sourceIdentity.PhysicalIdentity.DeviceMinor !=
                expectedSourceIdentity.DeviceMinor ||
            sourceIdentity.PhysicalIdentity.Inode !=
                expectedSourceIdentity.Inode ||
            sourceIdentity.PhysicalIdentity.MountId !=
                expectedSourceIdentity.MountId)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceIdentityMismatch,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                error:
                    "The freshly reacquired source physical identity does " +
                    "not equal the durable plan's source snapshot identity."
            );
        }

        if (sourceIdentity.InodeGeneration != plan.SourceInodeGeneration)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceGenerationMismatch,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                error:
                    "The freshly reacquired source inode generation does " +
                    "not equal the durable plan's recorded generation."
            );
        }

        LinuxNoFollowPathOpenResult startingParentOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                plan.InitialDestinationParentSnapshot.PhysicalPath
            );

        if (!startingParentOpen.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DestinationParentOpenFailed,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                error:
                    startingParentOpen.Error ??
                    startingParentOpen.State.ToString()
            );
        }

        var createdDirectoryPaths =
            new List<string>();

        LinuxNoFollowPathHandle? currentParent =
            startingParentOpen.OpenedPath!;

        // Tracks the plan-declared (logical) parent path each operation
        // must be a direct child of. This is deliberately independent of
        // currentParent's own physical path: once an alias is created,
        // currentParent is reopened via the alias's real physical target
        // (see below) so that every subsequent filesystem operation lands
        // in the one real directory - never through the symlink itself -
        // while the plan's own destination-path bookkeeping continues to
        // use the alias's declared casing, exactly as validated by
        // DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate.
        string expectedParentPath =
            currentParent.FullPath;

        try
        {
            foreach (
                DataRelativePathRepairPlanOperation directoryOperation
                in directoryOperations)
            {
                string? directoryParentPath =
                    Path.GetDirectoryName(
                        directoryOperation.DestinationPath
                    );

                if (
                    directoryParentPath is null ||
                    !string.Equals(
                        directoryParentPath,
                        expectedParentPath,
                        StringComparison.Ordinal))
                {
                    return Result(
                        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .UnsupportedOperationShape,
                        plan.PlanId,
                        destinationPath:
                            fileOperation.DestinationPath,
                        createdDirectoryPaths:
                            createdDirectoryPaths,
                        error:
                            "A CreateDirectory operation's parent does " +
                            "not match the previous step's exact " +
                            "destination."
                    );
                }

                string directoryChildName =
                    Path.GetFileName(
                        directoryOperation.DestinationPath
                    );

                bool isAliasOperation =
                    directoryOperation.Kind ==
                    DataRelativePathRepairPlanOperationKind
                        .CreateAliasSymlink;

                bool isVerifyOperation =
                    directoryOperation.Kind ==
                    DataRelativePathRepairPlanOperationKind
                        .VerifyExistingDirectory;

                // An alias operation skips this generic preflight
                // entirely - CreateAliasIntoPlace runs its own targeted
                // probe instead, since (unlike a directory) an alias that
                // already exists and is already exactly correct is a
                // legitimate idempotent reuse, not a conflict. See its
                // own comment for why.
                //
                // A verify operation skips it too, for the opposite
                // reason: it exists specifically BECAUSE the projector
                // already found this exact-cased directory in place, so
                // "already exists" is the expected, required state here,
                // not a conflict - see the dedicated existence check
                // below instead.
                if (!isAliasOperation && !isVerifyOperation)
                {
                    LinuxInspectChildAtResult directoryPreflight =
                        LinuxInspectChildAt.Inspect(
                            currentParent,
                            directoryChildName
                        );

                    if (directoryPreflight.Success)
                    {
                        return Result(
                            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                .DirectoryAlreadyExists,
                            plan.PlanId,
                            destinationPath:
                                fileOperation.DestinationPath,
                            createdDirectoryPaths:
                                createdDirectoryPaths,
                            error:
                                "The requested directory already " +
                                "exists. Forward apply never " +
                                "overwrites it."
                        );
                    }

                    if (
                        directoryPreflight.State !=
                        LinuxInspectChildAtState.ChildUnavailable)
                    {
                        return Result(
                            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                .DirectoryInspectionFailed,
                            plan.PlanId,
                            destinationPath:
                                fileOperation.DestinationPath,
                            createdDirectoryPaths:
                                createdDirectoryPaths,
                            error:
                                directoryPreflight.Error ??
                                directoryPreflight.State.ToString()
                        );
                    }
                }

                if (isAliasOperation)
                {
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState?
                        aliasFailureState =
                            CreateAliasIntoPlace(
                                plan,
                                directoryOperation,
                                currentParent,
                                directoryChildName,
                                aliasesDirectory,
                                nowUtc,
                                out string? aliasFailureError
                            );

                    if (aliasFailureState is
                        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            failureState)
                    {
                        return Result(
                            failureState,
                            plan.PlanId,
                            destinationPath:
                                fileOperation.DestinationPath,
                            createdDirectoryPaths:
                                createdDirectoryPaths,
                            error:
                                aliasFailureError
                        );
                    }
                }
                else if (isVerifyOperation)
                {
                    // Always re-derive fresh proof immediately before
                    // trusting it: the plan says this directory was
                    // exactly correct at projection time, but that is a
                    // hint, not a fact this apply may rely on without
                    // re-checking. No mutation happens either way - the
                    // whole point of this step is that none is needed.
                    LinuxInspectChildAtResult verifyInspect =
                        LinuxInspectChildAt.Inspect(
                            currentParent,
                            directoryChildName
                        );

                    if (!verifyInspect.Success)
                    {
                        return Result(
                            verifyInspect.State ==
                            LinuxInspectChildAtState.ChildUnavailable
                                ? DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                    .VerifyExistingDirectoryMissing
                                : DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                    .VerifyExistingDirectoryInspectionFailed,
                            plan.PlanId,
                            destinationPath:
                                fileOperation.DestinationPath,
                            createdDirectoryPaths:
                                createdDirectoryPaths,
                            error:
                                verifyInspect.Error ??
                                verifyInspect.State.ToString()
                        );
                    }

                    if (
                        verifyInspect.Kind !=
                        LinuxChildObjectKind.Directory)
                    {
                        return Result(
                            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                .VerifyExistingDirectoryInspectionFailed,
                            plan.PlanId,
                            destinationPath:
                                fileOperation.DestinationPath,
                            createdDirectoryPaths:
                                createdDirectoryPaths,
                            error:
                                "The requested path component already " +
                                "exists with the exact requested casing " +
                                "but is not a directory."
                        );
                    }
                }
                else if (directoryOperation.SourcePath is null)
                {
                    LinuxCreateDirectoryAtResult directoryCreate =
                        LinuxCreateDirectoryAt.Create(
                            currentParent,
                            directoryChildName
                        );

                    if (!directoryCreate.Success)
                    {
                        return Result(
                            directoryCreate.State ==
                            LinuxCreateDirectoryAtState.DestinationExists
                                ? DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                    .DirectoryAlreadyExists
                                : DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                    .DirectoryCreateFailed,
                            plan.PlanId,
                            destinationPath:
                                fileOperation.DestinationPath,
                            createdDirectoryPaths:
                                createdDirectoryPaths,
                            error:
                                directoryCreate.Error ??
                                directoryCreate.State.ToString()
                        );
                    }
                }
                else
                {
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState?
                        renameFailureState =
                            RenameExistingDirectoryIntoPlace(
                                plan,
                                directoryOperation,
                                currentParent,
                                directoryChildName,
                                out string? renameFailureError
                            );

                    if (renameFailureState is
                        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            failureState)
                    {
                        return Result(
                            failureState,
                            plan.PlanId,
                            destinationPath:
                                fileOperation.DestinationPath,
                            createdDirectoryPaths:
                                createdDirectoryPaths,
                            error:
                                renameFailureError
                        );
                    }
                }

                createdDirectoryPaths.Add(
                    directoryOperation.DestinationPath
                );

                expectedParentPath =
                    directoryOperation.DestinationPath;

                // Reopening past an alias never follows the symlink
                // itself. The alias's SourcePath always holds the real
                // target's own physical (non-symlink) path, so this
                // reopens the same real directory the symlink points at
                // directly - sidestepping this project's blanket
                // "reject any symlink" traversal policy entirely for the
                // executor's own internal walk, rather than weakening it.
                //
                // Both branches reopen descriptor-relative to the
                // already-open, currently correct currentParent
                // (OpenReadOnlyUnderRoot), never via a fresh absolute-path
                // walk from the filesystem root. A single absolute
                // open(2) call only applies O_NOFOLLOW to the path's
                // final component - the kernel transparently follows a
                // symlink sitting at any earlier component - so
                // reopening via directoryOperation.DestinationPath's own
                // recorded string would silently walk back through an
                // earlier alias segment already resolved in this same
                // loop, corrupting currentParent.FullPath (and, in turn,
                // the ParentPath recorded for any alias created deeper
                // in this same operation list) with that alias's
                // declared casing instead of the real physical name.
                // directoryChildName is always the real, physically
                // verified name for the non-alias case (just created,
                // renamed, or verified above), so this reopen is exactly
                // as safe as the alias branch's.
                LinuxNoFollowPathOpenResult reopen =
                    isAliasOperation
                        ? LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                            currentParent.FullPath,
                            Path.GetFileName(
                                directoryOperation.SourcePath!
                            )
                        )
                        : LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                            currentParent.FullPath,
                            directoryChildName
                        );

                if (!reopen.Success)
                {
                    return Result(
                        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .DirectoryReopenFailed,
                        plan.PlanId,
                        destinationPath:
                            fileOperation.DestinationPath,
                        createdDirectoryPaths:
                            createdDirectoryPaths,
                        error:
                            reopen.Error ??
                            reopen.State.ToString()
                    );
                }

                currentParent.Dispose();

                currentParent =
                    reopen.OpenedPath!;
            }

            LinuxNoFollowPathHandle destinationParent =
                currentParent;

            string? finalParentPath =
                Path.GetDirectoryName(
                    fileOperation.DestinationPath
                );

            if (
                finalParentPath is null ||
                !string.Equals(
                    finalParentPath,
                    expectedParentPath,
                    StringComparison.Ordinal))
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .UnsupportedOperationShape,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    error:
                        "The final CreateFile operation's parent does " +
                        "not match the last created directory (or the " +
                        "plan's initial destination-parent snapshot when " +
                        "no directories were needed)."
                );
            }

            string destinationChildName =
                Path.GetFileName(
                    fileOperation.DestinationPath
                );

            LinuxInspectChildAtResult destinationPreflight =
                LinuxInspectChildAt.Inspect(
                    destinationParent,
                    destinationChildName
                );

            if (destinationPreflight.Success)
            {
                // Not necessarily a conflict: when every ancestor
                // between source and destination resolves (via alias
                // and/or exact-match verification) back to the same
                // real directory, and the leaf's own name was never
                // itself mismatched, source and destination are
                // literally the same file reached two different ways -
                // there is nothing left to rename. Re-derive fresh
                // proof of that (never trust the preflight alone):
                // open what is actually sitting at the destination and
                // compare its own incarnation against the source's.
                LinuxOpenChildRegularFileReadOnlyAtResult
                    existingDestinationOpen =
                        LinuxOpenChildRegularFileReadOnlyAt.Open(
                            destinationParent,
                            destinationChildName
                        );

                if (
                    existingDestinationOpen.Success &&
                    existingDestinationOpen.OpenedFile is not null)
                {
                    using LinuxOpenedChildHandle existingDestination =
                        existingDestinationOpen.OpenedFile;

                    LinuxOpenedFileIncarnationResult
                        existingDestinationIncarnation =
                            LinuxOpenedFileIncarnation.Capture(
                                existingDestination
                            );

                    if (
                        existingDestinationIncarnation.Success &&
                        existingDestinationIncarnation.Identity is not null &&
                        sourceIdentity.SameIncarnationAs(
                            existingDestinationIncarnation.Identity))
                    {
                        return Result(
                            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                .AppliedDurably,
                            plan.PlanId,
                            destinationPath:
                                fileOperation.DestinationPath,
                            createdDirectoryPaths:
                                createdDirectoryPaths,
                            appliedFileIncarnationIdentity:
                                existingDestinationIncarnation.Identity
                        );
                    }
                }

                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .DestinationExists,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    error:
                        "The destination already exists. Forward apply " +
                        "never overwrites it."
                );
            }

            if (
                destinationPreflight.State !=
                LinuxInspectChildAtState.ChildUnavailable)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .DestinationInspectionFailed,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    error:
                        destinationPreflight.Error ??
                        destinationPreflight.State.ToString()
                );
            }

            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
                intentTransition =
                    DataRelativePathTargetedConsumerCaseRepairApplyJournal
                        .CreateIntent(
                            plan.PlanId,
                            nowUtc,
                            plan.DataRoot,
                            fileOperation,
                            plan.SourceSnapshot
                        );

            if (!intentTransition.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .InitialJournalInvalid,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    error:
                        intentTransition.Error ??
                        intentTransition.State.ToString()
                );
            }

            string intentChildName =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .IntentChildName(
                        plan.PlanId
                    );

            DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
                intentWrite =
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                        .CreateInitial(
                            journalDirectory,
                            intentChildName,
                            intentTransition.Record!
                        );

            if (!intentWrite.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .InitialJournalWriteFailed,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    error:
                        intentWrite.Error ??
                        intentWrite.State.ToString()
                );
            }

            // Rename-based apply has no separate materialize-then-reveal
            // step, so Prepared and Applied necessarily carry the same
            // physical incarnation: the source's own inode. Prepared is
            // still recorded, immediately before the rename syscall, so
            // an interrupted apply leaves a durable record of exactly
            // which identity was about to move and where.
            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
                preparedTransition =
                    DataRelativePathTargetedConsumerCaseRepairApplyJournal
                        .MarkPrepared(
                            intentTransition.Record!,
                            sourceIdentity,
                            nowUtc
                        );

            if (!preparedTransition.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .PreparedJournalInvalid,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    intentJournalChildName:
                        intentChildName,
                    error:
                        preparedTransition.Error ??
                        preparedTransition.State.ToString()
                );
            }

            string preparedChildName =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .PreparedChildName(
                        plan.PlanId
                    );

            DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
                preparedWrite =
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                        .CreateInitial(
                            journalDirectory,
                            preparedChildName,
                            preparedTransition.Record!
                        );

            if (!preparedWrite.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .PreparedJournalWriteFailed,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    intentJournalChildName:
                        intentChildName,
                    error:
                        preparedWrite.Error ??
                        preparedWrite.State.ToString()
                );
            }

            LinuxRenameChildAtResult rename =
                LinuxRenameChildAt.Rename(
                    sourceParent,
                    sourceChildName,
                    destinationParent,
                    destinationChildName
                );

            if (!rename.Success)
            {
                return Result(
                    rename.State ==
                    LinuxRenameChildAtState.CrossDeviceRenameNotSupported
                        ? DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .CrossDeviceRenameNotSupported
                        : rename.State ==
                          LinuxRenameChildAtState.DestinationExists
                            ? DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                .DestinationExists
                            : DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                                .RenameFailed,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    intentJournalChildName:
                        intentChildName,
                    preparedJournalChildName:
                        preparedChildName,
                    error:
                        rename.Error ??
                        rename.State.ToString()
                );
            }

            bool sourceAndDestinationParentDiffer =
                !string.Equals(
                    sourceParent.FullPath,
                    destinationParent.FullPath,
                    StringComparison.Ordinal
                );

            if (sourceAndDestinationParentDiffer)
            {
                LinuxFsyncResult sourceParentSync =
                    LinuxFsync.Sync(
                        sourceParent
                    );

                if (!sourceParentSync.Success)
                {
                    return Result(
                        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .SourceParentSyncFailed,
                        plan.PlanId,
                        destinationPath:
                            fileOperation.DestinationPath,
                        createdDirectoryPaths:
                            createdDirectoryPaths,
                        intentJournalChildName:
                            intentChildName,
                        preparedJournalChildName:
                            preparedChildName,
                        error:
                            sourceParentSync.Error ??
                            sourceParentSync.State.ToString()
                    );
                }
            }

            LinuxFsyncResult destinationParentSync =
                LinuxFsync.Sync(
                    destinationParent
                );

            if (!destinationParentSync.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .DestinationParentSyncFailed,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    intentJournalChildName:
                        intentChildName,
                    preparedJournalChildName:
                        preparedChildName,
                    error:
                        destinationParentSync.Error ??
                        destinationParentSync.State.ToString()
                );
            }

            LinuxOpenChildRegularFileReadOnlyAtResult renamedOpen =
                LinuxOpenChildRegularFileReadOnlyAt.Open(
                    destinationParent,
                    destinationChildName
                );

            if (!renamedOpen.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .AppliedIdentityFailed,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    intentJournalChildName:
                        intentChildName,
                    preparedJournalChildName:
                        preparedChildName,
                    error:
                        renamedOpen.Error ??
                        renamedOpen.State.ToString()
                );
            }

            using LinuxOpenedChildHandle renamed =
                renamedOpen.OpenedFile!;

            LinuxOpenedFileIncarnationResult appliedIncarnation =
                LinuxOpenedFileIncarnation.Capture(
                    renamed
                );

            if (!appliedIncarnation.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .AppliedIdentityFailed,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    intentJournalChildName:
                        intentChildName,
                    preparedJournalChildName:
                        preparedChildName,
                    error:
                        appliedIncarnation.Error ??
                        appliedIncarnation.State.ToString()
                );
            }

            // A rename cannot change the underlying inode. This is a
            // sanity assertion, not a meaningful new risk: it confirms
            // the object now visible at the destination name is exactly
            // the same physical object captured before the rename.
            if (
                !sourceIdentity.SameIncarnationAs(
                    appliedIncarnation.Identity!
                ))
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .AppliedIdentityMismatch,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    intentJournalChildName:
                        intentChildName,
                    preparedJournalChildName:
                        preparedChildName,
                    error:
                        "The renamed destination's physical identity did " +
                        "not equal the pre-rename source identity."
                );
            }

            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
                appliedTransition =
                    DataRelativePathTargetedConsumerCaseRepairApplyJournal
                        .MarkApplied(
                            preparedTransition.Record!,
                            appliedIncarnation.Identity!,
                            nowUtc
                        );

            if (!appliedTransition.Success)
            {
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .AppliedJournalInvalid,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    intentJournalChildName:
                        intentChildName,
                    preparedJournalChildName:
                        preparedChildName,
                    appliedFileIncarnationIdentity:
                        appliedIncarnation.Identity,
                    error:
                        appliedTransition.Error ??
                        appliedTransition.State.ToString()
                );
            }

            string appliedChildName =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .AppliedChildName(
                        plan.PlanId
                    );

            DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
                appliedWrite =
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                        .CreateInitial(
                            journalDirectory,
                            appliedChildName,
                            appliedTransition.Record!
                        );

            if (!appliedWrite.Success)
            {
                /*
                 * The rename has already happened and its parent
                 * directory (or directories) have already been synced.
                 * Prepared remains a durable, inspectable checkpoint if
                 * this final journal write fails.
                 */
                return Result(
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .AppliedJournalWriteFailed,
                    plan.PlanId,
                    destinationPath:
                        fileOperation.DestinationPath,
                    createdDirectoryPaths:
                        createdDirectoryPaths,
                    intentJournalChildName:
                        intentChildName,
                    preparedJournalChildName:
                        preparedChildName,
                    appliedFileIncarnationIdentity:
                        appliedIncarnation.Identity,
                    error:
                        appliedWrite.Error ??
                        appliedWrite.State.ToString()
                );
            }

            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AppliedDurably,
                plan.PlanId,
                destinationPath:
                    fileOperation.DestinationPath,
                createdDirectoryPaths:
                    createdDirectoryPaths,
                intentJournalChildName:
                    intentChildName,
                preparedJournalChildName:
                    preparedChildName,
                appliedJournalChildName:
                    appliedChildName,
                appliedFileIncarnationIdentity:
                    appliedIncarnation.Identity
            );
        }
        finally
        {
            currentParent?.Dispose();
        }
    }

    private static DataRelativePathTargetedConsumerCaseRepairApplyExecution
        Result(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                state,
            Guid planId,
            string? destinationPath = null,
            IReadOnlyList<string>? createdDirectoryPaths = null,
            string? intentJournalChildName = null,
            string? preparedJournalChildName = null,
            string? appliedJournalChildName = null,
            LinuxFileIncarnationIdentity?
                appliedFileIncarnationIdentity = null,
            string? error = null)
    {
        return new(
            State:
                state,
            PlanId:
                planId,
            DestinationPath:
                destinationPath,
            CreatedDirectoryPaths:
                createdDirectoryPaths ??
                Array.Empty<string>(),
            IntentJournalChildName:
                intentJournalChildName,
            PreparedJournalChildName:
                preparedJournalChildName,
            AppliedJournalChildName:
                appliedJournalChildName,
            AppliedFileIncarnationIdentity:
                appliedFileIncarnationIdentity,
            Error:
                error
        );
    }

    // Renames an existing, possibly-populated physical directory into
    // place instead of creating a new empty one - see
    // DataRelativePathRepairDirectoryRenameSource. Re-derives fresh proof
    // of the source directory's identity and generation immediately
    // before renaming, exactly as the final file rename re-derives fresh
    // proof of its own source, rather than trusting the snapshot the
    // plan was built from.
    //
    // Returns null on success. On failure, returns the execution state
    // to report and sets errorMessage.
    private static
        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState?
        RenameExistingDirectoryIntoPlace(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan,
            DataRelativePathRepairPlanOperation directoryOperation,
            LinuxNoFollowPathHandle destinationParent,
            string destinationChildName,
            out string? errorMessage)
    {
        errorMessage =
            null;

        DataRelativePathRepairDirectoryRenameSource? expected =
            plan.DirectoryRenameSources
                .FirstOrDefault(
                    source =>
                        string.Equals(
                            source.DestinationPath,
                            directoryOperation.DestinationPath,
                            StringComparison.Ordinal
                        )
                );

        if (expected is null)
        {
            errorMessage =
                "No directory-rename source evidence was durably " +
                "recorded for this operation.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DirectoryRenameSourceMissing;
        }

        // The source and destination of a rename-flavored CreateDirectory
        // operation always share the same immediate parent: the plan
        // projector only ever finds an existing rename source by
        // scanning within the exact same parent it is about to create
        // the destination beneath. This holds for every step of a
        // chained rename, too - once an ancestor has been renamed, the
        // still-unrenamed child moved right along with it (renaming a
        // directory is a metadata-only operation; it does not disturb
        // anything beneath it), so it is now reachable as a child of
        // this same destinationParent under its original leaf name,
        // even though the operation's recorded SourcePath (an absolute
        // string captured at planning time, before any ancestor had
        // moved) no longer resolves on its own.
        string existingChildName =
            Path.GetFileName(
                directoryOperation.SourcePath!
            );

        LinuxOpenChildReadOnlyAtResult existingOpen =
            LinuxOpenChildReadOnlyAt.Open(
                destinationParent,
                existingChildName
            );

        if (
            !existingOpen.Success ||
            existingOpen.OpenedChild is null)
        {
            errorMessage =
                existingOpen.Error ??
                existingOpen.State.ToString();

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DirectorySourceOpenFailed;
        }

        using LinuxOpenedChildHandle existing =
            existingOpen.OpenedChild;

        LinuxOpenedDirectorySnapshotResult existingSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                existing,
                directoryOperation.SourcePath!
            );

        LinuxOpenedInodeGenerationResult existingGeneration =
            LinuxOpenedInodeGeneration.Capture(
                existing
            );

        if (
            !existingSnapshot.Success ||
            existingSnapshot.Identity is not
                LinuxFileIdentityResult existingIdentity ||
            !existingGeneration.Success ||
            existingGeneration.Generation is not
                uint existingGenerationValue)
        {
            errorMessage =
                existingSnapshot.Error ??
                existingGeneration.Error ??
                "The directory-rename source's identity could not be " +
                "freshly captured.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DirectorySourceIdentityUnavailable;
        }

        if (
            existingIdentity.DeviceMajor !=
                expected.Identity.DeviceMajor ||
            existingIdentity.DeviceMinor !=
                expected.Identity.DeviceMinor ||
            existingIdentity.Inode !=
                expected.Identity.Inode ||
            existingIdentity.MountId !=
                expected.Identity.MountId)
        {
            errorMessage =
                "The freshly reacquired directory-rename source's " +
                "physical identity does not equal the durable plan's " +
                "recorded identity.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DirectorySourceIdentityMismatch;
        }

        if (existingGenerationValue != expected.InodeGeneration)
        {
            errorMessage =
                "The freshly reacquired directory-rename source's inode " +
                "generation does not equal the durable plan's recorded " +
                "generation.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DirectorySourceGenerationMismatch;
        }

        LinuxRenameChildAtResult rename =
            LinuxRenameChildAt.Rename(
                destinationParent,
                existingChildName,
                destinationParent,
                destinationChildName
            );

        if (!rename.Success)
        {
            errorMessage =
                rename.Error ??
                rename.State.ToString();

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DirectoryRenameFailed;
        }

        LinuxFsyncResult destinationParentSync =
            LinuxFsync.Sync(
                destinationParent
            );

        if (!destinationParentSync.Success)
        {
            errorMessage =
                destinationParentSync.Error ??
                destinationParentSync.State.ToString();

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .DirectoryDestinationParentSyncFailed;
        }

        return null;
    }

    // Creates a symlink alias for one genuinely contested ancestor
    // segment instead of a directory - see DataRelativePathRepairAliasSource.
    // Re-derives fresh proof of the alias target's identity and
    // generation immediately before creating the symlink, exactly as
    // every other boundary in this pipeline re-derives fresh proof
    // rather than trusting the snapshot the plan was built from.
    //
    // Returns null on success. On failure, returns the execution state
    // to report and sets errorMessage.
    private static
        DataRelativePathTargetedConsumerCaseRepairApplyExecutionState?
        CreateAliasIntoPlace(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan,
            DataRelativePathRepairPlanOperation directoryOperation,
            LinuxNoFollowPathHandle currentParent,
            string linkName,
            LinuxNoFollowPathHandle? aliasesDirectory,
            DateTimeOffset nowUtc,
            out string? errorMessage)
    {
        errorMessage =
            null;

        if (aliasesDirectory is null)
        {
            errorMessage =
                "An alias registry directory is required to apply a " +
                "CreateAliasSymlink operation.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasesDirectoryRequired;
        }

        DataRelativePathRepairAliasSource? expected =
            plan.AliasSources
                .FirstOrDefault(
                    source =>
                        string.Equals(
                            source.DestinationPath,
                            directoryOperation.DestinationPath,
                            StringComparison.Ordinal
                        )
                );

        if (expected is null)
        {
            errorMessage =
                "No alias source evidence was durably recorded for this " +
                "operation.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasSourceMissing;
        }

        string targetName =
            Path.GetFileName(
                directoryOperation.SourcePath!
            );

        // Probe whatever currently occupies the alias name, if anything.
        // Unlike a plain directory create, an alias that already exists
        // and is already exactly correct is a legitimate idempotent
        // reuse - not a conflict - since a later, unrelated candidate
        // sharing the same contested ancestor is expected to compute the
        // exact same alias its predecessor already created. The live
        // target is re-read fresh here, never trusted from a hint; the
        // identity check below re-verifies the real directory it points
        // at regardless of whether a symlink is created below or reused.
        bool needsCreate =
            true;

        LinuxReadSymlinkAtResult existingLink =
            LinuxReadSymlinkAt.Read(
                currentParent,
                linkName
            );

        if (existingLink.Success)
        {
            if (
                !string.Equals(
                    existingLink.Target,
                    targetName,
                    StringComparison.Ordinal))
            {
                errorMessage =
                    "An existing symlink already occupies the alias " +
                    "name but points at a different target.";

                return
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .AliasAlreadyExists;
            }

            needsCreate =
                false;
        }
        else if (
            existingLink.State ==
            LinuxReadSymlinkAtState.ChildNotSymbolicLink)
        {
            errorMessage =
                "A non-symlink object already occupies the alias name.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasAlreadyExists;
        }
        else if (
            existingLink.State !=
            LinuxReadSymlinkAtState.ChildUnavailable)
        {
            errorMessage =
                existingLink.Error ??
                existingLink.State.ToString();

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasInspectionFailed;
        }

        // A symlink created via symlinkat resolves a bare target name
        // relative to the link's own parent directory, so the target
        // must be opened as a direct child of this exact currentParent -
        // not looked up by directoryOperation.SourcePath's own recorded
        // absolute string, which was captured at plan-build time and can
        // name an ancestor this same apply already renamed earlier in
        // this very loop. Opening by bare child name here is exactly
        // that same-parent lookup; the identity check below (immune to
        // that staleness, since it compares captured device/inode values
        // rather than path strings) is what actually re-verifies this is
        // still the plan's intended target.
        LinuxOpenChildReadOnlyAtResult targetOpen =
            LinuxOpenChildReadOnlyAt.Open(
                currentParent,
                targetName
            );

        if (
            !targetOpen.Success ||
            targetOpen.OpenedChild is null)
        {
            errorMessage =
                targetOpen.Error ??
                targetOpen.State.ToString();

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasTargetOpenFailed;
        }

        using LinuxOpenedChildHandle target =
            targetOpen.OpenedChild;

        // The label passed to Capture must be the target's current
        // physical path, not directoryOperation.SourcePath's own
        // plan-build-time string - which, exactly like the reopen above,
        // can be stale if this apply already renamed one of its
        // ancestors earlier in this same loop.
        string currentTargetPhysicalPath =
            Path.Combine(
                currentParent.FullPath,
                targetName
            );

        LinuxOpenedDirectorySnapshotResult targetSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                target,
                currentTargetPhysicalPath
            );

        LinuxOpenedInodeGenerationResult targetGeneration =
            LinuxOpenedInodeGeneration.Capture(
                target
            );

        if (
            !targetSnapshot.Success ||
            targetSnapshot.Identity is not
                LinuxFileIdentityResult targetIdentity ||
            !targetGeneration.Success ||
            targetGeneration.Generation is not
                uint targetGenerationValue)
        {
            errorMessage =
                targetSnapshot.Error ??
                targetGeneration.Error ??
                "The alias target's identity could not be freshly " +
                "captured.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasTargetIdentityUnavailable;
        }

        if (
            targetIdentity.DeviceMajor !=
                expected.Identity.DeviceMajor ||
            targetIdentity.DeviceMinor !=
                expected.Identity.DeviceMinor ||
            targetIdentity.Inode !=
                expected.Identity.Inode ||
            targetIdentity.MountId !=
                expected.Identity.MountId)
        {
            errorMessage =
                "The freshly reacquired alias target's physical identity " +
                "does not equal the durable plan's recorded identity.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasTargetIdentityMismatch;
        }

        if (targetGenerationValue != expected.InodeGeneration)
        {
            errorMessage =
                "The freshly reacquired alias target's inode generation " +
                "does not equal the durable plan's recorded generation.";

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasTargetGenerationMismatch;
        }

        if (needsCreate)
        {
            LinuxCreateSymlinkAtResult create =
                LinuxCreateSymlinkAt.Create(
                    currentParent,
                    linkName,
                    targetName
                );

            if (!create.Success)
            {
                errorMessage =
                    create.Error ??
                    create.State.ToString();

                return
                    create.State ==
                    LinuxCreateSymlinkAtState.DestinationExists
                        ? DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .AliasAlreadyExists
                        : DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                            .AliasCreateFailed;
            }

            LinuxFsyncResult parentSync =
                LinuxFsync.Sync(
                    currentParent
                );

            if (!parentSync.Success)
            {
                errorMessage =
                    parentSync.Error ??
                    parentSync.State.ToString();

                return
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                        .AliasParentSyncFailed;
            }
        }

        var aliasRecord =
            new DataRelativePathRepairAliasRecord(
                SchemaVersion:
                    DataRelativePathRepairAliasRecord.CurrentSchemaVersion,
                PlanId:
                    plan.PlanId,
                CreatedUtc:
                    nowUtc,
                DataRoot:
                    plan.DataRoot,
                ParentPath:
                    currentParent.FullPath,
                LinkName:
                    linkName,
                TargetName:
                    targetName,
                TargetIdentity:
                    targetIdentity
            );

        DataRelativePathRepairAliasRegistryRecordResult registration =
            DataRelativePathRepairAliasRegistry.Record(
                aliasesDirectory,
                aliasRecord
            );

        if (
            !registration.Success &&
            registration.State !=
                DataRelativePathRepairAliasRegistryRecordState
                    .AlreadyRecorded)
        {
            errorMessage =
                registration.Error ??
                registration.State.ToString();

            return
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasRegistrationFailed;
        }

        return null;
    }
}
