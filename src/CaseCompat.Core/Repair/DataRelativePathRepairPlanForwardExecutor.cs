using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairPlanForwardExecutor
{
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
                                    nowUtc
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

        return PreflightResult.Succeeded();
    }

    private static bool IsDirectoryForwardSafe(
        DataRelativePathRepairDirectoryRecoveryState state)
    {
        return state is
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMatches or
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
            DateTimeOffset nowUtc)
    {
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

    private sealed record PreflightResult(
        DataRelativePathRepairPlanForwardOperationExecution?
            Failure
    )
    {
        public bool Success =>
            Failure is null;

        public static PreflightResult Succeeded()
        {
            return new(
                Failure:
                    null
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
                    failure
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
