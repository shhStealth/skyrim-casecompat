using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairPlanRollbackExecutor
{
    /*
     * A successful action changes durable operation-journal state.
     * Re-read after every action and require convergence rather than
     * advancing from an in-memory success result.
     */
    private const int MaxOperationPasses =
        8;

    public static DataRelativePathRepairPlanRollbackExecution Execute(
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
                DataRelativePathRepairPlanRollbackExecutionState
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
         * A manifest is durable historical plan data. It does not
         * independently establish filesystem authority.
         */
        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                manifest.DataRoot,
                out string? rootBindingError
            ))
        {
            return PlanResult(
                DataRelativePathRepairPlanRollbackExecutionState
                    .ManifestDataRootMismatch,
                manifestRead,
                [],
                rootBindingError
            );
        }

        /*
         * Preflight is intentionally read/classify only.
         *
         * Existing operation journals must form one contiguous prefix.
         * Every existing journal is cross-bound to the immutable plan
         * and must already be in a state that this rollback executor
         * understands safely.
         *
         * This is not a global transaction lock. Each later mutation
         * still performs a fresh exact journal read and carries that
         * strong journal incarnation into the guarded action.
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
                DataRelativePathRepairPlanRollbackExecutionState
                    .PreflightFailed,
                manifestRead,
                preflight.Failure is null
                    ? []
                    : [preflight.Failure],
                preflight.Failure?.Error ??
                    "Rollback preflight failed."
            );
        }

        var results =
            new List<
                DataRelativePathRepairPlanRollbackOperationExecution
            >(
                manifest.Operations.Count
            );

        /*
         * Any operation beyond ExistingOperationCount was never
         * started. Report that truthfully rather than pretending a
         * nonexistent journal was rolled back.
         */
        for (
            int index = manifest.Operations.Count - 1;
            index >= preflight.ExistingOperationCount;
            index--)
        {
            DataRelativePathRepairPlanManifestOperation entry =
                manifest.Operations[index];

            results.Add(
                OperationResult(
                    entry,
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .NotStartedSkipped
                )
            );
        }

        /*
         * Destructive work is reverse ordered:
         *
         * file -> inner directory -> outer directory.
         *
         * The manifest chooses the exact journals and ordering only.
         * Individual operation journals and their generation-aware
         * prepared-object identities remain the authority for removal.
         */
        for (
            int index = preflight.ExistingOperationCount - 1;
            index >= 0;
            index--)
        {
            DataRelativePathRepairPlanManifestOperation entry =
                manifest.Operations[index];

            /*
             * Preflight may have proven a terminal descendant absent
             * through a directly observed missing rolled-back ancestor.
             *
             * Re-read and cross-bind that descendant journal now. Do
             * not rely only on the earlier preflight record, and do not
             * weaken the low-level recovery classifiers.
             */
            if (
                preflight.InferredRolledBackOperationIndexes.Contains(
                    index
                ))
            {
                DataRelativePathRepairPlanRollbackOperationExecution
                    inferred =
                        ValidateInferredRolledBackOperation(
                            journalDirectory,
                            manifest,
                            entry,
                            trustedDataRoot
                        );

                results.Add(
                    inferred
                );

                if (!inferred.Success)
                {
                    return PlanResult(
                        DataRelativePathRepairPlanRollbackExecutionState
                            .OperationFailed,
                        manifestRead,
                        results,
                        inferred.Error ??
                            inferred.State.ToString()
                    );
                }

                continue;
            }

            DataRelativePathRepairPlanRollbackOperationExecution result =
                entry.Operation.Kind switch
                {
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory =>
                            ExecuteDirectoryOperation(
                                journalDirectory,
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
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .JournalMismatch,
                            error:
                                $"Unsupported plan operation kind " +
                                $"{entry.Operation.Kind}."
                        )
                };

            results.Add(
                result
            );

            if (!result.Success)
            {
                return PlanResult(
                    DataRelativePathRepairPlanRollbackExecutionState
                        .OperationFailed,
                    manifestRead,
                    results,
                    result.Error ??
                        result.State.ToString()
                );
            }
        }

        return PlanResult(
            DataRelativePathRepairPlanRollbackExecutionState
                .RolledBackDurably,
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
         * A directly classified RolledBack directory whose final name
         * is absent proves that every lexical descendant is absent at
         * this instant as well.
         *
         * This is needed for nested completed rollback: once the outer
         * plan-created directory is gone, the ordinary classifier for
         * its already-RolledBack descendants cannot reacquire their
         * recorded destination parents.
         *
         * Only durable RolledBack descendant journals may use this
         * proof. Applied, Prepared, RollbackRequested, Intent, or
         * conflicting descendants still fail closed.
         */
        var provenMissingDirectoryDestinations =
            new List<string>();

        var inferredRolledBackOperationIndexes =
            new HashSet<int>();

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
                                DataRelativePathRepairPlanRollbackOperationExecutionState
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
                                DataRelativePathRepairPlanRollbackOperationExecutionState
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
                                DataRelativePathRepairPlanRollbackOperationExecutionState
                                    .JournalMismatch,
                                directoryJournalRead:
                                    read,
                                error:
                                    bindingError
                            )
                        );
                    }

                    /*
                     * If an earlier plan directory is directly proven
                     * absent, a later directory beneath it cannot have
                     * either its final name or its staging sibling
                     * present.
                     *
                     * Still require this descendant's exact durable
                     * journal to be RolledBack and cross-bound above.
                     */
                    if (
                        journal.State ==
                            DataRelativePathRepairDirectoryJournalState
                                .RolledBack &&
                        IsStrictlyWithinAny(
                            entry.Operation.DestinationPath,
                            provenMissingDirectoryDestinations
                        ))
                    {
                        inferredRolledBackOperationIndexes.Add(
                            index
                        );

                        /*
                         * Its complete subtree is absent by the same
                         * ancestor proof, so it may itself serve as a
                         * redundant missing ancestor for deeper entries.
                         */
                        provenMissingDirectoryDestinations.Add(
                            entry.Operation.DestinationPath
                        );

                        break;
                    }

                    DataRelativePathRepairDirectoryRecoveryClassification
                        classification =
                            DataRelativePathRepairDirectoryRecoveryClassifier
                                .Classify(
                                    journal,
                                    trustedDataRoot
                                );

                    if (
                        !IsDirectoryRollbackSafe(
                            classification.State
                        ))
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanRollbackOperationExecutionState
                                    .DirectoryRecoveryStateNotRollbackSafe,
                                directoryJournalRead:
                                    read,
                                directoryClassification:
                                    classification,
                                error:
                                    classification.Error ??
                                    $"Directory recovery state " +
                                    $"{classification.State} is not a " +
                                    "safe plan-rollback preflight state."
                            )
                        );
                    }

                    if (
                        classification.State ==
                        DataRelativePathRepairDirectoryRecoveryState
                            .RolledBackBothMissing)
                    {
                        provenMissingDirectoryDestinations.Add(
                            entry.Operation.DestinationPath
                        );
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
                                DataRelativePathRepairPlanRollbackOperationExecutionState
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
                                DataRelativePathRepairPlanRollbackOperationExecutionState
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
                                DataRelativePathRepairPlanRollbackOperationExecutionState
                                    .JournalMismatch,
                                fileJournalRead:
                                    read,
                                error:
                                    bindingError
                            )
                        );
                    }

                    /*
                     * A durable RolledBack file beneath a directly
                     * proven-missing plan directory is necessarily
                     * absent even though its recorded parent can no
                     * longer be opened by the ordinary classifier.
                     */
                    if (
                        journal.State ==
                            DataRelativePathRepairFileJournalState
                                .RolledBack &&
                        IsStrictlyWithinAny(
                            entry.Operation.DestinationPath,
                            provenMissingDirectoryDestinations
                        ))
                    {
                        inferredRolledBackOperationIndexes.Add(
                            index
                        );

                        break;
                    }

                    DataRelativePathRepairFileRecoveryClassification
                        classification =
                            DataRelativePathRepairFileRecoveryClassifier
                                .Classify(
                                    journal,
                                    trustedDataRoot
                                );

                    if (
                        !IsFileRollbackSafe(
                            classification.State
                        ))
                    {
                        return PreflightResult.Failed(
                            OperationResult(
                                entry,
                                DataRelativePathRepairPlanRollbackOperationExecutionState
                                    .FileRecoveryStateNotRollbackSafe,
                                fileJournalRead:
                                    read,
                                fileClassification:
                                    classification,
                                error:
                                    classification.Error ??
                                    $"File recovery state " +
                                    $"{classification.State} is not a " +
                                    "safe plan-rollback preflight state."
                            )
                        );
                    }

                    break;
                }

                default:
                    return PreflightResult.Failed(
                        OperationResult(
                            entry,
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .JournalMismatch,
                            error:
                                $"Unsupported plan operation kind " +
                                $"{entry.Operation.Kind}."
                        )
                    );
            }
        }

        return PreflightResult.Succeeded(
            firstMissingIndex ??
                manifest.Operations.Count,
            inferredRolledBackOperationIndexes
        );
    }

    private static bool IsStrictlyWithinAny(
        string path,
        IReadOnlyList<string> candidateAncestors)
    {
        foreach (
            string ancestor
            in candidateAncestors)
        {
            if (
                IsStrictlyWithin(
                    ancestor,
                    path
                ))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsStrictlyWithin(
        string ancestor,
        string path)
    {
        try
        {
            string normalizedAncestor =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        ancestor
                    )
                );

            string normalizedPath =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        path
                    )
                );

            string relative =
                Path.GetRelativePath(
                    normalizedAncestor,
                    normalizedPath
                );

            if (
                relative == "." ||
                relative == ".." ||
                Path.IsPathFullyQualified(
                    relative
                ))
            {
                return false;
            }

            string parentPrefix =
                ".." +
                Path.DirectorySeparatorChar;

            string alternateParentPrefix =
                ".." +
                Path.AltDirectorySeparatorChar;

            return
                !relative.StartsWith(
                    parentPrefix,
                    StringComparison.Ordinal
                ) &&
                !relative.StartsWith(
                    alternateParentPrefix,
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

    private static bool IsDirectoryRollbackSafe(
        DataRelativePathRepairDirectoryRecoveryState state)
    {
        return state is
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedFinalMatchesStagingMissing or
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMatches or
            DataRelativePathRepairDirectoryRecoveryState
                .RollbackRequestedFinalMissing or
            DataRelativePathRepairDirectoryRecoveryState
                .RollbackRequestedFinalMatches or
            DataRelativePathRepairDirectoryRecoveryState
                .RolledBackBothMissing;
    }

    private static bool IsFileRollbackSafe(
        DataRelativePathRepairFileRecoveryState state)
    {
        return state is
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMatches or
            DataRelativePathRepairFileRecoveryState
                .AppliedDestinationMatches or
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMissing or
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMatches or
            DataRelativePathRepairFileRecoveryState
                .RolledBackDestinationMissing;
    }

    private static
        DataRelativePathRepairPlanRollbackOperationExecution
        ValidateInferredRolledBackOperation(
            LinuxNoFollowPathHandle journalDirectory,
            DataRelativePathRepairPlanManifestRecord manifest,
            DataRelativePathRepairPlanManifestOperation entry,
            string trustedDataRoot)
    {
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

                if (!read.Success)
                {
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .JournalReadFailed,
                        directoryJournalRead:
                            read,
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
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .JournalMismatch,
                        directoryJournalRead:
                            read,
                        error:
                            bindingError
                    );
                }

                if (
                    journal.State !=
                    DataRelativePathRepairDirectoryJournalState
                        .RolledBack)
                {
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .DirectoryRecoveryStateNotRollbackSafe,
                        directoryJournalRead:
                            read,
                        error:
                            $"The descendant journal changed to durable " +
                            $"state {journal.State} after preflight; " +
                            "ancestor-absence terminal proof is refused."
                    );
                }

                return OperationResult(
                    entry,
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .RolledBackDurably,
                    directoryJournalRead:
                        read
                );
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

                if (!read.Success)
                {
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .JournalReadFailed,
                        fileJournalRead:
                            read,
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
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .JournalMismatch,
                        fileJournalRead:
                            read,
                        error:
                            bindingError
                    );
                }

                if (
                    journal.State !=
                    DataRelativePathRepairFileJournalState
                        .RolledBack)
                {
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .FileRecoveryStateNotRollbackSafe,
                        fileJournalRead:
                            read,
                        error:
                            $"The descendant journal changed to durable " +
                            $"state {journal.State} after preflight; " +
                            "ancestor-absence terminal proof is refused."
                    );
                }

                return OperationResult(
                    entry,
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .RolledBackDurably,
                    fileJournalRead:
                        read
                );
            }

            default:
                return OperationResult(
                    entry,
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .JournalMismatch,
                    error:
                        $"Unsupported plan operation kind " +
                        $"{entry.Operation.Kind}."
                );
        }
    }

    private static
        DataRelativePathRepairPlanRollbackOperationExecution
        ExecuteDirectoryOperation(
            LinuxNoFollowPathHandle journalDirectory,
            DataRelativePathRepairPlanManifestOperation entry,
            string trustedDataRoot,
            DateTimeOffset nowUtc)
    {
        DataRelativePathRepairDirectoryJournalReaderResult?
            lastRead =
                null;

        DataRelativePathRepairDirectoryRecoveryClassification?
            classification =
                null;

        DataRelativePathRepairDirectoryRollbackRequest?
            rollbackRequest =
                null;

        DataRelativePathRepairDirectoryRollbackRecovery?
            rollbackRecovery =
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

            if (!read.Success)
            {
                return OperationResult(
                    entry,
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .JournalReadFailed,
                    directoryJournalRead:
                        read,
                    directoryClassification:
                        classification,
                    directoryRollbackRequest:
                        rollbackRequest,
                    directoryRollbackRecovery:
                        rollbackRecovery,
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
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .JournalMismatch,
                    directoryJournalRead:
                        read,
                    directoryClassification:
                        classification,
                    directoryRollbackRequest:
                        rollbackRequest,
                    directoryRollbackRecovery:
                        rollbackRecovery,
                    directoryReconciliation:
                        reconciliation,
                    error:
                        bindingError
                );
            }

            classification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        journal,
                        trustedDataRoot
                    );

            switch (classification.State)
            {
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
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .DirectoryReconciliationFailed,
                            directoryJournalRead:
                                read,
                            directoryClassification:
                                classification,
                            directoryRollbackRequest:
                                rollbackRequest,
                            directoryRollbackRecovery:
                                rollbackRecovery,
                            directoryReconciliation:
                                reconciliation,
                            error:
                                reconciliation.Error ??
                                reconciliation.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .AppliedFinalMatches:
                    rollbackRequest =
                        DataRelativePathRepairDirectoryRollbackRequestAction
                            .Request(
                                journalDirectory,
                                entry.JournalChildName,
                                trustedDataRoot,
                                nowUtc,
                                read.JournalIncarnationIdentity!
                            );

                    if (!rollbackRequest.Success)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .DirectoryRollbackRequestFailed,
                            directoryJournalRead:
                                read,
                            directoryClassification:
                                classification,
                            directoryRollbackRequest:
                                rollbackRequest,
                            directoryRollbackRecovery:
                                rollbackRecovery,
                            directoryReconciliation:
                                reconciliation,
                            error:
                                rollbackRequest.Error ??
                                rollbackRequest.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .RollbackRequestedFinalMatches:
                    rollbackRecovery =
                        DataRelativePathRepairDirectoryRollbackRecoveryAction
                            .Recover(
                                journalDirectory,
                                entry.JournalChildName,
                                trustedDataRoot,
                                nowUtc,
                                read.JournalIncarnationIdentity!
                            );

                    if (!rollbackRecovery.Success)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .DirectoryRollbackRecoveryFailed,
                            directoryJournalRead:
                                read,
                            directoryClassification:
                                classification,
                            directoryRollbackRequest:
                                rollbackRequest,
                            directoryRollbackRecovery:
                                rollbackRecovery,
                            directoryReconciliation:
                                reconciliation,
                            error:
                                rollbackRecovery.Error ??
                                rollbackRecovery.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .RollbackRequestedFinalMissing:
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
                            .RolledBackDurably)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .DirectoryReconciliationFailed,
                            directoryJournalRead:
                                read,
                            directoryClassification:
                                classification,
                            directoryRollbackRequest:
                                rollbackRequest,
                            directoryRollbackRecovery:
                                rollbackRecovery,
                            directoryReconciliation:
                                reconciliation,
                            error:
                                reconciliation.Error ??
                                reconciliation.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairDirectoryRecoveryState
                        .RolledBackBothMissing:
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .RolledBackDurably,
                        directoryJournalRead:
                            read,
                        directoryClassification:
                            classification,
                        directoryRollbackRequest:
                            rollbackRequest,
                        directoryRollbackRecovery:
                            rollbackRecovery,
                        directoryReconciliation:
                            reconciliation
                    );

                default:
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .DirectoryRecoveryStateNotRollbackSafe,
                        directoryJournalRead:
                            read,
                        directoryClassification:
                            classification,
                        directoryRollbackRequest:
                            rollbackRequest,
                        directoryRollbackRecovery:
                            rollbackRecovery,
                        directoryReconciliation:
                            reconciliation,
                        error:
                            classification.Error ??
                            $"Directory recovery state " +
                            $"{classification.State} is not a safe " +
                            "plan-rollback execution state."
                    );
            }
        }

        return OperationResult(
            entry,
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .ProgressLimitExceeded,
            directoryJournalRead:
                lastRead,
            directoryClassification:
                classification,
            directoryRollbackRequest:
                rollbackRequest,
            directoryRollbackRecovery:
                rollbackRecovery,
            directoryReconciliation:
                reconciliation,
            error:
                $"Directory rollback did not converge within " +
                $"{MaxOperationPasses} durable-state passes."
        );
    }

    private static
        DataRelativePathRepairPlanRollbackOperationExecution
        ExecuteFileOperation(
            LinuxNoFollowPathHandle journalDirectory,
            DataRelativePathRepairPlanManifestRecord manifest,
            DataRelativePathRepairPlanManifestOperation entry,
            string trustedDataRoot,
            DateTimeOffset nowUtc)
    {
        DataRelativePathRepairFileJournalReaderResult?
            lastRead =
                null;

        DataRelativePathRepairFileRecoveryClassification?
            classification =
                null;

        DataRelativePathRepairFileRollbackRequest?
            rollbackRequest =
                null;

        DataRelativePathRepairFileRollbackRecovery?
            rollbackRecovery =
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

            if (!read.Success)
            {
                return OperationResult(
                    entry,
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .JournalReadFailed,
                    fileJournalRead:
                        read,
                    fileClassification:
                        classification,
                    fileRollbackRequest:
                        rollbackRequest,
                    fileRollbackRecovery:
                        rollbackRecovery,
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
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .JournalMismatch,
                    fileJournalRead:
                        read,
                    fileClassification:
                        classification,
                    fileRollbackRequest:
                        rollbackRequest,
                    fileRollbackRecovery:
                        rollbackRecovery,
                    fileReconciliation:
                        reconciliation,
                    error:
                        bindingError
                );
            }

            classification =
                DataRelativePathRepairFileRecoveryClassifier
                    .Classify(
                        journal,
                        trustedDataRoot
                    );

            switch (classification.State)
            {
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
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .FileReconciliationFailed,
                            fileJournalRead:
                                read,
                            fileClassification:
                                classification,
                            fileRollbackRequest:
                                rollbackRequest,
                            fileRollbackRecovery:
                                rollbackRecovery,
                            fileReconciliation:
                                reconciliation,
                            error:
                                reconciliation.Error ??
                                reconciliation.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairFileRecoveryState
                        .AppliedDestinationMatches:
                    rollbackRequest =
                        DataRelativePathRepairFileRollbackRequestAction
                            .Request(
                                journalDirectory,
                                entry.JournalChildName,
                                trustedDataRoot,
                                nowUtc,
                                read.JournalIncarnationIdentity!
                            );

                    if (!rollbackRequest.Success)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .FileRollbackRequestFailed,
                            fileJournalRead:
                                read,
                            fileClassification:
                                classification,
                            fileRollbackRequest:
                                rollbackRequest,
                            fileRollbackRecovery:
                                rollbackRecovery,
                            fileReconciliation:
                                reconciliation,
                            error:
                                rollbackRequest.Error ??
                                rollbackRequest.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairFileRecoveryState
                        .RollbackRequestedDestinationMatches:
                    rollbackRecovery =
                        DataRelativePathRepairFileRollbackRecoveryAction
                            .Recover(
                                journalDirectory,
                                entry.JournalChildName,
                                trustedDataRoot,
                                nowUtc,
                                read.JournalIncarnationIdentity!
                            );

                    if (!rollbackRecovery.Success)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .FileRollbackRecoveryFailed,
                            fileJournalRead:
                                read,
                            fileClassification:
                                classification,
                            fileRollbackRequest:
                                rollbackRequest,
                            fileRollbackRecovery:
                                rollbackRecovery,
                            fileReconciliation:
                                reconciliation,
                            error:
                                rollbackRecovery.Error ??
                                rollbackRecovery.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairFileRecoveryState
                        .RollbackRequestedDestinationMissing:
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
                            .RolledBackDurably)
                    {
                        return OperationResult(
                            entry,
                            DataRelativePathRepairPlanRollbackOperationExecutionState
                                .FileReconciliationFailed,
                            fileJournalRead:
                                read,
                            fileClassification:
                                classification,
                            fileRollbackRequest:
                                rollbackRequest,
                            fileRollbackRecovery:
                                rollbackRecovery,
                            fileReconciliation:
                                reconciliation,
                            error:
                                reconciliation.Error ??
                                reconciliation.State.ToString()
                        );
                    }

                    continue;

                case
                    DataRelativePathRepairFileRecoveryState
                        .RolledBackDestinationMissing:
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .RolledBackDurably,
                        fileJournalRead:
                            read,
                        fileClassification:
                            classification,
                        fileRollbackRequest:
                            rollbackRequest,
                        fileRollbackRecovery:
                            rollbackRecovery,
                        fileReconciliation:
                            reconciliation
                    );

                default:
                    return OperationResult(
                        entry,
                        DataRelativePathRepairPlanRollbackOperationExecutionState
                            .FileRecoveryStateNotRollbackSafe,
                        fileJournalRead:
                            read,
                        fileClassification:
                            classification,
                        fileRollbackRequest:
                            rollbackRequest,
                        fileRollbackRecovery:
                            rollbackRecovery,
                        fileReconciliation:
                            reconciliation,
                        error:
                            classification.Error ??
                            $"File recovery state " +
                            $"{classification.State} is not a safe " +
                            "plan-rollback execution state."
                    );
            }
        }

        return OperationResult(
            entry,
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .ProgressLimitExceeded,
            fileJournalRead:
                lastRead,
            fileClassification:
                classification,
            fileRollbackRequest:
                rollbackRequest,
            fileRollbackRecovery:
                rollbackRecovery,
            fileReconciliation:
                reconciliation,
            error:
                $"File rollback did not converge within " +
                $"{MaxOperationPasses} durable-state passes."
        );
    }

    private static
        DataRelativePathRepairPlanRollbackOperationExecution
        OperationResult(
            DataRelativePathRepairPlanManifestOperation entry,
            DataRelativePathRepairPlanRollbackOperationExecutionState
                state,
            DataRelativePathRepairDirectoryJournalReaderResult?
                directoryJournalRead = null,
            DataRelativePathRepairDirectoryRecoveryClassification?
                directoryClassification = null,
            DataRelativePathRepairDirectoryRollbackRequest?
                directoryRollbackRequest = null,
            DataRelativePathRepairDirectoryRollbackRecovery?
                directoryRollbackRecovery = null,
            DataRelativePathRepairDirectoryRecoveryReconciliation?
                directoryReconciliation = null,
            DataRelativePathRepairFileJournalReaderResult?
                fileJournalRead = null,
            DataRelativePathRepairFileRecoveryClassification?
                fileClassification = null,
            DataRelativePathRepairFileRollbackRequest?
                fileRollbackRequest = null,
            DataRelativePathRepairFileRollbackRecovery?
                fileRollbackRecovery = null,
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
            DirectoryJournalRead:
                directoryJournalRead,
            DirectoryClassification:
                directoryClassification,
            DirectoryRollbackRequest:
                directoryRollbackRequest,
            DirectoryRollbackRecovery:
                directoryRollbackRecovery,
            DirectoryReconciliation:
                directoryReconciliation,
            FileJournalRead:
                fileJournalRead,
            FileClassification:
                fileClassification,
            FileRollbackRequest:
                fileRollbackRequest,
            FileRollbackRecovery:
                fileRollbackRecovery,
            FileReconciliation:
                fileReconciliation,
            Error:
                error
        );
    }

    private static DataRelativePathRepairPlanRollbackExecution
        PlanResult(
            DataRelativePathRepairPlanRollbackExecutionState state,
            DataRelativePathRepairPlanManifestReaderResult?
                manifestRead,
            IReadOnlyList<
                DataRelativePathRepairPlanRollbackOperationExecution
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

    private sealed record PreflightResult(
        int ExistingOperationCount,
        IReadOnlySet<int> InferredRolledBackOperationIndexes,
        DataRelativePathRepairPlanRollbackOperationExecution?
            Failure
    )
    {
        public bool Success =>
            Failure is null;

        public static PreflightResult Succeeded(
            int existingOperationCount,
            IReadOnlySet<int> inferredRolledBackOperationIndexes)
        {
            ArgumentNullException.ThrowIfNull(
                inferredRolledBackOperationIndexes
            );

            return new(
                ExistingOperationCount:
                    existingOperationCount,
                InferredRolledBackOperationIndexes:
                    inferredRolledBackOperationIndexes,
                Failure:
                    null
            );
        }

        public static PreflightResult Failed(
            DataRelativePathRepairPlanRollbackOperationExecution failure)
        {
            ArgumentNullException.ThrowIfNull(
                failure
            );

            return new(
                ExistingOperationCount:
                    0,
                InferredRolledBackOperationIndexes:
                    new HashSet<int>(),
                Failure:
                    failure
            );
        }
    }
}
