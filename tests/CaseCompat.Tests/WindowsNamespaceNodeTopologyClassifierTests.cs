using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class WindowsNamespaceNodeTopologyClassifierTests
{
    [Fact]
    public void Classify_NoParticipants()
    {
        WindowsNamespaceNode node =
            Node();

        Assert.Equal(
            WindowsNamespaceNodeTopology.NoPhysicalParticipants,
            WindowsNamespaceNodeTopologyClassifier.Classify(
                node
            )
        );
    }

    [Fact]
    public void Classify_SingleSupportedPhysicalObject()
    {
        WindowsNamespaceNode node =
            Node(
                Participant(
                    "Meshes/Foo.nif",
                    WindowsNamespacePhysicalObjectKind.File
                )
            );

        Assert.Equal(
            WindowsNamespaceNodeTopology.SinglePhysicalObject,
            WindowsNamespaceNodeTopologyClassifier.Classify(
                node
            )
        );
    }

    [Fact]
    public void Classify_MultipleDirectories()
    {
        WindowsNamespaceNode node =
            Node(
                Participant(
                    "Meshes/Armor",
                    WindowsNamespacePhysicalObjectKind.Directory
                ),
                Participant(
                    "meshes/armor",
                    WindowsNamespacePhysicalObjectKind.Directory
                )
            );

        Assert.Equal(
            WindowsNamespaceNodeTopology.MultipleDirectories,
            WindowsNamespaceNodeTopologyClassifier.Classify(
                node
            )
        );
    }

    [Fact]
    public void Classify_MultipleFiles()
    {
        WindowsNamespaceNode node =
            Node(
                Participant(
                    "Meshes/Foo/Sword.nif",
                    WindowsNamespacePhysicalObjectKind.File
                ),
                Participant(
                    "meshes/foo/sword.NIF",
                    WindowsNamespacePhysicalObjectKind.File
                )
            );

        Assert.Equal(
            WindowsNamespaceNodeTopology.MultipleFiles,
            WindowsNamespaceNodeTopologyClassifier.Classify(
                node
            )
        );
    }

    [Fact]
    public void Classify_FileDirectoryCollision()
    {
        WindowsNamespaceNode node =
            Node(
                Participant(
                    "Meshes/Foo",
                    WindowsNamespacePhysicalObjectKind.File
                ),
                Participant(
                    "meshes/foo",
                    WindowsNamespacePhysicalObjectKind.Directory
                )
            );

        Assert.Equal(
            WindowsNamespaceNodeTopology.FileDirectoryCollision,
            WindowsNamespaceNodeTopologyClassifier.Classify(
                node
            )
        );
    }

    [Fact]
    public void Classify_UnsupportedObjectTakesPrecedence()
    {
        WindowsNamespaceNode node =
            Node(
                Participant(
                    "Meshes/Foo",
                    WindowsNamespacePhysicalObjectKind.Directory
                ),
                Participant(
                    "meshes/foo",
                    WindowsNamespacePhysicalObjectKind.SymbolicLink
                )
            );

        Assert.Equal(
            WindowsNamespaceNodeTopology.UnsupportedObject,
            WindowsNamespaceNodeTopologyClassifier.Classify(
                node
            )
        );
    }

    private static WindowsNamespaceNode Node(
        params WindowsNamespacePhysicalParticipant[] participants)
    {
        return new WindowsNamespaceNode(
            LogicalPath:
                WindowsLogicalPath.FromRelativePath(
                    "Meshes/Foo"
                ),
            Participants:
                participants
        );
    }

    private static WindowsNamespacePhysicalParticipant Participant(
        string relativePath,
        WindowsNamespacePhysicalObjectKind kind)
    {
        return new WindowsNamespacePhysicalParticipant(
            FullPath:
                "/fixture/Data/" +
                relativePath,
            RelativePath:
                relativePath,
            Name:
                Path.GetFileName(
                    relativePath
                ),
            Kind:
                kind,
            DeviceMajor:
                8,
            DeviceMinor:
                1,
            Inode:
                123,
            MountId:
                42,
            IdentityError:
                null
        );
    }
}
