namespace CaseCompat.Filesystem.Linux;

public sealed record DirectoryCasefoldResult(
    string FullPath,
    bool Exists,
    bool? CasefoldEnabled,
    long? RawFlags,
    string? Error
);
