using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Reflection;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzerTests
{
    [Fact]
    public void Analyze_ExactSingleRepresentation_ReturnsStableEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string file =
            fixture.Write(
                "meshes/Test/File.nif",
                "one"
            );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Test/File.nif"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            new[]
            {
                "meshes"
            },
            result.PhysicalRootNames
        );

        DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
            representation =
                Assert.Single(
                    result.Representations
                );

        Assert.Equal(
            "meshes/Test/File.nif",
            representation.RelativePath
        );

        Assert.Equal(
            new FileInfo(file).Length,
            representation.Size
        );

        Assert.True(
            representation.IncarnationIdentity.Success
        );
    }

    [Fact]
    public void Analyze_NestedWindowsEquivalentBranches_ReturnsEveryRepresentation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.Write(
            "meshes/Actors/File.nif",
            "a"
        );

        fixture.Write(
            "meshes/actors/file.NIF",
            "b"
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Actors/File.nif"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            2,
            result.Representations.Count
        );

        Assert.Equal(
            new[]
            {
                "meshes/Actors/File.nif",
                "meshes/actors/file.NIF"
            },
            result.Representations
                .Select(
                    representation =>
                        representation.RelativePath
                )
                .ToArray()
        );
    }

    [Fact]
    public void Analyze_MissingLogicalLeaf_ReturnsZeroRepresentations()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "meshes/Test"
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Test/Missing.nif"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Empty(
            result.Representations
        );
    }

    [Fact]
    public void Analyze_NewEquivalentDataRoot_IsIncludedInCurrentRootSet()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.Write(
            "meshes/File.nif",
            "a"
        );

        fixture.Write(
            "MESHES/file.NIF",
            "b"
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/File.nif"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            new[]
            {
                "MESHES",
                "meshes"
            },
            result.PhysicalRootNames
        );

        Assert.Equal(
            2,
            result.Representations.Count
        );
    }

    [Fact]
    public void Analyze_IntermediateSymbolicLink_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "meshes"
        );

        string outside =
            fixture.CreateDirectory(
                "outside"
            );

        Directory.CreateSymbolicLink(
            Path.Combine(
                fixture.DataRoot,
                "meshes",
                "Actors"
            ),
            outside
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Actors/File.nif"
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .IntermediateEquivalentObjectConflict,
            result.State
        );
    }

    [Fact]
    public void Analyze_FinalSymbolicLink_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.Write(
            "target.nif",
            "target"
        );

        fixture.CreateDirectory(
            "meshes/Test"
        );

        File.CreateSymbolicLink(
            Path.Combine(
                fixture.DataRoot,
                "meshes",
                "Test",
                "File.nif"
            ),
            Path.Combine(
                fixture.DataRoot,
                "target.nif"
            )
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Test/File.nif"
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .FinalEquivalentObjectConflict,
            result.State
        );
    }

    [Fact]
    public void Analyze_FileDirectoryCollision_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "meshes/Actors"
        );

        fixture.Write(
            "meshes/actors",
            "file-collision"
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Actors/File.nif"
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .IntermediateEquivalentObjectConflict,
            result.State
        );
    }

    [Fact]
    public void Analyze_ContentChangeDuringObservation_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string file =
            fixture.Write(
                "meshes/Test/File.nif",
                "before"
            );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.AnalyzeCore(
                "meshes/Test/File.nif",
                afterContent:
                    () =>
                        File.WriteAllText(
                            file,
                            "after-content-change"
                        )
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .HierarchyRevalidationFailed,
            result.State
        );
    }

    [Fact]
    public void Analyze_FinalNameReplacementAfterObservation_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string file =
            fixture.Write(
                "meshes/Test/File.nif",
                "same"
            );

        string backup =
            file + ".old";

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.AnalyzeCore(
                "meshes/Test/File.nif",
                afterContent:
                    () =>
                    {
                        File.Move(
                            file,
                            backup
                        );

                        File.WriteAllText(
                            file,
                            "same"
                        );
                    }
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .HierarchyRevalidationFailed,
            result.State
        );
    }

    [Fact]
    public void Analyze_DirectoryReplacementDuringTraversal_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string directory =
            fixture.CreateDirectory(
                "meshes/Actors"
            );

        string file =
            Path.Combine(
                directory,
                "File.nif"
            );

        File.WriteAllText(
            file,
            "same"
        );

        string backup =
            directory + ".old";

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.AnalyzeCore(
                "meshes/Actors/File.nif",
                afterContent:
                    () =>
                    {
                        Directory.Move(
                            directory,
                            backup
                        );

                        Directory.CreateDirectory(
                            directory
                        );

                        File.WriteAllText(
                            file,
                            "same"
                        );
                    }
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .HierarchyRevalidationFailed,
            result.State
        );
    }

    private sealed class Fixture :
        IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-c4a-current-leaf-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Data"
                    )
                ).FullName;

            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    DataRoot
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            DataRootHandle =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    opened.OpenedPath
                );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public LinuxNoFollowPathHandle DataRootHandle { get; }

        public string CreateDirectory(
            string relativePath)
        {
            return Directory.CreateDirectory(
                Path.Combine(
                    DataRoot,
                    relativePath
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar
                        )
                )
            ).FullName;
        }

        public string Write(
            string relativePath,
            string content)
        {
            string path =
                Path.Combine(
                    DataRoot,
                    relativePath
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar
                        )
                );

            string? parent =
                Path.GetDirectoryName(
                    path
                );

            if (parent is not null)
            {
                Directory.CreateDirectory(
                    parent
                );
            }

            File.WriteAllText(
                path,
                content
            );

            return path;
        }

        public DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
            Analyze(
                string requestedPath)
        {
            return
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                    .Analyze(
                        DataRootHandle,
                        "MESHES",
                        requestedPath
                    );
        }

        public DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
            AnalyzeCore(
                string requestedPath,
                Action? afterRoot = null,
                Action? afterContent = null)
        {
            MethodInfo method =
                Assert.IsType<MethodInfo>(
                    typeof(
                        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                    )
                    .GetMethod(
                        "AnalyzeCore",
                        BindingFlags.Static |
                        BindingFlags.NonPublic
                    ),
                    exactMatch: false
                );

            object? result =
                method.Invoke(
                    null,
                    new object?[]
                    {
                        DataRootHandle,
                        "MESHES",
                        requestedPath,
                        afterRoot,
                        afterContent
                    }
                );

            return Assert.IsType<
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
            >(
                result
            );
        }

        public void Dispose()
        {
            DataRootHandle.Dispose();

            if (
                Directory.Exists(
                    RootPath))
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
