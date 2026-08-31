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

        LinuxDirectoryIncarnationIdentity preparedIdentity =
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
        LinuxDirectoryIncarnationIdentity afterRename =
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

        LinuxDirectoryIncarnationIdentity finalIdentity =
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

        LinuxDirectoryIncarnationIdentity identity =
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

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureIdentity(
                staging,
                ".stage"
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
    public void Publish_WrongExpectedGeneration_DoesNotRename()
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

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureIdentity(
                staging,
                ".stage"
            );

        LinuxDirectoryIncarnationIdentity wrong =
            identity with
            {
                InodeGeneration =
                    unchecked(
                        identity.InodeGeneration + 1U
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

        Assert.NotNull(
            result.HandleIdentity
        );

        Assert.True(
            identity.SameIncarnationAs(
                result.HandleIdentity!
            )
        );

        Assert.NotEqual(
            wrong.InodeGeneration,
            result.HandleIdentity!.InodeGeneration
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
    public void Publish_RecreatedSourceWithReusedInode_IsRejected()
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

        LinuxDirectoryIncarnationIdentity expected;

        using (
            LinuxOpenedChildHandle original =
                fixture.OpenDirectory(
                    ".stage"
                ))
        {
            expected =
                fixture.CaptureIdentity(
                    original,
                    ".stage"
                );
        }

        Directory.Delete(
            fixture.PathFor(
                ".stage"
            )
        );

        LinuxDirectoryIncarnationIdentity? replacement =
            null;

        for (int attempt = 0; attempt < 128; attempt++)
        {
            fixture.CreateDirectory(
                ".stage"
            );

            using LinuxOpenedChildHandle candidateHandle =
                fixture.OpenDirectory(
                    ".stage"
                );

            LinuxDirectoryIncarnationIdentity candidate =
                fixture.CaptureIdentity(
                    candidateHandle,
                    ".stage"
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

            candidateHandle.Dispose();

            Directory.Delete(
                fixture.PathFor(
                    ".stage"
                )
            );
        }

        /*
         * Not every Linux filesystem immediately reuses inode
         * numbers. WrongExpectedGeneration remains the portable
         * deterministic proof of the generation gate.
         */
        if (replacement is null)
        {
            return;
        }

        Assert.NotEqual(
            expected.InodeGeneration,
            replacement.InodeGeneration
        );

        using LinuxOpenedChildHandle replacementHandle =
            fixture.OpenDirectory(
                ".stage"
            );

        LinuxPublishOwnedDirectoryAtResult result =
            LinuxPublishOwnedDirectoryAt.Publish(
                fixture.Parent,
                ".stage",
                "Final",
                replacementHandle,
                expected
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxPublishOwnedDirectoryAtState
                .SourceIdentityMismatch,
            result.State
        );

        Assert.NotNull(
            result.HandleIdentity
        );

        Assert.Equal(
            expected.PhysicalIdentity.DeviceMajor,
            result.HandleIdentity!.PhysicalIdentity.DeviceMajor
        );

        Assert.Equal(
            expected.PhysicalIdentity.DeviceMinor,
            result.HandleIdentity.PhysicalIdentity.DeviceMinor
        );

        Assert.Equal(
            expected.PhysicalIdentity.Inode,
            result.HandleIdentity.PhysicalIdentity.Inode
        );

        Assert.Equal(
            expected.PhysicalIdentity.MountId,
            result.HandleIdentity.PhysicalIdentity.MountId
        );

        Assert.NotEqual(
            expected.InodeGeneration,
            result.HandleIdentity.InodeGeneration
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

        LinuxDirectoryIncarnationIdentity identity =
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

        LinuxDirectoryIncarnationIdentity identity =
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

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("child/grandchild")]
    [InlineData(@"child\grandchild")]
    [InlineData("")]
    [InlineData("\0")]
    public void Publish_InvalidSourceChildName_IsRejectedWithoutRenaming(
        string sourceChildName)
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

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureIdentity(
                staging,
                ".stage"
            );

        LinuxPublishOwnedDirectoryAtResult result =
            LinuxPublishOwnedDirectoryAt.Publish(
                fixture.Parent,
                sourceChildName,
                "Final",
                staging,
                identity
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxPublishOwnedDirectoryAtState.InvalidName,
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

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("child/grandchild")]
    [InlineData(@"child\grandchild")]
    [InlineData("")]
    [InlineData("\0")]
    public void Publish_InvalidDestinationChildName_IsRejectedWithoutRenaming(
        string destinationChildName)
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

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureIdentity(
                staging,
                ".stage"
            );

        LinuxPublishOwnedDirectoryAtResult result =
            LinuxPublishOwnedDirectoryAt.Publish(
                fixture.Parent,
                ".stage",
                destinationChildName,
                staging,
                identity
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxPublishOwnedDirectoryAtState.InvalidName,
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

    private static void AssertSameIdentity(
        LinuxDirectoryIncarnationIdentity expected,
        LinuxDirectoryIncarnationIdentity actual)
    {
        Assert.True(
            expected.Success
        );

        Assert.True(
            actual.Success
        );

        Assert.Equal(
            expected.PhysicalIdentity.DeviceMajor,
            actual.PhysicalIdentity.DeviceMajor
        );

        Assert.Equal(
            expected.PhysicalIdentity.DeviceMinor,
            actual.PhysicalIdentity.DeviceMinor
        );

        Assert.Equal(
            expected.PhysicalIdentity.Inode,
            actual.PhysicalIdentity.Inode
        );

        Assert.Equal(
            expected.PhysicalIdentity.MountId,
            actual.PhysicalIdentity.MountId
        );

        Assert.Equal(
            expected.InodeGeneration,
            actual.InodeGeneration
        );

        Assert.True(
            expected.SameIncarnationAs(
                actual
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

        public LinuxDirectoryIncarnationIdentity CaptureIdentity(
            ILinuxOpenedHandle handle,
            string displayName)
        {
            LinuxOpenedDirectoryIncarnationResult incarnation =
                LinuxOpenedDirectoryIncarnation.Capture(
                    handle,
                    PathFor(
                        displayName
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
