namespace CaseCompat.Core.Repair;

public sealed record DataRelativePathRepairBatchManifestChild(
    string ChildName,
    Guid PlanId,
    string ManifestSha256
);

public sealed record DataRelativePathRepairBatchManifestRecord(
    int SchemaVersion,
    Guid BatchId,
    DateTimeOffset CreatedUtc,
    string DataRoot,
    string ChildManifestName,
    int InputPathCount,
    int SafeRejectionCount,
    IReadOnlyList<DataRelativePathRepairBatchManifestChild>
        Children
)
{
    public const int SchemaVersion1 =
        1;

    public const int CurrentSchemaVersion =
        SchemaVersion1;
}

public enum DataRelativePathRepairBatchManifestCreationState
{
    Created,
    InvalidInput
}

public sealed record DataRelativePathRepairBatchManifestCreation(
    DataRelativePathRepairBatchManifestCreationState State,
    DataRelativePathRepairBatchManifestRecord? Manifest,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairBatchManifestCreationState.Created &&
        Manifest is not null;
}

public static class DataRelativePathRepairBatchManifest
{
    public static DataRelativePathRepairBatchManifestCreation
        Create(
            Guid batchId,
            DateTimeOffset createdUtc,
            string dataRoot,
            string childManifestName,
            int inputPathCount,
            int safeRejectionCount,
            IReadOnlyList<
                DataRelativePathRepairBatchManifestChild
            > children)
    {
        ArgumentNullException.ThrowIfNull(
            children
        );

        var manifest =
            new DataRelativePathRepairBatchManifestRecord(
                SchemaVersion:
                    DataRelativePathRepairBatchManifestRecord
                        .CurrentSchemaVersion,
                BatchId:
                    batchId,
                CreatedUtc:
                    createdUtc,
                DataRoot:
                    dataRoot,
                ChildManifestName:
                    childManifestName,
                InputPathCount:
                    inputPathCount,
                SafeRejectionCount:
                    safeRejectionCount,
                Children:
                    children.ToArray()
            );

        string? validationError =
            Validate(
                manifest
            );

        if (validationError is not null)
        {
            return new(
                State:
                    DataRelativePathRepairBatchManifestCreationState
                        .InvalidInput,
                Manifest:
                    null,
                Error:
                    validationError
            );
        }

        return new(
            State:
                DataRelativePathRepairBatchManifestCreationState
                    .Created,
            Manifest:
                manifest,
            Error:
                null
        );
    }

    public static string? Validate(
        DataRelativePathRepairBatchManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        if (
            manifest.SchemaVersion !=
            DataRelativePathRepairBatchManifestRecord
                .SchemaVersion1)
        {
            return
                $"Unsupported batch-manifest schema version " +
                $"{manifest.SchemaVersion}.";
        }

        if (manifest.BatchId == Guid.Empty)
        {
            return
                "The batch manifest requires a non-empty BatchId.";
        }

        if (
            !TryNormalizeAbsolutePath(
                manifest.DataRoot,
                out _))
        {
            return
                "The batch manifest Data root must be an absolute " +
                "valid path.";
        }

        if (
            !IsValidChildName(
                manifest.ChildManifestName))
        {
            return
                "The batch manifest child-manifest name must identify " +
                "exactly one direct child.";
        }

        if (manifest.InputPathCount < 0)
        {
            return
                "The batch manifest input-path count cannot be negative.";
        }

        if (manifest.SafeRejectionCount < 0)
        {
            return
                "The batch manifest safe-rejection count cannot be " +
                "negative.";
        }

        if (manifest.Children is null)
        {
            return
                "The batch manifest requires a child-plan collection.";
        }

        if (
            manifest.SafeRejectionCount >
                manifest.InputPathCount ||
            manifest.Children.Count !=
                manifest.InputPathCount -
                manifest.SafeRejectionCount)
        {
            return
                "The batch manifest counts are inconsistent: input " +
                "paths must equal safe rejections plus child plans.";
        }

        var seenPlanIds =
            new HashSet<Guid>();

        for (
            int index = 0;
            index < manifest.Children.Count;
            index++)
        {
            DataRelativePathRepairBatchManifestChild? child =
                manifest.Children[index];

            if (child is null)
            {
                return
                    $"Batch child {index} is missing.";
            }

            string expectedChildName =
                $"plan-{index + 1:D6}";

            if (
                !string.Equals(
                    child.ChildName,
                    expectedChildName,
                    StringComparison.Ordinal))
            {
                return
                    $"Batch child {index} name does not match the " +
                    $"expected contiguous name {expectedChildName}.";
            }

            if (
                !IsValidChildName(
                    child.ChildName))
            {
                return
                    $"Batch child {index} name must identify exactly " +
                    "one direct child.";
            }

            if (child.PlanId == Guid.Empty)
            {
                return
                    $"Batch child {index} requires a non-empty PlanId.";
            }

            if (!seenPlanIds.Add(child.PlanId))
            {
                return
                    $"Batch child {index} reuses a PlanId.";
            }

            if (
                !IsSha256(
                    child.ManifestSha256))
            {
                return
                    $"Batch child {index} must contain a 64-character " +
                    "manifest SHA-256 value.";
            }
        }

        return null;
    }

    private static bool TryNormalizeAbsolutePath(
        string? path,
        out string normalized)
    {
        normalized =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                path) ||
            !Path.IsPathFullyQualified(
                path))
        {
            return false;
        }

        try
        {
            normalized =
                Path.GetFullPath(
                    path
                );

            return
                Path.IsPathFullyQualified(
                    normalized
                );
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            normalized =
                string.Empty;

            return false;
        }
    }

    private static bool IsValidChildName(
        string? childName)
    {
        if (
            string.IsNullOrEmpty(
                childName) ||
            childName is "." or "..")
        {
            return false;
        }

        return
            !childName.Contains('/') &&
            !childName.Contains('\\') &&
            !childName.Contains('\0');
    }

    private static bool IsSha256(
        string? value)
    {
        if (
            value is null ||
            value.Length != 64)
        {
            return false;
        }

        foreach (
            char character
            in value)
        {
            bool hexadecimal =
                character is >= '0' and <= '9' ||
                character is >= 'A' and <= 'F' ||
                character is >= 'a' and <= 'f';

            if (!hexadecimal)
            {
                return false;
            }
        }

        return true;
    }
}
