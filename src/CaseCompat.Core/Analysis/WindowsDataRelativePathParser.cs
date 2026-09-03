namespace CaseCompat.Core.Analysis;

/*
 * Pure validation and component parsing for one requested
 * Data-relative Windows-style file path.
 *
 * This type performs no filesystem access and does not infer:
 *
 * - physical spelling;
 * - lookup success;
 * - provider precedence;
 * - canonical spelling;
 * - repair eligibility.
 *
 * Separator normalization exists only to identify path components.
 * Component spelling itself is preserved exactly.
 */
public static class WindowsDataRelativePathParser
{
    public static bool TryParse(
        string? requestedRelativePath,
        out string[] components,
        out string? error)
    {
        components =
            Array.Empty<string>();

        error =
            null;

        if (string.IsNullOrWhiteSpace(
                requestedRelativePath))
        {
            error =
                "The requested Data-relative file path is empty.";

            return false;
        }

        if (requestedRelativePath.IndexOf('\0') >= 0)
        {
            error =
                "The requested path contains a NUL character.";

            return false;
        }

        string normalized =
            requestedRelativePath.Replace(
                '\\',
                '/'
            );

        if (
            normalized.StartsWith(
                "/",
                StringComparison.Ordinal
            ) ||
            normalized.EndsWith(
                "/",
                StringComparison.Ordinal
            ))
        {
            error =
                "The requested path must be Data-relative and must not " +
                "start or end with a directory separator.";

            return false;
        }

        components =
            normalized.Split(
                '/',
                StringSplitOptions.None
            );

        if (
            components.Length == 0 ||
            components.Any(
                component =>
                    component.Length == 0
            ))
        {
            error =
                "The requested path contains an empty component.";

            components =
                Array.Empty<string>();

            return false;
        }

        if (components.Any(
                component =>
                    string.Equals(
                        component,
                        ".",
                        StringComparison.Ordinal
                    ) ||
                    string.Equals(
                        component,
                        "..",
                        StringComparison.Ordinal
                    )))
        {
            error =
                "The requested path contains a traversal component.";

            components =
                Array.Empty<string>();

            return false;
        }

        return true;
    }
}
