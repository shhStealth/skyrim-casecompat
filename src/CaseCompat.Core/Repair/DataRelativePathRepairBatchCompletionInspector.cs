using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairBatchCompletionInspectionState
{
    Verified,

    EnumerationFailed,

    ManifestUnavailable,
    ManifestReadFailed,

    TrustedDataRootValidationFailed,
    TrustedDataRootMismatch,
    ChildManifestNameMismatch,
    TopologyInvalid,

    ChildOpenFailed,
    ChildInspectionFailed,
    ChildManifestBindingUnavailable,
    ChildPlanIdMismatch,
    ChildManifestSha256Mismatch,

    ApplyAuthorizationReadFailed,
    ApplyAuthorizationBindingMismatch
}

public sealed record
    DataRelativePathRepairBatchCompletionInspectedChild(
        string ChildName,
        DataRelativePathRepairBatchManifestChild ExpectedChild,
        DataRelativePathRepairPlanStatusInspection Inspection
    );

public sealed record
    DataRelativePathRepairBatchCompletionInspection(
        DataRelativePathRepairBatchCompletionInspectionState State,
        LinuxEnumerateDirectoryAtResult? Enumeration,
        DataRelativePathRepairBatchManifestReaderResult?
            BatchManifestRead,
        DataRelativePathRepairBatchManifestRecord? Manifest,
        IReadOnlyList<
            DataRelativePathRepairBatchCompletionInspectedChild>
            Children,
        string? FailedChildName,
        string? Error
    )
{
    /*
     * Carries the observed reserved batch-wide apply-authorization read when
     * that entry was encountered.
     *
     * This property may also be populated on a fail-closed authorization
     * read or binding failure so callers can inspect the exact observed
     * evidence. It does not grant authority by itself.
     *
     * Only a Verified completion inspection with a non-null successful
     * ApplyAuthorizationRead guarantees that the authorization was validated
     * and rebound to the exact durable batch-manifest bytes.
     *
     * Absence is valid: a schema-v2 completed batch has not necessarily
     * crossed the batch-apply authorization boundary yet.
     */
    public DataRelativePathRepairBatchApplyAuthorizationReaderResult?
        ApplyAuthorizationRead
    {
        get;
        init;
    }

    public bool Success =>
        State ==
            DataRelativePathRepairBatchCompletionInspectionState
                .Verified &&
        Enumeration?.Success == true &&
        BatchManifestRead?.Success == true &&
        Manifest is not null;
}

/*
 * Canonical read-only verifier for a durable completed repair batch.
 *
 * This inspector grants no mutation authority.
 *
 * It proves, from one retained batch-directory descriptor:
 *
 *   - the durable batch manifest is readable and valid;
 *   - its recorded Data root matches an independently trusted Data root;
 *   - its recorded child-manifest name matches the requested name;
 *   - the retained batch root has exactly the recorded completed topology;
 *   - every recorded child is reachable descriptor-relatively;
 *   - every child is a completely inspectable persisted repair plan;
 *   - every child's PlanId and exact manifest-byte SHA-256 match the
 *     durable batch manifest.
 *
 * Enumeration deliberately occurs before the batch-manifest read. This
 * preserves the existing status race behavior:
 *
 *   - a manifest that appears after enumeration is absent from the
 *     enumerated topology and therefore cannot verify;
 *   - a manifest that disappears after enumeration yields
 *     ManifestUnavailable, while its enumerated name prevents the legacy
 *     contiguous-plan fallback from accepting the directory.
 *
 * A mutating batch caller must still revalidate each selected child at
 * mutation time. In particular, ExecuteExpectedBatchManifest(...) performs
 * the PlanId/SHA-256 check again against the authoritative manifest reread
 * held under the existing per-plan execution lock.
 */
public static class DataRelativePathRepairBatchCompletionInspector
{
    private const string ApplyAuthorizationChildName =
        "batch-apply-authorization.json";

