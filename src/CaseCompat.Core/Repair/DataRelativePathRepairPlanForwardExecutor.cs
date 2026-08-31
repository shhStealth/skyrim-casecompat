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
                                    trustedDataRoot,
                                    nowUtc
                                ),

                        DataRelativePathRepairPlanOperationKind
                            .CreateFile =>
                                ExecuteFileOperation(
                                    journalDirectory,
                                    manifest,
                                    entry,
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

    private static
        DataRelativePathRepairPlanForwardOperationExecution
        ExecuteDirectoryOperation(
            LinuxNoFollowPathHandle journalDirectory,
            DataRelativePathRepairPlanManifestRecord manifest,
            DataRelativePathRepairPlanManifestOperation entry,
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
                            trustedDataRoot,
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
                ValidateDirectoryJournalBinding(
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
                            trustedDataRoot,
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
                ValidateFileJournalBinding(
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
            string trustedDataRoot,
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
                    trustedDataRoot,
                    parentPath
                );

        return capture.Success
            ? capture.Snapshot
            : null;
    }

    /*
     * Exact manifest journal names identify where an operation journal
     * belongs, but the filename itself is not authority.
     *
     * Cross-bind the loaded durable journal to the manifest operation
     * before allowing its state to drive recovery.
     */
    private static string? ValidateDirectoryJournalBinding(
        DataRelativePathRepairPlanManifestOperation entry,
        DataRelativePathRepairDirectoryJournalRecord journal,
        string trustedDataRoot)
    {
        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                journal.DataRoot,
                out string? rootBindingError
            ))
        {
            return
                "The directory operation journal Data root does not " +
                "match the independently trusted Data root: " +
                rootBindingError;
        }

        return ValidateOperationBinding(
            entry,
            journal.Operation
        );
    }

    private static string? ValidateFileJournalBinding(
        DataRelativePathRepairPlanManifestRecord manifest,
        DataRelativePathRepairPlanManifestOperation entry,
        DataRelativePathRepairFileJournalRecord journal,
        string trustedDataRoot)
    {
        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                journal.DataRoot,
                out string? rootBindingError
            ))
        {
            return
                "The file operation journal Data root does not match " +
                "the independently trusted Data root: " +
                rootBindingError;
        }

        string? operationError =
            ValidateOperationBinding(
                entry,
                journal.Operation
            );

        if (operationError is not null)
        {
            return operationError;
        }

        if (
            !SameSourceSnapshot(
                manifest.SourceSnapshot,
                journal.SourceSnapshot
            ))
        {
            return
                "The file operation journal source snapshot does not " +
                "match the immutable plan manifest source evidence.";
        }

        return null;
    }

    private static string? ValidateOperationBinding(
        DataRelativePathRepairPlanManifestOperation entry,
        DataRelativePathRepairPlanOperation journalOperation)
    {
        DataRelativePathRepairPlanOperation expected =
            entry.Operation;

        if (journalOperation.Kind != expected.Kind)
        {
            return
                $"Operation journal {entry.JournalChildName} has kind " +
                $"{journalOperation.Kind}, but the manifest requires " +
                $"{expected.Kind}.";
        }

        if (
            !PathEquals(
                journalOperation.DestinationPath,
                expected.DestinationPath
            ))
        {
            return
                $"Operation journal {entry.JournalChildName} has a " +
                "destination that does not match the immutable plan " +
                "manifest.";
        }

        if (
            !NullablePathEquals(
                journalOperation.SourcePath,
                expected.SourcePath
            ))
        {
            return
                $"Operation journal {entry.JournalChildName} has a " +
                "source path that does not match the immutable plan " +
                "manifest.";
        }

        return null;
    }

    private static bool SameSourceSnapshot(
        DataRelativePathRepairSourceSnapshot expected,
        DataRelativePathRepairSourceSnapshot actual)
    {
        LinuxFileIdentityResult expectedIdentity =
            expected.Identity;

        LinuxFileIdentityResult actualIdentity =
            actual.Identity;

        return
            PathEquals(
                expected.PhysicalPath,
                actual.PhysicalPath
            ) &&
            expected.Size ==
                actual.Size &&
            string.Equals(
                expected.Sha256,
                actual.Sha256,
                StringComparison.OrdinalIgnoreCase
            ) &&
            PathEquals(
                expectedIdentity.FullPath,
                actualIdentity.FullPath
            ) &&
            expectedIdentity.DeviceMajor ==
                actualIdentity.DeviceMajor &&
            expectedIdentity.DeviceMinor ==
                actualIdentity.DeviceMinor &&
            expectedIdentity.Inode ==
                actualIdentity.Inode &&
            expectedIdentity.LinkCount ==
                actualIdentity.LinkCount &&
            expectedIdentity.MountId ==
                actualIdentity.MountId;
    }

    private static bool NullablePathEquals(
        string? left,
        string? right)
    {
        if (
            left is null ||
            right is null)
        {
            return
                left is null &&
                right is null;
        }

        return PathEquals(
            left,
            right
        );
    }

    private static bool PathEquals(
        string left,
        string right)
    {
        try
        {
            string normalizedLeft =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        left
                    )
                );

            string normalizedRight =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        right
                    )
                );

            return string.Equals(
                normalizedLeft,
                normalizedRight,
                StringComparison.Ordinal
            );
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return false;
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
