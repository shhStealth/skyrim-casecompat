namespace CaseCompat.Filesystem.Linux;

public static class DirectoryProbe
{
    public static DirectoryProbeResult Inspect(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A directory path is required.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);

        return new DirectoryProbeResult(
            RequestedPath: path,
            FullPath: fullPath,
            Exists: Directory.Exists(fullPath),
            IsLinux: OperatingSystem.IsLinux()
        );
    }
}
