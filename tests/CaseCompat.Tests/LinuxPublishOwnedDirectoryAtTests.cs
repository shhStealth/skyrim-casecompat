using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxPublishOwnedDirectoryAtTests
{
    [Fact]
    public void Publish_MatchingStagingDirectory_PublishesWithoutChangingIdentity()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        using LinuxOpenedChildHandle staging =
            fixture.OpenDirectory(
                ".stage"
            );

        LinuxFileIdentityResult preparedIdentity =
            fixture.CaptureIdentity(
                staging,
                ".stage"
            );

        LinuxPublishOwnedDirectoryAtResult result =
            LinuxPublishOwnedDirectoryAt.Publish(
                fixture.Parent,
                ".stage",
                "Final",
                staging,
                preparedIdentity
            );

        if (
            result.State ==
            LinuxPublishOwnedDirectoryAtState
                .NoReplaceUnsupported)
        {
            return;
        }

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    ".stage"
                )
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        /*
         * The descriptor opened while the directory still had its
         * staging name remains attached to the same inode after
         * renameat2 publication.
         */
        LinuxFileIdentityResult afterRename =
            fixture.CaptureIdentity(
                staging,
                "Final"
            );

        AssertSameIdentity(
            preparedIdentity,
            afterRename
        );

        using LinuxOpenedChildHandle final =
            fixture.OpenDirectory(
                "Final"
            );

        LinuxFileIdentityResult finalIdentity =
            fixture.CaptureIdentity(
                final,
                "Final"
            );

        AssertSameIdentity(
            preparedIdentity,
            finalIdentity
        );
    }

    [Fact]
    public void Publish_ExistingDestination_IsNotOverwrittenOrMerged()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        fixture.CreateDirectory(
            "Final"
        );

        string existingPayload =
            Path.Combine(
                fixture.PathFor(
                    "Final"
                ),
                "existing.txt"
            );

        File.WriteAllText(
            existingPayload,
            "existing"
        );

        using LinuxOpenedChildHandle staging =
            fixture.OpenDirectory(
                ".stage"
            );

        LinuxFileIdentityResult identity =
            fixture.CaptureIdentity(
                staging,
                ".stage"
            );

        LinuxPublishOwnedDirectoryAtResult result =
            LinuxPublishOwnedDirectoryAt.Publish(
                fixture.Parent,
                ".stage",
                "Final",
                staging,
                identity
            );

        if (
            result.State ==
            LinuxPublishOwnedDirectoryAtState
                .NoReplaceUnsupported)
        {
            return;
        }

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxPublishOwnedDirectoryAtState
                .DestinationExists,
            result.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    ".stage"
                )
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        Assert.Equal(
            "existing",
            File.ReadAllText(
                existingPayload
            )
        );
    }

    [Fact]
    public void Publish_WrongExpectedIdentity_DoesNotRename()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        using LinuxOpenedChildHandle staging =
            fixture.OpenDirectory(
                ".stage"
            );

        LinuxFileIdentityResult identity =
            fixture.CaptureIdentity(
                staging,
                ".stage"
            );

        LinuxFileIdentityResult wrong =
            identity with
            {
                Inode =
                    checked(
                        identity.Inode!.Value + 1UL
                    )
            };

        LinuxPublishOwnedDirectoryAtResult result =
            LinuxPublishOwnedDirectoryAt.Publish(
                fixture.Parent,
                ".stage",
                "Final",
                staging,
                wrong
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxPublishOwnedDirectoryAtState
                .SourceIdentityMismatch,
            result.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    ".stage"
                )
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );
    }

    [Fact]
    public void Publish_NamedSourceReplacedBySymlink_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        fixture.CreateDirectory(
            "ReplacementTarget"
        );

        using LinuxOpenedChildHandle staging =
            fixture.OpenDirectory(
                ".stage"
            );

        LinuxFileIdentityResult identity =
            fixture.CaptureIdentity(
                staging,
                ".stage"
            );

        Directory.Delete(
            fixture.PathFor(
                ".stage"
            )
        );

        Directory.CreateSymbolicLink(
            fixture.PathFor(
                ".stage"
            ),
            fixture.PathFor(
                "ReplacementTarget"
            )
        );

        LinuxPublishOwnedDirectoryAtResult result =
            LinuxPublishOwnedDirectoryAt.Publish(
                fixture.Parent,
                ".stage",
                "Final",
                staging,
                identity
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxPublishOwnedDirectoryAtState
                .SourceSymbolicLinkRejected,
            result.State
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "ReplacementTarget"
                )
            )
        );
    }

    [Fact]
    public void Publish_PublishedDescriptor_CanAnchorNestedCreation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        using LinuxOpenedChildHandle staging =
            fixture.OpenDirectory(
                ".stage"
            );

        LinuxFileIdentityResult identity =
            fixture.CaptureIdentity(
                staging,
                ".stage"
            );

        LinuxPublishOwnedDirectoryAtResult publish =
            LinuxPublishOwnedDirectoryAt.Publish(
                fixture.Parent,
                ".stage",
                "Final",
                staging,
                identity
            );

        if (
            publish.State ==
            LinuxPublishOwnedDirectoryAtState
                .NoReplaceUnsupported)
        {
            return;
        }

        Assert.True(
            publish.Success,
            publish.Error
        );

        /*
         * No reopen of Final is needed. The descriptor obtained
         * under the staging name remains our anchor.
         */
        LinuxCreateDirectoryAtResult nested =
            LinuxCreateDirectoryAt.Create(
                staging,
                "Nested"
            );

        Assert.True(
            nested.Success,
            nested.Error
        );

        Assert.True(
            Directory.Exists(
                Path.Combine(
                    fixture.PathFor(
                        "Final"
                    ),
                    "Nested"
                )
            )
        );
    }

    private static void AssertSameIdentity(
        LinuxFileIdentityResult expected,
        LinuxFileIdentityResult actual)
    {
        Assert.True(
            expected.Success
        );

        Assert.True(
            actual.Success
        );

        Assert.Equal(
            expected.DeviceMajor,
            actual.DeviceMajor
        );

        Assert.Equal(
            expected.DeviceMinor,
            actual.DeviceMinor
        );

        Assert.Equal(
            expected.Inode,
            actual.Inode
        );

        Assert.Equal(
            expected.MountId,
            actual.MountId
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
                    "casecompat-owned-directory-publish-tests",
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
            LinuxCreateDirectoryAtResult result =
                LinuxCreateDirectoryAt.Create(
                    Parent,
                    childName
                );

            Assert.True(
                result.Success,
                result.Error
            );
        }

        public LinuxOpenedChildHandle OpenDirectory(
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

            LinuxOpenedChildHandle child =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    opened.OpenedChild
                );

            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    child,
                    PathFor(
                        childName
                    )
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            return child;
        }

        public LinuxFileIdentityResult CaptureIdentity(
            ILinuxOpenedHandle handle,
            string displayName)
        {
            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    handle,
                    PathFor(
                        displayName
                    )
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            Assert.NotNull(
                snapshot.Identity
            );

            Assert.NotNull(
                snapshot.Identity!.DeviceMajor
            );

            Assert.NotNull(
                snapshot.Identity.DeviceMinor
            );

            Assert.NotNull(
                snapshot.Identity.Inode
            );

            Assert.NotNull(
                snapshot.Identity.MountId
            );

            return snapshot.Identity;
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
