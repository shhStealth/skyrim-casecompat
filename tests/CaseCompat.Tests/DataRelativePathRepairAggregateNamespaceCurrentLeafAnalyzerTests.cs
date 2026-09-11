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
    public void
        Analyze_UnregisteredSymlinkWithAliasesDirectoryProvided_IsStillRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Providing an aliases directory must never weaken this
        // analyzer's blanket "reject any symlink it didn't create
        // itself" policy - an arbitrary, foreign symlink the registry
        // knows nothing about is refused exactly as before.
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

        string aliasesDirectoryPath =
            fixture.CreateDirectory(
                "../Aliases"
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            OpenAliasesDirectory(
                aliasesDirectoryPath
            );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Actors/File.nif",
                aliasesDirectory
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .IntermediateEquivalentObjectConflict,
            result.State
        );
    }

    [Fact]
    public void
        Analyze_IntermediateKnownAlias_WithoutAliasesDirectory_IsStillRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Regression check: omitting aliasesDirectory (the default)
        // must preserve today's behavior exactly, even for a symlink
        // that genuinely is a registered alias.
        using Fixture fixture =
            new();

        fixture.Write(
            "meshes/actors/DeeperFile.nif",
            "deeper"
        );

        string aliasesDirectoryPath =
            fixture.CreateDirectory(
                "../Aliases"
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            OpenAliasesDirectory(
                aliasesDirectoryPath
            );

        fixture.CreateAlias(
            aliasesDirectory,
            "meshes",
            linkName:
                "Actors",
            targetName:
                "actors"
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Actors/DeeperFile.nif"
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .IntermediateEquivalentObjectConflict,
            result.State
        );
    }

    [Fact]
    public void
        Analyze_IntermediateKnownAlias_WithAliasesDirectory_ResolvesThroughRealTarget()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // The actual "subsequent run" scenario this fix exists for: a
        // real directory ("actors") and a previously-created alias
        // ("Actors") both exist. A requested path traversing the
        // aliased name must resolve through the one real directory
        // instead of being refused as an unresolved conflict.
        using Fixture fixture =
            new();

        fixture.Write(
            "meshes/actors/DeeperFile.nif",
            "deeper"
        );

        string aliasesDirectoryPath =
            fixture.CreateDirectory(
                "../Aliases"
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            OpenAliasesDirectory(
                aliasesDirectoryPath
            );

        fixture.CreateAlias(
            aliasesDirectory,
            "meshes",
            linkName:
                "Actors",
            targetName:
                "actors"
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Actors/DeeperFile.nif",
                aliasesDirectory
            );

        Assert.True(
            result.Success,
            result.Error
        );

        DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
            representation =
                Assert.Single(
                    result.Representations
                );

        Assert.Equal(
            "meshes/actors/DeeperFile.nif",
            representation.RelativePath
        );
    }

    [Fact]
    public void
        Analyze_RootLevelKnownAlias_WithAliasesDirectory_ResolvesThroughRealTarget()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Same fix, exercised at the Data-root level (the top-level
        // path component) rather than an intermediate directory - a
        // structurally distinct code path in this analyzer.
        using Fixture fixture =
            new();

        fixture.Write(
            "meshes/Test/File.nif",
            "root-level"
        );

        string aliasesDirectoryPath =
            fixture.CreateDirectory(
                "../Aliases"
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            OpenAliasesDirectory(
                aliasesDirectoryPath
            );

        fixture.CreateAlias(
            aliasesDirectory,
            parentRelativePath:
                "",
            linkName:
                "Meshes",
            targetName:
                "meshes"
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "Meshes/Test/File.nif",
                aliasesDirectory
            );

        Assert.True(
            result.Success,
            result.Error
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
    public void Analyze_FinalDirectory_ProducesNoRepresentationRatherThanConflict()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // A directory happening to occupy the exact requested leaf's name
        // (observed on a real install: a vanilla Dragonborn Static record
        // whose Model path names a directory, not a mesh) is not a
        // case-sensitivity problem - no rename could ever satisfy it. This
        // must be treated the same as the name matching nothing at all,
        // not as an equivalent-object conflict that aborts the whole
        // discovery batch. Contrast with Analyze_FinalSymbolicLink_IsRejected
        // above: a symlink at this same position is deliberately NOT given
        // this treatment.
        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "meshes/Test/File.nif"
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Test/File.nif"
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .Analyzed,
            result.State
        );

        Assert.Empty(
            result.Representations
        );
    }

    [Fact]
    public void
        Analyze_FinalVerifiedAlias_ProducesNoRepresentationRatherThanConflict()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Observed on a real install: an alias created to resolve a
        // contested ANCESTOR for unrelated files nested under it can
        // coincidentally occupy the exact same name a separate,
        // malformed leaf-level request also names (a vanilla Dragonborn
        // Static record whose Model path names a directory - see
        // Analyze_FinalDirectory_ProducesNoRepresentationRatherThanConflict
        // above - happened to share its name with an ancestor this
        // project's own alias system had already resolved for other
        // files). The alias's real target is itself a directory, so
        // this must still be treated as "nothing here to fix" for the
        // leaf request, not FinalEquivalentObjectConflict. Contrast with
        // Analyze_FinalSymbolicLink_IsRejected: an UNVERIFIED symlink at
        // this same position is deliberately NOT given this treatment.
        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "meshes/Test/file.nif"
        );

        string aliasesDirectoryPath =
            fixture.CreateDirectory(
                "../Aliases"
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            OpenAliasesDirectory(
                aliasesDirectoryPath
            );

        fixture.CreateAlias(
            aliasesDirectory,
            "meshes/Test",
            linkName:
                "File.nif",
            targetName:
                "file.nif"
        );

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis result =
            fixture.Analyze(
                "meshes/Test/File.nif",
                aliasesDirectory
            );

        Assert.Equal(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .Analyzed,
            result.State
        );

        Assert.Empty(
            result.Representations
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

    private static LinuxNoFollowPathHandle OpenAliasesDirectory(
        string path)
    {
        LinuxNoFollowPathOpenResult opened =
            LinuxNoFollowPath.OpenRootReadOnly(
                path
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
                string requestedPath,
                LinuxNoFollowPathHandle? aliasesDirectory = null)
        {
            return
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                    .Analyze(
                        DataRootHandle,
                        "MESHES",
                        requestedPath,
                        aliasesDirectory
                    );
        }

        // Creates a real symlink alias exactly as the apply executor
        // does (LinuxCreateSymlinkAt plus a matching durable registry
        // record), so tests can exercise this analyzer against a
        // genuine, previously-created alias rather than an ad hoc
        // symlink the registry knows nothing about.
        public void CreateAlias(
            LinuxNoFollowPathHandle aliasesDirectory,
            string parentRelativePath,
            string linkName,
            string targetName)
        {
            string parentPath =
                string.IsNullOrEmpty(
                    parentRelativePath)
                    ? DataRoot
                    : Path.Combine(
                        DataRoot,
                        parentRelativePath
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar
                            )
                    );

            LinuxNoFollowPathOpenResult parentOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    parentPath
                );

            Assert.True(
                parentOpen.Success,
                parentOpen.Error
            );

            using LinuxNoFollowPathHandle parent =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    parentOpen.OpenedPath
                );

            LinuxCreateSymlinkAtResult create =
                LinuxCreateSymlinkAt.Create(
                    parent,
                    linkName,
                    targetName
                );

            Assert.True(
                create.Success,
                create.Error
            );

            LinuxFileIdentityResult targetIdentity =
                LinuxFileIdentity.Inspect(
                    Path.Combine(
                        parentPath,
                        targetName
                    )
                );

            Assert.True(
                targetIdentity.Success,
                targetIdentity.Error
            );

            var record =
                new DataRelativePathRepairAliasRecord(
                    SchemaVersion:
                        DataRelativePathRepairAliasRecord.CurrentSchemaVersion,
                    PlanId:
                        Guid.NewGuid(),
                    CreatedUtc:
                        DateTimeOffset.UtcNow,
                    DataRoot:
                        DataRoot,
                    ParentPath:
                        parentPath,
                    LinkName:
                        linkName,
                    TargetName:
                        targetName,
                    TargetIdentity:
                        targetIdentity
                );

            DataRelativePathRepairAliasRegistryRecordResult registered =
                DataRelativePathRepairAliasRegistry.Record(
                    aliasesDirectory,
                    record
                );

            Assert.True(
                registered.Success,
                registered.Error
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
                        null,
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
