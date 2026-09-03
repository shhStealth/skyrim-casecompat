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
    IReadOnlyList<WindowsNamespaceNode> Nodes,
    IReadOnlyList<string> Errors
)
{
    public bool Complete =>
        Errors.Count == 0;
}
