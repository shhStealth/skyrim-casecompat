namespace CaseCompat.Core.Repair;

internal static class
    DataRelativePathRepairRecoveryDataRootAuthority
{
    public static bool Matches(
        string trustedDataRoot,
        string journalDataRoot,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(trustedDataRoot))
        {
            throw new ArgumentException(
                "A trusted Data root is required for recovery.",
                nameof(trustedDataRoot)
            );
        }

        /*
         * Recovery authority must not depend on the process current
         * directory. Require the caller to identify the authorized
         * Data tree with an absolute path.
         */
        if (!Path.IsPathFullyQualified(trustedDataRoot))
        {
            throw new ArgumentException(
                "The trusted recovery Data root must be an "
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
                    journalDataRoot
                );
        }
        catch (Exception ex)
        {
            error =
                "The durable journal Data root is invalid: "
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
                "The durable journal Data root does not match the "
                + "caller-supplied trusted recovery Data root.";

            return false;
        }

        error = null;

        return true;
    }
}
