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
    ChildManifestSha256Mismatch
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
 * A future mutating batch caller must still revalidate each selected child
 * at mutation time. In particular, ExecuteExpectedManifest(...) performs
 * the PlanId/SHA-256 check again against the authoritative manifest reread
 * held under the existing per-plan execution lock.
 */
public static class DataRelativePathRepairBatchCompletionInspector
{
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

        string? topologyError =
            ValidateManifestBackedTopology(
                enumeration.ChildNames,
                batchManifestChildName,
                manifest
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
        );
    }

    private static string? ValidateManifestBackedTopology(
        IReadOnlyList<string> childNames,
        string batchManifestChildName,
        DataRelativePathRepairBatchManifestRecord manifest)
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

        int expectedEntryCount =
            manifest.Children.Count + 1;

        if (names.Count != expectedEntryCount)
        {
            return
                $"The completed batch requires exactly " +
                $"{expectedEntryCount:N0} direct entries: the durable " +
                $"batch manifest plus {manifest.Children.Count:N0} " +
                $"recorded plan children. Observed {names.Count:N0}.";
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
        );
    }
}
