namespace CaseCompat.Core.Analysis;

public readonly record struct WindowsLogicalPath(
    string Value
)
{
    public static WindowsLogicalPath FromRelativePath(
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            relativePath
        );

        string normalized = relativePath
            .Replace('\\', '/')
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            )
            .Select(component =>
                component.ToUpperInvariant())
            .Aggregate(
                (left, right) =>
                    $"{left}/{right}"
            );

        return new WindowsLogicalPath(normalized);
    }

    public override string ToString()
    {
        return Value;
    }
}