    public static DataRelativePathRepairBatchCompletionInspection
        Inspect(
            LinuxNoFollowPathHandle batchDirectory,
            string batchManifestChildName,
            string childManifestName,
            string trustedDataRoot)
    {
        ArgumentNullException.ThrowIfNull(
            batchDirectory
        );

        LinuxEnumerateDirectoryAtResult enumeration;

        try
        {
            enumeration =
                LinuxEnumerateDirectoryAt.Enumerate(
                    batchDirectory
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairBatchCompletionInspectionState
                    .EnumerationFailed,
                error:
                    ex.Message
            );
        }

        if (!enumeration.Success)
        {
            return Result(
                DataRelativePathRepairBatchCompletionInspectionState
                    .EnumerationFailed,
                enumeration:
                    enumeration,
                error:
                    enumeration.Error ??
                    enumeration.State.ToString()
            );
        }

        DataRelativePathRepairBatchManifestReaderResult
            batchManifestRead;

        try
        {
            batchManifestRead =
                DataRelativePathRepairBatchManifestReader.Read(
                    batchDirectory,
                    batchManifestChildName
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairBatchCompletionInspectionState
                    .ManifestReadFailed,
                enumeration:
                    enumeration,
                error:
                    ex.Message
            );
        }

        if (
            batchManifestRead.State ==
            DataRelativePathRepairBatchManifestReadState
                .ManifestUnavailable)
        {
            return Result(
                DataRelativePathRepairBatchCompletionInspectionState
                    .ManifestUnavailable,
                enumeration:
                    enumeration,
                batchManifestRead:
                    batchManifestRead,
                error:
                    batchManifestRead.Error
            );
        }

        if (!batchManifestRead.Success)
        {
            return Result(
                DataRelativePathRepairBatchCompletionInspectionState
                    .ManifestReadFailed,
                enumeration:
                    enumeration,
                batchManifestRead:
                    batchManifestRead,
                error:
                    batchManifestRead.Error ??
                    batchManifestRead.State.ToString()
            );
        }

        DataRelativePathRepairBatchManifestRecord manifest =
            batchManifestRead.Manifest!;

        string? trustedRootError;

        try
        {
            trustedRootError =
                DataRelativePathRepairBatchManifest
                    .ValidateTrustedDataRoot(
                        manifest,
                        trustedDataRoot
                    );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairBatchCompletionInspectionState
                    .TrustedDataRootValidationFailed,
                enumeration:
                    enumeration,
                batchManifestRead:
                    batchManifestRead,
                manifest:
                    manifest,
                error:
                    ex.Message
            );
        }

        if (trustedRootError is not null)
        {
            return Result(
                DataRelativePathRepairBatchCompletionInspectionState
                    .TrustedDataRootMismatch,
                enumeration:
                    enumeration,
                batchManifestRead:
                    batchManifestRead,
                manifest:
                    manifest,
                error:
                    trustedRootError
            );
        }

        if (
            !string.Equals(
                manifest.ChildManifestName,
                childManifestName,
                StringComparison.Ordinal))
        {
            return Result(
                DataRelativePathRepairBatchCompletionInspectionState
                    .ChildManifestNameMismatch,
                enumeration:
                    enumeration,
                batchManifestRead:
                    batchManifestRead,
                manifest:
                    manifest,
                error:
                    $"Requested child manifest {childManifestName} does " +
                    $"not match recorded child manifest " +
                    $"{manifest.ChildManifestName}."
            );
        }

        bool hasApplyAuthorization =
            enumeration.ChildNames.Any(childName =>
                string.Equals(
                    childName,
                    ApplyAuthorizationChildName,
                    StringComparison.Ordinal
                )
            );

        string? topologyError =
            ValidateManifestBackedTopology(
                enumeration.ChildNames,
                batchManifestChildName,
                manifest,
                hasApplyAuthorization
            );

        if (topologyError is not null)
        {
            return Result(
                DataRelativePathRepairBatchCompletionInspectionState
                    .TopologyInvalid,
                enumeration:
                    enumeration,
                batchManifestRead:
                    batchManifestRead,
                manifest:
                    manifest,
                error:
                    topologyError
            );
        }

