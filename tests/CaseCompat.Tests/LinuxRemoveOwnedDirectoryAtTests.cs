using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxRemoveOwnedDirectoryAtTests
{
    [Fact]
    public void Remove_MatchingEmptyDirectory_RemovesDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                "Owned"
            );

        LinuxRemoveOwnedDirectoryAtResult result =
            LinuxRemoveOwnedDirectoryAt.Remove(
                fixture.Parent,
                "Owned",
                identity
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState.Removed,
            result.State
        );

        Assert.NotNull(
            result.ActualIdentity
        );

        Assert.Equal(
            identity.PhysicalIdentity.DeviceMajor,
            result.ActualIdentity!.PhysicalIdentity.DeviceMajor
        );

        Assert.Equal(
            identity.PhysicalIdentity.DeviceMinor,
            result.ActualIdentity.PhysicalIdentity.DeviceMinor
        );

        Assert.Equal(
            identity.PhysicalIdentity.Inode,
            result.ActualIdentity.PhysicalIdentity.Inode
        );

        Assert.Equal(
            identity.PhysicalIdentity.MountId,
            result.ActualIdentity.PhysicalIdentity.MountId
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    "Owned"
                )
            )
        );
    }

    [Fact]
    public void Remove_WrongIdentity_DoesNotRemoveDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                "Owned"
            );

        LinuxDirectoryIncarnationIdentity wrong =
            identity with
            {
                PhysicalIdentity =
                    identity.PhysicalIdentity with
                    {
                        Inode =
                            checked(
                                identity.PhysicalIdentity
                                    .Inode!.Value + 1UL
                            )
                    }
            };

        LinuxRemoveOwnedDirectoryAtResult result =
            LinuxRemoveOwnedDirectoryAt.Remove(
                fixture.Parent,
                "Owned",
                wrong
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState.IdentityMismatch,
            result.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Owned"
                )
            )
        );
    }

    [Fact]
    public void Remove_WrongGeneration_DoesNotRemoveDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                "Owned"
            );

        LinuxDirectoryIncarnationIdentity wrong =
            identity with
            {
                InodeGeneration =
                    unchecked(
                        identity.InodeGeneration + 1U
                    )
            };

        LinuxRemoveOwnedDirectoryAtResult result =
            LinuxRemoveOwnedDirectoryAt.Remove(
                fixture.Parent,
                "Owned",
                wrong
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState.IdentityMismatch,
            result.State
        );

        Assert.NotNull(
            result.ActualIdentity
        );

        Assert.Equal(
            identity.PhysicalIdentity,
            result.ActualIdentity!.PhysicalIdentity
        );

        Assert.NotEqual(
            wrong.InodeGeneration,
            result.ActualIdentity.InodeGeneration
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Owned"
                )
            )
        );
    }

    [Fact]
    public void Remove_RecreatedDirectoryWithReusedInode_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        LinuxDirectoryIncarnationIdentity expected =
            fixture.CaptureDirectoryIdentity(
                "Owned"
            );

        Directory.Delete(
            fixture.PathFor(
                "Owned"
            )
        );

        LinuxDirectoryIncarnationIdentity? replacement =
            null;

        for (int attempt = 0; attempt < 128; attempt++)
        {
            fixture.CreateDirectory(
                "Owned"
            );

            LinuxDirectoryIncarnationIdentity candidate =
                fixture.CaptureDirectoryIdentity(
                    "Owned"
                );

            bool samePhysicalIdentity =
                expected.PhysicalIdentity.DeviceMajor ==
                    candidate.PhysicalIdentity.DeviceMajor &&
                expected.PhysicalIdentity.DeviceMinor ==
                    candidate.PhysicalIdentity.DeviceMinor &&
                expected.PhysicalIdentity.Inode ==
                    candidate.PhysicalIdentity.Inode &&
                expected.PhysicalIdentity.MountId ==
                    candidate.PhysicalIdentity.MountId;

            if (samePhysicalIdentity)
            {
                replacement =
                    candidate;

                break;
            }

            Directory.Delete(
                fixture.PathFor(
                    "Owned"
                )
            );
        }

        /*
         * Not every Linux filesystem immediately reuses inode
         * numbers. The deterministic WrongGeneration test above
         * remains the portable proof of the generation gate.
         */
        if (replacement is null)
        {
            return;
        }

        Assert.NotEqual(
            expected.InodeGeneration,
            replacement.InodeGeneration
        );

        LinuxRemoveOwnedDirectoryAtResult result =
            LinuxRemoveOwnedDirectoryAt.Remove(
                fixture.Parent,
                "Owned",
                expected
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState.IdentityMismatch,
            result.State
        );

        Assert.NotNull(
            result.ActualIdentity
        );

        Assert.Equal(
            expected.PhysicalIdentity.DeviceMajor,
            result.ActualIdentity!.PhysicalIdentity.DeviceMajor
        );

        Assert.Equal(
            expected.PhysicalIdentity.DeviceMinor,
            result.ActualIdentity.PhysicalIdentity.DeviceMinor
        );

        Assert.Equal(
            expected.PhysicalIdentity.Inode,
            result.ActualIdentity.PhysicalIdentity.Inode
        );

        Assert.Equal(
            expected.PhysicalIdentity.MountId,
            result.ActualIdentity.PhysicalIdentity.MountId
        );

        Assert.NotEqual(
            expected.InodeGeneration,
            result.ActualIdentity.InodeGeneration
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Owned"
                )
            )
        );
    }

    [Fact]
    public void Remove_MatchingNonEmptyDirectory_IsRefusedByKernel()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                "Owned"
            );

        string payload =
            Path.Combine(
                fixture.PathFor(
                    "Owned"
                ),
                "payload.txt"
            );

        File.WriteAllText(
            payload,
            "external"
        );

        LinuxRemoveOwnedDirectoryAtResult result =
            LinuxRemoveOwnedDirectoryAt.Remove(
                fixture.Parent,
                "Owned",
                identity
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState.DirectoryNotEmpty,
            result.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Owned"
                )
            )
        );

        Assert.Equal(
            "external",
            File.ReadAllText(
                payload
            )
        );
    }

    [Fact]
    public void Remove_SymbolicLink_IsRejectedWithoutTouchingTarget()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Target"
        );

        LinuxDirectoryIncarnationIdentity targetIdentity =
            fixture.CaptureDirectoryIdentity(
                "Target"
            );

        string link =
            fixture.PathFor(
                "Owned"
            );

        Directory.CreateSymbolicLink(
            link,
            fixture.PathFor(
                "Target"
            )
        );

        LinuxRemoveOwnedDirectoryAtResult result =
            LinuxRemoveOwnedDirectoryAt.Remove(
                fixture.Parent,
                "Owned",
                targetIdentity
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState
                .ChildSymbolicLinkRejected,
            result.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Target"
                )
            )
        );

        Assert.True(
            File.Exists(link) ||
            Directory.Exists(link)
        );
    }

    [Fact]
    public void Remove_MissingChild_IsClassifiedWithoutMutation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Evidence"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                "Evidence"
            );

        LinuxRemoveOwnedDirectoryAtResult result =
            LinuxRemoveOwnedDirectoryAt.Remove(
                fixture.Parent,
                "Missing",
                identity
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState.ChildUnavailable,
            result.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Evidence"
                )
            )
        );
    }

    [Fact]
    public void Remove_RegularFile_IsRejectedAsNotDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Evidence"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                "Evidence"
            );

        string file =
            fixture.PathFor(
                "Owned"
            );

        File.WriteAllText(
            file,
            "file"
        );

        LinuxRemoveOwnedDirectoryAtResult result =
            LinuxRemoveOwnedDirectoryAt.Remove(
                fixture.Parent,
                "Owned",
                identity
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState.ChildNotDirectory,
            result.State
        );

        Assert.Equal(
            "file",
            File.ReadAllText(
                file
            )
        );
    }

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-owned-directory-remove-tests",
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );

            Parent =
                OpenRoot(
                    RootPath
                );
        }

        public string RootPath { get; }

        public LinuxNoFollowPathHandle Parent { get; }

        public string PathFor(
            string childName)
        {
            return Path.Combine(
                RootPath,
                childName
            );
        }

        public void CreateDirectory(
            string childName)
        {
            Directory.CreateDirectory(
                PathFor(
                    childName
                )
            );
        }

        public LinuxDirectoryIncarnationIdentity CaptureDirectoryIdentity(
            string childName)
        {
            LinuxOpenChildReadOnlyAtResult opened =
                LinuxOpenChildReadOnlyAt.Open(
                    Parent,
                    childName
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            using LinuxOpenedChildHandle child =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    opened.OpenedChild
                );

            LinuxOpenedDirectoryIncarnationResult incarnation =
                LinuxOpenedDirectoryIncarnation.Capture(
                    child,
                    PathFor(
                        childName
                    )
                );

            Assert.True(
                incarnation.Success,
                incarnation.Error ??
                incarnation.State.ToString()
            );

            return Assert.IsType<
                LinuxDirectoryIncarnationIdentity
            >(
                incarnation.Identity
            );
        }

        private static LinuxNoFollowPathHandle OpenRoot(
            string path)
        {
            LinuxNoFollowPathOpenResult result =
                LinuxNoFollowPath.OpenRootReadOnly(
                    path
                );

            Assert.True(
                result.Success,
                result.Error
            );

            return Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                result.OpenedPath
            );
        }

        public void Dispose()
        {
            Parent.Dispose();

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
