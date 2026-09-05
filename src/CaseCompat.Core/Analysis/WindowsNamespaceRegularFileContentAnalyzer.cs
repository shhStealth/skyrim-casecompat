namespace CaseCompat.Core.Analysis;

/*
 * Observe stable content for every Windows-logical regular-file leaf already
 * discovered by a complete WindowsNamespaceAnalysis.
 *
 * Supported file-leaf topologies:
 *
 * - SinglePhysicalObject when its sole participant is a regular file;
 * - MultipleFiles.
 *
 * Directory-only nodes are skipped because they are not file leaves.
 *
 * File/directory collisions, unsupported objects, and nodes with no physical
 * participants are recorded as errors and are not hashed. They therefore
 * prevent Complete from becoming true.
 *
 * This analyzer performs no source selection, provider-precedence decision,
 * persistence, repair planning, or execution.
 */
public static class WindowsNamespaceRegularFileContentAnalyzer
{
    public static WindowsNamespaceRegularFileContentAnalysis Analyze(
        WindowsNamespaceAnalysis analysis)
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

        /*
         * Preflight malformed in-memory shapes before beginning any content
         * observation. The public record types are non-nullable, but callers
         * can still construct invalid runtime collections manually.
         */
        for (
            int nodeIndex = 0;
            nodeIndex < analysis.Nodes.Count;
            nodeIndex++)
        {
            WindowsNamespaceNode? node =
                analysis.Nodes[nodeIndex];

            if (node is null)
            {
                return Failure(
                    $"Namespace node at index {nodeIndex} is null."
                );
            }

            if (node.Participants is null)
            {
                return Failure(
                    $"{node.LogicalPath.Value}: participant collection " +
                    "is null."
                );
            }

            for (
                int participantIndex = 0;
                participantIndex < node.Participants.Count;
                participantIndex++)
            {
                WindowsNamespacePhysicalParticipant? participant =
                    node.Participants[participantIndex];

                if (participant is null)
                {
                    return Failure(
                        $"{node.LogicalPath.Value}: physical participant " +
                        $"at index {participantIndex} is null."
                    );
                }
            }
        }

        if (!analysis.Complete)
        {
            return Failure(
                "All-regular-file content observation requires a complete " +
                "pass-1 namespace analysis."
            );
        }

        var nodes =
            new List<WindowsNamespaceRegularFileContentNodeAnalysis>();

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
            WindowsNamespaceNodeTopology topology =
                WindowsNamespaceNodeTopologyClassifier.Classify(
                    node
                );

            switch (topology)
            {
                case WindowsNamespaceNodeTopology.SinglePhysicalObject:
                {
                    WindowsNamespacePhysicalParticipant participant =
                        node.Participants[0];

                    if (
                        participant.Kind ==
                        WindowsNamespacePhysicalObjectKind.Directory)
                    {
                        continue;
                    }

                    if (
                        participant.Kind !=
                        WindowsNamespacePhysicalObjectKind.File)
                    {
                        errors.Add(
                            $"{node.LogicalPath.Value}: topology " +
                            $"{topology} does not contain a supported " +
                            "regular-file participant."
                        );

                        continue;
                    }

                    ObserveFileNode(
                        analysis,
                        node,
                        topology,
                        node.Participants,
                        nodes,
                        errors
                    );

                    break;
                }

                case WindowsNamespaceNodeTopology.MultipleFiles:
                {
                    ObserveFileNode(
                        analysis,
                        node,
                        topology,
                        node.Participants,
                        nodes,
                        errors
                    );

                    break;
                }

                case WindowsNamespaceNodeTopology.MultipleDirectories:
                {
                    continue;
                }

                case WindowsNamespaceNodeTopology.FileDirectoryCollision:
                case WindowsNamespaceNodeTopology.UnsupportedObject:
                case WindowsNamespaceNodeTopology.NoPhysicalParticipants:
                {
                    errors.Add(
                        $"{node.LogicalPath.Value}: topology {topology} " +
                        "cannot be represented as a Windows-logical " +
                        "regular-file leaf."
                    );

                    break;
                }

                default:
                {
                    errors.Add(
                        $"{node.LogicalPath.Value}: unrecognized namespace " +
                        $"topology value {topology}."
                    );

                    break;
                }
            }
        }

        return new WindowsNamespaceRegularFileContentAnalysis(
            Nodes:
                nodes.ToArray(),
            Errors:
                errors.ToArray()
        );
    }

    private static void ObserveFileNode(
        WindowsNamespaceAnalysis analysis,
        WindowsNamespaceNode node,
        WindowsNamespaceNodeTopology topology,
        IReadOnlyList<WindowsNamespacePhysicalParticipant> participants,
        List<WindowsNamespaceRegularFileContentNodeAnalysis> nodes,
        List<string> errors)
    {
        WindowsNamespacePhysicalFileContentEvidence[] files =
            participants
                .OrderBy(
                    participant =>
                        participant.RelativePath,
                    StringComparer.Ordinal
                )
                .Select(
                    participant =>
                        WindowsNamespacePhysicalFileContentObserver.Observe(
                            analysis,
                            participant
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
            new WindowsNamespaceRegularFileContentNodeAnalysis(
                LogicalPath:
                    node.LogicalPath,
                Topology:
                    topology,
                Files:
                    files
            )
        );
    }

    private static WindowsNamespaceRegularFileContentAnalysis Failure(
        string error)
    {
        return new WindowsNamespaceRegularFileContentAnalysis(
            Nodes:
                Array.Empty<
                    WindowsNamespaceRegularFileContentNodeAnalysis
                >(),
            Errors:
                new[]
                {
                    error
                }
        );
    }
}
