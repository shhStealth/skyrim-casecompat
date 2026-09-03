namespace CaseCompat.Core.Analysis;

/*
 * Pure interpretation of already-published checkpoint-9A content evidence.
 *
 * These states describe only whether the physical regular-file participants
 * of one Windows-logical MultipleFiles node have equal observed contents.
 *
 * They do not identify:
 *
 * - provider precedence;
 * - canonical spelling;
 * - a winning participant;
 * - reconciliation eligibility;
 * - repair safety.
 */
public enum WindowsNamespaceMultipleFileContentInterpretationState
{
    IdenticalContent,
    DivergentContent,
    IndeterminateEvidence
}

public sealed record WindowsNamespaceMultipleFileContentNodeInterpretation(
    WindowsNamespaceMultipleFileContentNodeAnalysis ContentEvidence,
    WindowsNamespaceMultipleFileContentInterpretationState State,
    string? Error
)
{
    public WindowsLogicalPath LogicalPath =>
        ContentEvidence.LogicalPath;

    public bool Determinate =>
        State !=
            WindowsNamespaceMultipleFileContentInterpretationState
                .IndeterminateEvidence;
}

public sealed record WindowsNamespaceMultipleFileContentInterpretation(
    WindowsNamespaceMultipleFileContentAnalysis ContentAnalysis,
    IReadOnlyList<WindowsNamespaceMultipleFileContentNodeInterpretation>
        Nodes,
    IReadOnlyList<string> Errors
)
{
    public int IdenticalContentNodes =>
        Nodes.Count(
            node =>
                node.State ==
                WindowsNamespaceMultipleFileContentInterpretationState
                    .IdenticalContent
        );

    public int DivergentContentNodes =>
        Nodes.Count(
            node =>
                node.State ==
                WindowsNamespaceMultipleFileContentInterpretationState
                    .DivergentContent
        );

    public int IndeterminateEvidenceNodes =>
        Nodes.Count(
            node =>
                node.State ==
                WindowsNamespaceMultipleFileContentInterpretationState
                    .IndeterminateEvidence
        );

    /*
     * A divergent result is still complete: the content difference was
     * successfully observed.
     *
     * Complete becomes false when checkpoint 9A was incomplete or when any
     * node cannot be interpreted from successfully published stable evidence.
     */
    public bool Complete =>
        Errors.Count == 0 &&
        ContentAnalysis.Errors is not null &&
        ContentAnalysis.Errors.Count == 0 &&
        ContentAnalysis.Nodes is not null &&
        Nodes.Count ==
            ContentAnalysis.Nodes.Count &&
        IndeterminateEvidenceNodes == 0;
}
