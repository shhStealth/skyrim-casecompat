using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathTargetedConsumerCaseRepairPlanProjectorTests
{
    [Fact]
    public void Project_FinalComponentMissingProjectsOneCreateFile()
    {
        string dataRoot =
            CreateDataRoot();

        CreateFile(
            dataRoot,
            "meshes/actors/file.nif",
            "source"
        );

        Directory.CreateDirectory(
            Path.Combine(
                dataRoot,
                "Meshes",
                "Actors"
            )
        );

        string requestedPath =
            "Meshes/Actors/File.NIF";

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            CreateCandidate(
                dataRoot,
                requestedPath
            );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection result =
            DataRelativePathTargetedConsumerCaseRepairPlanProjector.Project(
                root,
                candidate
            );

        Assert.True(
            result.HasPlan,
            result.Error
        );

        Assert.Equal(
            2,
            result.FirstMissingComponentIndex
        );

        DataRelativePathRepairPlanOperation operation =
            Assert.Single(
                result.Operations
            );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateFile,
            operation.Kind
        );

        Assert.Equal(
            Path.Combine(
                dataRoot,
                "Meshes",
                "Actors",
                "File.NIF"
            ),
            operation.DestinationPath
        );

        Assert.Equal(
            candidate.SourceSnapshot.PhysicalPath,
            operation.SourcePath
        );

        Assert.Equal(
            Path.Combine(
                dataRoot,
                "Meshes",
                "Actors"
            ),
            result.DestinationParentSnapshot!.PhysicalPath
        );
    }

    [Fact]
    public void
        Project_FirstRequestedDirectoryMissingProjectsDirectoriesThenFile()
    {
        string dataRoot =
            CreateDataRoot();

        CreateFile(
            dataRoot,
            "meshes/actors/file.nif",
            "source"
        );

        string requestedPath =
            "Meshes/Actors/File.NIF";

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            CreateCandidate(
                dataRoot,
                requestedPath
            );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection result =
            DataRelativePathTargetedConsumerCaseRepairPlanProjector.Project(
                root,
                candidate
            );

        Assert.True(
            result.HasPlan,
            result.Error
        );

        Assert.Equal(
            0,
            result.FirstMissingComponentIndex
        );

        Assert.Equal(
            3,
            result.Operations.Count
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateDirectory,
            result.Operations[0].Kind
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateDirectory,
            result.Operations[1].Kind
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateFile,
            result.Operations[2].Kind
        );

        Assert.Equal(
            dataRoot,
            result.DestinationParentSnapshot!.PhysicalPath
        );
    }

    [Fact]
    public void Project_SourceIsNotReacquiredByDestinationShapeProjection()
    {
        string dataRoot =
            CreateDataRoot();

        string sourcePath =
            CreateFile(
                dataRoot,
                "meshes/actors/file.nif",
                "source"
            );

        string requestedPath =
            "Meshes/Actors/File.NIF";

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            CreateCandidate(
                dataRoot,
                requestedPath
            );

        File.Delete(
            sourcePath
        );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection result =
            DataRelativePathTargetedConsumerCaseRepairPlanProjector.Project(
                root,
                candidate
            );

        Assert.True(
            result.HasPlan,
            result.Error
        );

        Assert.Equal(
            candidate.SourceSnapshot.PhysicalPath,
            result.Operations[^1].SourcePath
        );
    }

    [Fact]
    public void Project_ExactDestinationAppearingAfterCandidateIsConflict()
    {
        string dataRoot =
            CreateDataRoot();

        CreateFile(
            dataRoot,
            "meshes/actors/file.nif",
            "source"
        );

        Directory.CreateDirectory(
            Path.Combine(
                dataRoot,
                "Meshes",
                "Actors"
            )
        );

        string requestedPath =
            "Meshes/Actors/File.NIF";

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            CreateCandidate(
                dataRoot,
                requestedPath
            );

        CreateFile(
            dataRoot,
            requestedPath,
            "later destination"
        );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection result =
            DataRelativePathTargetedConsumerCaseRepairPlanProjector.Project(
                root,
                candidate
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                .DestinationConflict,
            result.State
        );

        Assert.False(
            result.HasPlan
        );

        Assert.Empty(
            result.Operations
        );
    }

    [Fact]
    public void Project_SymbolicLinkInExactDestinationPrefixIsConflict()
    {
        string dataRoot =
            CreateDataRoot();

        CreateFile(
            dataRoot,
            "meshes/actors/file.nif",
            "source"
        );

        string requestedPath =
            "Meshes/Actors/File.NIF";

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            CreateCandidate(
                dataRoot,
                requestedPath
            );

        Directory.CreateSymbolicLink(
            Path.Combine(
                dataRoot,
                "Meshes"
            ),
            Path.Combine(
                dataRoot,
                "meshes"
            )
        );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection result =
            DataRelativePathTargetedConsumerCaseRepairPlanProjector.Project(
                root,
                candidate
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                .DestinationConflict,
            result.State
        );

        Assert.False(
            result.HasPlan
        );
    }

    [Fact]
    public void Project_NonDirectoryInExactDestinationPrefixIsConflict()
    {
        string dataRoot =
            CreateDataRoot();

        CreateFile(
            dataRoot,
            "meshes/actors/file.nif",
            "source"
        );

        string requestedPath =
            "Meshes/Actors/File.NIF";

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            CreateCandidate(
                dataRoot,
                requestedPath
            );

        File.WriteAllText(
            Path.Combine(
                dataRoot,
                "Meshes"
            ),
            "not a directory"
        );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection result =
            DataRelativePathTargetedConsumerCaseRepairPlanProjector.Project(
                root,
                candidate
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                .DestinationConflict,
            result.State
        );

        Assert.False(
            result.HasPlan
        );
    }

    [Fact]
    public void Project_DifferentTrustedDataRootFailsClosed()
    {
        string candidateDataRoot =
            CreateDataRoot();

        CreateFile(
            candidateDataRoot,
            "meshes/actors/file.nif",
            "source"
        );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            CreateCandidate(
                candidateDataRoot,
                "Meshes/Actors/File.NIF"
            );

        string otherDataRoot =
            CreateDataRoot();

        using LinuxNoFollowPathHandle otherRoot =
            OpenRoot(
                otherDataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection result =
            DataRelativePathTargetedConsumerCaseRepairPlanProjector.Project(
                otherRoot,
                candidate
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                .DataRootMismatch,
            result.State
        );

        Assert.False(
            result.HasPlan
        );

        Assert.Empty(
            result.Operations
        );
    }

    [Fact]
    public void Project_UsesExactAuthoritativeRequestedCaseForEveryOperation()
    {
        string dataRoot =
            CreateDataRoot();

        CreateFile(
            dataRoot,
            "meshes/actors/character/file.nif",
            "source"
        );

        string requestedPath =
            "Meshes/Actors/Character/File.NIF";

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            CreateCandidate(
                dataRoot,
                requestedPath
            );

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection result =
            DataRelativePathTargetedConsumerCaseRepairPlanProjector.Project(
                root,
                candidate
            );

        Assert.True(
            result.HasPlan,
            result.Error
        );

        Assert.Collection(
            result.Operations,
            first =>
            {
                Assert.Equal(
                    Path.Combine(
                        dataRoot,
                        "Meshes"
                    ),
                    first.DestinationPath
                );
            },
            second =>
            {
                Assert.Equal(
                    Path.Combine(
                        dataRoot,
                        "Meshes",
                        "Actors"
                    ),
                    second.DestinationPath
                );
            },
            third =>
            {
                Assert.Equal(
                    Path.Combine(
                        dataRoot,
                        "Meshes",
                        "Actors",
                        "Character"
                    ),
                    third.DestinationPath
                );
            },
            fourth =>
            {
                Assert.Equal(
                    Path.Combine(
                        dataRoot,
                        "Meshes",
                        "Actors",
                        "Character",
                        "File.NIF"
                    ),
                    fourth.DestinationPath
                );

                Assert.Equal(
                    candidate.SourceSnapshot.PhysicalPath,
                    fourth.SourcePath
                );
            }
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairCandidate
        CreateCandidate(
            string dataRoot,
            string requestedPath)
    {
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
            projection =
                DataRelativePathTargetedConsumerCaseRepairCandidateProjector
                    .Project(
                        dataRoot,
                        consumer,
                        current
                    );

        Assert.True(
            projection.HasCandidate,
            projection.Error
        );

        return Assert.IsType<
            DataRelativePathTargetedConsumerCaseRepairCandidate
        >(
            projection.Candidate
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
                "casecompat-targeted-plan-tests",
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
}
