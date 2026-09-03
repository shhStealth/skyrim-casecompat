namespace CaseCompat.Bethesda.Plugins;

/*
 * Checkpoint 10G-B in-memory archive evidence composition.
 *
 * The existing SkyrimArchiveCandidateIndexResult is reused directly as
 * Windows-logical archive-key authority. Its TryGetProviders method
 * converts the requested Data-relative spelling through the same
 * WindowsLogicalPath factory used when BSA internal entries are indexed.
 *
 * The existing SkyrimRuntimeArchivePrecedenceResolver is also reused
 * directly. It consumes only archive providers and already-produced
 * runtime archive evidence.
 *
 * No loose-path resolver, filesystem scan, plugin parsing, namespace
 * lookup, canonical-spelling choice, final provider diagnostic, or
 * repair decision occurs here.
 */
public static class
    SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
{
    public static
        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult
        Compose(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult
                diagnostics,
            SkyrimArchiveCandidateIndexResult archiveIndex,
            SkyrimRuntimeArchiveEvidenceResult runtimeArchiveEvidence)
    {
        ArgumentNullException.ThrowIfNull(
            diagnostics
        );

        ArgumentNullException.ThrowIfNull(
            archiveIndex
        );

        ArgumentNullException.ThrowIfNull(
            runtimeArchiveEvidence
        );

        if (diagnostics.Projection is null)
        {
            throw new ArgumentException(
                "The checkpoint-10F result must retain its " +
                "checkpoint-10E projection.",
                nameof(diagnostics)
            );
        }

        if (diagnostics.Projection.PathInterpretations is null)
        {
            throw new ArgumentException(
                "The checkpoint-10E projection must retain its path " +
                "interpretation collection.",
                nameof(diagnostics)
            );
        }

        EnsureSameDataRoot(
            diagnostics,
            archiveIndex,
            runtimeArchiveEvidence
        );

        /*
         * Checkpoint-10F precedence remains authoritative. When winner
         * discovery is incomplete, no path is archive-eligible.
         *
         * We intentionally avoid even constructing the runtime precedence
         * resolver in this branch.
         */
        if (!diagnostics.WinnerSearchComplete)
        {
            return new
                SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult(
                    Diagnostics:
                        diagnostics,
                    ArchiveIndex:
                        archiveIndex,
                    RuntimeArchiveEvidence:
                        runtimeArchiveEvidence,
                    Paths:
                        Array.Empty<
                            SkyrimWinningArmorAddonSnapshotPathArchiveEvidence
                        >()
                );
        }

        var precedenceResolver =
            new SkyrimRuntimeArchivePrecedenceResolver(
                runtimeArchiveEvidence
            );

        var paths =
            new List<
                SkyrimWinningArmorAddonSnapshotPathArchiveEvidence
            >();

        foreach (
            SkyrimArmorAddonSnapshotLoosePathInterpretation?
                interpretation
            in diagnostics.Projection.PathInterpretations)
        {
            if (interpretation is null)
            {
                throw new ArgumentException(
                    "The checkpoint-10E path interpretation collection " +
                    "must not contain null entries.",
                    nameof(diagnostics)
                );
            }

            if (!IsArchiveEligible(
                    interpretation))
            {
                continue;
            }

            /*
             * Exactly one archive lookup is performed for this already
             * deduplicated checkpoint-10E path interpretation.
             */
            archiveIndex.TryGetProviders(
                interpretation.Evidence.RequestedPath,
                out IReadOnlyList<SkyrimArchiveAssetProvider>
                    archiveCandidates
            );

            SkyrimRuntimeArchivePrecedenceDecision
                archivePrecedence =
                    precedenceResolver.Resolve(
                        archiveCandidates
                    );

            paths.Add(
                new SkyrimWinningArmorAddonSnapshotPathArchiveEvidence(
                    PathInterpretation:
                        interpretation,
                    ArchiveCandidates:
                        archiveCandidates,
                    ArchivePrecedence:
                        archivePrecedence
                )
            );
        }

        return new
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult(
                Diagnostics:
                    diagnostics,
                ArchiveIndex:
                    archiveIndex,
                RuntimeArchiveEvidence:
                    runtimeArchiveEvidence,
                Paths:
                    paths.ToArray()
            );
    }

    private static bool IsArchiveEligible(
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation)
    {
        return
            interpretation.Evidence is not null &&
            interpretation.EvidenceStructureValid &&
            interpretation.InterpretationError is null &&
            Enum.IsDefined(
                typeof(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                ),
                interpretation.State
            ) &&
            interpretation.State ==
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved;
    }

    private static void EnsureSameDataRoot(
        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult diagnostics,
        SkyrimArchiveCandidateIndexResult archiveIndex,
        SkyrimRuntimeArchiveEvidenceResult runtimeArchiveEvidence)
    {
        string diagnosticDataRoot =
            Path.GetFullPath(
                diagnostics
                    .Projection
                    .Scan
                    .Inventory
                    .DataRoot
            );

        string archiveDataRoot =
            Path.GetFullPath(
                archiveIndex.DataRoot
            );

        string runtimeDataRoot =
            Path.GetFullPath(
                runtimeArchiveEvidence.DataRoot
            );

        if (!string.Equals(
                diagnosticDataRoot,
                archiveDataRoot,
                StringComparison.Ordinal) ||
            !string.Equals(
                diagnosticDataRoot,
                runtimeDataRoot,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Checkpoint-10F diagnostics, archive index, and runtime " +
                "archive evidence must refer to the same Data root."
            );
        }
    }
}
