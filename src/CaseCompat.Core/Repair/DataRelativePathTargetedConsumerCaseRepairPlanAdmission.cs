using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// In-memory admission state for one targeted consumer-authoritative repair
// plan.
//
// Admission is deliberately not durable repair authority. It proves that:
//
// - the supplied C4E-4D2A destination plan still canonically reprojects;
// - its source snapshot still binds to the current exact source;
// - the freshly captured source inode generation exactly equals the
//   generation carried by the C4E-4B candidate.
//
// No manifest schema, batch schema, aggregate namespace evidence, sidecar,
// persistence, execution, rollback, recovery, or mutation is involved.
public enum
    DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
{
    Admitted,

    InvalidPlanProjection,
    PlanRevalidationFailed,
    PlanProjectionMismatch,

    SourceGenerationBindingFailed,
    SourceGenerationMismatch
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult(
        DataRelativePathTargetedConsumerCaseRepairPlanProjection
            SuppliedProjection,
        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState State,
        DataRelativePathTargetedConsumerCaseRepairPlanProjection?
            RevalidatedProjection,
        DataRelativePathRepairSourceGenerationBinding?
            SourceGenerationBinding,
        string? Error
    )
{
    public bool Admitted =>
        State ==
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                .Admitted &&
        RevalidatedProjection is not null &&
        SourceGenerationBinding?.Success == true;
}

// Manifest-independent C4E-4D2B admission seam.
//
// Candidate is not authorization:
//
// C4E-4B source-bound candidate
//     + C4E-4D2A destination plan shape
//     + fresh destination reprojection
//     + fresh exact source-generation binding
//     + exact generation equality
//     -> admitted in-memory evidence.
//
// A later durable-design checkpoint decides whether source generation must
// itself cross a persistence boundary. This type intentionally does not make
// that policy decision.
public static class
    DataRelativePathTargetedConsumerCaseRepairPlanAdmission
{
    public static
        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult
        Admit(
            LinuxNoFollowPathHandle trustedDataRoot,
            DataRelativePathTargetedConsumerCaseRepairPlanProjection
                projection,
            IReadOnlySet<string>? contestedAncestorPrefixes = null,
            LinuxNoFollowPathHandle? aliasesDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(
            trustedDataRoot
        );

        ArgumentNullException.ThrowIfNull(
            projection
        );

        if (!projection.HasPlan)
        {
            return Result(
                projection,
                DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                    .InvalidPlanProjection,
                error:
                    "Only a complete projected C4E-4D2A plan may enter " +
                    "targeted source admission."
            );
        }

        // The supplied projection may have chosen a CreateAliasSymlink
        // operation over a rename precisely because its caller (batch
        // apply, the guided wizard) saw this exact ancestor as contested
        // across the whole discovered candidate set - visibility this
        // single-candidate re-projection has no way to derive on its
        // own. Re-deriving fresh proof on a different basis than the
        // plan was built on would make a perfectly valid alias plan
        // fail equivalence below, so the same contested-ancestor set
        // must be supplied here too.
        DataRelativePathTargetedConsumerCaseRepairPlanProjection
            revalidated =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        trustedDataRoot,
                        projection.Candidate,
                        contestedAncestorPrefixes,
                        aliasesDirectory
                    );

        if (!revalidated.HasPlan)
        {
            return Result(
                projection,
                DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                    .PlanRevalidationFailed,
                revalidatedProjection:
                    revalidated,
                error:
                    revalidated.Error ??
                    revalidated.State.ToString()
            );
        }

        if (!EquivalentPlan(
                projection,
                revalidated,
                out string? mismatchError))
        {
            return Result(
                projection,
                DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                    .PlanProjectionMismatch,
                revalidatedProjection:
                    revalidated,
                error:
                    mismatchError
            );
        }

        DataRelativePathRepairSourceGenerationBinding binding =
            DataRelativePathRepairSourceGenerationBinder.Bind(
                trustedDataRoot,
                projection.Candidate.SourceSnapshot
            );

        if (
            !binding.Success ||
            binding.SourceInodeGeneration is null)
        {
            return Result(
                projection,
                DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                    .SourceGenerationBindingFailed,
                revalidatedProjection:
                    revalidated,
                sourceGenerationBinding:
                    binding,
                error:
                    binding.Error ??
                    binding.State.ToString()
            );
        }

        if (
            binding.SourceInodeGeneration.Value !=
            projection.Candidate.SourceInodeGeneration)
        {
            return Result(
                projection,
                DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                    .SourceGenerationMismatch,
                revalidatedProjection:
                    revalidated,
                sourceGenerationBinding:
                    binding,
                error:
                    "The freshly reacquired source inode generation does " +
                    "not equal the generation bound by the C4E-4B " +
                    "candidate."
            );
        }

        return new(
            SuppliedProjection:
                projection,
            State:
                DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                    .Admitted,
            RevalidatedProjection:
                revalidated,
            SourceGenerationBinding:
                binding,
            Error:
                null
        );
    }

    private static bool EquivalentPlan(
        DataRelativePathTargetedConsumerCaseRepairPlanProjection supplied,
        DataRelativePathTargetedConsumerCaseRepairPlanProjection current,
        out string? error)
    {
        error =
            null;

        if (
            supplied.FirstMissingComponentIndex !=
                current.FirstMissingComponentIndex ||
            !EquivalentCandidate(
                supplied.Candidate,
                current.Candidate) ||
            !EquivalentDestinationSnapshot(
                supplied.DestinationParentSnapshot,
                current.DestinationParentSnapshot) ||
            !EquivalentOperations(
                supplied.Operations,
                current.Operations) ||
            !EquivalentDirectoryRenameSources(
                supplied.DirectoryRenameSources,
                current.DirectoryRenameSources) ||
            !EquivalentAliasSources(
                supplied.AliasSources,
                current.AliasSources))
        {
            error =
                "The supplied targeted plan no longer exactly matches its " +
                "fresh canonical C4E-4D2A reprojection.";

            return false;
        }

        return true;
    }

    private static bool EquivalentCandidate(
        DataRelativePathTargetedConsumerCaseRepairCandidate left,
        DataRelativePathTargetedConsumerCaseRepairCandidate right)
    {
        return
            string.Equals(
                left.WindowsLogicalPath,
                right.WindowsLogicalPath,
                StringComparison.Ordinal) &&
            string.Equals(
                left.AuthoritativeRequestedPath,
                right.AuthoritativeRequestedPath,
                StringComparison.Ordinal) &&
            left.SourceInodeGeneration ==
                right.SourceInodeGeneration &&
            string.Equals(
                left.SourceRepresentation.RelativePath,
                right.SourceRepresentation.RelativePath,
                StringComparison.Ordinal) &&
            EquivalentSourceSnapshot(
                left.SourceSnapshot,
                right.SourceSnapshot);
    }

    private static bool EquivalentSourceSnapshot(
        DataRelativePathRepairSourceSnapshot left,
        DataRelativePathRepairSourceSnapshot right)
    {
        return
            string.Equals(
                left.PhysicalPath,
                right.PhysicalPath,
                StringComparison.Ordinal) &&
            left.Size ==
                right.Size &&
            string.Equals(
                left.Sha256,
                right.Sha256,
                StringComparison.OrdinalIgnoreCase) &&
            left.Identity is not null &&
            right.Identity is not null &&
            left.Identity.DeviceMajor ==
                right.Identity.DeviceMajor &&
            left.Identity.DeviceMinor ==
                right.Identity.DeviceMinor &&
            left.Identity.Inode ==
                right.Identity.Inode &&
            left.Identity.MountId ==
                right.Identity.MountId;
    }

    private static bool EquivalentDestinationSnapshot(
        DataRelativePathRepairDestinationParentSnapshot? left,
        DataRelativePathRepairDestinationParentSnapshot? right)
    {
        if (left is null ||
            right is null ||
            left.Identity is null ||
            right.Identity is null)
        {
            return false;
        }

        return
            string.Equals(
                left.PhysicalPath,
                right.PhysicalPath,
                StringComparison.Ordinal) &&
            left.CasefoldEnabled ==
                right.CasefoldEnabled &&
            left.RawFlags ==
                right.RawFlags &&
            left.Identity.DeviceMajor ==
                right.Identity.DeviceMajor &&
            left.Identity.DeviceMinor ==
                right.Identity.DeviceMinor &&
            left.Identity.Inode ==
                right.Identity.Inode &&
            left.Identity.MountId ==
                right.Identity.MountId;
    }

    private static bool EquivalentOperations(
        IReadOnlyList<DataRelativePathRepairPlanOperation> left,
        IReadOnlyList<DataRelativePathRepairPlanOperation> right)
    {
        if (left.Count !=
            right.Count)
        {
            return false;
        }

        for (
            int index = 0;
            index < left.Count;
            index++)
        {
            DataRelativePathRepairPlanOperation leftOperation =
                left[index];

            DataRelativePathRepairPlanOperation rightOperation =
                right[index];

            if (
                leftOperation.Kind !=
                    rightOperation.Kind ||
                !string.Equals(
                    leftOperation.DestinationPath,
                    rightOperation.DestinationPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    leftOperation.SourcePath,
                    rightOperation.SourcePath,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EquivalentDirectoryRenameSources(
        IReadOnlyList<DataRelativePathRepairDirectoryRenameSource> left,
        IReadOnlyList<DataRelativePathRepairDirectoryRenameSource> right)
    {
        if (left.Count !=
            right.Count)
        {
            return false;
        }

        for (
            int index = 0;
            index < left.Count;
            index++)
        {
            DataRelativePathRepairDirectoryRenameSource leftSource =
                left[index];

            DataRelativePathRepairDirectoryRenameSource rightSource =
                right[index];

            if (
                !string.Equals(
                    leftSource.DestinationPath,
                    rightSource.DestinationPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    leftSource.PhysicalPath,
                    rightSource.PhysicalPath,
                    StringComparison.Ordinal) ||
                leftSource.InodeGeneration !=
                    rightSource.InodeGeneration ||
                leftSource.Identity.DeviceMajor !=
                    rightSource.Identity.DeviceMajor ||
                leftSource.Identity.DeviceMinor !=
                    rightSource.Identity.DeviceMinor ||
                leftSource.Identity.Inode !=
                    rightSource.Identity.Inode ||
                leftSource.Identity.MountId !=
                    rightSource.Identity.MountId)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EquivalentAliasSources(
        IReadOnlyList<DataRelativePathRepairAliasSource> left,
        IReadOnlyList<DataRelativePathRepairAliasSource> right)
    {
        if (left.Count !=
            right.Count)
        {
            return false;
        }

        for (
            int index = 0;
            index < left.Count;
            index++)
        {
            DataRelativePathRepairAliasSource leftSource =
                left[index];

            DataRelativePathRepairAliasSource rightSource =
                right[index];

            if (
                !string.Equals(
                    leftSource.DestinationPath,
                    rightSource.DestinationPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    leftSource.PhysicalPath,
                    rightSource.PhysicalPath,
                    StringComparison.Ordinal) ||
                leftSource.InodeGeneration !=
                    rightSource.InodeGeneration ||
                leftSource.Identity.DeviceMajor !=
                    rightSource.Identity.DeviceMajor ||
                leftSource.Identity.DeviceMinor !=
                    rightSource.Identity.DeviceMinor ||
                leftSource.Identity.Inode !=
                    rightSource.Identity.Inode ||
                leftSource.Identity.MountId !=
                    rightSource.Identity.MountId)
            {
                return false;
            }
        }

        return true;
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult
        Result(
            DataRelativePathTargetedConsumerCaseRepairPlanProjection
                projection,
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState state,
            DataRelativePathTargetedConsumerCaseRepairPlanProjection?
                revalidatedProjection = null,
            DataRelativePathRepairSourceGenerationBinding?
                sourceGenerationBinding = null,
            string? error = null)
    {
        return new(
            SuppliedProjection:
                projection,
            State:
                state,
            RevalidatedProjection:
                revalidatedProjection,
            SourceGenerationBinding:
                sourceGenerationBinding,
            Error:
                error
        );
    }
}
