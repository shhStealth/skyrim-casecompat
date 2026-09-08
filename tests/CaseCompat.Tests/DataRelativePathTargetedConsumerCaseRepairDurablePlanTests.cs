using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathTargetedConsumerCaseRepairDurablePlanTests
{
    [Fact]
    public void Create_UnchangedPlanCreatesIndependentTargetedV1Record()
    {
        Fixture fixture =
            CreateFixture();

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                fixture.DataRoot
            );

        Guid planId =
            Guid.NewGuid();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creation =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    planId,
                    DateTimeOffset.UtcNow,
                    fixture.Plan
                );

        Assert.True(
            creation.Success,
            creation.Error
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            Assert.IsType<
                DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
            >(
                creation.Record
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                .SchemaVersion1,
            record.SchemaVersion
        );

        Assert.Equal(
            planId,
            record.PlanId
        );

        Assert.Equal(
            fixture.Candidate.AuthoritativeRequestedPath,
            record.RequestedPath
        );

        Assert.Equal(
            fixture.Candidate.SourceInodeGeneration,
            record.SourceInodeGeneration
        );

        Assert.Equal(
            creation.Admission!
                .SourceGenerationBinding!
                .SourceInodeGeneration!
                .Value,
            record.SourceInodeGeneration
        );

        Assert.Null(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                record
            )
        );
    }

    [Fact]
    public void Create_EmptyPlanIdIsRejectedBeforeAdmission()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creation =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    Guid.Empty,
                    DateTimeOffset.UtcNow,
                    fixture.Plan
                );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                .InvalidInput,
            creation.State
        );

        Assert.Null(
            creation.Admission
        );

        Assert.Null(
            creation.Record
        );
    }

    [Fact]
    public void Create_DestinationAppearedAfterPlanningRejectsAdmission()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creation =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    fixture.Plan
                );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                .AdmissionRejected,
            creation.State
        );

        Assert.NotNull(
            creation.Admission
        );

        Assert.Null(
            creation.Record
        );
    }

    [Fact]
    public void Create_SourceContentChangedAfterPlanningRejectsAdmission()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creation =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    fixture.Plan
                );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                .AdmissionRejected,
            creation.State
        );

        Assert.Null(
            creation.Record
        );
    }

    [Fact]
    public void Validate_RequestedPathTamperIsRejected()
    {
        Fixture fixture =
            CreateFixture();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            CreateDurable(
                fixture
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord changed =
            record with
            {
                RequestedPath =
                    "Meshes/Actors/Character/Other.NIF"
            };

        Assert.NotNull(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                changed
            )
        );
    }

    [Fact]
    public void Validate_SourceAndRequestedMustRemainWindowsLogicalEquivalent()
    {
        Fixture fixture =
            CreateFixture();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            CreateDurable(
                fixture
            );

        string changedSourcePath =
            Path.GetFullPath(
                Path.Combine(
                    fixture.DataRoot,
                    "meshes",
                    "other",
                    "file.nif"
                )
            );

        LinuxFileIdentityResult originalIdentity =
            record.SourceSnapshot.Identity;

        var changedIdentity =
            new LinuxFileIdentityResult(
                FullPath:
                    changedSourcePath,
                DeviceMajor:
                    originalIdentity.DeviceMajor,
                DeviceMinor:
                    originalIdentity.DeviceMinor,
                Inode:
                    originalIdentity.Inode,
                LinkCount:
                    originalIdentity.LinkCount,
                MountId:
                    originalIdentity.MountId,
                Error:
                    originalIdentity.Error
            );

        DataRelativePathRepairSourceSnapshot changedSnapshot =
            record.SourceSnapshot with
            {
                PhysicalPath =
                    changedSourcePath,
                Identity =
                    changedIdentity
            };

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord changed =
            record with
            {
                SourceSnapshot =
                    changedSnapshot
            };

        Assert.NotNull(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                changed
            )
        );
    }

    [Fact]
    public void Validate_CasefoldDestinationParentIsRejected()
    {
        Fixture fixture =
            CreateFixture();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            CreateDurable(
                fixture
            );

        DataRelativePathRepairDestinationParentSnapshot changedParent =
            record.InitialDestinationParentSnapshot with
            {
                CasefoldEnabled =
                    true
            };

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord changed =
            record with
            {
                InitialDestinationParentSnapshot =
                    changedParent
            };

        Assert.NotNull(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                changed
            )
        );
    }

    [Fact]
    public void Validate_OperationKindTamperIsRejected()
    {
        Fixture fixture =
            CreateFixture();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            CreateDurable(
                fixture
            );

        DataRelativePathRepairPlanOperation[] operations =
            record.Operations
                .ToArray();

        operations[0] =
            operations[0] with
            {
                Kind =
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile,
                SourcePath =
                    record.SourceSnapshot.PhysicalPath
            };

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord changed =
            record with
            {
                Operations =
                    operations
            };

        Assert.NotNull(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                changed
            )
        );
    }

    [Fact]
    public void Validate_FinalDestinationTamperIsRejected()
    {
        Fixture fixture =
            CreateFixture();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            CreateDurable(
                fixture
            );

        DataRelativePathRepairPlanOperation[] operations =
            record.Operations
                .ToArray();

        operations[^1] =
            operations[^1] with
            {
                DestinationPath =
                    operations[^1].DestinationPath +
                    ".tampered"
            };

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord changed =
            record with
            {
                Operations =
                    operations
            };

        Assert.NotNull(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                changed
            )
        );
    }

    [Fact]
    public void Validate_FinalFileSourcePathTamperIsRejected()
    {
        Fixture fixture =
            CreateFixture();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            CreateDurable(
                fixture
            );

        DataRelativePathRepairPlanOperation[] operations =
            record.Operations
                .ToArray();

        operations[^1] =
            operations[^1] with
            {
                SourcePath =
                    operations[^1].SourcePath +
                    ".tampered"
            };

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord changed =
            record with
            {
                Operations =
                    operations
            };

        Assert.NotNull(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                changed
            )
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
        CreateDurable(
            Fixture fixture)
    {
        using LinuxNoFollowPathHandle root =
            OpenRoot(
                fixture.DataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creation =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    fixture.Plan
                );

        Assert.True(
            creation.Success,
            creation.Error
        );

        return Assert.IsType<
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
        >(
            creation.Record
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
                "casecompat-targeted-durable-plan-tests",
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
