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
    /*
     * Schema v1 predates aggregate namespace-coverage authorization.
     *
     * Keep the marker nullable and omit it from JSON while null so
     * existing schema-v1 batch manifests retain their exact legacy shape.
     */
    [System.Text.Json.Serialization.JsonIgnore(
        Condition =
            System.Text.Json.Serialization.JsonIgnoreCondition
                .WhenWritingNull)]
    public int? CoveragePolicyVersion
    {
        get;
        init;
    }

    public const int SchemaVersion1 =
        1;

    public const int SchemaVersion2 =
        2;

    /*
     * Schema v3 represents a batch whose complete alternate physical
     * source namespace was authorized under coverage-policy version 2.
     *
     * It does not become the default merely by being representable.
     */
    public const int SchemaVersion3 =
        3;

    public const int CoveragePolicyVersion1 =
        1;

    /*
     * Coverage policy v2 is reserved for aggregate alternate-branch
     * schema-v4 child plans whose requested destination hierarchy begins
     * missing before the leaf.
     */
    public const int CoveragePolicyVersion2 =
        2;

    public const int CurrentSchemaVersion =
        SchemaVersion2;
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
                        .SchemaVersion1,
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

    /*
     * Create a durable batch completion record that proves the batch was
     * completed under aggregate namespace-coverage policy version 1.
     *
     * This factory does not itself perform coverage authorization. Its
     * caller must already have completed that authorization before using
     * this narrowly named persistence format.
     *
     * Legacy Create(...) deliberately remains schema v1.
     */
    public static DataRelativePathRepairBatchManifestCreation
        CreateCoverageAuthorized(
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
        DataRelativePathRepairBatchManifestCreation legacy =
            Create(
                batchId,
                createdUtc,
                dataRoot,
                childManifestName,
                inputPathCount,
                safeRejectionCount,
                children
            );

        if (
            !legacy.Success ||
            legacy.Manifest is null)
        {
            return legacy;
        }

        DataRelativePathRepairBatchManifestRecord manifest =
            legacy.Manifest with
            {
                SchemaVersion =
                    DataRelativePathRepairBatchManifestRecord
                        .SchemaVersion2,
                CoveragePolicyVersion =
                    DataRelativePathRepairBatchManifestRecord
                        .CoveragePolicyVersion1
            };

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

    /*
     * Persist a future aggregate alternate-branch batch completion record
     * as schema-v3 / coverage-policy-v2.
     *
     * This factory performs no namespace coverage proof and grants no
     * execution authority. Its caller must already possess the separately
     * produced policy-v2 proof.
     *
     * No CLI path calls this factory in this increment.
     */
    public static DataRelativePathRepairBatchManifestCreation
        CreateAggregateAlternateBranchCoverageAuthorized(
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
        DataRelativePathRepairBatchManifestCreation legacy =
            Create(
                batchId,
                createdUtc,
                dataRoot,
                childManifestName,
                inputPathCount,
                safeRejectionCount,
                children
            );

        if (
            !legacy.Success ||
            legacy.Manifest is null)
        {
            return legacy;
        }

        DataRelativePathRepairBatchManifestRecord manifest =
            legacy.Manifest with
            {
                SchemaVersion =
                    DataRelativePathRepairBatchManifestRecord
                        .SchemaVersion3,
                CoveragePolicyVersion =
                    DataRelativePathRepairBatchManifestRecord
                        .CoveragePolicyVersion2
            };

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

    /*
     * Bind durable batch metadata to an independently supplied trusted
     * Skyrim Data root without exposing the generic repair-authority
     * helper outside Core.
     *
     * This is metadata validation only. It grants no filesystem handle,
     * mutation authority, or historical object ownership.
     */
    public static string? ValidateTrustedDataRoot(
        DataRelativePathRepairBatchManifestRecord manifest,
        string trustedDataRoot)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        string? validationError =
            Validate(
                manifest
            );

        if (validationError is not null)
        {
            return
                "The batch manifest is invalid: " +
                validationError;
        }

        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                manifest.DataRoot,
                out string? rootBindingError
            ))
        {
            return
                rootBindingError ??
                "The batch manifest Data root does not match the " +
                "independently supplied trusted Data root.";
        }

        return null;
    }

    public static string? Validate(
        DataRelativePathRepairBatchManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        if (
            manifest.SchemaVersion ==
            DataRelativePathRepairBatchManifestRecord
                .SchemaVersion1)
        {
            if (manifest.CoveragePolicyVersion is not null)
            {
                return
                    "Schema-v1 batch manifests must not claim aggregate " +
                    "namespace-coverage authorization.";
            }
        }
        else if (
            manifest.SchemaVersion ==
            DataRelativePathRepairBatchManifestRecord
                .SchemaVersion2)
        {
            if (
                manifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion1)
            {
                return
                    "Schema-v2 batch manifests require aggregate " +
                    "namespace-coverage policy version " +
                    $"{DataRelativePathRepairBatchManifestRecord.CoveragePolicyVersion1}.";
            }
        }
        else if (
            manifest.SchemaVersion ==
            DataRelativePathRepairBatchManifestRecord
                .SchemaVersion3)
        {
            if (
                manifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion2)
            {
                return
                    "Schema-v3 batch manifests require aggregate " +
                    "alternate-branch namespace-coverage policy version " +
                    $"{DataRelativePathRepairBatchManifestRecord.CoveragePolicyVersion2}.";
            }
        }
        else
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
