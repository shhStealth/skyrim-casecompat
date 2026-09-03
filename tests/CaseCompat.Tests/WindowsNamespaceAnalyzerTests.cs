using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class WindowsNamespaceAnalyzerTests
{
    [Fact]
    public void Analyze_DiscoversSplitRootAndDistributedDescendants()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                Path.Combine(root, "Data");

            string upper =
                Path.Combine(data, "Meshes");

            string lower =
                Path.Combine(data, "meshes");

            Directory.CreateDirectory(upper);
            Directory.CreateDirectory(lower);

            File.WriteAllText(
                Path.Combine(upper, "A.nif"),
                "a"
            );

            File.WriteAllText(
                Path.Combine(lower, "B.nif"),
                "b"
            );

            WindowsNamespaceAnalysis result =
                WindowsNamespaceAnalyzer.Analyze(
                    data,
                    "Meshes"
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            Assert.Equal(
                "MESHES",
                result.RootLogicalPath.Value
            );

            Assert.Equal(
                3,
                result.Nodes.Count
            );

            WindowsNamespaceNode rootNode =
                Assert.Single(
                    result.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES"
                );

            Assert.Equal(
                2,
                rootNode.Participants.Count
            );

            Assert.True(
                rootNode.HasMultiplePhysicalObjects
            );

            Assert.True(
                rootNode.HasSpellingSplit
            );

            Assert.False(
                rootNode.HasFileDirectoryCollision
            );

            Assert.Contains(
                rootNode.Participants,
                participant =>
                    participant.Name == "Meshes"
            );

            Assert.Contains(
                rootNode.Participants,
                participant =>
                    participant.Name == "meshes"
            );

            Assert.All(
                rootNode.Participants,
                participant =>
                    Assert.Equal(
                        WindowsNamespacePhysicalObjectKind
                            .Directory,
                        participant.Kind
                    )
            );

            WindowsNamespaceNode a =
                Assert.Single(
                    result.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES/A.NIF"
                );

            WindowsNamespaceNode b =
                Assert.Single(
                    result.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES/B.NIF"
                );

            Assert.Single(a.Participants);
            Assert.Single(b.Participants);

            Assert.Equal(
                "Meshes/A.nif",
                a.Participants[0]
                    .RelativePath
                    .Replace('\\', '/')
            );

            Assert.Equal(
                "meshes/B.nif",
                b.Participants[0]
                    .RelativePath
                    .Replace('\\', '/')
            );

            Assert.NotNull(
                rootNode.Participants[0].Inode
            );

            Assert.NotNull(
                rootNode.Participants[1].Inode
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Analyze_GroupsNestedCaseEquivalentDirectories()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                Path.Combine(root, "Data");

            string upperArmor =
                Path.Combine(
                    data,
                    "Meshes",
                    "Armor"
                );

            string lowerArmor =
                Path.Combine(
                    data,
                    "meshes",
                    "armor"
                );

            Directory.CreateDirectory(upperArmor);
            Directory.CreateDirectory(lowerArmor);

            File.WriteAllText(
                Path.Combine(
                    upperArmor,
                    "UpperOnly.nif"
                ),
                "upper"
            );

            File.WriteAllText(
                Path.Combine(
                    lowerArmor,
                    "LowerOnly.nif"
                ),
                "lower"
            );

            WindowsNamespaceAnalysis result =
                WindowsNamespaceAnalyzer.Analyze(
                    data,
                    "Meshes"
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            WindowsNamespaceNode rootNode =
                Assert.Single(
                    result.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES"
                );

            WindowsNamespaceNode armorNode =
                Assert.Single(
                    result.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES/ARMOR"
                );

            Assert.Equal(
                2,
                rootNode.Participants.Count
            );

            Assert.Equal(
                2,
                armorNode.Participants.Count
            );

            Assert.True(
                rootNode.HasSpellingSplit
            );

            Assert.True(
                armorNode.HasMultiplePhysicalObjects
            );

            Assert.True(
                armorNode.HasSpellingSplit
            );

            Assert.Contains(
                armorNode.Participants,
                participant =>
                    participant.Name == "Armor"
            );

            Assert.Contains(
                armorNode.Participants,
                participant =>
                    participant.Name == "armor"
            );

            Assert.Single(
                result.Nodes,
                node =>
                    node.LogicalPath.Value ==
                    "MESHES/ARMOR/UPPERONLY.NIF"
            );

            Assert.Single(
                result.Nodes,
                node =>
                    node.LogicalPath.Value ==
                    "MESHES/ARMOR/LOWERONLY.NIF"
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Analyze_RetainsMultiplePhysicalFilesForOneLogicalPath()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                Path.Combine(root, "Data");

            string upperFoo =
                Path.Combine(
                    data,
                    "Meshes",
                    "Foo"
                );

            string lowerFoo =
                Path.Combine(
                    data,
                    "meshes",
                    "foo"
                );

            Directory.CreateDirectory(upperFoo);
            Directory.CreateDirectory(lowerFoo);

            string upperFile =
                Path.Combine(
                    upperFoo,
                    "Sword.nif"
                );

            string lowerFile =
                Path.Combine(
                    lowerFoo,
                    "sword.NIF"
                );

            File.WriteAllText(
                upperFile,
                "upper-version"
            );

            File.WriteAllText(
                lowerFile,
                "lower-version"
            );

            WindowsNamespaceAnalysis result =
                WindowsNamespaceAnalyzer.Analyze(
                    data,
                    "Meshes"
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            WindowsNamespaceNode fooNode =
                Assert.Single(
                    result.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES/FOO"
                );

            Assert.Equal(
                2,
                fooNode.Participants.Count
            );

            Assert.True(
                fooNode.HasMultiplePhysicalObjects
            );

            Assert.True(
                fooNode.HasSpellingSplit
            );

            WindowsNamespaceNode swordNode =
                Assert.Single(
                    result.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES/FOO/SWORD.NIF"
                );

            Assert.Equal(
                2,
                swordNode.Participants.Count
            );

            Assert.True(
                swordNode.HasMultiplePhysicalObjects
            );

            Assert.True(
                swordNode.HasSpellingSplit
            );

            Assert.False(
                swordNode.HasFileDirectoryCollision
            );

            Assert.All(
                swordNode.Participants,
                participant =>
                    Assert.Equal(
                        WindowsNamespacePhysicalObjectKind
                            .File,
                        participant.Kind
                    )
            );

            Assert.Contains(
                swordNode.Participants,
                participant =>
                    participant.RelativePath
                        .Replace('\\', '/') ==
                    "Meshes/Foo/Sword.nif"
            );

            Assert.Contains(
                swordNode.Participants,
                participant =>
                    participant.RelativePath
                        .Replace('\\', '/') ==
                    "meshes/foo/sword.NIF"
            );

            Assert.All(
                swordNode.Participants,
                participant =>
                    Assert.NotNull(
                        participant.Inode
                    )
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Analyze_DetectsFileDirectoryLogicalCollision()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                Path.Combine(root, "Data");

            string upper =
                Path.Combine(
                    data,
                    "Meshes"
                );

            string lower =
                Path.Combine(
                    data,
                    "meshes"
                );

            Directory.CreateDirectory(upper);
            Directory.CreateDirectory(lower);

            File.WriteAllText(
                Path.Combine(
                    upper,
                    "Foo"
                ),
                "file"
            );

            Directory.CreateDirectory(
                Path.Combine(
                    lower,
                    "foo"
                )
            );

            WindowsNamespaceAnalysis result =
                WindowsNamespaceAnalyzer.Analyze(
                    data,
                    "Meshes"
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            WindowsNamespaceNode collision =
                Assert.Single(
                    result.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES/FOO"
                );

            Assert.Equal(
                2,
                collision.Participants.Count
            );

            Assert.True(
                collision.HasMultiplePhysicalObjects
            );

            Assert.True(
                collision.HasSpellingSplit
            );

            Assert.True(
                collision.HasFileDirectoryCollision
            );

            Assert.Contains(
                collision.Participants,
                participant =>
                    participant.Kind ==
                        WindowsNamespacePhysicalObjectKind
                            .File
            );

            Assert.Contains(
                collision.Participants,
                participant =>
                    participant.Kind ==
                        WindowsNamespacePhysicalObjectKind
                            .Directory
            );

            Assert.Contains(
                collision.Participants,
                participant =>
                    participant.RelativePath
                        .Replace('\\', '/') ==
                    "Meshes/Foo"
            );

            Assert.Contains(
                collision.Participants,
                participant =>
                    participant.RelativePath
                        .Replace('\\', '/') ==
                    "meshes/foo"
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Analyze_RecordsDescriptorDirectoryLookupSemantics()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                Path.Combine(
                    root,
                    "Data"
                );

            string meshes =
                Path.Combine(
                    data,
                    "Meshes"
                );

            string armor =
                Path.Combine(
                    meshes,
                    "Armor"
                );

            Directory.CreateDirectory(
                armor
            );

            File.WriteAllText(
                Path.Combine(
                    armor,
                    "Example.nif"
                ),
                "example"
            );

            WindowsNamespaceAnalysis result =
                WindowsNamespaceAnalyzer.Analyze(
                    data,
                    "Meshes"
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            Assert.Equal(
                3,
                result.DirectoryLookupObservations.Count
            );

            WindowsNamespaceDirectoryLookupObservation
                dataObservation =
                    Assert.Single(
                        result.DirectoryLookupObservations,
                        observation =>
                            observation.RelativePath == "."
                    );

            WindowsNamespaceDirectoryLookupObservation
                meshesObservation =
                    Assert.Single(
                        result.DirectoryLookupObservations,
                        observation =>
                            observation.RelativePath
                                .Replace('\\', '/') ==
                            "Meshes"
                    );

            WindowsNamespaceDirectoryLookupObservation
                armorObservation =
                    Assert.Single(
                        result.DirectoryLookupObservations,
                        observation =>
                            observation.RelativePath
                                .Replace('\\', '/') ==
                            "Meshes/Armor"
                    );

            DirectoryCasefoldResult dataFlags =
                LinuxDirectoryFlags.Inspect(
                    data
                );

            DirectoryCasefoldResult meshesFlags =
                LinuxDirectoryFlags.Inspect(
                    meshes
                );

            DirectoryCasefoldResult armorFlags =
                LinuxDirectoryFlags.Inspect(
                    armor
                );

            Assert.Null(
                dataFlags.Error
            );

            Assert.Null(
                meshesFlags.Error
            );

            Assert.Null(
                armorFlags.Error
            );

            Assert.Equal(
                dataFlags.CasefoldEnabled,
                dataObservation.CasefoldEnabled
            );

            Assert.Equal(
                dataFlags.RawFlags,
                dataObservation.RawFlags
            );

            Assert.Equal(
                meshesFlags.CasefoldEnabled,
                meshesObservation.CasefoldEnabled
            );

            Assert.Equal(
                meshesFlags.RawFlags,
                meshesObservation.RawFlags
            );

            Assert.Equal(
                armorFlags.CasefoldEnabled,
                armorObservation.CasefoldEnabled
            );

            Assert.Equal(
                armorFlags.RawFlags,
                armorObservation.RawFlags
            );

            Assert.Null(
                dataObservation.Error
            );

            Assert.Null(
                meshesObservation.Error
            );

            Assert.Null(
                armorObservation.Error
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Analyze_RecordsDescriptorBoundRegularFileIncarnation()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                Path.Combine(
                    root,
                    "Data"
                );

            string meshes =
                Path.Combine(
                    data,
                    "Meshes"
                );

            Directory.CreateDirectory(
                meshes
            );

            string filePath =
                Path.Combine(
                    meshes,
                    "Example.nif"
                );

            File.WriteAllText(
                filePath,
                "example"
            );

            WindowsNamespaceAnalysis result =
                WindowsNamespaceAnalyzer.Analyze(
                    data,
                    "Meshes"
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            WindowsNamespaceFileIncarnationObservation
                observation =
                    Assert.Single(
                        result.FileIncarnationObservations
                    );

            Assert.Equal(
                "Meshes/Example.nif",
                observation.RelativePath
                    .Replace('\\', '/')
            );

            Assert.NotNull(
                observation.InodeGeneration
            );

            Assert.Null(
                observation.Error
            );

            LinuxNoFollowPathOpenResult parentOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    meshes
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

            LinuxOpenChildRegularFileReadOnlyAtResult opened =
                LinuxOpenChildRegularFileReadOnlyAt.Open(
                    parent,
                    "Example.nif"
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            using LinuxOpenedChildHandle openedFile =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    opened.OpenedFile
                );

            LinuxOpenedFileIncarnationResult expected =
                LinuxOpenedFileIncarnation.Capture(
                    openedFile
                );

            Assert.True(
                expected.Success,
                expected.Error
            );

            Assert.Equal(
                expected.Identity!.InodeGeneration,
                observation.InodeGeneration
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [Fact]
    public void Analyze_ReportsSymbolicLinkAsIncompleteWithoutTraversal()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                Path.Combine(root, "Data");

            string meshes =
                Path.Combine(
                    data,
                    "Meshes"
                );

            string outside =
                Path.Combine(
                    root,
                    "Outside"
                );

            Directory.CreateDirectory(meshes);
            Directory.CreateDirectory(outside);

            File.WriteAllText(
                Path.Combine(
                    outside,
                    "Escaped.nif"
                ),
                "outside"
            );

            string link =
                Path.Combine(
                    meshes,
                    "Linked"
                );

            Directory.CreateSymbolicLink(
                link,
                outside
            );

            WindowsNamespaceAnalysis result =
                WindowsNamespaceAnalyzer.Analyze(
                    data,
                    "Meshes"
                );

            Assert.False(
                result.Complete
            );

            Assert.Contains(
                result.Errors,
                error =>
                    error.Contains(
                        "Symbolic link is unsupported",
                        StringComparison.Ordinal
                    )
            );

            WindowsNamespaceNode linkNode =
                Assert.Single(
                    result.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES/LINKED"
                );

            WindowsNamespacePhysicalParticipant
                participant =
                    Assert.Single(
                        linkNode.Participants
                    );

            Assert.Equal(
                WindowsNamespacePhysicalObjectKind
                    .SymbolicLink,
                participant.Kind
            );

            Assert.NotNull(
                participant.Inode
            );

            Assert.DoesNotContain(
                result.Nodes,
                node =>
                    node.LogicalPath.Value ==
                    "MESHES/LINKED/ESCAPED.NIF"
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                $"casecompat-windows-namespace-" +
                $"{Guid.NewGuid():N}"
            );

        Directory.CreateDirectory(path);

        return path;
    }
}
