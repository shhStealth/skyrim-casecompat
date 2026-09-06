using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairSourceGenerationBinderTests
{
    [Fact]
    public void Bind_NullDataRoot_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairSourceGenerationBinder.Bind(
                    null!,
                    null!
                )
        );
    }

    [Fact]
    public void Bind_NullSourceSnapshot_Throws()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairSourceGenerationBinder.Bind(
                    fixture.DataRootHandle,
                    null!
                )
        );
    }

    [Fact]
    public void Bind_ExactStableSource_BindsFreshInodeGeneration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairSourceGenerationBinding result =
            fixture.Bind();

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.NotNull(
            result.SourceInodeGeneration
        );
    }

    [Fact]
    public void Bind_SourceShaHexCaseDifference_IsAccepted()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairSourceSnapshot snapshot =
            fixture.Snapshot with
            {
                Sha256 =
                    fixture.Snapshot.Sha256.ToLowerInvariant()
            };

        Assert.True(
            fixture.Bind(
                snapshot
            ).Success
        );
    }

    [Fact]
    public void Bind_NegativeSourceSize_IsInvalidSourceSnapshot()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .InvalidSourceSnapshot,
            fixture.Bind(
                fixture.Snapshot with
                {
                    Size = -1
                }
            ).State
        );
    }

    [Fact]
    public void Bind_MalformedSourceSha_IsInvalidSourceSnapshot()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .InvalidSourceSnapshot,
            fixture.Bind(
                fixture.Snapshot with
                {
                    Sha256 = "not-a-sha"
                }
            ).State
        );
    }

    [Fact]
    public void Bind_IncompleteSourceIdentity_IsInvalidSourceSnapshot()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        LinuxFileIdentityResult identity =
            fixture.Snapshot.Identity with
            {
                MountId = null
            };

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .InvalidSourceSnapshot,
            fixture.Bind(
                fixture.Snapshot with
                {
                    Identity = identity
                }
            ).State
        );
    }

    [Fact]
    public void Bind_SourceIdentityFullPathMismatch_IsInvalidSourceSnapshot()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .InvalidSourceSnapshot,
            fixture.Bind(
                fixture.Snapshot with
                {
                    Identity =
                        fixture.Snapshot.Identity with
                        {
                            FullPath =
                                fixture.SourcePath + ".wrong"
                        }
                }
            ).State
        );
    }

    [Fact]
    public void Bind_SourceOutsideDataRoot_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string outside =
            Path.Combine(
                fixture.RootPath,
                "outside.nif"
            );

        DataRelativePathRepairSourceSnapshot snapshot =
            fixture.WithPhysicalPath(
                outside
            );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .SourceOutsideDataRoot,
            fixture.Bind(
                snapshot
            ).State
        );
    }

    [Fact]
    public void Bind_NonCanonicalPhysicalPath_IsInvalidSourceSnapshot()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string nonCanonical =
            Path.Combine(
                fixture.DataRoot,
                "meshes",
                "source",
                "..",
                "source",
                "fixture.nif"
            );

        DataRelativePathRepairSourceSnapshot snapshot =
            fixture.WithPhysicalPath(
                nonCanonical
            );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .InvalidSourceSnapshot,
            fixture.Bind(
                snapshot
            ).State
        );
    }

    [Fact]
    public void Bind_MissingIntermediateDirectory_ReportsExactSpellingUnavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string path =
            Path.Combine(
                fixture.DataRoot,
                "meshes",
                "missing",
                "fixture.nif"
            );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .ExactDirectorySpellingUnavailable,
            fixture.Bind(
                fixture.WithPhysicalPath(
                    path
                )
            ).State
        );
    }

    [Fact]
    public void Bind_IntermediateDirectoryCaseMismatch_ReportsExactSpellingUnavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string path =
            Path.Combine(
                fixture.DataRoot,
                "meshes",
                "Source",
                "fixture.nif"
            );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .ExactDirectorySpellingUnavailable,
            fixture.Bind(
                fixture.WithPhysicalPath(
                    path
                )
            ).State
        );
    }

    [Fact]
    public void Bind_IntermediateDirectorySymbolicLink_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string link =
            Path.Combine(
                fixture.DataRoot,
                "meshes",
                "link"
            );

        Directory.CreateSymbolicLink(
            link,
            fixture.SourceDirectory
        );

        string path =
            Path.Combine(
                link,
                "fixture.nif"
            );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .DirectoryOpenFailed,
            fixture.Bind(
                fixture.WithPhysicalPath(
                    path
                )
            ).State
        );
    }

    [Fact]
    public void Bind_MissingFinalFile_ReportsExactSpellingUnavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string path =
            Path.Combine(
                fixture.SourceDirectory,
                "missing.nif"
            );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .ExactFileSpellingUnavailable,
            fixture.Bind(
                fixture.WithPhysicalPath(
                    path
                )
            ).State
        );
    }

    [Fact]
    public void Bind_FinalFileCaseMismatch_ReportsExactSpellingUnavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string path =
            Path.Combine(
                fixture.SourceDirectory,
                "Fixture.nif"
            );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .ExactFileSpellingUnavailable,
            fixture.Bind(
                fixture.WithPhysicalPath(
                    path
                )
            ).State
        );
    }

    [Fact]
    public void Bind_FinalFileSymbolicLink_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string link =
            Path.Combine(
                fixture.SourceDirectory,
                "link.nif"
            );

        File.CreateSymbolicLink(
            link,
            fixture.SourcePath
        );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .FileOpenFailed,
            fixture.Bind(
                fixture.WithPhysicalPath(
                    link
                )
            ).State
        );
    }

    [Fact]
    public void Bind_FinalDirectory_IsRejectedAsFileOpenFailure()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string directory =
            Directory.CreateDirectory(
                Path.Combine(
                    fixture.SourceDirectory,
                    "directory.nif"
                )
            ).FullName;

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .FileOpenFailed,
            fixture.Bind(
                fixture.WithPhysicalPath(
                    directory
                )
            ).State
        );
    }

    [Fact]
    public void Bind_SourceDeviceMajorMismatch_IsSourceSnapshotMismatch()
    {
        AssertMismatch(
            snapshot =>
                snapshot with
                {
                    Identity =
                        snapshot.Identity with
                        {
                            DeviceMajor =
                                snapshot.Identity.DeviceMajor!.Value + 1
                        }
                }
        );
    }

    [Fact]
    public void Bind_SourceDeviceMinorMismatch_IsSourceSnapshotMismatch()
    {
        AssertMismatch(
            snapshot =>
                snapshot with
                {
                    Identity =
                        snapshot.Identity with
                        {
                            DeviceMinor =
                                snapshot.Identity.DeviceMinor!.Value + 1
                        }
                }
        );
    }

    [Fact]
    public void Bind_SourceInodeMismatch_IsSourceSnapshotMismatch()
    {
        AssertMismatch(
            snapshot =>
                snapshot with
                {
                    Identity =
                        snapshot.Identity with
                        {
                            Inode =
                                snapshot.Identity.Inode!.Value + 1UL
                        }
                }
        );
    }

    [Fact]
    public void Bind_SourceMountIdMismatch_IsSourceSnapshotMismatch()
    {
        AssertMismatch(
            snapshot =>
                snapshot with
                {
                    Identity =
                        snapshot.Identity with
                        {
                            MountId =
                                snapshot.Identity.MountId!.Value + 1UL
                        }
                }
        );
    }

    [Fact]
    public void Bind_SourceSizeMismatch_IsSourceSnapshotMismatch()
    {
        AssertMismatch(
            snapshot =>
                snapshot with
                {
                    Size =
                        snapshot.Size + 1
                }
        );
    }

    [Fact]
    public void Bind_SourceShaMismatch_IsSourceSnapshotMismatch()
    {
        AssertMismatch(
            snapshot =>
                snapshot with
                {
                    Sha256 =
                        new string(
                            'A',
                            64
                        )
                }
        );
    }

    [Fact]
    public void Bind_SourceLinkCountDifference_IsIgnored()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        LinuxFileIdentityResult identity =
            fixture.Snapshot.Identity with
            {
                LinkCount =
                    (
                        fixture.Snapshot.Identity.LinkCount ??
                        0U
                    ) + 100U
            };

        DataRelativePathRepairSourceGenerationBinding result =
            fixture.Bind(
                fixture.Snapshot with
                {
                    Identity = identity
                }
            );

        Assert.True(
            result.Success,
            result.Error
        );
    }

    [Fact]
    public void Bind_PathReplacementAfterStableContent_IsDetectedByPostReacquisition()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string backup =
            fixture.SourcePath + ".old";

        System.Reflection.MethodInfo bindCore =
            Assert.IsAssignableFrom<System.Reflection.MethodInfo>(
                typeof(DataRelativePathRepairSourceGenerationBinder)
                    .GetMethod(
                        "BindCore",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.NonPublic
                    )
            );

        var afterStableContentObservation =
            new Action(
                () =>
                {
                    File.Move(
                        fixture.SourcePath,
                        backup
                    );

                    File.WriteAllText(
                        fixture.SourcePath,
                        Fixture.Content
                    );
                }
            );

        DataRelativePathRepairSourceGenerationBinding result =
            Assert.IsType<
                DataRelativePathRepairSourceGenerationBinding
            >(
                bindCore.Invoke(
                    null,
                    [
                        fixture.DataRootHandle,
                        fixture.Snapshot,
                        afterStableContentObservation
                    ]
                )
            );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .SourceIncarnationChanged,
            result.State
        );
    }

    [Fact]
    public void Bind_DoesNotConsumeAggregateNamespaceSidecarEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairSourceGenerationBinding result =
            fixture.Bind();

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.DoesNotContain(
            "AggregateNamespace",
            typeof(DataRelativePathRepairSourceGenerationBinder)
                .GetMethod(
                    "Bind"
                )!
                .ToString(),
            StringComparison.Ordinal
        );
    }

    private static void AssertMismatch(
        Func<
            DataRelativePathRepairSourceSnapshot,
            DataRelativePathRepairSourceSnapshot
        > mutate)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairSourceGenerationBinding result =
            fixture.Bind(
                mutate(
                    fixture.Snapshot
                )
            );

        Assert.Equal(
            DataRelativePathRepairSourceGenerationBindingState
                .SourceSnapshotMismatch,
            result.State
        );
    }

    private sealed class Fixture
        : IDisposable
    {
        public const string Content =
            "casecompat-source-generation-binding-fixture";

        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-source-generation-binder-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Data"
                    )
                ).FullName;

            SourceDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        "source"
                    )
                ).FullName;

            SourcePath =
                Path.Combine(
                    SourceDirectory,
                    "fixture.nif"
                );

            File.WriteAllText(
                SourcePath,
                Content
            );

            LinuxNoFollowPathOpenResult rootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    DataRoot
                );

            Assert.True(
                rootOpen.Success,
                rootOpen.Error
            );

            DataRootHandle =
                Assert.IsType<
                    LinuxNoFollowPathHandle>(
                        rootOpen.OpenedPath
                    );

            LinuxNoFollowPathOpenResult sourceOpen =
                LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                    DataRoot,
                    "meshes/source/fixture.nif"
                );

            Assert.True(
                sourceOpen.Success,
                sourceOpen.Error
            );

            using LinuxNoFollowPathHandle openedSource =
                Assert.IsType<
                    LinuxNoFollowPathHandle>(
                        sourceOpen.OpenedPath
                    );

            LinuxOpenedFileSnapshotResult snapshot =
                LinuxOpenedFileSnapshot.Capture(
                    openedSource,
                    SourcePath
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            Snapshot =
                new(
                    PhysicalPath:
                        SourcePath,
                    Size:
                        snapshot.Size!.Value,
                    Sha256:
                        snapshot.Sha256!,
                    Identity:
                        snapshot.Identity!
                );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string SourceDirectory { get; }

        public string SourcePath { get; }

        public LinuxNoFollowPathHandle DataRootHandle { get; }

        public DataRelativePathRepairSourceSnapshot Snapshot { get; }

        public DataRelativePathRepairSourceGenerationBinding Bind(
            DataRelativePathRepairSourceSnapshot? snapshot = null)
        {
            return DataRelativePathRepairSourceGenerationBinder.Bind(
                DataRootHandle,
                snapshot ??
                    Snapshot
            );
        }

        public DataRelativePathRepairSourceSnapshot WithPhysicalPath(
            string physicalPath)
        {
            return Snapshot with
            {
                PhysicalPath =
                    physicalPath,
                Identity =
                    Snapshot.Identity with
                    {
                        FullPath =
                            physicalPath
                    }
            };
        }

        public void Dispose()
        {
            DataRootHandle.Dispose();

            if (Directory.Exists(
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
