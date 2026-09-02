using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairPlanForwardExecutor
{
    private const string BatchManifestChildName =
        "batch-manifest.json";

    private const string BatchApplyAuthorizationChildName =
        "batch-apply-authorization.json";

    /*
     * Each successful recovery action advances a durable operation
     * transaction toward Applied. This bound is therefore not a retry
     * policy; it is a fail-closed guard against an unexpected state
     * machine that reports success without converging.
     */
    private const int MaxOperationPasses =
        8;

    public static DataRelativePathRepairPlanForwardExecution Execute(
        LinuxNoFollowPathHandle journalDirectory,
        string manifestChildName,
        string trustedDataRoot,
        DateTimeOffset nowUtc)
    {
        return ExecuteCore(
            journalDirectory,
            manifestChildName,
            trustedDataRoot,
            nowUtc,
            expectedPlanId:
                null,
            expectedManifestSha256:
                null,
            batchScope:
                null
        );
    }

    /*
     * Execute the persisted plan only if the authoritative manifest
     * reread under the existing per-PlanId execution lock matches an
     * independently supplied logical PlanId and exact manifest-byte
     * SHA-256.
     *
     * This strengthens caller binding without changing the existing
     * single-plan Execute(...) contract. The expectation itself grants
     * no filesystem authority.
     */
    public static DataRelativePathRepairPlanForwardExecution
        ExecuteExpectedManifest(
            LinuxNoFollowPathHandle journalDirectory,
            string manifestChildName,
            string trustedDataRoot,
            DateTimeOffset nowUtc,
            Guid expectedPlanId,
            string expectedManifestSha256)
    {
        ArgumentNullException.ThrowIfNull(
            expectedManifestSha256
        );

        return ExecuteCore(
            journalDirectory,
            manifestChildName,
            trustedDataRoot,
            nowUtc,
            expectedPlanId,
            expectedManifestSha256,
            batchScope:
                null
        );
    }

    /*
     * Batch-only whole-plan entry point.
     *
     * This increment only carries the retained batch descriptor and
     * factory-created logical context through the same locked forward
     * execution path. It deliberately adds no reuse behavior.
     *
     * Standalone repair-apply cannot call this path accidentally because
     * its existing Execute(...) and ExecuteExpectedManifest(...) entry
     * points always enter ExecuteCore with no batch scope.
     */
    public static DataRelativePathRepairPlanForwardExecution
        ExecuteExpectedBatchManifest(
            LinuxNoFollowPathHandle batchDirectory,
            DataRelativePathRepairBatchExecutionContext batchContext,
            LinuxNoFollowPathHandle journalDirectory,
            string trustedDataRoot,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            batchDirectory
        );

        ArgumentNullException.ThrowIfNull(
            batchContext
        );

        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        LinuxOpenedDirectoryIdentityResult suppliedIdentity =
            LinuxOpenedDirectoryIdentity.Capture(
                journalDirectory
            );

        if (!suppliedIdentity.Success)
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .BatchChildBindingFailed,
                manifestRead:
                    null,
                [],
                "The supplied batch child journal directory identity " +
                    "could not be captured: " +
                    (suppliedIdentity.Error ??
                        suppliedIdentity.State.ToString())
            );
        }

        LinuxOpenChildDirectoryReadOnlyAtResult expectedChildOpen =
            LinuxOpenChildDirectoryReadOnlyAt.Open(
                batchDirectory,
                batchContext.CurrentChild.ChildName
            );

        if (
            !expectedChildOpen.Success ||
            expectedChildOpen.OpenedDirectory is null)
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .BatchChildBindingFailed,
                manifestRead:
                    null,
                [],
                "The exact current batch child directory could not be " +
                    "opened descriptor-relative from the retained batch " +
                    "directory: " +
                    (expectedChildOpen.Error ??
                        expectedChildOpen.State.ToString())
            );
        }

        using LinuxNoFollowPathHandle expectedChildDirectory =
            expectedChildOpen.OpenedDirectory;

        LinuxOpenedDirectoryIdentityResult expectedIdentity =
            LinuxOpenedDirectoryIdentity.Capture(
                expectedChildDirectory
            );

        if (!expectedIdentity.Success)
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .BatchChildBindingFailed,
                manifestRead:
                    null,
                [],
                "The exact current batch child directory identity " +
                    "could not be captured: " +
                    (expectedIdentity.Error ??
                        expectedIdentity.State.ToString())
            );
        }

        if (
            !suppliedIdentity.SameObjectAs(
                expectedIdentity
            ))
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .BatchChildBindingFailed,
                manifestRead:
                    null,
                [],
                "The supplied batch child journal directory is not the " +
                    "same mounted filesystem object as the exact current " +
                    $"batch child '{batchContext.CurrentChild.ChildName}'."
            );
        }

        var batchScope =
            new BatchExecutionScope(
                BatchDirectory:
                    batchDirectory,
                Context:
                    batchContext
            );

        return ExecuteCore(
            journalDirectory,
            batchContext.ChildManifestName,
            trustedDataRoot,
            nowUtc,
            batchContext.CurrentChild.PlanId,
            batchContext.CurrentChild.ManifestSha256,
            batchScope
        );
    }

    private static DataRelativePathRepairPlanForwardExecution
        ExecuteCore(
            LinuxNoFollowPathHandle journalDirectory,
            string manifestChildName,
            string trustedDataRoot,
            DateTimeOffset nowUtc,
            Guid? expectedPlanId,
            string? expectedManifestSha256,
            BatchExecutionScope? batchScope)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        DataRelativePathRepairPlanManifestReaderResult manifestRead =
            DataRelativePathRepairPlanManifestReader.Read(
                journalDirectory,
                manifestChildName
            );

        if (!manifestRead.Success)
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .ManifestReadFailed,
                manifestRead,
                [],
                manifestRead.Error ??
                    manifestRead.State.ToString()
            );
        }

        /*
         * If the caller supplied an expected immutable manifest, reject
         * an already-obvious mismatch before acquiring the persistent
         * per-PlanId execution lock.
         *
         * This preliminary comparison is only a side-effect guard. It is
         * not authoritative for execution: the same expectation is
         * checked again against the locked manifest reread below so a
         * replacement or in-place content change cannot race this check.
         */
        if (
            expectedPlanId.HasValue &&
            (
                manifestRead.Manifest!.PlanId != expectedPlanId.Value ||
                expectedManifestSha256 is null ||
                !string.Equals(
                    manifestRead.ManifestSha256,
                    expectedManifestSha256,
                    StringComparison.OrdinalIgnoreCase
                )
            ))
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .ExpectedManifestMismatch,
                manifestRead,
                [],
                $"The initially read plan manifest does not match the " +
                $"caller expectation. Expected PlanId " +
                $"{expectedPlanId.Value}, observed " +
                $"{manifestRead.Manifest.PlanId}; expected manifest " +
                $"SHA-256 {expectedManifestSha256 ?? "<missing>"}, " +
                $"observed " +
                $"{manifestRead.ManifestSha256 ?? "<missing>"}."
            );
        }

        /*
         * The first manifest read discovers the immutable PlanId and
         * exact manifest incarnation only.
         *
         * Acquire the persistent per-PlanId execution lock, then
         * re-read and revalidate that exact manifest while the lock is
         * held. The locked read becomes authoritative below.
         */
        DataRelativePathRepairPlanExecutionLockAcquisition
            executionLockAcquisition =
                DataRelativePathRepairPlanExecutionLock.Acquire(
                    journalDirectory,
                    manifestChildName,
                    manifestRead
                );

        if (!executionLockAcquisition.Success)
        {
            DataRelativePathRepairPlanForwardExecutionState failureState =
                executionLockAcquisition.State ==
                DataRelativePathRepairPlanExecutionLockState
                    .LockUnavailable
                    ? DataRelativePathRepairPlanForwardExecutionState
                        .PlanExecutionLockUnavailable
                    : DataRelativePathRepairPlanForwardExecutionState
                        .ManifestRevalidationFailed;

            return PlanResult(
                failureState,
                executionLockAcquisition.LockedManifestRead ??
                    manifestRead,
                [],
                executionLockAcquisition.Error ??
                    executionLockAcquisition.State.ToString()
            );
        }

        using LinuxExclusiveChildFileLockLease executionLock =
            executionLockAcquisition.Lease!;

        manifestRead =
            executionLockAcquisition.LockedManifestRead!;

        DataRelativePathRepairPlanManifestRecord manifest =
            manifestRead.Manifest!;

        /*
         * An optional caller expectation is metadata binding only.
         *
         * The comparison deliberately occurs after the existing
         * per-PlanId lock has re-read and revalidated the manifest.
         * Therefore the exact bytes compared here are the same locked
         * bytes that become authoritative for all later preflight and
         * execution.
         *
         * Do not move this check to a preceding CLI/status inspection:
         * doing so would recreate a manifest-replacement race between
         * membership verification and mutation.
         */
        if (
            expectedPlanId.HasValue &&
            (
                manifest.PlanId != expectedPlanId.Value ||
                expectedManifestSha256 is null ||
                !string.Equals(
                    manifestRead.ManifestSha256,
                    expectedManifestSha256,
                    StringComparison.OrdinalIgnoreCase
                )
            ))
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .ExpectedManifestMismatch,
                manifestRead,
                [],
                $"The authoritative locked plan manifest does not match " +
                $"the caller expectation. Expected PlanId " +
                $"{expectedPlanId.Value}, observed {manifest.PlanId}; " +
                $"expected manifest SHA-256 " +
                $"{expectedManifestSha256 ?? "<missing>"}, observed " +
                $"{manifestRead.ManifestSha256 ?? "<missing>"}."
            );
        }

        /*
         * A durable manifest is historical plan data, not filesystem
         * authority. Bind its recorded root to the independently trusted
         * root supplied by the caller before using any operation.
         */
        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                manifest.DataRoot,
                out string? rootBindingError
            ))
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .ManifestDataRootMismatch,
                manifestRead,
                [],
                rootBindingError
            );
        }

        /*
         * Retain the exact trusted Data-root directory descriptor for
         * this whole invocation.
         *
         * Later operations that have no durable journal yet must derive
         * their initial destination-parent snapshot from this descriptor,
         * not by reopening the trustedDataRoot pathname.
         *
         * If that pathname is replaced while execution is in progress,
         * the retained capability still identifies the originally opened
         * filesystem object. Existing operation-level identity checks may
         * then fail closed, but a replacement root cannot silently become
         * new repair authority.
         */
        LinuxNoFollowPathOpenResult trustedDataRootOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                trustedDataRoot
            );

        if (
            !trustedDataRootOpen.Success ||
            trustedDataRootOpen.OpenedPath is null)
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .TrustedDataRootOpenFailed,
                manifestRead,
                [],
                trustedDataRootOpen.Error ??
                    $"The trusted Data root could not be opened: " +
                    $"{trustedDataRootOpen.State}."
            );
        }

        using LinuxNoFollowPathHandle trustedDataRootHandle =
            trustedDataRootOpen.OpenedPath;

        /*
         * Validate all durable plan history before starting or
         * recovering any individual operation.
         *
         * This preflight is deliberately read/classify only.
         *
         * The per-PlanId execution lock is already held for this whole
         * invocation. It serializes cooperating whole-plan forward and
         * rollback executors, but grants no filesystem mutation
         * authority.
         *
         * Every operation still performs its normal fresh journal read,
         * classification, directory lock/re-read, and
         * incarnation-aware guarded action.
         *
         * Existing journals must form one contiguous prefix. Every
         * existing journal must also cross-bind to the immutable plan
         * and already be in a recovery state understood by this forward
         * executor.
         */
        PreflightResult preflight =
            Preflight(
                journalDirectory,
                manifest,
                trustedDataRoot
            );

        if (!preflight.Success)
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .PreflightFailed,
                manifestRead,
                preflight.Failure is null
                    ? []
                    : [preflight.Failure],
                preflight.Failure?.Error ??
                    "Forward plan preflight failed."
            );
        }

        /*
         * Batch metadata alone is never mutation authority.
         *
         * If this execution arrived through the batch-only entry point,
         * reauthenticate the retained batch root now, while the current
         * child's authoritative manifest is held under its persistent
         * per-PlanId execution lock.
         *
         * For a schema-v2 / coverage-policy-v1 batch, successful
         * authentication proves the durable batch-wide authorization was
         * published after aggregate namespace coverage and is bound to the
         * exact current batch-manifest bytes.
         *
         * Legacy schema-v1 batches deliberately receive no aggregate
         * authority and therefore continue through the ordinary standalone
         * sparse-branch rule below.
         *
         * A started schema-v2 child still requires the durable batch-wide
         * authorization. Its existing operation journal supplies recovery
         * authority only after this immutable batch boundary is proven.
         */
        bool hasAggregateBatchApplyAuthority =
            false;

        if (batchScope is not null)
        {
            BatchApplyAuthorityAuthentication authority =
                AuthenticateBatchApplyAuthority(
                    batchScope,
                    trustedDataRoot,
                    requireFreshCoverage:
                        !preflight.HasExistingJournal
                );

            if (!authority.Success)
            {
                return PlanResult(
                    DataRelativePathRepairPlanForwardExecutionState
                        .BatchChildBindingFailed,
                    manifestRead,
                    [],
                    authority.Error ??
                        "Batch-wide apply authorization could not be " +
                        "authenticated."
                );
            }

            hasAggregateBatchApplyAuthority =
                authority.AggregateCoverageAuthorized;
        }

        /*
         * A plan with no durable operation journal is still genuinely
         * unstarted.
         *
         * Standalone plans and legacy schema-v1 batch children must
         * revalidate the single-plan sparse case-variant branch invariant
         * immediately before any operation is allowed to create durable
         * mutation history.
         *
         * A schema-v2 batch child may use aggregate authority only after the
         * retained batch descriptor, exact batch manifest, structural batch
         * context, and durable apply authorization were all reauthenticated
         * above.
         *
         * Once a durable operation prefix exists, normal recovery/idempotence
         * owns the namespace interpretation. Do not reinterpret a started
         * plan using fresh unrelated namespace contents.
         */
        if (
            !preflight.HasExistingJournal &&
            !hasAggregateBatchApplyAuthority &&
            !ValidateUnstartedCaseVariantSourceBranch(
                trustedDataRootHandle,
                trustedDataRoot,
                manifest,
                out string? sourceBranchSafetyError
            ))
        {
            return PlanResult(
                DataRelativePathRepairPlanForwardExecutionState
                    .PreflightFailed,
                manifestRead,
                [],
                sourceBranchSafetyError ??
                    "Unstarted case-variant source-branch safety " +
                    "validation failed."
            );
        }

        var results =
            new List<
                DataRelativePathRepairPlanForwardOperationExecution
            >(
                manifest.Operations.Count
            );

        foreach (
            DataRelativePathRepairPlanManifestOperation entry
            in manifest.Operations)
        {
            DataRelativePathRepairPlanForwardOperationExecution
                operationResult =
                    entry.Operation.Kind switch
                    {
                        DataRelativePathRepairPlanOperationKind
                            .CreateDirectory =>
                                ExecuteDirectoryOperation(
                                    journalDirectory,
                                    manifest,
                                    entry,
                                    trustedDataRootHandle,
                                    trustedDataRoot,
                                    nowUtc,
                                    batchScope
                                ),

                        DataRelativePathRepairPlanOperationKind
                            .CreateFile =>
                                ExecuteFileOperation(
                                    journalDirectory,
                                    manifest,
                                    entry,
                                    trustedDataRootHandle,
                                    trustedDataRoot,
                                    nowUtc
                                ),

                        _ =>
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .JournalMismatch,
                                error:
                                    $"Unsupported plan operation kind " +
                                    $"{entry.Operation.Kind}."
                            )
                    };

            results.Add(
                operationResult
            );

            if (!operationResult.Success)
            {
                return PlanResult(
                    DataRelativePathRepairPlanForwardExecutionState
                        .OperationFailed,
                    manifestRead,
                    results,
                    operationResult.Error ??
                        operationResult.State.ToString()
                );
            }
        }

        return PlanResult(
            DataRelativePathRepairPlanForwardExecutionState
                .AppliedDurably,
            manifestRead,
            results,
            error:
                null
        );
    }

    private static PreflightResult Preflight(
        LinuxNoFollowPathHandle journalDirectory,
        DataRelativePathRepairPlanManifestRecord manifest,
        string trustedDataRoot)
    {
        int? firstMissingIndex =
            null;

        bool hasExistingJournal =
            false;

        /*
         * Forward execution advances to operation N+1 only after
         * operation N has converged to durable Applied.
         *
         * Therefore, once an existing forward-safe journal is observed
         * in Intent or Prepared, that journal must be the final existing
         * journal in the contiguous prefix.
         *
         * A later bound, forward-safe journal proves a durable history
         * that the forward orchestrator itself cannot have produced.
         * Refuse that history before any recovery or mutation.
         */
        DataRelativePathRepairPlanManifestOperation?
            firstNonAppliedEntry =
                null;

        string? firstNonAppliedDurableState =
            null;

        for (
            int index = 0;
            index < manifest.Operations.Count;
            index++)
        {
            DataRelativePathRepairPlanManifestOperation entry =
                manifest.Operations[index];

            switch (entry.Operation.Kind)
            {
                case
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory:
                {
                    DataRelativePathRepairDirectoryJournalReaderResult read =
                        DataRelativePathRepairDirectoryJournalReader.Read(
                            journalDirectory,
                            entry.JournalChildName
                        );

                    if (
                        read.State ==
                        DataRelativePathRepairDirectoryJournalReadState
                            .JournalUnavailable)
                    {
                        firstMissingIndex ??=
                            index;

                        continue;
                    }

                    hasExistingJournal =
                        true;

                    if (!read.Success)
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .JournalReadFailed,
                                directoryJournalRead:
                                    read,
                                error:
                                    read.Error ??
                                    read.State.ToString()
                            )
                        );
                    }

                    if (firstMissingIndex is not null)
                    {
                        DataRelativePathRepairPlanManifestOperation
                            missingEntry =
                                manifest.Operations[
                                    firstMissingIndex.Value
                                ];

                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .JournalGap,
                                directoryJournalRead:
                                    read,
                                error:
                                    $"Operation journal " +
                                    $"{missingEntry.JournalChildName} " +
                                    $"at index {missingEntry.Index} is " +
                                    "missing while a later operation " +
                                    $"journal {entry.JournalChildName} " +
                                    $"at index {entry.Index} exists."
                            )
                        );
                    }

                    DataRelativePathRepairDirectoryJournalRecord journal =
                        read.Record!;

                    string? bindingError =
                        DataRelativePathRepairPlanJournalBinding
                            .ValidateDirectory(
                                entry,
                                journal,
                                trustedDataRoot
                            );

                    if (bindingError is not null)
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .JournalMismatch,
                                directoryJournalRead:
                                    read,
                                error:
                                    bindingError
                            )
                        );
                    }

                    DataRelativePathRepairDirectoryRecoveryClassification
                        classification =
                            DataRelativePathRepairDirectoryRecoveryClassifier
                                .Classify(
                                    journal,
                                    trustedDataRoot
                                );

                    if (
                        !IsDirectoryForwardSafe(
                            classification.State
                        ))
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .DirectoryRecoveryStateNotForwardSafe,
                                directoryJournalRead:
                                    read,
                                directoryClassification:
                                    classification,
                                error:
                                    classification.Error ??
                                    $"Directory recovery state " +
                                    $"{classification.State} is not a " +
                                    "safe plan-forward preflight state."
                            )
                        );
                    }

                    if (firstNonAppliedEntry is not null)
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .CausalHistoryConflict,
                                directoryJournalRead:
                                    read,
                                directoryClassification:
                                    classification,
                                error:
                                    $"Operation journal " +
                                    $"{entry.JournalChildName} at index " +
                                    $"{entry.Index} exists after earlier " +
                                    $"operation journal " +
                                    $"{firstNonAppliedEntry.JournalChildName} " +
                                    $"at index {firstNonAppliedEntry.Index} " +
                                    $"whose durable state is " +
                                    $"{firstNonAppliedDurableState}. Every " +
                                    "existing journal before a later " +
                                    "operation journal must already be " +
                                    "durably Applied."
                            )
                        );
                    }

                    if (
                        journal.State !=
                        DataRelativePathRepairDirectoryJournalState
                            .Applied)
                    {
                        firstNonAppliedEntry =
                            entry;

                        firstNonAppliedDurableState =
                            journal.State.ToString();
                    }

                    break;
                }

                case
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile:
                {
                    DataRelativePathRepairFileJournalReaderResult read =
                        DataRelativePathRepairFileJournalReader.Read(
                            journalDirectory,
                            entry.JournalChildName
                        );

                    if (
                        read.State ==
                        DataRelativePathRepairFileJournalReadState
                            .JournalUnavailable)
                    {
                        firstMissingIndex ??=
                            index;

                        continue;
                    }

                    hasExistingJournal =
                        true;

                    if (!read.Success)
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .JournalReadFailed,
                                fileJournalRead:
                                    read,
                                error:
                                    read.Error ??
                                    read.State.ToString()
                            )
                        );
                    }

                    if (firstMissingIndex is not null)
                    {
                        DataRelativePathRepairPlanManifestOperation
                            missingEntry =
                                manifest.Operations[
                                    firstMissingIndex.Value
                                ];

                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .JournalGap,
                                fileJournalRead:
                                    read,
                                error:
                                    $"Operation journal " +
                                    $"{missingEntry.JournalChildName} " +
                                    $"at index {missingEntry.Index} is " +
                                    "missing while a later operation " +
                                    $"journal {entry.JournalChildName} " +
                                    $"at index {entry.Index} exists."
                            )
                        );
                    }

                    DataRelativePathRepairFileJournalRecord journal =
                        read.Record!;

                    string? bindingError =
                        DataRelativePathRepairPlanJournalBinding
                            .ValidateFile(
                                manifest,
                                entry,
                                journal,
                                trustedDataRoot
                            );

                    if (bindingError is not null)
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .JournalMismatch,
                                fileJournalRead:
                                    read,
                                error:
                                    bindingError
                            )
                        );
                    }

                    DataRelativePathRepairFileRecoveryClassification
                        classification =
                            DataRelativePathRepairFileRecoveryClassifier
                                .Classify(
                                    journal,
                                    trustedDataRoot
                                );

                    if (
                        !IsFileForwardSafe(
                            classification.State
                        ))
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .FileRecoveryStateNotForwardSafe,
                                fileJournalRead:
                                    read,
                                fileClassification:
                                    classification,
                                error:
                                    classification.Error ??
                                    $"File recovery state " +
                                    $"{classification.State} is not a " +
                                    "safe plan-forward preflight state."
                            )
                        );
                    }

                    if (firstNonAppliedEntry is not null)
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanForwardOperationExecutionState
                                    .CausalHistoryConflict,
                                fileJournalRead:
                                    read,
                                fileClassification:
                                    classification,
                                error:
                                    $"Operation journal " +
                                    $"{entry.JournalChildName} at index " +
                                    $"{entry.Index} exists after earlier " +
                                    $"operation journal " +
                                    $"{firstNonAppliedEntry.JournalChildName} " +
                                    $"at index {firstNonAppliedEntry.Index} " +
                                    $"whose durable state is " +
                                    $"{firstNonAppliedDurableState}. Every " +
                                    "existing journal before a later " +
                                    "operation journal must already be " +
                                    "durably Applied."
                            )
                        );
                    }

                    if (
                        journal.State !=
                        DataRelativePathRepairFileJournalState
                            .Applied)
                    {
                        firstNonAppliedEntry =
                            entry;

                        firstNonAppliedDurableState =
                            journal.State.ToString();
                    }

                    break;
                }

                default:
                    return PreflightResult.Failed(
                        OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .JournalMismatch,
                            error:
                                $"Unsupported plan operation kind " +
                                $"{entry.Operation.Kind}."
                        )
                    );
            }
        }

        return PreflightResult.Succeeded(
            hasExistingJournal
        );
    }

    private static bool ValidateUnstartedCaseVariantSourceBranch(
        LinuxNoFollowPathHandle trustedDataRootHandle,
        string trustedDataRoot,
        DataRelativePathRepairPlanManifestRecord manifest,
        out string? error)
    {
        error =
            null;

        DataRelativePathRepairPlanManifestOperation?
            firstDirectoryEntry =
                manifest.Operations
                    .FirstOrDefault(entry =>
                        entry.Operation.Kind ==
                        DataRelativePathRepairPlanOperationKind
                            .CreateDirectory
                    );

        /*
         * A file-only repair cannot create a sparse parallel directory
         * hierarchy, so this particular guard does not apply.
         */
        if (firstDirectoryEntry is null)
        {
            return true;
        }

        string fullDataRoot;
        string fullSourcePath;
        string fullInitialParent;
        string fullFirstDestination;

        try
        {
            fullDataRoot =
                Path.GetFullPath(
                    trustedDataRoot
                );

            fullSourcePath =
                Path.GetFullPath(
                    manifest.SourceSnapshot.PhysicalPath
                );

            fullInitialParent =
                Path.GetFullPath(
                    manifest
                        .InitialDestinationParentSnapshot
                        .PhysicalPath
                );

            fullFirstDestination =
                Path.GetFullPath(
                    firstDirectoryEntry
                        .Operation
                        .DestinationPath
                );
        }
        catch (Exception ex)
        {
            error =
                "The unstarted plan's case-variant source-branch paths " +
                "could not be normalized: " +
                ex.Message;

            return false;
        }

        string sourceRelative =
            Path.GetRelativePath(
                fullDataRoot,
                fullSourcePath
            );

        string parentRelative =
            Path.GetRelativePath(
                fullDataRoot,
                fullInitialParent
            );

        string firstDestinationRelative =
            Path.GetRelativePath(
                fullDataRoot,
                fullFirstDestination
            );

        if (
            IsOutsideRelativePath(
                sourceRelative
            ) ||
            IsOutsideRelativePath(
                parentRelative
            ) ||
            IsOutsideRelativePath(
                firstDestinationRelative
            ))
        {
            error =
                "The unstarted plan's case-variant source-branch " +
                "validation escaped the trusted Data root.";

            return false;
        }

        string[] sourceComponents =
            SplitRelativeComponents(
                sourceRelative
            );

        string[] parentComponents =
            SplitRelativeComponents(
                parentRelative
            );

        string[] firstDestinationComponents =
            SplitRelativeComponents(
                firstDestinationRelative
            );

        int failedIndex =
            parentComponents.Length;

        if (
            sourceComponents.Length <=
                failedIndex ||
            firstDestinationComponents.Length !=
                failedIndex + 1)
        {
            error =
                "The unstarted plan no longer has a valid direct " +
                "case-mismatch path shape.";

            return false;
        }

        string? firstDestinationParent =
            Path.GetDirectoryName(
                fullFirstDestination
            );

        if (
            string.IsNullOrEmpty(
                firstDestinationParent
            ) ||
            !string.Equals(
                Path.GetFullPath(
                    firstDestinationParent
                ),
                fullInitialParent,
                StringComparison.Ordinal
            ))
        {
            error =
                "The first projected directory is no longer bound to " +
                "the manifest's initial destination parent.";

            return false;
        }

        string physicalVariant =
            sourceComponents[
                failedIndex
            ];

        string requestedVariant =
            firstDestinationComponents[
                failedIndex
            ];

        /*
         * This guard is specifically for a case-variant sibling branch.
         * Preserve legacy/non-case-variant manifest behavior here; other
         * manifest and operation validation remains authoritative for it.
         */
        if (
            string.Equals(
                physicalVariant,
                requestedVariant,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                physicalVariant,
                requestedVariant,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return true;
        }

        LinuxNoFollowPathHandle current =
            trustedDataRootHandle;

        LinuxNoFollowPathHandle? ownedCurrent =
            null;

        try
        {
            /*
             * Reach the exact physical case-variant directory using only
             * descriptor-relative, O_NOFOLLOW directory opens.
             */
            for (
                int index = 0;
                index <= failedIndex;
                index++)
            {
                LinuxOpenChildDirectoryReadOnlyAtResult opened =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        current,
                        sourceComponents[index]
                    );

                if (
                    !opened.Success ||
                    opened.OpenedDirectory is null)
                {
                    error =
                        "The unstarted plan's proven physical source " +
                        $"directory \"{sourceComponents[index]}\" could " +
                        "not be reopened descriptor-relatively without " +
                        $"following symlinks ({opened.State}): " +
                        (
                            opened.Error ??
                            "no additional error"
                        );

                    return false;
                }

                ownedCurrent?.Dispose();

                ownedCurrent =
                    opened.OpenedDirectory;

                current =
                    ownedCurrent;
            }

            /*
             * From the case-variant directory down to the source file,
             * every physical directory must still contain exactly the
             * unique next component. Any extra entry would be stranded
             * if this unstarted durable plan created a sparse parallel
             * requested hierarchy.
             */
            for (
                int index = failedIndex;
                index < sourceComponents.Length - 1;
                index++)
            {
                string expectedChild =
                    sourceComponents[
                        index + 1
                    ];

                LinuxEnumerateDirectoryAtResult enumeration =
                    LinuxEnumerateDirectoryAt.Enumerate(
                        current
                    );

                if (!enumeration.Success)
                {
                    error =
                        "The unstarted plan's physical case-variant " +
                        "source branch could not be enumerated " +
                        $"descriptor-relatively ({enumeration.State}): " +
                        (
                            enumeration.Error ??
                            "no additional error"
                        );

                    return false;
                }

                if (
                    enumeration.ChildNames.Count != 1 ||
                    !string.Equals(
                        enumeration.ChildNames[0],
                        expectedChild,
                        StringComparison.Ordinal
                    ))
                {
                    error =
                        "The unstarted plan's physical case-variant " +
                        "source branch now contains untargeted content. " +
                        "Creating the requested parallel hierarchy would " +
                        "strand that content.";

                    return false;
                }

                bool expectedChildIsDirectory =
                    index + 1 <
                    sourceComponents.Length - 1;

                if (!expectedChildIsDirectory)
                {
                    continue;
                }

                LinuxOpenChildDirectoryReadOnlyAtResult opened =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        current,
                        expectedChild
                    );

                if (
                    !opened.Success ||
                    opened.OpenedDirectory is null)
                {
                    error =
                        "The unstarted plan's next physical source " +
                        $"directory \"{expectedChild}\" could not be " +
                        "opened descriptor-relatively without following " +
                        $"symlinks ({opened.State}): " +
                        (
                            opened.Error ??
                            "no additional error"
                        );

                    return false;
                }

                ownedCurrent?.Dispose();

                ownedCurrent =
                    opened.OpenedDirectory;

                current =
                    ownedCurrent;
            }

            return true;
        }
        finally
        {
            ownedCurrent?.Dispose();
        }
    }

    private static bool IsOutsideRelativePath(
        string relativePath)
    {
        return
            Path.IsPathRooted(
                relativePath
            ) ||
            relativePath == ".." ||
            relativePath.StartsWith(
                "../",
                StringComparison.Ordinal
            ) ||
            relativePath.StartsWith(
                "..\\",
                StringComparison.Ordinal
            );
    }

    private static string[] SplitRelativeComponents(
        string relativePath)
    {
        if (relativePath == ".")
        {
            return [];
        }

        return relativePath
            .Replace(
                '\\',
                '/'
            )
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            );
    }

    private static bool IsDirectoryForwardSafe(
        DataRelativePathRepairDirectoryRecoveryState state)
    {
        return state is
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMatches or
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedAppliedFinalMatches or
            DataRelativePathRepairDirectoryRecoveryState
                .IntentFinalMissing or
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedBothMissing or
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedStagingMatchesFinalMissing or
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedFinalMatchesStagingMissing;
    }

    private static bool IsFileForwardSafe(
        DataRelativePathRepairFileRecoveryState state)
    {
        return state is
            DataRelativePathRepairFileRecoveryState
                .AppliedDestinationMatches or
            DataRelativePathRepairFileRecoveryState
                .IntentDestinationMissing or
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMissing or
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMatches;
    }

    private static
        DataRelativePathRepairPlanForwardOperationExecution
        ExecuteDirectoryOperation(
            LinuxNoFollowPathHandle journalDirectory,
            DataRelativePathRepairPlanManifestRecord manifest,
            DataRelativePathRepairPlanManifestOperation entry,
            LinuxNoFollowPathHandle trustedDataRootHandle,
            string trustedDataRoot,
            DateTimeOffset nowUtc,
            BatchExecutionScope? batchScope)
    {
        /*
         * Standalone execution deliberately supplies no batch scope.
         *
         * Only a batch-bound execution may convert the ordinary
         * DestinationExists result into authenticated same-batch reuse.
         * All other directory-execution failures retain their existing
         * fail-closed behavior.
         */

        DataRelativePathRepairDestinationParentSnapshotCaptureResult?
            parentCapture =
                null;

        DataRelativePathRepairDirectoryJournalReaderResult?
            lastRead =
                null;

        DataRelativePathRepairDirectoryRecoveryClassification?
            lastClassification =
                null;

        DataRelativePathRepairDirectoryExecution?
            initialExecution =
                null;

        DataRelativePathRepairDirectoryIntentRecovery?
            intentRecovery =
                null;

        DataRelativePathRepairDirectoryReprepareRecovery?
            reprepareRecovery =
                null;

        DataRelativePathRepairDirectoryForwardRecovery?
            forwardRecovery =
                null;

        DataRelativePathRepairDirectoryRecoveryReconciliation?
            reconciliation =
                null;

        for (
            int pass = 0;
            pass < MaxOperationPasses;
            pass++)
        {
            DataRelativePathRepairDirectoryJournalReaderResult read =
                DataRelativePathRepairDirectoryJournalReader.Read(
                    journalDirectory,
                    entry.JournalChildName
                );

            lastRead =
                read;

            if (
                read.State ==
                DataRelativePathRepairDirectoryJournalReadState
                    .JournalUnavailable)
            {
                DataRelativePathRepairDestinationParentSnapshot?
                    parentSnapshot =
                        GetStartParentSnapshot(
                            manifest,
                            entry,
                            trustedDataRootHandle,
                            out parentCapture
                        );

                if (parentSnapshot is null)
                {
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanForwardOperationExecutionState
                            .DestinationParentSnapshotCaptureFailed,
                        parentSnapshotCapture:
                            parentCapture,
                        directoryJournalRead:
                            read,
                        error:
                            parentCapture?.Error ??
                            "The operation destination parent could not " +
                            "be captured."
                    );
                }

                initialExecution =
                    DataRelativePathRepairDirectoryExecutor.Execute(
                        journalDirectory,
                        entry.JournalChildName,
                        entry.Operation,
                        parentSnapshot,
                        trustedDataRoot,
                        nowUtc
                    );

                if (!initialExecution.Success)
                {
                    /*
                     * DestinationExists remains a hard standalone failure.
                     *
                     * Batch execution may reuse the existing directory only
                     * when all of the independently durable batch evidence
                     * proves that an authenticated earlier child owns this
                     * exact strong filesystem incarnation.
                     */
                    if (
                        initialExecution.State !=
                            DataRelativePathRepairDirectoryExecutionState
                                .DestinationExists ||
                        batchScope is null)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .DirectoryExecutionFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            directoryJournalRead:
                                read,
                            directoryExecution:
                                initialExecution,
                            error:
                                initialExecution.Error ??
                                initialExecution.State.ToString()
                        );
                    }

                    /*
                     * The ordinary directory executor released its parent
                     * lease when it returned DestinationExists.
                     *
                     * Reacquire a fresh validated lease and retain that exact
                     * descriptor through authorization and publication.
                     */
                    DataRelativePathRepairDestinationParentLeaseAcquisition
                        reuseParentAcquisition =
                            DataRelativePathRepairDestinationParentLeaseAcquirer
                                .Acquire(
                                    trustedDataRoot,
                                    parentSnapshot
                                );

                    if (
                        !reuseParentAcquisition.Success ||
                        reuseParentAcquisition.Lease is null)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .DirectoryReuseAuthorizationFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            directoryJournalRead:
                                read,
                            directoryExecution:
                                initialExecution,
                            error:
                                "Batch directory reuse destination-parent " +
                                $"validation failed " +
                                $"({reuseParentAcquisition.Validation.State}): " +
                                (
                                    reuseParentAcquisition.Validation.Error ??
                                    reuseParentAcquisition.Validation.State
                                        .ToString()
                                )
                        );
                    }

                    using DataRelativePathRepairValidatedDestinationParentLease
                        reuseParent =
                            reuseParentAcquisition.Lease;

                    DataRelativePathRepairBatchDirectoryReuseAuthorization
                        reuseAuthorization =
                            DataRelativePathRepairBatchDirectoryReuseAuthorizer
                                .Authorize(
                                    batchScope.BatchDirectory,
                                    batchScope.Context,
                                    reuseParent,
                                    entry
                                );

                    if (
                        !reuseAuthorization.Success ||
                        reuseAuthorization.Provenance is null)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .DirectoryReuseAuthorizationFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            directoryJournalRead:
                                read,
                            directoryExecution:
                                initialExecution,
                            error:
                                "Batch directory reuse authorization failed " +
                                $"({reuseAuthorization.State}): " +
                                (
                                    reuseAuthorization.Error ??
                                    reuseAuthorization.State.ToString()
                                )
                        );
                    }

                    /*
                     * Authorization is point-in-time evidence.
                     *
                     * PublishAuthorized closes that boundary by reopening
                     * the final child under this SAME retained parent lease,
                     * recapturing strong incarnation identity, comparing it
                     * with the authorized provenance, and retaining that
                     * descriptor through durable journal publication.
                     */
                    DataRelativePathRepairBatchDirectoryReusePublication
                        reusePublication =
                            DataRelativePathRepairBatchDirectoryReusePublisher
                                .PublishAuthorized(
                                    journalDirectory,
                                    entry.JournalChildName,
                                    reuseParent,
                                    entry,
                                    trustedDataRoot,
                                    nowUtc,
                                    reuseAuthorization.Provenance
                                );

                    if (!reusePublication.Success)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .DirectoryReusePublicationFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            directoryJournalRead:
                                read,
                            directoryExecution:
                                initialExecution,
                            error:
                                "Batch directory reuse publication failed " +
                                $"({reusePublication.State}): " +
                                (
                                    reusePublication.Error ??
                                    reusePublication.State.ToString()
                                )
                        );
                    }

                    /*
                     * Do not treat the publication result itself as
                     * plan-level success. Reopen the exact durable journal
                     * and classify it on the next pass, just like ordinary
                     * directory execution.
                     */
                    continue;
                }

                /*
                 * Do not trust the composed executor result as plan-level
                 * progress truth. Reopen the exact durable journal and
                 * classify it on the next pass.
                 */
                continue;
            }

            if (!read.Success)
            {
                return OperationResult(
                    entry,
                    DataRelativePathRepairPlanForwardOperationExecutionState
                        .JournalReadFailed,
                    parentSnapshotCapture:
                        parentCapture,
                    directoryJournalRead:
                        read,
                    directoryExecution:
                        initialExecution,
                    directoryIntentRecovery:
                        intentRecovery,
                    directoryReprepareRecovery:
                        reprepareRecovery,
                    directoryForwardRecovery:
                        forwardRecovery,
                    directoryReconciliation:
                        reconciliation,
                    error:
                        read.Error ??
                        read.State.ToString()
                );
            }

            DataRelativePathRepairDirectoryJournalRecord journal =
                read.Record!;

            string? bindingError =
                DataRelativePathRepairPlanJournalBinding
                    .ValidateDirectory(
                        entry,
                        journal,
                        trustedDataRoot
                    );

            if (bindingError is not null)
            {
                return OperationResult(
                    entry,
                    DataRelativePathRepairPlanForwardOperationExecutionState
                        .JournalMismatch,
                    parentSnapshotCapture:
                        parentCapture,
                    directoryJournalRead:
                        read,
                    directoryExecution:
                        initialExecution,
                    directoryIntentRecovery:
                        intentRecovery,
                    directoryReprepareRecovery:
                        reprepareRecovery,
                    directoryForwardRecovery:
                        forwardRecovery,
                    directoryReconciliation:
                        reconciliation,
                    error:
                        bindingError
                );
            }

            DataRelativePathRepairDirectoryRecoveryClassification
                classification =
                    DataRelativePathRepairDirectoryRecoveryClassifier
                        .Classify(
                            journal,
                            trustedDataRoot
                        );

            lastClassification =
                classification;

            switch (classification.State)
            {
                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .AppliedFinalMatches:
                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .ReusedAppliedFinalMatches:
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanForwardOperationExecutionState
                            .AppliedDurably,
                        parentSnapshotCapture:
                            parentCapture,
                        directoryJournalRead:
                            read,
                        directoryClassification:
                            classification,
                        directoryExecution:
                            initialExecution,
                        directoryIntentRecovery:
                            intentRecovery,
                        directoryReprepareRecovery:
                            reprepareRecovery,
                        directoryForwardRecovery:
                            forwardRecovery,
                        directoryReconciliation:
                            reconciliation
                    );

                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .IntentFinalMissing:
                    intentRecovery =
                        DataRelativePathRepairDirectoryIntentRecoveryAction
                            .Recover(
                                journalDirectory,
                                entry.JournalChildName,
                                trustedDataRoot,
                                nowUtc,
                                read.JournalIncarnationIdentity!
                            );

                    if (!intentRecovery.Success)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .DirectoryIntentRecoveryFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            directoryJournalRead:
                                read,
                            directoryClassification:
                                classification,
                            directoryExecution:
                                initialExecution,
                            directoryIntentRecovery:
                                intentRecovery,
                            directoryReprepareRecovery:
                                reprepareRecovery,
                            directoryForwardRecovery:
                                forwardRecovery,
                            directoryReconciliation:
                                reconciliation,
                            error:
                                intentRecovery.Error ??
                                intentRecovery.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .PreparedBothMissing:
                    reprepareRecovery =
                        DataRelativePathRepairDirectoryReprepareRecoveryAction
                            .Recover(
                                journalDirectory,
                                entry.JournalChildName,
                                trustedDataRoot,
                                nowUtc,
                                read.JournalIncarnationIdentity!
                            );

                    if (!reprepareRecovery.Success)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .DirectoryReprepareRecoveryFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            directoryJournalRead:
                                read,
                            directoryClassification:
                                classification,
                            directoryExecution:
                                initialExecution,
                            directoryIntentRecovery:
                                intentRecovery,
                            directoryReprepareRecovery:
                                reprepareRecovery,
                            directoryForwardRecovery:
                                forwardRecovery,
                            directoryReconciliation:
                                reconciliation,
                            error:
                                reprepareRecovery.Error ??
                                reprepareRecovery.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .PreparedStagingMatchesFinalMissing:
                    forwardRecovery =
                        DataRelativePathRepairDirectoryForwardRecoveryAction
                            .Recover(
                                journalDirectory,
                                entry.JournalChildName,
                                trustedDataRoot,
                                nowUtc,
                                read.JournalIncarnationIdentity!
                            );

                    if (!forwardRecovery.Success)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .DirectoryForwardRecoveryFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            directoryJournalRead:
                                read,
                            directoryClassification:
                                classification,
                            directoryExecution:
                                initialExecution,
                            directoryIntentRecovery:
                                intentRecovery,
                            directoryReprepareRecovery:
                                reprepareRecovery,
                            directoryForwardRecovery:
                                forwardRecovery,
                            directoryReconciliation:
                                reconciliation,
                            error:
                                forwardRecovery.Error ??
                                forwardRecovery.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .PreparedFinalMatchesStagingMissing:
                    reconciliation =
                        DataRelativePathRepairDirectoryRecoveryReconciler
                            .Reconcile(
                                journalDirectory,
                                entry.JournalChildName,
                                read.JournalIncarnationIdentity!,
                                journal,
                                trustedDataRoot,
                                nowUtc
                            );

                    if (
                        reconciliation.State !=
                        DataRelativePathRepairDirectoryRecoveryReconciliationState
                            .AppliedDurably)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .DirectoryReconciliationFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            directoryJournalRead:
                                read,
                            directoryClassification:
                                classification,
                            directoryExecution:
                                initialExecution,
                            directoryIntentRecovery:
                                intentRecovery,
                            directoryReprepareRecovery:
                                reprepareRecovery,
                            directoryForwardRecovery:
                                forwardRecovery,
                            directoryReconciliation:
                                reconciliation,
                            error:
                                reconciliation.Error ??
                                reconciliation.State.ToString()
                        );
                    }

                    continue;

                default:
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanForwardOperationExecutionState
                            .DirectoryRecoveryStateNotForwardSafe,
                        parentSnapshotCapture:
                            parentCapture,
                        directoryJournalRead:
                            read,
                        directoryClassification:
                            classification,
                        directoryExecution:
                            initialExecution,
                        directoryIntentRecovery:
                            intentRecovery,
                        directoryReprepareRecovery:
                            reprepareRecovery,
                        directoryForwardRecovery:
                            forwardRecovery,
                        directoryReconciliation:
                            reconciliation,
                        error:
                            classification.Error ??
                            $"Directory recovery state " +
                            $"{classification.State} is not a safe " +
                            "forward-plan state."
                    );
            }
        }

        return OperationResult(
            entry,
            DataRelativePathRepairPlanForwardOperationExecutionState
                .ProgressLimitExceeded,
            parentSnapshotCapture:
                parentCapture,
            directoryJournalRead:
                lastRead,
            directoryClassification:
                lastClassification,
            directoryExecution:
                initialExecution,
            directoryIntentRecovery:
                intentRecovery,
            directoryReprepareRecovery:
                reprepareRecovery,
            directoryForwardRecovery:
                forwardRecovery,
            directoryReconciliation:
                reconciliation,
            error:
                "The directory operation did not converge to a durable " +
                $"Applied state within {MaxOperationPasses} passes."
        );
    }

    private static
        DataRelativePathRepairPlanForwardOperationExecution
        ExecuteFileOperation(
            LinuxNoFollowPathHandle journalDirectory,
            DataRelativePathRepairPlanManifestRecord manifest,
            DataRelativePathRepairPlanManifestOperation entry,
            LinuxNoFollowPathHandle trustedDataRootHandle,
            string trustedDataRoot,
            DateTimeOffset nowUtc)
    {
        DataRelativePathRepairDestinationParentSnapshotCaptureResult?
            parentCapture =
                null;

        DataRelativePathRepairFileJournalReaderResult?
            lastRead =
                null;

        DataRelativePathRepairFileRecoveryClassification?
            lastClassification =
                null;

        DataRelativePathRepairFileJournalTransitionResult?
            intentTransition =
                null;

        DataRelativePathRepairFileExecution?
            initialExecution =
                null;

        DataRelativePathRepairFileForwardRecovery?
            forwardRecovery =
                null;

        DataRelativePathRepairFileRecoveryReconciliation?
            reconciliation =
                null;

        for (
            int pass = 0;
            pass < MaxOperationPasses;
            pass++)
        {
            DataRelativePathRepairFileJournalReaderResult read =
                DataRelativePathRepairFileJournalReader.Read(
                    journalDirectory,
                    entry.JournalChildName
                );

            lastRead =
                read;

            if (
                read.State ==
                DataRelativePathRepairFileJournalReadState
                    .JournalUnavailable)
            {
                DataRelativePathRepairDestinationParentSnapshot?
                    parentSnapshot =
                        GetStartParentSnapshot(
                            manifest,
                            entry,
                            trustedDataRootHandle,
                            out parentCapture
                        );

                if (parentSnapshot is null)
                {
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanForwardOperationExecutionState
                            .DestinationParentSnapshotCaptureFailed,
                        parentSnapshotCapture:
                            parentCapture,
                        fileJournalRead:
                            read,
                        error:
                            parentCapture?.Error ??
                            "The operation destination parent could not " +
                            "be captured."
                    );
                }

                intentTransition =
                    DataRelativePathRepairFileJournal.CreateIntent(
                        Guid.NewGuid(),
                        nowUtc,
                        trustedDataRoot,
                        entry.Operation,
                        manifest.SourceSnapshot,
                        parentSnapshot
                    );

                if (!intentTransition.Success)
                {
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanForwardOperationExecutionState
                            .FileIntentCreationFailed,
                        parentSnapshotCapture:
                            parentCapture,
                        fileJournalRead:
                            read,
                        fileIntentTransition:
                            intentTransition,
                        error:
                            intentTransition.Error ??
                            intentTransition.State.ToString()
                    );
                }

                DataRelativePathRepairFileJournalRecord intent =
                    intentTransition.Record!;

                initialExecution =
                    DataRelativePathRepairFileExecutor.Execute(
                        journalDirectory,
                        entry.JournalChildName,
                        intent,
                        trustedDataRoot,
                        nowUtc
                    );

                if (!initialExecution.Success)
                {
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanForwardOperationExecutionState
                            .FileExecutionFailed,
                        parentSnapshotCapture:
                            parentCapture,
                        fileJournalRead:
                            read,
                        fileIntentTransition:
                            intentTransition,
                        fileExecution:
                            initialExecution,
                        error:
                            initialExecution.Error ??
                            initialExecution.State.ToString()
                    );
                }

                /*
                 * As with directory execution, durable journal state is
                 * the plan-level progress truth. Re-read it rather than
                 * advancing from an in-memory success result.
                 */
                continue;
            }

            if (!read.Success)
            {
                return OperationResult(
                    entry,
                    DataRelativePathRepairPlanForwardOperationExecutionState
                        .JournalReadFailed,
                    parentSnapshotCapture:
                        parentCapture,
                    fileJournalRead:
                        read,
                    fileIntentTransition:
                        intentTransition,
                    fileExecution:
                        initialExecution,
                    fileForwardRecovery:
                        forwardRecovery,
                    fileReconciliation:
                        reconciliation,
                    error:
                        read.Error ??
                        read.State.ToString()
                );
            }

            DataRelativePathRepairFileJournalRecord journal =
                read.Record!;

            string? bindingError =
                DataRelativePathRepairPlanJournalBinding
                    .ValidateFile(
                        manifest,
                        entry,
                        journal,
                        trustedDataRoot
                    );

            if (bindingError is not null)
            {
                return OperationResult(
                    entry,
                    DataRelativePathRepairPlanForwardOperationExecutionState
                        .JournalMismatch,
                    parentSnapshotCapture:
                        parentCapture,
                    fileJournalRead:
                        read,
                    fileIntentTransition:
                        intentTransition,
                    fileExecution:
                        initialExecution,
                    fileForwardRecovery:
                        forwardRecovery,
                    fileReconciliation:
                        reconciliation,
                    error:
                        bindingError
                );
            }

            DataRelativePathRepairFileRecoveryClassification
                classification =
                    DataRelativePathRepairFileRecoveryClassifier.Classify(
                        journal,
                        trustedDataRoot
                    );

            lastClassification =
                classification;

            switch (classification.State)
            {
                case
                    DataRelativePathRepairFileRecoveryState
                        .AppliedDestinationMatches:
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanForwardOperationExecutionState
                            .AppliedDurably,
                        parentSnapshotCapture:
                            parentCapture,
                        fileJournalRead:
                            read,
                        fileClassification:
                            classification,
                        fileIntentTransition:
                            intentTransition,
                        fileExecution:
                            initialExecution,
                        fileForwardRecovery:
                            forwardRecovery,
                        fileReconciliation:
                            reconciliation
                    );

                case
                    DataRelativePathRepairFileRecoveryState
                        .IntentDestinationMissing:
                case
                    DataRelativePathRepairFileRecoveryState
                        .PreparedDestinationMissing:
                    forwardRecovery =
                        DataRelativePathRepairFileForwardRecoveryAction
                            .Recover(
                                journalDirectory,
                                entry.JournalChildName,
                                trustedDataRoot,
                                nowUtc,
                                read.JournalIncarnationIdentity!
                            );

                    if (!forwardRecovery.Success)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .FileForwardRecoveryFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            fileJournalRead:
                                read,
                            fileClassification:
                                classification,
                            fileIntentTransition:
                                intentTransition,
                            fileExecution:
                                initialExecution,
                            fileForwardRecovery:
                                forwardRecovery,
                            fileReconciliation:
                                reconciliation,
                            error:
                                forwardRecovery.Error ??
                                forwardRecovery.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairFileRecoveryState
                        .PreparedDestinationMatches:
                    reconciliation =
                        DataRelativePathRepairFileRecoveryReconciler
                            .Reconcile(
                                journalDirectory,
                                entry.JournalChildName,
                                read.JournalIncarnationIdentity!,
                                journal,
                                trustedDataRoot,
                                nowUtc
                            );

                    if (
                        reconciliation.State !=
                        DataRelativePathRepairFileRecoveryReconciliationState
                            .AppliedDurably)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanForwardOperationExecutionState
                                .FileReconciliationFailed,
                            parentSnapshotCapture:
                                parentCapture,
                            fileJournalRead:
                                read,
                            fileClassification:
                                classification,
                            fileIntentTransition:
                                intentTransition,
                            fileExecution:
                                initialExecution,
                            fileForwardRecovery:
                                forwardRecovery,
                            fileReconciliation:
                                reconciliation,
                            error:
                                reconciliation.Error ??
                                reconciliation.State.ToString()
                        );
                    }

                    continue;

                default:
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanForwardOperationExecutionState
                            .FileRecoveryStateNotForwardSafe,
                        parentSnapshotCapture:
                            parentCapture,
                        fileJournalRead:
                            read,
                        fileClassification:
                            classification,
                        fileIntentTransition:
                            intentTransition,
                        fileExecution:
                            initialExecution,
                        fileForwardRecovery:
                            forwardRecovery,
                        fileReconciliation:
                            reconciliation,
                        error:
                            classification.Error ??
                            $"File recovery state {classification.State} " +
                            "is not a safe forward-plan state."
                    );
            }
        }

        return OperationResult(
            entry,
            DataRelativePathRepairPlanForwardOperationExecutionState
                .ProgressLimitExceeded,
            parentSnapshotCapture:
                parentCapture,
            fileJournalRead:
                lastRead,
            fileClassification:
                lastClassification,
            fileIntentTransition:
                intentTransition,
            fileExecution:
                initialExecution,
            fileForwardRecovery:
                forwardRecovery,
            fileReconciliation:
                reconciliation,
            error:
                "The file operation did not converge to a durable " +
                $"Applied state within {MaxOperationPasses} passes."
        );
    }

    private static
        DataRelativePathRepairDestinationParentSnapshot?
        GetStartParentSnapshot(
            DataRelativePathRepairPlanManifestRecord manifest,
            DataRelativePathRepairPlanManifestOperation entry,
            LinuxNoFollowPathHandle trustedDataRootHandle,
            out
                DataRelativePathRepairDestinationParentSnapshotCaptureResult?
                    capture)
    {
        capture =
            null;

        if (entry.Index == 0)
        {
            return manifest.InitialDestinationParentSnapshot;
        }

        string? parentPath =
            Path.GetDirectoryName(
                entry.Operation.DestinationPath
            );

        if (string.IsNullOrEmpty(parentPath))
        {
            return null;
        }

        capture =
            DataRelativePathRepairDestinationParentSnapshotCapture
                .Capture(
                    trustedDataRootHandle,
                    parentPath
                );

        return capture.Success
            ? capture.Snapshot
            : null;
    }

    /*
     * Authenticate durable batch-wide apply authority from the retained
     * batch-directory descriptor.
     *
     * The caller-supplied BatchExecutionContext is metadata only. Re-read the
     * exact durable batch manifest, recreate the logical context from those
     * bytes, and compare every scalar/member structurally before consulting
     * the authorization record.
     *
     * A schema-v1 batch remains valid batch metadata but never receives
     * aggregate namespace authority.
     */
    private static BatchApplyAuthorityAuthentication
        AuthenticateBatchApplyAuthority(
            BatchExecutionScope batchScope,
            string trustedDataRoot,
            bool requireFreshCoverage)
    {
        DataRelativePathRepairBatchManifestReaderResult batchManifestRead;

        try
        {
            batchManifestRead =
                DataRelativePathRepairBatchManifestReader.Read(
                    batchScope.BatchDirectory,
                    BatchManifestChildName
                );
        }
        catch (Exception ex)
        {
            return BatchApplyAuthorityAuthentication.Failed(
                "The retained batch manifest could not be read while " +
                "authenticating batch apply authority: " +
                ex.Message
            );
        }

        if (
            !batchManifestRead.Success ||
            batchManifestRead.Manifest is null ||
            string.IsNullOrWhiteSpace(
                batchManifestRead.ManifestSha256))
        {
            return BatchApplyAuthorityAuthentication.Failed(
                "The retained batch manifest could not be authenticated " +
                "for batch apply authority: " +
                (
                    batchManifestRead.Error ??
                    batchManifestRead.State.ToString()
                )
            );
        }

        DataRelativePathRepairBatchManifestRecord batchManifest =
            batchManifestRead.Manifest;

        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                batchManifest.DataRoot,
                out string? batchRootBindingError))
        {
            return BatchApplyAuthorityAuthentication.Failed(
                batchRootBindingError ??
                "The retained batch manifest Data root does not match the " +
                "independently trusted Data root."
            );
        }

        DataRelativePathRepairBatchExecutionContext suppliedContext =
            batchScope.Context;

        if (
            suppliedContext.CurrentChildIndex < 0 ||
            suppliedContext.CurrentChildIndex >=
                batchManifest.Children.Count)
        {
            return BatchApplyAuthorityAuthentication.Failed(
                "The supplied batch execution context current-child index " +
                "is outside the exact retained batch manifest."
            );
        }

        DataRelativePathRepairBatchExecutionContextCreation
            recreatedContextCreation =
                DataRelativePathRepairBatchExecutionContext.Create(
                    batchManifest,
                    suppliedContext.CurrentChildIndex,
                    batchManifest.Children[
                        suppliedContext.CurrentChildIndex
                    ]
                );

        if (
            !recreatedContextCreation.Success ||
            recreatedContextCreation.Context is null)
        {
            return BatchApplyAuthorityAuthentication.Failed(
                "The exact retained batch manifest could not recreate the " +
                "current execution context: " +
                (
                    recreatedContextCreation.Error ??
                    recreatedContextCreation.State.ToString()
                )
            );
        }

        DataRelativePathRepairBatchExecutionContext recreatedContext =
            recreatedContextCreation.Context;

        if (
            !BatchExecutionContextsMatch(
                suppliedContext,
                recreatedContext,
                out string? contextBindingError))
        {
            return BatchApplyAuthorityAuthentication.Failed(
                contextBindingError ??
                "The supplied batch execution context no longer matches " +
                "the exact retained batch manifest."
            );
        }

        if (
            batchManifest.SchemaVersion ==
                DataRelativePathRepairBatchManifestRecord.SchemaVersion1)
        {
            return BatchApplyAuthorityAuthentication.Succeeded(
                aggregateCoverageAuthorized:
                    false
            );
        }

        if (
            batchManifest.SchemaVersion !=
                DataRelativePathRepairBatchManifestRecord.SchemaVersion2 ||
            batchManifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion1)
        {
            return BatchApplyAuthorityAuthentication.Failed(
                "The retained batch manifest has no supported aggregate " +
                "namespace apply-authorization policy."
            );
        }

        DataRelativePathRepairBatchApplyAuthorizationReaderResult
            authorizationRead;

        try
        {
            authorizationRead =
                DataRelativePathRepairBatchApplyAuthorizationReader.Read(
                    batchScope.BatchDirectory,
                    BatchApplyAuthorizationChildName
                );
        }
        catch (Exception ex)
        {
            return BatchApplyAuthorityAuthentication.Failed(
                "The durable batch apply authorization could not be read: " +
                ex.Message
            );
        }

        if (
            !authorizationRead.Success ||
            authorizationRead.Authorization is null)
        {
            return BatchApplyAuthorityAuthentication.Failed(
                "A coverage-authorized schema-v2 batch requires an exact " +
                "durable batch-wide apply authorization before child " +
                "mutation: " +
                (
                    authorizationRead.Error ??
                    authorizationRead.State.ToString()
                )
            );
        }

        string? authorizationBindingError =
            DataRelativePathRepairBatchApplyAuthorization
                .ValidateCompletedBatchBinding(
                    authorizationRead.Authorization,
                    batchManifest,
                    batchManifestRead.ManifestSha256
                );

        if (authorizationBindingError is not null)
        {
            return BatchApplyAuthorityAuthentication.Failed(
                authorizationBindingError
            );
        }

        if (requireFreshCoverage)
        {
            string? freshCoverageError =
                ValidateFreshAggregateBatchCoverage(
                    batchScope.BatchDirectory,
                    batchManifest,
                    trustedDataRoot
                );

            if (freshCoverageError is not null)
            {
                return BatchApplyAuthorityAuthentication.Failed(
                    freshCoverageError
                );
            }
        }

        return BatchApplyAuthorityAuthentication.Succeeded(
            aggregateCoverageAuthorized:
                true
        );
    }

    /*
     * Fresh aggregate coverage for one not-yet-started coverage-v2 child.
     *
     * The durable batch authorization proves that the immutable batch
     * legitimately crossed its initial mutation boundary. It does not freeze
     * the source filesystem forever.
     *
     * Before another child creates its first durable operation journal,
     * authenticate every child manifest again from the retained batch
     * descriptor and re-run aggregate physical namespace coverage against
     * the current filesystem.
     *
     * Once the current child has any durable operation journal, this check is
     * deliberately skipped and normal recovery/idempotence remains
     * authoritative.
     */
    private static string? ValidateFreshAggregateBatchCoverage(
        LinuxNoFollowPathHandle batchDirectory,
        DataRelativePathRepairBatchManifestRecord batchManifest,
        string trustedDataRoot)
    {
        var authenticatedManifests =
            new DataRelativePathRepairPlanManifestRecord[
                batchManifest.Children.Count
            ];

        for (
            int index = 0;
            index < batchManifest.Children.Count;
            index++)
        {
            DataRelativePathRepairBatchManifestChild expectedChild =
                batchManifest.Children[index];

            LinuxOpenChildDirectoryReadOnlyAtResult childOpen;

            try
            {
                childOpen =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        batchDirectory,
                        expectedChild.ChildName
                    );
            }
            catch (Exception ex)
            {
                return
                    "Fresh aggregate namespace coverage could not open " +
                    $"recorded child {index} " +
                    $"\"{expectedChild.ChildName}\" descriptor-relatively: " +
                    ex.Message;
            }

            if (
                !childOpen.Success ||
                childOpen.OpenedDirectory is null)
            {
                return
                    "Fresh aggregate namespace coverage could not open " +
                    $"recorded child {index} " +
                    $"\"{expectedChild.ChildName}\" descriptor-relatively " +
                    $"({childOpen.State}): " +
                    (
                        childOpen.Error ??
                        "no additional error"
                    );
            }

            using LinuxNoFollowPathHandle childDirectory =
                childOpen.OpenedDirectory;

            DataRelativePathRepairPlanManifestReaderResult childManifestRead;

            try
            {
                childManifestRead =
                    DataRelativePathRepairPlanManifestReader.Read(
                        childDirectory,
                        batchManifest.ChildManifestName
                    );
            }
            catch (Exception ex)
            {
                return
                    "Fresh aggregate namespace coverage could not read the " +
                    $"recorded manifest for child {index} " +
                    $"\"{expectedChild.ChildName}\": " +
                    ex.Message;
            }

            if (
                !childManifestRead.Success ||
                childManifestRead.Manifest is null ||
                string.IsNullOrWhiteSpace(
                    childManifestRead.ManifestSha256))
            {
                return
                    "Fresh aggregate namespace coverage could not " +
                    $"authenticate recorded child {index} " +
                    $"\"{expectedChild.ChildName}\": " +
                    (
                        childManifestRead.Error ??
                        childManifestRead.State.ToString()
                    );
            }

            if (
                childManifestRead.Manifest.PlanId !=
                    expectedChild.PlanId)
            {
                return
                    "Fresh aggregate namespace coverage observed a PlanId " +
                    $"mismatch for recorded child {index} " +
                    $"\"{expectedChild.ChildName}\".";
            }

            if (
                !string.Equals(
                    childManifestRead.ManifestSha256,
                    expectedChild.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    "Fresh aggregate namespace coverage observed a manifest " +
                    $"SHA-256 mismatch for recorded child {index} " +
                    $"\"{expectedChild.ChildName}\".";
            }

            if (
                !DataRelativePathRepairDataRootAuthority.Matches(
                    trustedDataRoot,
                    childManifestRead.Manifest.DataRoot,
                    out string? childRootBindingError))
            {
                return
                    "Fresh aggregate namespace coverage observed a child " +
                    $"Data-root mismatch for recorded child {index} " +
                    $"\"{expectedChild.ChildName}\": " +
                    (
                        childRootBindingError ??
                        "the child manifest is not bound to the trusted Data root"
                    );
            }

            authenticatedManifests[index] =
                childManifestRead.Manifest;
        }

        DataRelativePathRepairBatchCoverageAuthorization coverage;

        try
        {
            coverage =
                DataRelativePathRepairBatchCoverageAuthorizer
                    .AuthorizePersistedManifests(
                        authenticatedManifests
                    );
        }
        catch (Exception ex)
        {
            return
                "Fresh aggregate namespace coverage inspection failed: " +
                ex.Message;
        }

        if (coverage.AllAuthorized)
        {
            return null;
        }

        DataRelativePathRepairBatchCoverageDecision? firstFailure =
            coverage.Decisions
                .FirstOrDefault(
                    decision =>
                        decision.State !=
                        DataRelativePathRepairBatchCoverageDecisionState
                            .Authorized
                );

        if (firstFailure is null)
        {
            return
                "Fresh aggregate namespace coverage was not authorized, " +
                "but no specific failed decision was retained.";
        }

        return
            "Fresh aggregate namespace coverage rejected the unstarted " +
            $"batch child boundary at candidate " +
            $"{firstFailure.CandidateIndex} " +
            $"({firstFailure.State}): " +
            (
                firstFailure.Error ??
                "no additional error"
            );
    }

    private static bool BatchExecutionContextsMatch(
        DataRelativePathRepairBatchExecutionContext supplied,
        DataRelativePathRepairBatchExecutionContext recreated,
        out string? error)
    {
        error =
            null;

        if (supplied.BatchId != recreated.BatchId)
        {
            error =
                "The supplied batch execution context BatchId does not " +
                "match the exact retained batch manifest.";

            return false;
        }

        if (
            !string.Equals(
                supplied.DataRoot,
                recreated.DataRoot,
                StringComparison.Ordinal))
        {
            error =
                "The supplied batch execution context Data root text does " +
                "not match the exact retained batch manifest.";

            return false;
        }

        if (
            !string.Equals(
                supplied.ChildManifestName,
                recreated.ChildManifestName,
                StringComparison.Ordinal))
        {
            error =
                "The supplied batch execution context child-manifest name " +
                "does not match the exact retained batch manifest.";

            return false;
        }

        if (
            supplied.CurrentChildIndex !=
                recreated.CurrentChildIndex)
        {
            error =
                "The supplied batch execution context current-child index " +
                "does not match the exact retained batch manifest.";

            return false;
        }

        if (
            !BatchExecutionChildrenMatch(
                supplied.CurrentChild,
                recreated.CurrentChild))
        {
            error =
                "The supplied batch execution context current-child " +
                "expectation does not match the exact retained batch " +
                "manifest.";

            return false;
        }

        if (
            supplied.EarlierChildren.Count !=
                recreated.EarlierChildren.Count)
        {
            error =
                "The supplied batch execution context earlier-child count " +
                "does not match the exact retained batch manifest.";

            return false;
        }

        for (
            int index = 0;
            index < supplied.EarlierChildren.Count;
            index++)
        {
            if (
                !BatchExecutionChildrenMatch(
                    supplied.EarlierChildren[index],
                    recreated.EarlierChildren[index]))
            {
                error =
                    $"The supplied batch execution context earlier child " +
                    $"at index {index} does not match the exact retained " +
                    "batch manifest.";

                return false;
            }
        }

        return true;
    }

    private static bool BatchExecutionChildrenMatch(
        DataRelativePathRepairBatchExecutionChildExpectation left,
        DataRelativePathRepairBatchExecutionChildExpectation right)
    {
        return
            left.Index == right.Index &&
            string.Equals(
                left.ChildName,
                right.ChildName,
                StringComparison.Ordinal
            ) &&
            left.PlanId == right.PlanId &&
            string.Equals(
                left.ManifestSha256,
                right.ManifestSha256,
                StringComparison.OrdinalIgnoreCase
            );
    }

    /*
     * The descriptor and logical context travel together so same-batch
     * authorization cannot receive membership metadata without the retained
     * batch descriptor needed to reauthenticate earlier children. This
     * record itself grants no mutation authority.
     */
    private sealed record BatchExecutionScope(
        LinuxNoFollowPathHandle BatchDirectory,
        DataRelativePathRepairBatchExecutionContext Context
    );

    private sealed record BatchApplyAuthorityAuthentication(
        bool Success,
        bool AggregateCoverageAuthorized,
        string? Error
    )
    {
        public static BatchApplyAuthorityAuthentication Succeeded(
            bool aggregateCoverageAuthorized)
        {
            return new(
                Success:
                    true,
                AggregateCoverageAuthorized:
                    aggregateCoverageAuthorized,
                Error:
                    null
            );
        }

        public static BatchApplyAuthorityAuthentication Failed(
            string error)
        {
            return new(
                Success:
                    false,
                AggregateCoverageAuthorized:
                    false,
                Error:
                    error
            );
        }
    }

    private sealed record PreflightResult(
        DataRelativePathRepairPlanForwardOperationExecution?
            Failure,
        bool HasExistingJournal
    )
    {
        public bool Success =>
            Failure is null;

        public static PreflightResult Succeeded(
            bool hasExistingJournal)
        {
            return new(
                Failure:
                    null,
                HasExistingJournal:
                    hasExistingJournal
            );
        }

        public static PreflightResult Failed(
            DataRelativePathRepairPlanForwardOperationExecution failure)
        {
            ArgumentNullException.ThrowIfNull(
                failure
            );

            return new(
                Failure:
                    failure,
                HasExistingJournal:
                    true
            );
        }
    }

    private static
        DataRelativePathRepairPlanForwardOperationExecution
        OperationResult(
            DataRelativePathRepairPlanManifestOperation entry,
            DataRelativePathRepairPlanForwardOperationExecutionState
                state,
            DataRelativePathRepairDestinationParentSnapshotCaptureResult?
                parentSnapshotCapture = null,
            DataRelativePathRepairDirectoryJournalReaderResult?
                directoryJournalRead = null,
            DataRelativePathRepairDirectoryRecoveryClassification?
                directoryClassification = null,
            DataRelativePathRepairDirectoryExecution?
                directoryExecution = null,
            DataRelativePathRepairDirectoryIntentRecovery?
                directoryIntentRecovery = null,
            DataRelativePathRepairDirectoryReprepareRecovery?
                directoryReprepareRecovery = null,
            DataRelativePathRepairDirectoryForwardRecovery?
                directoryForwardRecovery = null,
            DataRelativePathRepairDirectoryRecoveryReconciliation?
                directoryReconciliation = null,
            DataRelativePathRepairFileJournalReaderResult?
                fileJournalRead = null,
            DataRelativePathRepairFileRecoveryClassification?
                fileClassification = null,
            DataRelativePathRepairFileJournalTransitionResult?
                fileIntentTransition = null,
            DataRelativePathRepairFileExecution?
                fileExecution = null,
            DataRelativePathRepairFileForwardRecovery?
                fileForwardRecovery = null,
            DataRelativePathRepairFileRecoveryReconciliation?
                fileReconciliation = null,
            string? error = null)
    {
        return new(
            Index:
                entry.Index,
            Kind:
                entry.Operation.Kind,
            JournalChildName:
                entry.JournalChildName,
            State:
                state,
            ParentSnapshotCapture:
                parentSnapshotCapture,
            DirectoryJournalRead:
                directoryJournalRead,
            DirectoryClassification:
                directoryClassification,
            DirectoryExecution:
                directoryExecution,
            DirectoryIntentRecovery:
                directoryIntentRecovery,
            DirectoryReprepareRecovery:
                directoryReprepareRecovery,
            DirectoryForwardRecovery:
                directoryForwardRecovery,
            DirectoryReconciliation:
                directoryReconciliation,
            FileJournalRead:
                fileJournalRead,
            FileClassification:
                fileClassification,
            FileIntentTransition:
                fileIntentTransition,
            FileExecution:
                fileExecution,
            FileForwardRecovery:
                fileForwardRecovery,
            FileReconciliation:
                fileReconciliation,
            Error:
                error
        );
    }

    private static DataRelativePathRepairPlanForwardExecution
        PlanResult(
            DataRelativePathRepairPlanForwardExecutionState state,
            DataRelativePathRepairPlanManifestReaderResult?
                manifestRead,
            IReadOnlyList<
                DataRelativePathRepairPlanForwardOperationExecution
            > operationResults,
            string? error)
    {
        return new(
            State:
                state,
            ManifestRead:
                manifestRead,
            OperationResults:
                operationResults,
            Error:
                error
        );
    }
}
