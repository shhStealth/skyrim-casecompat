namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure checkpoint-10G-C consumer projection.
 *
 * The join between checkpoint-10F consumers and checkpoint-10G-B path
 * archive evidence is by retained PathInterpretation object identity.
 *
 * RequestedPath is never reparsed or used as a reconstructed join key.
 *
 * This projector performs:
 *   - no archive index lookup,
 *   - no runtime archive precedence resolution,
 *   - no loose-path resolution,
 *   - no namespace lookup,
 *   - no final provider classification,
 *   - no filesystem access,
 *   - no repair decision.
 */
public static class
    SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
{
    public static
        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult
        Project(
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult
                pathArchiveResult)
    {
        ArgumentNullException.ThrowIfNull(
            pathArchiveResult
        );

        if (pathArchiveResult.Diagnostics is null)
        {
            throw new ArgumentException(
                "The checkpoint-10G-B result must retain its " +
                "checkpoint-10F diagnostics.",
                nameof(pathArchiveResult)
            );
        }

        if (pathArchiveResult.Diagnostics.Diagnostics is null)
        {
            throw new ArgumentException(
                "The checkpoint-10F result must retain its diagnostic " +
                "collection.",
                nameof(pathArchiveResult)
            );
        }

        if (pathArchiveResult.Paths is null)
        {
            throw new ArgumentException(
                "The checkpoint-10G-B result must retain its path archive " +
                "evidence collection.",
                nameof(pathArchiveResult)
            );
        }

        var diagnosticStatesByInterpretation =
            new Dictionary<
                SkyrimArmorAddonSnapshotLoosePathInterpretation,
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
            >(
                ReferenceEqualityComparer.Instance
            );

        foreach (
            SkyrimWinningArmorAddonSnapshotConsumerDiagnostic? diagnostic
            in pathArchiveResult.Diagnostics.Diagnostics)
        {
            if (diagnostic is null)
            {
                throw new ArgumentException(
                    "The checkpoint-10F diagnostic collection must not " +
                    "contain null entries.",
                    nameof(pathArchiveResult)
                );
            }

            if (diagnostic.Consumer is null)
            {
                throw new ArgumentException(
                    "Each checkpoint-10F diagnostic must retain its " +
                    "checkpoint-10E consumer.",
                    nameof(pathArchiveResult)
                );
            }

            SkyrimArmorAddonSnapshotLoosePathInterpretation?
                interpretation =
                    diagnostic.Consumer.PathInterpretation;

            if (interpretation is null)
            {
                throw new ArgumentException(
                    "Each checkpoint-10E consumer must retain its " +
                    "checkpoint-10D path interpretation.",
                    nameof(pathArchiveResult)
                );
            }

            if (
                diagnosticStatesByInterpretation.TryGetValue(
                    interpretation,
                    out SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                        existingState) &&
                existingState != diagnostic.State)
            {
                throw new ArgumentException(
                    "Consumers sharing one checkpoint-10D path " +
                    "interpretation must not carry conflicting " +
                    "checkpoint-10F diagnostic states.",
                    nameof(pathArchiveResult)
                );
            }

            diagnosticStatesByInterpretation[
                interpretation
            ] =
                diagnostic.State;
        }

        var archiveEvidenceByInterpretation =
            new Dictionary<
                SkyrimArmorAddonSnapshotLoosePathInterpretation,
                SkyrimWinningArmorAddonSnapshotPathArchiveEvidence
            >(
                ReferenceEqualityComparer.Instance
            );

        foreach (
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidence?
                pathEvidence
            in pathArchiveResult.Paths)
        {
            if (pathEvidence is null)
            {
                throw new ArgumentException(
                    "The checkpoint-10G-B path archive evidence " +
                    "collection must not contain null entries.",
                    nameof(pathArchiveResult)
                );
            }

            if (pathEvidence.PathInterpretation is null)
            {
                throw new ArgumentException(
                    "Each checkpoint-10G-B path evidence entry must " +
                    "retain its checkpoint-10D interpretation.",
                    nameof(pathArchiveResult)
                );
            }

            SkyrimArmorAddonSnapshotLoosePathInterpretation
                interpretation =
                    pathEvidence.PathInterpretation;

            if (
                !diagnosticStatesByInterpretation.TryGetValue(
                    interpretation,
                    out SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                        diagnosticState))
            {
                throw new ArgumentException(
                    "Checkpoint-10G-B path archive evidence does not " +
                    "correspond by object identity to any retained " +
                    "checkpoint-10F consumer path interpretation.",
                    nameof(pathArchiveResult)
                );
            }

            if (
                diagnosticState !=
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved)
            {
                throw new ArgumentException(
                    "Checkpoint-10G-B path archive evidence may only " +
                    "correspond to checkpoint-10F LooseUnresolved " +
                    "consumers.",
                    nameof(pathArchiveResult)
                );
            }

            if (
                !archiveEvidenceByInterpretation.TryAdd(
                    interpretation,
                    pathEvidence))
            {
                throw new ArgumentException(
                    "Multiple checkpoint-10G-B path archive evidence " +
                    "entries retain the same checkpoint-10D " +
                    "interpretation object.",
                    nameof(pathArchiveResult)
                );
            }
        }

        var consumers =
            new List<
                SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection
            >(
                pathArchiveResult.Diagnostics.Diagnostics.Count
            );

        foreach (
            SkyrimWinningArmorAddonSnapshotConsumerDiagnostic diagnostic
            in pathArchiveResult.Diagnostics.Diagnostics)
        {
            SkyrimArmorAddonSnapshotLoosePathInterpretation
                interpretation =
                    diagnostic.PathInterpretation;

            bool hasArchiveEvidence =
                archiveEvidenceByInterpretation.TryGetValue(
                    interpretation,
                    out SkyrimWinningArmorAddonSnapshotPathArchiveEvidence?
                        pathEvidence
                );

            if (
                diagnostic.State ==
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved)
            {
                if (!hasArchiveEvidence)
                {
                    throw new ArgumentException(
                        "A checkpoint-10F LooseUnresolved consumer is " +
                        "missing its checkpoint-10G-B path archive " +
                        "evidence.",
                        nameof(pathArchiveResult)
                    );
                }
            }
            else if (hasArchiveEvidence)
            {
                throw new ArgumentException(
                    "A checkpoint-10F consumer whose state is not " +
                    "LooseUnresolved unexpectedly has checkpoint-10G-B " +
                    "path archive evidence.",
                    nameof(pathArchiveResult)
                );
            }

            consumers.Add(
                new SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection(
                    Diagnostic:
                        diagnostic,
                    PathArchiveEvidence:
                        pathEvidence
                )
            );
        }

        return new
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult(
                PathArchiveResult:
                    pathArchiveResult,
                Consumers:
                    consumers.ToArray()
            );
    }
}
