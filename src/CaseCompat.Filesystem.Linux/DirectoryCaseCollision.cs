namespace CaseCompat.Filesystem.Linux;

public sealed record DirectoryCollisionMember(
    string Name,
    string FullPath,
    bool IsDirectory,
    bool IsSymbolicLink
);

public sealed record DirectoryCaseCollision(
    string ParentPath,
    string LogicalName,
    IReadOnlyList<DirectoryCollisionMember> Members
);
