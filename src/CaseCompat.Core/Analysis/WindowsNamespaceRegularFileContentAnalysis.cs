namespace CaseCompat.Core.Analysis;

/*
 * Stable, read-only content evidence for one Windows-logical regular-file
 * leaf discovered by a complete WindowsNamespaceAnalysis.
 *
 * Topology is retained explicitly:
 *
 * - SinglePhysicalObject means exactly one regular-file representation;
 * - MultipleFiles means two or more regular-file representations.
 *
 * Directory-only nodes are outside this result. File/directory collisions,
 * unsupported objects, and malformed empty nodes cannot be represented as
 * regular-file leaves and therefore make the containing analysis incomplete.
 *
 * This is observational evidence only. It grants no repair, persistence,
 * source-selection, provider-precedence, reconciliation, or execution
 * authority.
 */
public sealed record WindowsNamespaceRegularFileContentNodeAnalysis(
    WindowsLogicalPath LogicalPath,
    WindowsNamespaceNodeTopology Topology,
    IReadOnlyList<WindowsNamespacePhysicalFileContentEvidence> Files
)
{
    public bool Complete =>
        Files is not null &&
        (
            Topology switch
            {
                WindowsNamespaceNodeTopology.SinglePhysicalObject =>
                    Files.Count == 1 &&
                    Files.All(
                        file =>
                            file is not null &&
                            file.Success
                    ),

                WindowsNamespaceNodeTopology.MultipleFiles =>
                    Files.Count >= 2 &&
                    Files.All(
                        file =>
                            file is not null &&
                            file.Success
                    ),

                _ =>
                    false
            }
        );
}

/*
 * Complete stable-content evidence for every Windows-logical regular-file
 * leaf represented by a complete namespace analysis.
 *
 * Directory-only nodes are deliberately omitted. Errors record namespace
 * topologies that cannot safely be interpreted as regular-file leaves and
 * any participant whose stable content observation failed.
 */
public sealed record WindowsNamespaceRegularFileContentAnalysis(
    IReadOnlyList<WindowsNamespaceRegularFileContentNodeAnalysis> Nodes,
    IReadOnlyList<string> Errors
)
{
    public bool Complete =>
        Nodes is not null &&
        Errors is not null &&
        Errors.Count == 0 &&
        Nodes.All(
            node =>
                node is not null &&
                node.Complete
        );
}
