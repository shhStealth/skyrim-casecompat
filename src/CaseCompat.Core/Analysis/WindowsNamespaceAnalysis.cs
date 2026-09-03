namespace CaseCompat.Core.Analysis;

public enum WindowsNamespacePhysicalObjectKind
{
    Directory,
    File,
    SymbolicLink,
    Other
}

public sealed record WindowsNamespacePhysicalParticipant(
    string FullPath,
    string RelativePath,
    string Name,
    WindowsNamespacePhysicalObjectKind Kind,
    uint? DeviceMajor,
    uint? DeviceMinor,
    ulong? Inode,
    ulong? MountId,
    string? IdentityError
);

/*
 * Descriptor-bound lookup semantics for one physical directory.
 *
 * RelativePath "." represents the Data root itself.
 *
 * CasefoldEnabled and RawFlags are null when directory flag evidence
 * could not be obtained. In that case Error explains why the overall
 * namespace analysis is incomplete.
 */
public sealed record WindowsNamespaceDirectoryLookupObservation(
    string FullPath,
    string RelativePath,
    bool? CasefoldEnabled,
    long? RawFlags,
    string? Error
);

/*
 * Descriptor-bound incarnation evidence for one regular-file participant.
 *
 * InodeGeneration is published only after the safely opened descriptor's
 * device, inode, and mount identity has been matched back to the participant
 * observed by descriptor-relative statx(AT_SYMLINK_NOFOLLOW).
 *
 * A null generation means complete incarnation evidence was unavailable.
 */
public sealed record WindowsNamespaceFileIncarnationObservation(
    string FullPath,
    string RelativePath,
    uint? InodeGeneration,
    string? Error
);

public sealed record WindowsNamespaceNode(
    WindowsLogicalPath LogicalPath,
    IReadOnlyList<WindowsNamespacePhysicalParticipant> Participants
)
{
    public bool HasMultiplePhysicalObjects =>
        Participants.Count > 1;

    public bool HasSpellingSplit =>
        Participants
            .Select(participant => participant.Name)
            .Distinct(StringComparer.Ordinal)
            .Count() > 1;

    public bool HasFileDirectoryCollision =>
        Participants.Any(participant =>
            participant.Kind ==
                WindowsNamespacePhysicalObjectKind.File) &&
        Participants.Any(participant =>
            participant.Kind ==
                WindowsNamespacePhysicalObjectKind.Directory);
}

public sealed record WindowsNamespaceAnalysis(
    string DataRootPath,
    WindowsLogicalPath RootLogicalPath,
    IReadOnlyList<WindowsNamespaceDirectoryLookupObservation>
        DirectoryLookupObservations,
    IReadOnlyList<WindowsNamespaceFileIncarnationObservation>
        FileIncarnationObservations,
    IReadOnlyList<WindowsNamespaceNode> Nodes,
    IReadOnlyList<string> Errors
)
{
    public bool Complete =>
        Errors.Count == 0;
}
