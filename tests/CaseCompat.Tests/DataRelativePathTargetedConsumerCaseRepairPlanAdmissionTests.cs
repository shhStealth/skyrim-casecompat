using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathTargetedConsumerCaseRepairPlanAdmissionTests
{
    [Fact]
    public void Admit_UnchangedCandidateAndPlanAreAdmitted()
    {
        Fixture fixture =
            CreateFixture();

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                fixture.DataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult result =
            DataRelativePathTargetedConsumerCaseRepairPlanAdmission.Admit(
                root,
                fixture.Plan
            );

        Assert.True(
            result.Admitted,
            result.Error
        );

        Assert.Equal(
            fixture.Candidate.SourceInodeGeneration,
            result.SourceGenerationBinding!
                .SourceInodeGeneration
        );
    }

    [Fact]
    public void Admit_SourceDeletedAfterPlanningFailsBinding()
    {
        Fixture fixture =
            CreateFixture();

        File.Delete(
            fixture.SourcePath
        );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                fixture.DataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult result =
            DataRelativePathTargetedConsumerCaseRepairPlanAdmission.Admit(
                root,
                fixture.Plan
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                .SourceGenerationBindingFailed,
            result.State
        );

        Assert.False(
            result.Admitted
        );
    }

    [Fact]
    public void Admit_SourceContentChangedAfterPlanningFailsBinding()
    {
        Fixture fixture =
            CreateFixture();

        File.WriteAllText(
            fixture.SourcePath,
            "changed source contents"
        );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                fixture.DataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult result =
            DataRelativePathTargetedConsumerCaseRepairPlanAdmission.Admit(
                root,
                fixture.Plan
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                .SourceGenerationBindingFailed,
            result.State
        );

        Assert.False(
            result.Admitted
        );
    }

    [Fact]
    public void Admit_CandidateGenerationMismatchIsRejected()
    {
        Fixture fixture =
            CreateFixture();

        uint changedGeneration =
            fixture.Candidate.SourceInodeGeneration ==
                uint.MaxValue
                ? fixture.Candidate.SourceInodeGeneration - 1
                : fixture.Candidate.SourceInodeGeneration + 1;

        DataRelativePathTargetedPhysicalFileRepresentation changedSource =
            fixture.Candidate.SourceRepresentation with
            {
                InodeGeneration =
                    changedGeneration
            };

        DataRelativePathTargetedConsumerCaseRepairCandidate changedCandidate =
            fixture.Candidate with
            {
                SourceRepresentation =
                    changedSource
            };

        DataRelativePathTargetedConsumerCaseRepairPlanProjection changedPlan =
            fixture.Plan with
            {
                Candidate =
                    changedCandidate
            };

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                fixture.DataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult result =
            DataRelativePathTargetedConsumerCaseRepairPlanAdmission.Admit(
                root,
                changedPlan
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                .SourceGenerationMismatch,
            result.State
        );

        Assert.False(
            result.Admitted
        );
    }

    [Fact]
    public void Admit_DestinationAppearsAfterPlanningFailsRevalidation()
    {
        Fixture fixture =
            CreateFixture();

        string destination =
            fixture.Plan.Operations[^1]
                .DestinationPath;

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                destination
            )!
        );

        File.WriteAllText(
            destination,
            "appeared"
        );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                fixture.DataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult result =
            DataRelativePathTargetedConsumerCaseRepairPlanAdmission.Admit(
                root,
                fixture.Plan
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                .PlanRevalidationFailed,
            result.State
        );

        Assert.False(
            result.Admitted
        );

        Assert.Null(
            result.SourceGenerationBinding
        );
    }

    [Fact]
    public void Admit_TamperedOperationIsRejectedBeforeSourceBinding()
    {
        Fixture fixture =
            CreateFixture();

        DataRelativePathRepairPlanOperation[] changedOperations =
            fixture.Plan.Operations
                .ToArray();

        changedOperations[^1] =
            changedOperations[^1] with
            {
                DestinationPath =
                    changedOperations[^1].DestinationPath +
                    ".tampered"
            };

        DataRelativePathTargetedConsumerCaseRepairPlanProjection changedPlan =
            fixture.Plan with
            {
                Operations =
                    changedOperations
            };

        File.Delete(
            fixture.SourcePath
        );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                fixture.DataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult result =
            DataRelativePathTargetedConsumerCaseRepairPlanAdmission.Admit(
                root,
                changedPlan
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                .PlanProjectionMismatch,
            result.State
        );

        Assert.Null(
            result.SourceGenerationBinding
        );
    }

    [Fact]
    public void Admit_NonProjectedInputIsRejectedBeforeFilesystemWork()
    {
        Fixture fixture =
            CreateFixture();

        DataRelativePathTargetedConsumerCaseRepairPlanProjection invalid =
            fixture.Plan with
            {
                State =
                    DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                        .DestinationConflict,
                Operations =
                    Array.Empty<
                        DataRelativePathRepairPlanOperation
                    >()
            };

        File.Delete(
            fixture.SourcePath
        );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                fixture.DataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult result =
            DataRelativePathTargetedConsumerCaseRepairPlanAdmission.Admit(
                root,
                invalid
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                .InvalidPlanProjection,
            result.State
        );

        Assert.Null(
            result.RevalidatedProjection
        );

        Assert.Null(
            result.SourceGenerationBinding
        );
    }

    [Fact]
    public void Admit_DifferentDataRootFailsClosed()
    {
        Fixture fixture =
            CreateFixture();

        string otherDataRoot =
            CreateDataRoot();

        using LinuxNoFollowPathHandle otherRoot =
            OpenRoot(
                otherDataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult result =
            DataRelativePathTargetedConsumerCaseRepairPlanAdmission.Admit(
                otherRoot,
                fixture.Plan
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionState
                .PlanRevalidationFailed,
            result.State
        );

        Assert.False(
            result.Admitted
        );

        Assert.Null(
            result.SourceGenerationBinding
        );
    }

    private static Fixture CreateFixture()
    {
        string dataRoot =
            CreateDataRoot();

        string requestedPath =
            "Meshes/Actors/Character/File.NIF";

        string sourcePath =
            CreateFile(
                dataRoot,
                "meshes/actors/character/file.nif",
                "source"
            );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                dataRoot
            );

        string rootLogical =
            requestedPath
                .Split('/')[0]
                .ToUpperInvariant();

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
            current =
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                    .Analyze(
                        root,
                        rootLogical,
                        requestedPath
                    );

        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier.Classify(
                requestedPath.ToUpperInvariant(),
                new[]
                {
                    requestedPath
                }
            );

        DataRelativePathTargetedConsumerCaseRepairCandidateProjection
            candidateProjection =
                DataRelativePathTargetedConsumerCaseRepairCandidateProjector
                    .Project(
                        dataRoot,
                        consumer,
                        current
                    );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            Assert.IsType<
                DataRelativePathTargetedConsumerCaseRepairCandidate
            >(
                candidateProjection.Candidate
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection plan =
            DataRelativePathTargetedConsumerCaseRepairPlanProjector.Project(
                root,
                candidate
            );

        Assert.True(
            plan.HasPlan,
            plan.Error
        );

        return new(
            DataRoot:
                dataRoot,
            SourcePath:
                sourcePath,
            Candidate:
                candidate,
            Plan:
                plan
        );
    }

    private static LinuxNoFollowPathHandle OpenRoot(
        string dataRoot)
    {
        LinuxNoFollowPathOpenResult opened =
            LinuxNoFollowPath.OpenRootReadOnly(
                dataRoot
            );

        Assert.True(
            opened.Success,
            opened.Error
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
            opened.OpenedPath
        );
    }

    private static string CreateDataRoot()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-targeted-admission-tests",
                Guid.NewGuid().ToString("N"),
                "Data"
            );

        Directory.CreateDirectory(
            path
        );

        return Path.GetFullPath(
            path
        );
    }

    private static string CreateFile(
        string dataRoot,
        string relativePath,
        string contents)
    {
        string fullPath =
            Path.GetFullPath(
                Path.Combine(
                    dataRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar
                    )
                )
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                fullPath
            )!
        );

        File.WriteAllText(
            fullPath,
            contents
        );

        return fullPath;
    }

    private sealed record Fixture(
        string DataRoot,
        string SourcePath,
        DataRelativePathTargetedConsumerCaseRepairCandidate Candidate,
        DataRelativePathTargetedConsumerCaseRepairPlanProjection Plan
    );
}
