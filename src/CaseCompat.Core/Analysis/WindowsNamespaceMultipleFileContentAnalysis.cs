using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

/*
 * Targeted, read-only content evidence for one regular-file participant
 * belonging to a Windows-logical MultipleFiles node.
 *
 * StableContentEvidence means:
 *
 * 1. the participant was reacquired from the complete pass-1 namespace
 *    analysis, including exact spelling and generation-aware hierarchy
 *    binding;
 * 2. the same retained readable file descriptor produced stable
 *    before/hash/after content evidence;
 * 3. the participant was reacquired again after hashing, re-observing exact
 *    spelling and rebinding the complete physical hierarchy to pass 1.
 *
 * This remains observational evidence. It provides no write exclusion and
 * makes no provider-precedence or repair-safety claim.
 */
public enum WindowsNamespacePhysicalFileContentEvidenceState
{
    StableContentEvidence,
    InitialReacquisitionFailed,
    ContentObservationFailed,
    PostObservationReacquisitionFailed
}

public sealed record WindowsNamespacePhysicalFileContentEvidence(
    WindowsNamespacePhysicalParticipant Participant,
    WindowsNamespacePhysicalFileContentEvidenceState State,
    WindowsNamespaceFileIncarnationObservation?
        ExpectedIncarnationObservation,
    WindowsNamespacePhysicalFileReacquisitionState
        InitialReacquisitionState,
    LinuxOpenedFileIncarnationResult?
        InitialIncarnation,
    LinuxOpenedFileContentObservationResult?
        ContentObservation,
    WindowsNamespacePhysicalFileReacquisitionState?
        PostObservationReacquisitionState,
    LinuxOpenedFileIncarnationResult?
        PostObservationIncarnation,
    string? FailedComponent,
    string? Error
)
{
    public bool Success =>
        State ==
            WindowsNamespacePhysicalFileContentEvidenceState
                .StableContentEvidence &&
        ExpectedIncarnationObservation is not null &&
        InitialReacquisitionState ==
            WindowsNamespacePhysicalFileReacquisitionState.Reacquired &&
        InitialIncarnation is not null &&
        InitialIncarnation.Success &&
        ContentObservation is not null &&
        ContentObservation.Success &&
        PostObservationReacquisitionState ==
            WindowsNamespacePhysicalFileReacquisitionState.Reacquired &&
        PostObservationIncarnation is not null &&
        PostObservationIncarnation.Success;

    public long? Size =>
        Success
            ? ContentObservation!.Size
            : null;

    public string? Sha256 =>
        Success
            ? ContentObservation!.Sha256
            : null;
}

public sealed record WindowsNamespaceMultipleFileContentNodeAnalysis(
    WindowsLogicalPath LogicalPath,
    IReadOnlyList<WindowsNamespacePhysicalFileContentEvidence> Files
)
{
    public bool Complete =>
        Files.Count >= 2 &&
        Files.All(file => file.Success);
}

public sealed record WindowsNamespaceMultipleFileContentAnalysis(
    IReadOnlyList<WindowsNamespaceMultipleFileContentNodeAnalysis> Nodes,
    IReadOnlyList<string> Errors
)
{
    public bool Complete =>
        Errors.Count == 0 &&
        Nodes.All(node => node.Complete);
}
