namespace CaseCompat.Core.Repair;

public sealed record
    DataRelativePathRepairBatchApplyAuthorizationRecord(
        int SchemaVersion,
        Guid BatchId,
        DateTimeOffset CreatedUtc,
        string DataRoot,
        string BatchManifestSha256,
        int CoveragePolicyVersion
    )
{
    public const int SchemaVersion1 =
        1;

    public const int CurrentSchemaVersion =
        SchemaVersion1;
}

public enum
    DataRelativePathRepairBatchApplyAuthorizationCreationState
{
    Created,
    InvalidInput
}

public sealed record
    DataRelativePathRepairBatchApplyAuthorizationCreation(
        DataRelativePathRepairBatchApplyAuthorizationCreationState
            State,
        DataRelativePathRepairBatchApplyAuthorizationRecord?
            Authorization,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathRepairBatchApplyAuthorizationCreationState
                .Created &&
        Authorization is not null;
}

/*
 * Immutable durable provenance for the batch-wide namespace check that must
 * precede the first mutation of a coverage-authorized repair batch.
 *
 * This record alone grants no mutation authority.
 *
 * Its writer publishes it only after the mutating caller has established:
 *
 *   1. the exact completed batch has been descriptor-authenticated;
 *   2. the batch is schema v2 / coverage-policy version 1;
 *   3. fresh aggregate physical namespace coverage has succeeded.
 *
 * Recovery can then bind this immutable record back to the exact durable
 * batch-manifest bytes instead of inferring batch authorization merely from
 * the existence of a child operation journal.
 */
public static class
    DataRelativePathRepairBatchApplyAuthorization
{
    public static
        DataRelativePathRepairBatchApplyAuthorizationCreation
        CreateForCompletedBatch(
            DataRelativePathRepairBatchManifestRecord batchManifest,
            string batchManifestSha256,
            DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(
            batchManifest
        );

        string? batchValidationError =
            DataRelativePathRepairBatchManifest.Validate(
                batchManifest
            );

        if (batchValidationError is not null)
        {
            return Invalid(
                "The completed batch manifest is invalid: " +
                batchValidationError
            );
        }

        if (
            batchManifest.SchemaVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .SchemaVersion2 ||
            batchManifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion1)
        {
            return Invalid(
                "Batch apply authorization requires a schema-v2 completed " +
                "batch carrying aggregate namespace-coverage policy " +
                $"{DataRelativePathRepairBatchManifestRecord.CoveragePolicyVersion1}."
            );
        }

        var authorization =
            new DataRelativePathRepairBatchApplyAuthorizationRecord(
                SchemaVersion:
                    DataRelativePathRepairBatchApplyAuthorizationRecord
                        .CurrentSchemaVersion,
                BatchId:
                    batchManifest.BatchId,
                CreatedUtc:
                    createdUtc,
                DataRoot:
                    batchManifest.DataRoot,
                BatchManifestSha256:
                    batchManifestSha256,
                CoveragePolicyVersion:
                    batchManifest.CoveragePolicyVersion.Value
            );

        string? validationError =
            Validate(
                authorization
            );

        if (validationError is not null)
        {
            return Invalid(
                validationError
            );
        }

        return new(
            State:
                DataRelativePathRepairBatchApplyAuthorizationCreationState
                    .Created,
            Authorization:
                authorization,
            Error:
                null
        );
    }

    public static string? Validate(
        DataRelativePathRepairBatchApplyAuthorizationRecord
            authorization)
    {
        ArgumentNullException.ThrowIfNull(
            authorization
        );

        if (
            authorization.SchemaVersion !=
            DataRelativePathRepairBatchApplyAuthorizationRecord
                .SchemaVersion1)
        {
            return
                $"Unsupported batch apply-authorization schema version " +
                $"{authorization.SchemaVersion}.";
        }

        if (authorization.BatchId == Guid.Empty)
        {
            return
                "Batch apply authorization requires a non-empty BatchId.";
        }

        if (
            !TryNormalizeAbsolutePath(
                authorization.DataRoot,
                out _))
        {
            return
                "Batch apply authorization Data root must be an absolute " +
                "valid path.";
        }

        if (
            !IsSha256(
                authorization.BatchManifestSha256))
        {
            return
                "Batch apply authorization requires a 64-character " +
                "batch-manifest SHA-256 value.";
        }

        if (
            authorization.CoveragePolicyVersion !=
            DataRelativePathRepairBatchManifestRecord
                .CoveragePolicyVersion1)
        {
            return
                "Batch apply authorization requires aggregate " +
                "namespace-coverage policy version " +
                $"{DataRelativePathRepairBatchManifestRecord.CoveragePolicyVersion1}.";
        }

        return null;
    }

    /*
     * Reauthenticate durable apply provenance against the exact currently
     * verified completed batch.
     *
     * The exact batch-manifest SHA binds all durable child membership,
     * PlanIds, child-manifest SHA-256 values, counts, Data root, and policy
     * marker carried by that immutable batch manifest.
     */
    public static string? ValidateCompletedBatchBinding(
        DataRelativePathRepairBatchApplyAuthorizationRecord
            authorization,
        DataRelativePathRepairBatchManifestRecord batchManifest,
        string batchManifestSha256)
    {
        ArgumentNullException.ThrowIfNull(
            authorization
        );

        ArgumentNullException.ThrowIfNull(
            batchManifest
        );

        string? authorizationError =
            Validate(
                authorization
            );

        if (authorizationError is not null)
        {
            return
                "The batch apply authorization is invalid: " +
                authorizationError;
        }

        string? batchError =
            DataRelativePathRepairBatchManifest.Validate(
                batchManifest
            );

        if (batchError is not null)
        {
            return
                "The completed batch manifest is invalid: " +
                batchError;
        }

        if (
            batchManifest.SchemaVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .SchemaVersion2 ||
            batchManifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion1)
        {
            return
                "The completed batch is not eligible for aggregate " +
                "namespace apply authorization.";
        }

        if (
            authorization.BatchId !=
            batchManifest.BatchId)
        {
            return
                "The batch apply authorization BatchId does not match " +
                "the completed batch.";
        }

        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                authorization.DataRoot,
                batchManifest.DataRoot,
                out string? rootBindingError))
        {
            return
                rootBindingError ??
                "The batch apply authorization Data root does not match " +
                "the completed batch Data root.";
        }

        if (
            !string.Equals(
                authorization.BatchManifestSha256,
                batchManifestSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "The batch apply authorization does not bind the exact " +
                "current batch-manifest bytes.";
        }

        if (
            authorization.CoveragePolicyVersion !=
            batchManifest.CoveragePolicyVersion)
        {
            return
                "The batch apply authorization coverage-policy version " +
                "does not match the completed batch.";
        }

        return null;
    }

    private static
        DataRelativePathRepairBatchApplyAuthorizationCreation
        Invalid(
            string error)
    {
        return new(
            State:
                DataRelativePathRepairBatchApplyAuthorizationCreationState
                    .InvalidInput,
            Authorization:
                null,
            Error:
                error
        );
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
