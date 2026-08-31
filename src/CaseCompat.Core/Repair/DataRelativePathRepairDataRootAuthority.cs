namespace CaseCompat.Core.Repair;

internal static class
    DataRelativePathRepairDataRootAuthority
{
    public static bool Matches(
        string trustedDataRoot,
        string recordedDataRoot,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(trustedDataRoot))
        {
            throw new ArgumentException(
                "A trusted Data root is required for repair authority.",
                nameof(trustedDataRoot)
            );
        }

        /*
         * Filesystem authority must not depend on the process current
         * directory. Require the caller to identify the authorized
         * Data tree with an absolute path.
         */
        if (!Path.IsPathFullyQualified(trustedDataRoot))
        {
            throw new ArgumentException(
                "The trusted Data root must be an "
                + "absolute path.",
                nameof(trustedDataRoot)
            );
        }

        string trusted =
            Path.GetFullPath(
                trustedDataRoot
            );

        string recorded;

        try
        {
            recorded =
                Path.GetFullPath(
                    recordedDataRoot
                );
        }
        catch (Exception ex)
        {
            error =
                "The recorded Data root is invalid: "
                + ex.Message;

            return false;
        }

        if (
            !string.Equals(
                trusted,
                recorded,
                StringComparison.Ordinal
            ))
        {
            error =
                "The recorded Data root does not match the "
                + "caller-supplied trusted Data root.";

            return false;
        }

        error = null;

        return true;
    }
}
