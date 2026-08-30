using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairPlanProjectorTests
{
    [Fact]
    public void Project_DirectStrictMismatch_ProducesCreateOnlyPlanWithoutWriting()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "00Taliesin",
                    "freehorse"
                )
            ).FullName;

        string physicalFile =
            Path.Combine(
                physicalDirectory,
                "imperialsaddle.nif"
            );

        const string content =
            "projector-fixture";

        File.WriteAllText(
            physicalFile,
            content
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/00Taliesin/FreeHorse/" +
                "imperialsaddle.nif"
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );

        string requestedDirectory =
            Path.Combine(
                dataRoot,
                "meshes",
                "00Taliesin",
                "FreeHorse"
            );

        string requestedFile =
            Path.Combine(
                requestedDirectory,
                "imperialsaddle.nif"
            );

        Assert.False(
            Directory.Exists(
                requestedDirectory
            )
        );

        Assert.False(
            File.Exists(
                requestedFile
            )
        );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .Projected,
            projection.State
        );

        Assert.True(
            projection.HasPlan
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            projection.TopologyState
        );

        DataRelativePathRepairSourceSnapshot snapshot =
            Assert.IsType<
                DataRelativePathRepairSourceSnapshot
            >(
                projection.SourceSnapshot
            );

        Assert.Equal(
            Path.GetFullPath(
                physicalFile
            ),
            snapshot.PhysicalPath
        );

        Assert.Equal(
            Encoding.UTF8.GetByteCount(
                content
            ),
            snapshot.Size
        );

        string expectedHash =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        content
                    )
                )
            );

        Assert.Equal(
            expectedHash,
            snapshot.Sha256
        );

        Assert.True(
            snapshot.Identity.Success
        );

        Assert.Equal(
            Path.GetFullPath(
                physicalFile
            ),
            snapshot.Identity.FullPath
        );

        Assert.Equal(
            2,
            projection.Operations.Count
        );

        DataRelativePathRepairPlanOperation createDirectory =
            projection.Operations[0];

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateDirectory,
            createDirectory.Kind
        );

        Assert.Equal(
            requestedDirectory,
            createDirectory.DestinationPath
        );

        Assert.Null(
            createDirectory.SourcePath
        );

        DataRelativePathRepairPlanOperation createFile =
            projection.Operations[1];

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateFile,
            createFile.Kind
        );

        Assert.Equal(
            requestedFile,
            createFile.DestinationPath
        );

        Assert.Equal(
            Path.GetFullPath(
                physicalFile
            ),
            createFile.SourcePath
        );

        Assert.Null(
            projection.Error
        );

        // Projection is strictly read-only.
        Assert.False(
            Directory.Exists(
                requestedDirectory
            )
        );

        Assert.False(
            File.Exists(
                requestedFile
            )
        );

        Assert.True(
            File.Exists(
                physicalFile
            )
        );
    }

    [Fact]
    public void Project_DestinationAppearsAfterResolution_IsBlocked()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "00Taliesin",
                    "freehorse"
                )
            ).FullName;

        string physicalFile =
            Path.Combine(
                physicalDirectory,
                "imperialsaddle.nif"
            );

        File.WriteAllText(
            physicalFile,
            "destination-conflict-fixture"
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/00Taliesin/FreeHorse/" +
                "imperialsaddle.nif"
            );

        string requestedDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "00Taliesin",
                    "FreeHorse"
                )
            ).FullName;

        string requestedFile =
            Path.Combine(
                requestedDirectory,
                "imperialsaddle.nif"
            );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .DestinationConflict,
            projection.State
        );

        Assert.False(
            projection.HasPlan
        );

        Assert.Empty(
            projection.Operations
        );

        Assert.False(
            File.Exists(
                requestedFile
            )
        );
    }

    [Fact]
    public void Project_SourceDisappearsAfterResolution_IsBlocked()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "00Taliesin",
                    "freehorse"
                )
            ).FullName;

        string physicalFile =
            Path.Combine(
                physicalDirectory,
                "imperialsaddle.nif"
            );

        File.WriteAllText(
            physicalFile,
            "source-disappears-fixture"
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/00Taliesin/FreeHorse/" +
                "imperialsaddle.nif"
            );

        File.Delete(
            physicalFile
        );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .SourceUnavailable,
            projection.State
        );

        Assert.False(
            projection.HasPlan
        );

        Assert.Null(
            projection.SourceSnapshot
        );

        Assert.Empty(
            projection.Operations
        );
    }

    [Fact]
    public void Project_AlternatePhysicalHierarchy_IsNotProjected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        Directory.CreateDirectory(
            Path.Combine(
                dataRoot,
                "meshes",
                "Actors"
            )
        );

        string alternateDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "actors",
                    "atronachflame"
                )
            ).FullName;

        string physicalFile =
            Path.Combine(
                alternateDirectory,
                "fixture.nif"
            );

        File.WriteAllText(
            physicalFile,
            "alternate-hierarchy-fixture"
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/Actors/AtronachFlame/" +
                "fixture.nif"
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateBranchesBeforeFailure,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .NotDirectStrictCaseMismatch,
            projection.State
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateBranchesBeforeFailure,
            projection.TopologyState
        );

        Assert.False(
            projection.HasPlan
        );

        Assert.Null(
            projection.SourceSnapshot
        );

        Assert.Empty(
            projection.Operations
        );
    }

    private static string CreateDataRoot(
        TemporaryDirectory temp)
    {
        return Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                "Data"
            )
        ).FullName;
    }

    private static DataRelativePathResolution Resolve(
        string dataRoot,
        string requestedPath)
    {
        return DataRelativePathResolver.ResolveFile(
            dataRoot,
            requestedPath,
            path =>
                InspectFixtureCasefold(
                    path,
                    dataRoot
                )
        );
    }

    private static DirectoryCasefoldResult
        InspectFixtureCasefold(
            string path,
            string dataRoot)
    {
        string fullPath =
            Path.GetFullPath(
                path
            );

        bool casefoldEnabled =
            string.Equals(
                fullPath,
                Path.GetFullPath(
                    dataRoot
                ),
                StringComparison.Ordinal
            );

        return new DirectoryCasefoldResult(
            FullPath:
                fullPath,
            Exists:
                Directory.Exists(
                    fullPath
                ),
            CasefoldEnabled:
                casefoldEnabled,
            RawFlags:
                casefoldEnabled
                    ? LinuxDirectoryFlags
                        .FsCasefoldFlag
                    : 0L,
            Error:
                null
        );
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-repair-plan-tests",
                    Guid.NewGuid()
                        .ToString(
                            "N"
                        )
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (
                Directory.Exists(
                    RootPath
                ))
            {
                Directory.Delete(
                    RootPath,
                    recursive:
                        true
                );
            }
        }
    }
}
