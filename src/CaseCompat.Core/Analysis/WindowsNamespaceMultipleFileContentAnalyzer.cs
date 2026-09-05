using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

/*
 * Perform targeted stable-content observation only for Windows-logical nodes
 * whose already-recorded physical topology is MultipleFiles.
 *
 * Single objects, multiple directories, file/directory collisions, and
 * unsupported objects are deliberately not hashed here.
 *
 * The supplied namespace analysis must be complete. No attempt is made to
 * salvage local content evidence from an incomplete pass-1 namespace view.
 */
public static class WindowsNamespaceMultipleFileContentAnalyzer
{
    public static WindowsNamespaceMultipleFileContentAnalysis Analyze(
        WindowsNamespaceAnalysis analysis)
    {
        return AnalyzeCore(
            analysis,
            afterStableContentObservation:
                null
        );
    }

    /*
     * Private deterministic seam used only to prove the namespace race
     * boundary in tests.
     *
     * Production callers always enter through Analyze(...) above, which
     * supplies no callback.
     */
    private static WindowsNamespaceMultipleFileContentAnalysis AnalyzeCore(
        WindowsNamespaceAnalysis analysis,
        Action<WindowsNamespacePhysicalParticipant>?
            afterStableContentObservation)
    {
        ArgumentNullException.ThrowIfNull(
            analysis
        );

        if (
            analysis.Errors is null ||
            analysis.Nodes is null)
        {
            return Failure(
                "The supplied namespace analysis is missing required " +
                "node or error collections."
            );
        }

        if (!analysis.Complete)
        {
            return Failure(
                "Targeted content observation requires a complete " +
                "pass-1 namespace analysis."
            );
        }

        var nodes =
            new List<WindowsNamespaceMultipleFileContentNodeAnalysis>();

        var errors =
            new List<string>();

        foreach (
            WindowsNamespaceNode node
            in analysis.Nodes.OrderBy(
                node =>
                    node.LogicalPath.Value,
                StringComparer.Ordinal
            ))
        {
            if (
                WindowsNamespaceNodeTopologyClassifier.Classify(
                    node
                ) !=
                WindowsNamespaceNodeTopology.MultipleFiles)
            {
                continue;
            }

            WindowsNamespacePhysicalFileContentEvidence[] files =
                node.Participants
                    .OrderBy(
                        participant =>
                            participant.RelativePath,
                        StringComparer.Ordinal
                    )
                    .Select(
                        participant =>
                            WindowsNamespacePhysicalFileContentObserver
                                .ObserveCore(
                                    analysis,
                                    participant,
                                    afterStableContentObservation
                                )
                    )
                    .ToArray();

            foreach (
                WindowsNamespacePhysicalFileContentEvidence file
                in files.Where(file => !file.Success))
            {
                errors.Add(
                    $"{node.LogicalPath.Value}: " +
                    $"{file.Participant.RelativePath}: " +
                    (
                        file.Error ??
                        file.State.ToString()
                    )
                );
            }

            nodes.Add(
                new WindowsNamespaceMultipleFileContentNodeAnalysis(
                    LogicalPath:
                        node.LogicalPath,
                    Files:
                        files
                )
            );
        }

        return new WindowsNamespaceMultipleFileContentAnalysis(
            Nodes:
                nodes.ToArray(),
            Errors:
                errors.ToArray()
        );
    }

    private static WindowsNamespaceMultipleFileContentAnalysis Failure(
        string error)
    {
        return new WindowsNamespaceMultipleFileContentAnalysis(
            Nodes:
                Array.Empty<
                    WindowsNamespaceMultipleFileContentNodeAnalysis
                >(),
            Errors:
                new[]
                {
                    error
                }
        );
    }
}