        DataRelativePathRepairBatchApplyAuthorizationReaderResult?
            applyAuthorizationRead =
                null;

        if (hasApplyAuthorization)
        {
            try
            {
                applyAuthorizationRead =
                    DataRelativePathRepairBatchApplyAuthorizationReader
                        .Read(
                            batchDirectory,
                            ApplyAuthorizationChildName
                        );
            }
            catch (Exception ex)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ApplyAuthorizationReadFailed,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    error:
                        ex.Message
                );
            }

            if (!applyAuthorizationRead.Success)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ApplyAuthorizationReadFailed,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    applyAuthorizationRead:
                        applyAuthorizationRead,
                    error:
                        applyAuthorizationRead.Error ??
                        applyAuthorizationRead.State.ToString()
                );
            }

            if (
                batchManifestRead.ManifestSha256 is null ||
                applyAuthorizationRead.Authorization is null)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ApplyAuthorizationBindingMismatch,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    applyAuthorizationRead:
                        applyAuthorizationRead,
                    error:
                        "The exact validated batch-manifest bytes required " +
                        "to bind batch apply authorization were unavailable."
                );
            }

            string? authorizationBindingError =
                DataRelativePathRepairBatchApplyAuthorization
                    .ValidateCompletedBatchBinding(
                        applyAuthorizationRead.Authorization,
                        manifest,
                        batchManifestRead.ManifestSha256
                    );

            if (authorizationBindingError is not null)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ApplyAuthorizationBindingMismatch,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    applyAuthorizationRead:
                        applyAuthorizationRead,
                    error:
                        authorizationBindingError
                );
            }
        }

        var children =
            new List<
                DataRelativePathRepairBatchCompletionInspectedChild>(
                    manifest.Children.Count
                );

        foreach (
            DataRelativePathRepairBatchManifestChild expectedChild
            in manifest.Children)
        {
            LinuxOpenChildReadOnlyAtResult childOpen;

            try
            {
                childOpen =
                    LinuxOpenChildReadOnlyAt.Open(
                        batchDirectory,
                        expectedChild.ChildName
                    );
            }
            catch (Exception ex)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ChildOpenFailed,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    children:
                        children,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        ex.Message
                );
            }

            if (!childOpen.Success)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ChildOpenFailed,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    children:
                        children,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        childOpen.Error ??
                        childOpen.State.ToString()
                );
            }

            using LinuxOpenedChildHandle childDirectory =
                childOpen.OpenedChild!;

            DataRelativePathRepairPlanStatusInspection inspection;

            try
            {
                inspection =
                    DataRelativePathRepairPlanStatusInspector.Inspect(
                        childDirectory,
                        childManifestName,
                        trustedDataRoot
                    );
            }
            catch (Exception ex)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ChildInspectionFailed,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    children:
                        children,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        ex.Message
                );
            }

            if (!inspection.Success)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ChildInspectionFailed,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    children:
                        children,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        inspection.Error ??
                        inspection.State.ToString()
                );
            }

            DataRelativePathRepairPlanManifestReaderResult?
                manifestRead =
                    inspection.ManifestRead;

            if (
                manifestRead is null ||
                !manifestRead.Success ||
                manifestRead.ManifestSha256 is null ||
                inspection.Manifest is null)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ChildManifestBindingUnavailable,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    children:
                        children,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        "The inspected child did not retain the exact " +
                        "validated manifest bytes required for durable " +
                        "batch membership."
                );
            }

            if (
                inspection.Manifest.PlanId !=
                expectedChild.PlanId)
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ChildPlanIdMismatch,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    children:
                        children,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        $"Recorded PlanId {expectedChild.PlanId} does not " +
                        $"match observed PlanId " +
                        $"{inspection.Manifest.PlanId}."
                );
            }

            if (
                !string.Equals(
                    manifestRead.ManifestSha256,
                    expectedChild.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Result(
                    DataRelativePathRepairBatchCompletionInspectionState
                        .ChildManifestSha256Mismatch,
                    enumeration:
                        enumeration,
                    batchManifestRead:
                        batchManifestRead,
                    manifest:
                        manifest,
                    children:
                        children,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        "The observed child manifest SHA-256 does not " +
                        "match the durable batch membership record."
                );
            }

            children.Add(
                new(
                    ChildName:
                        expectedChild.ChildName,
                    ExpectedChild:
                        expectedChild,
                    Inspection:
                        inspection
                )
            );
        }

        return new(
            State:
                DataRelativePathRepairBatchCompletionInspectionState
                    .Verified,
            Enumeration:
                enumeration,
            BatchManifestRead:
                batchManifestRead,
            Manifest:
                manifest,
            Children:
                children,
            FailedChildName:
                null,
            Error:
                null
        )
        {
            ApplyAuthorizationRead =
                applyAuthorizationRead
        };
    }

    private static string? ValidateManifestBackedTopology(
        IReadOnlyList<string> childNames,
        string batchManifestChildName,
        DataRelativePathRepairBatchManifestRecord manifest,
        bool hasApplyAuthorization)
    {
        var names =
            new HashSet<string>(
                childNames,
                StringComparer.Ordinal
            );

        if (names.Count != childNames.Count)
        {
            return
                "The retained batch directory enumeration contained " +
                "duplicate literal child names.";
        }

        bool authorizationEntryAllowed =
            manifest.SchemaVersion ==
                DataRelativePathRepairBatchManifestRecord
                    .SchemaVersion2 &&
            manifest.CoveragePolicyVersion ==
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion1;

        int expectedEntryCount =
            manifest.Children.Count +
            1 +
            (
                authorizationEntryAllowed &&
                hasApplyAuthorization
                    ? 1
                    : 0
            );

        if (names.Count != expectedEntryCount)
        {
            return
                $"The completed batch requires exactly " +
                $"{expectedEntryCount:N0} direct entries for its current " +
                $"durable state: the batch manifest, " +
                $"{manifest.Children.Count:N0} recorded plan children" +
                (
                    authorizationEntryAllowed &&
                    hasApplyAuthorization
                        ? ", and the reserved batch apply-authorization entry"
                        : string.Empty
                ) +
                $". Observed {names.Count:N0}.";
        }

        if (
            hasApplyAuthorization &&
            !authorizationEntryAllowed)
        {
            return
                "The reserved batch apply-authorization entry is valid " +
                "only for schema-v2 batches carrying aggregate " +
                "namespace-coverage policy version 1.";
        }

        if (!names.Contains(batchManifestChildName))
        {
            return
                $"The completed batch is missing the exact durable " +
                $"completion child {batchManifestChildName}.";
        }

        foreach (
            DataRelativePathRepairBatchManifestChild child
            in manifest.Children)
        {
            if (!names.Contains(child.ChildName))
            {
                return
                    $"The completed batch is missing recorded child " +
                    $"{child.ChildName}.";
            }
        }

        return null;
    }

    private static DataRelativePathRepairBatchCompletionInspection
        Result(
            DataRelativePathRepairBatchCompletionInspectionState state,
            LinuxEnumerateDirectoryAtResult? enumeration = null,
            DataRelativePathRepairBatchManifestReaderResult?
                batchManifestRead = null,
            DataRelativePathRepairBatchManifestRecord? manifest = null,
            IReadOnlyList<
                DataRelativePathRepairBatchCompletionInspectedChild>?
                children = null,
            DataRelativePathRepairBatchApplyAuthorizationReaderResult?
                applyAuthorizationRead = null,
            string? failedChildName = null,
            string? error = null)
    {
        return new(
            State:
                state,
            Enumeration:
                enumeration,
            BatchManifestRead:
                batchManifestRead,
            Manifest:
                manifest,
            Children:
                children ??
                [],
            FailedChildName:
                failedChildName,
            Error:
                error
        )
        {
            ApplyAuthorizationRead =
                applyAuthorizationRead
        };
    }
}
