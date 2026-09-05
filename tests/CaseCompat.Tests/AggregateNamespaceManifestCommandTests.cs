using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;

namespace CaseCompat.Tests;

public sealed class
    AggregateNamespaceManifestCommandTests
{
    private static readonly DateTimeOffset CreatedUtc =
        new(
            year:
                2026,
            month:
                9,
            day:
                5,
            hour:
                12,
            minute:
                0,
            second:
                0,
            offset:
                TimeSpan.Zero
        );

    [Fact]
    public void
        Run_CompleteNamespace_PublishesExactSchemaV1Manifest()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new NamespaceFixture();

        if (
            !fixture.SupportsDistinctCaseRoots() ||
            !SupportsManifestPublication(
                fixture.OutputDirectory))
        {
            return;
        }

        WindowsNamespaceAnalysis expectedNamespace =
            WindowsNamespaceAnalyzer.Analyze(
                fixture.DataRoot,
                "Meshes"
            );

        Assert.True(
            expectedNamespace.Complete,
            string.Join(
                Environment.NewLine,
                expectedNamespace.Errors
            )
        );

        WindowsNamespaceRegularFileContentAnalysis
            expectedContent =
                WindowsNamespaceRegularFileContentAnalyzer
                    .Analyze(
                        expectedNamespace
                    );

        Assert.True(
            expectedContent.Complete,
            string.Join(
                Environment.NewLine,
                expectedContent.Errors
            )
        );

        DataRelativePathAggregateNamespaceManifestRecord
            expectedManifest =
                WindowsNamespaceAggregateManifestProjector
                    .Project(
                        expectedNamespace,
                        expectedContent,
                        CreatedUtc
                    );

        byte[] expectedBytes =
            DataRelativePathAggregateNamespaceManifestJson
                .Serialize(
                    expectedManifest
                );

        string expectedSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    expectedBytes
                )
            );

        int result =
            global::AggregateNamespaceManifestCommand.Run(
                [
                    "aggregate-namespace-manifest",
                    fixture.DataRoot,
                    "Meshes",
                    fixture.OutputDirectory
                ],
                createdUtcOverride:
                    CreatedUtc,
                afterManifestPublishBeforeRead:
                    null
            );

        Assert.Equal(
            0,
            result
        );

        using LinuxNoFollowPathHandle output =
            OpenDirectory(
                fixture.OutputDirectory
            );

        DataRelativePathAggregateNamespaceManifestReaderResult
            read =
                DataRelativePathAggregateNamespaceManifestReader
                    .Read(
                        output,
                        global::AggregateNamespaceManifestCommand
                            .DefaultManifestName
                    );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            expectedBytes.LongLength,
            read.Length
        );

        Assert.Equal(
            expectedSha256,
            read.ManifestSha256
        );

        DataRelativePathAggregateNamespaceManifestRecord
            manifest =
                Assert.IsType<
                    DataRelativePathAggregateNamespaceManifestRecord
                >(
                    read.Manifest
                );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestRecord
                .SchemaVersion1,
            manifest.SchemaVersion
        );

        Assert.Equal(
            CreatedUtc,
            manifest.CreatedUtc
        );

        Assert.Equal(
            Path.GetFullPath(
                fixture.DataRoot
            ),
            manifest.DataRoot
        );

        Assert.Equal(
            "MESHES",
            manifest.RootWindowsLogicalPath
        );

        Assert.Equal(
            3,
            manifest.LogicalLeaves.Count
        );

        Assert.Contains(
            manifest.LogicalLeaves,
            leaf =>
                leaf.State ==
                DataRelativePathAggregateLogicalLeafState
                    .UniqueRepresentation
        );

        Assert.Contains(
            manifest.LogicalLeaves,
            leaf =>
                leaf.State ==
                DataRelativePathAggregateLogicalLeafState
                    .EquivalentContentMultipleRepresentations
        );

        Assert.Contains(
            manifest.LogicalLeaves,
            leaf =>
                leaf.State ==
                DataRelativePathAggregateLogicalLeafState
                    .ConflictingContentMultipleRepresentations
        );

        Assert.Contains(
            "Textures",
            manifest.DataRootChildNames
        );

        Assert.True(
            File.Exists(
                fixture.UniquePath
            )
        );

        Assert.True(
            File.Exists(
                fixture.SharedUpperPath
            )
        );

        Assert.True(
            File.Exists(
                fixture.SharedLowerPath
            )
        );
    }

    [Fact]
    public void
        Run_InvalidNamespaceName_IsRejectedWithoutPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new NamespaceFixture();

        int result =
            global::AggregateNamespaceManifestCommand.Run(
                [
                    "aggregate-namespace-manifest",
                    fixture.DataRoot,
                    "Meshes/Foo",
                    fixture.OutputDirectory
                ],
                createdUtcOverride:
                    CreatedUtc,
                afterManifestPublishBeforeRead:
                    null
            );

        Assert.Equal(
            2,
            result
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                fixture.OutputDirectory
            )
        );
    }

    [Fact]
    public void
        Run_OutputDirectoryInsideData_IsRejectedWithoutPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new NamespaceFixture();

        string insideData =
            Directory.CreateDirectory(
                Path.Combine(
                    fixture.DataRoot,
                    "CaseCompatEvidence"
                )
            ).FullName;

        int result =
            global::AggregateNamespaceManifestCommand.Run(
                [
                    "aggregate-namespace-manifest",
                    fixture.DataRoot,
                    "Meshes",
                    insideData
                ],
                createdUtcOverride:
                    CreatedUtc,
                afterManifestPublishBeforeRead:
                    null
            );

        Assert.Equal(
            3,
            result
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                insideData
            )
        );
    }

    [Fact]
    public void
        Run_MissingNamespace_FailsClosedWithoutPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new NamespaceFixture();

        int result =
            global::AggregateNamespaceManifestCommand.Run(
                [
                    "aggregate-namespace-manifest",
                    fixture.DataRoot,
                    "Scripts",
                    fixture.OutputDirectory
                ],
                createdUtcOverride:
                    CreatedUtc,
                afterManifestPublishBeforeRead:
                    null
            );

        Assert.Equal(
            4,
            result
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                fixture.OutputDirectory
            )
        );
    }

    [Fact]
    public void
        Run_IncompleteStableContentEvidence_FailsClosedWithoutPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new NamespaceFixture();

        var incompleteContent =
            new WindowsNamespaceRegularFileContentAnalysis(
                Array.Empty<
                    WindowsNamespaceRegularFileContentNodeAnalysis
                >(),
                new[]
                {
                    "Injected incomplete stable content evidence."
                }
            );

        int result =
            global::AggregateNamespaceManifestCommand.Run(
                [
                    "aggregate-namespace-manifest",
                    fixture.DataRoot,
                    "Textures",
                    fixture.OutputDirectory
                ],
                createdUtcOverride:
                    CreatedUtc,
                afterManifestPublishBeforeRead:
                    null,
                contentAnalysisOverride:
                    _ => incompleteContent
            );

        Assert.Equal(
            5,
            result
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                fixture.OutputDirectory
            )
        );
    }

    [Fact]
    public void
        Run_ExistingManifest_IsNotOverwritten()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new NamespaceFixture();

        if (
            !fixture.SupportsDistinctCaseRoots() ||
            !SupportsManifestPublication(
                fixture.OutputDirectory))
        {
            return;
        }

        string manifestPath =
            Path.Combine(
                fixture.OutputDirectory,
                global::AggregateNamespaceManifestCommand
                    .DefaultManifestName
            );

        byte[] original =
            "do-not-overwrite"u8.ToArray();

        File.WriteAllBytes(
            manifestPath,
            original
        );

        int result =
            global::AggregateNamespaceManifestCommand.Run(
                [
                    "aggregate-namespace-manifest",
                    fixture.DataRoot,
                    "Meshes",
                    fixture.OutputDirectory
                ],
                createdUtcOverride:
                    CreatedUtc,
                afterManifestPublishBeforeRead:
                    null
            );

        Assert.Equal(
            7,
            result
        );

        Assert.Equal(
            original,
            File.ReadAllBytes(
                manifestPath
            )
        );
    }

    [Fact]
    public void
        Run_PostPublicationValidByteChange_IsRejectedByExactShaReadback()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new NamespaceFixture();

        if (
            !fixture.SupportsDistinctCaseRoots() ||
            !SupportsManifestPublication(
                fixture.OutputDirectory))
        {
            return;
        }

        string manifestPath =
            Path.Combine(
                fixture.OutputDirectory,
                global::AggregateNamespaceManifestCommand
                    .DefaultManifestName
            );

        int result =
            global::AggregateNamespaceManifestCommand.Run(
                [
                    "aggregate-namespace-manifest",
                    fixture.DataRoot,
                    "Meshes",
                    fixture.OutputDirectory
                ],
                createdUtcOverride:
                    CreatedUtc,
                afterManifestPublishBeforeRead:
                    (
                        _,
                        _,
                        manifest) =>
                    {
                        DataRelativePathAggregateNamespaceManifestRecord
                            changed =
                                manifest with
                                {
                                    CreatedUtc =
                                        CreatedUtc.AddSeconds(
                                            1
                                        )
                                };

                        File.WriteAllBytes(
                            manifestPath,
                            DataRelativePathAggregateNamespaceManifestJson
                                .Serialize(
                                    changed
                                )
                        );
                    }
            );

        Assert.Equal(
            8,
            result
        );

        using LinuxNoFollowPathHandle output =
            OpenDirectory(
                fixture.OutputDirectory
            );

        DataRelativePathAggregateNamespaceManifestReaderResult
            read =
                DataRelativePathAggregateNamespaceManifestReader
                    .Read(
                        output,
                        global::AggregateNamespaceManifestCommand
                            .DefaultManifestName
                    );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            CreatedUtc.AddSeconds(
                1
            ),
            read.Manifest!.CreatedUtc
        );
    }

    [Fact]
    public void
        Run_MissingOutputDirectory_IsNotCreated()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new NamespaceFixture();

        string missingOutput =
            Path.Combine(
                fixture.RootPath,
                "DoesNotExist"
            );

        int result =
            global::AggregateNamespaceManifestCommand.Run(
                [
                    "aggregate-namespace-manifest",
                    fixture.DataRoot,
                    "Meshes",
                    missingOutput
                ],
                createdUtcOverride:
                    CreatedUtc,
                afterManifestPublishBeforeRead:
                    null
            );

        Assert.Equal(
            3,
            result
        );

        Assert.False(
            Directory.Exists(
                missingOutput
            )
        );
    }

    private static LinuxNoFollowPathHandle
        OpenDirectory(
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

    private static bool SupportsManifestPublication(
        string directoryPath)
    {
        using LinuxNoFollowPathHandle directory =
            OpenDirectory(
                directoryPath
            );

        LinuxCreateUnnamedFileAtResult probe =
            LinuxCreateUnnamedFileAt.Create(
                directory
            );

        if (
            probe.State ==
            LinuxCreateUnnamedFileAtState
                .TmpfileUnsupported)
        {
            return false;
        }

        Assert.True(
            probe.Success,
            probe.Error
        );

        probe.OpenedFile!.Dispose();

        return true;
    }

    private sealed class NamespaceFixture :
        IDisposable
    {
        public NamespaceFixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-aggregate-namespace-cli-tests-" +
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );

            DataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Data"
                    )
                ).FullName;

            OutputDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Evidence"
                    )
                ).FullName;

            string upperFoo =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "Meshes",
                        "Foo"
                    )
                ).FullName;

            string lowerFoo =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        "foo"
                    )
                ).FullName;

            Directory.CreateDirectory(
                Path.Combine(
                    DataRoot,
                    "Textures"
                )
            );

            UniquePath =
                Path.Combine(
                    upperFoo,
                    "Unique.nif"
                );

            SharedUpperPath =
                Path.Combine(
                    upperFoo,
                    "Shared.nif"
                );

            SharedLowerPath =
                Path.Combine(
                    lowerFoo,
                    "shared.nif"
                );

            string conflictUpper =
                Path.Combine(
                    upperFoo,
                    "Conflict.nif"
                );

            string conflictLower =
                Path.Combine(
                    lowerFoo,
                    "conflict.nif"
                );

            File.WriteAllText(
                UniquePath,
                "unique"
            );

            File.WriteAllText(
                SharedUpperPath,
                "shared"
            );

            File.WriteAllText(
                SharedLowerPath,
                "shared"
            );

            File.WriteAllText(
                conflictUpper,
                "left"
            );

            File.WriteAllText(
                conflictLower,
                "right"
            );

            File.WriteAllText(
                Path.Combine(
                    DataRoot,
                    "Textures",
                    "Unrelated.dds"
                ),
                "unrelated"
            );
        }

        public string RootPath
        {
            get;
        }

        public string DataRoot
        {
            get;
        }

        public string OutputDirectory
        {
            get;
        }

        public string UniquePath
        {
            get;
        }

        public string SharedUpperPath
        {
            get;
        }

        public string SharedLowerPath
        {
            get;
        }

        public bool SupportsDistinctCaseRoots()
        {
            return
                Directory
                    .GetDirectories(
                        DataRoot
                    )
                    .Count(
                        path =>
                            string.Equals(
                                Path.GetFileName(
                                    path
                                ),
                                "meshes",
                                StringComparison.OrdinalIgnoreCase
                            )
                    ) ==
                2;
        }

        public void Dispose()
        {
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
