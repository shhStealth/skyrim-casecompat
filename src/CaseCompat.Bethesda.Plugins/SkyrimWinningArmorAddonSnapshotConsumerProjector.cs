namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure checkpoint-10E projection.
 *
 * Checkpoint 10D is invoked exactly once per checkpoint-10C path group.
 * The resulting interpretation instance is then shared by every retained
 * winning reference context in that group.
 *
 * No plugin parsing, filesystem access, namespace lookup, loose resolver,
 * provider/archive precedence, hashing, canonical-spelling selection, or
 * repair operation occurs here.
 */
public static class SkyrimWinningArmorAddonSnapshotConsumerProjector
{
    public static
        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult Project(
            SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan)
    {
        ArgumentNullException.ThrowIfNull(
            scan
        );

        if (scan.Paths is null)
        {
            throw new ArgumentException(
                "The checkpoint-10C scan must retain its path collection.",
                nameof(scan)
            );
        }

        var pathInterpretations =
            new List<
                SkyrimArmorAddonSnapshotLoosePathInterpretation
            >(
                scan.Paths.Count
            );

        var consumers =
            new List<
                SkyrimWinningArmorAddonSnapshotConsumerProjection
            >();

        foreach (
            SkyrimWinningArmorAddonSnapshotPathEvidence? path
            in scan.Paths)
        {
            if (path is null)
            {
                throw new ArgumentException(
                    "The checkpoint-10C path collection must not " +
                    "contain null entries.",
                    nameof(scan)
                );
            }

            /*
             * Critical checkpoint-10E boundary:
             * exactly one interpretation object is created for this
             * entire exact requested-path group.
             */
            SkyrimArmorAddonSnapshotLoosePathInterpretation
                interpretation =
                    SkyrimArmorAddonSnapshotLoosePathInterpreter
                        .Interpret(
                            path
                        );

            pathInterpretations.Add(
                interpretation
            );

            if (path.References is null)
            {
                /*
                 * Checkpoint 10D has already represented this malformed
                 * path as IndeterminateEvidence with invalid structure.
                 * The path interpretation remains published above, but
                 * there are no reference contexts that can be projected.
                 */
                continue;
            }

            foreach (
                SkyrimWinningArmorAddonSnapshotReferenceContext? context
                in path.References)
            {
                if (context is null ||
                    context.Reference is null)
                {
                    /*
                     * A null consumer context cannot itself be projected.
                     * The shared checkpoint-10D interpretation still
                     * records that the path evidence was malformed.
                     */
                    continue;
                }

                consumers.Add(
                    new SkyrimWinningArmorAddonSnapshotConsumerProjection(
                        WinningPluginName:
                            context.WinningPluginName,
                        WinningLoadOrderIndex:
                            context.WinningLoadOrderIndex,
                        Reference:
                            context.Reference,
                        PathInterpretation:
                            interpretation
                    )
                );
            }
        }

        return new
            SkyrimWinningArmorAddonSnapshotConsumerProjectionResult(
                Scan:
                    scan,
                PathInterpretations:
                    pathInterpretations.ToArray(),
                Consumers:
                    consumers.ToArray()
            );
    }
}
