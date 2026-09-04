using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducerTests
{
    [Fact]
    public void Produce_EmptyInventory_ProducesNoAnalyses()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateData(root);

            IReadOnlyList<WindowsNamespaceAnalysis> analyses =
                SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                    .Produce(
                        Inventory(
                            data
                        )
                    );

            Assert.Empty(
                analyses
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
    public void Produce_NullInventory_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                    .Produce(
                        null!
                    )
        );
    }

    [Fact]
    public void Produce_InvalidRequestedPaths_DoNotAcquireNamespaces()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateData(root);

            SkyrimWinningArmorAddonInventoryResult inventory =
                Inventory(
                    data,
                    Reference(
                        "../bad.nif"
                    ),
                    Reference(
                        "/Meshes/bad.nif"
                    ),
                    Reference(
                        "Meshes//bad.nif"
                    )
                );

            IReadOnlyList<WindowsNamespaceAnalysis> analyses =
                SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                    .Produce(
                        inventory
                    );

            Assert.Empty(
                analyses
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
    public void Produce_WindowsEquivalentRequestedRoots_AreAnalyzedOnce()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateData(root);

            Directory.CreateDirectory(
                Path.Combine(
                    data,
                    "Meshes"
                )
            );

            SkyrimWinningArmorAddonInventoryResult inventory =
                Inventory(
                    data,
                    Reference(
                        "Meshes/Armor/A.nif"
                    ),
                    Reference(
                        "meshes/Weapons/B.nif"
                    ),
                    Reference(
                        @"MeShEs\Actors\C.nif"
                    )
                );

            WindowsNamespaceAnalysis analysis =
                Assert.Single(
                    SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                        .Produce(
                            inventory
                        )
                );

            Assert.Equal(
                "MESHES",
                analysis.RootLogicalPath.Value
            );

            WindowsNamespaceNode rootNode =
                Assert.Single(
                    analysis.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES"
                );

            Assert.Single(
                rootNode.Participants
            );

            Assert.Equal(
                "Meshes",
                rootNode.Participants[0].Name
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
    public void Produce_DistinctRoots_AreProducedOnceInLogicalOrder()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateData(root);

            Directory.CreateDirectory(
                Path.Combine(
                    data,
                    "Textures"
                )
            );

            Directory.CreateDirectory(
                Path.Combine(
                    data,
                    "Meshes"
                )
            );

            SkyrimWinningArmorAddonInventoryResult inventory =
                Inventory(
                    data,
                    Reference(
                        @"Textures\Armor\A.dds"
                    ),
                    Reference(
                        "meshes/Armor/A.nif"
                    ),
                    Reference(
                        "Meshes/Weapons/B.nif"
                    ),
                    Reference(
                        "textures/Weapons/B.dds"
                    )
                );

            IReadOnlyList<WindowsNamespaceAnalysis> analyses =
                SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                    .Produce(
                        inventory
                    );

            Assert.Equal(
                new[]
                {
                    "MESHES",
                    "TEXTURES"
                },
                analyses
                    .Select(
                        analysis =>
                            analysis.RootLogicalPath.Value
                    )
                    .ToArray()
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
    public void Produce_MissingPhysicalRoot_RetainsIncompleteAnalysis()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateData(root);

            WindowsNamespaceAnalysis analysis =
                Assert.Single(
                    SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                        .Produce(
                            Inventory(
                                data,
                                Reference(
                                    "Meshes/Armor/A.nif"
                                )
                            )
                        )
                );

            Assert.Equal(
                "MESHES",
                analysis.RootLogicalPath.Value
            );

            Assert.False(
                analysis.Complete
            );

            Assert.NotNull(
                analysis.DataRootChildNames
            );

            Assert.Contains(
                analysis.Errors,
                error =>
                    error.Contains(
                        "No physical representative was found",
                        StringComparison.Ordinal
                    )
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
    public void Produce_RootRegularFile_RetainsIncompleteAnalyzerEvidence()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateData(root);

            File.WriteAllText(
                Path.Combine(
                    data,
                    "Meshes"
                ),
                "not a directory"
            );

            WindowsNamespaceAnalysis analysis =
                Assert.Single(
                    SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                        .Produce(
                            Inventory(
                                data,
                                Reference(
                                    "Meshes/Armor/A.nif"
                                )
                            )
                        )
                );

            Assert.False(
                analysis.Complete
            );

            WindowsNamespaceNode rootNode =
                Assert.Single(
                    analysis.Nodes,
                    node =>
                        node.LogicalPath.Value ==
                        "MESHES"
                );

            WindowsNamespacePhysicalParticipant participant =
                Assert.Single(
                    rootNode.Participants
                );

            Assert.Equal(
                WindowsNamespacePhysicalObjectKind.File,
                participant.Kind
            );

            Assert.Contains(
                analysis.Errors,
                error =>
                    error.Contains(
                        "namespace root is a regular file",
                        StringComparison.Ordinal
                    )
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
    public void Produce_PhysicalRootSpellingSplit_IsOneAnalysisWithAllParticipants()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateData(root);

            Directory.CreateDirectory(
                Path.Combine(
                    data,
                    "Meshes"
                )
            );

            Directory.CreateDirectory(
                Path.Combine(
                    data,
                    "meshes"
                )
            );

            WindowsNamespaceAnalysis analysis =
                Assert.Single(
                    SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                        .Produce(
                            Inventory(
                                data,
                                Reference(
                                    "Meshes/Armor/A.nif"
                                ),
                                Reference(
                                    "meshes/Weapons/B.nif"
                                )
                            )
                        )
                );

            Assert.Equal(
                "MESHES",
                analysis.RootLogicalPath.Value
            );

            WindowsNamespaceNode rootNode =
                Assert.Single(
                    analysis.Nodes,
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

            Assert.Contains(
                rootNode.Participants,
                participant =>
                    participant.Name ==
                    "Meshes"
            );

            Assert.Contains(
                rootNode.Participants,
                participant =>
                    participant.Name ==
                    "meshes"
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

    private static SkyrimWinningArmorAddonInventoryResult Inventory(
        string dataRoot,
        params SkyrimArmorAddonModelReference[] references)
    {
        IReadOnlyList<SkyrimWinningArmorAddonRecord> winners =
            references.Length == 0
                ? Array.Empty<
                    SkyrimWinningArmorAddonRecord
                >()
                : new[]
                {
                    new SkyrimWinningArmorAddonRecord(
                        FormKey:
                            "000800:Fixture.esp",
                        EditorId:
                            "Fixture",
                        WinningPluginName:
                            "Fixture.esp",
                        WinningLoadOrderIndex:
                            10,
                        ModelReferences:
                            references
                    )
                };

        return new SkyrimWinningArmorAddonInventoryResult(
            DataRoot:
                dataRoot,
            RuntimeActivePluginCount:
                winners.Count == 0
                    ? 0
                    : 1,
            PluginsOpened:
                winners.Count == 0
                    ? 0
                    : 1,
            MissingPluginFiles:
                Array.Empty<string>(),
            ReadErrors:
                Array.Empty<
                    SkyrimPluginReadError
                >(),
            Winners:
                winners
        );
    }

    private static SkyrimArmorAddonModelReference Reference(
        string dataRelativePath)
    {
        return new SkyrimArmorAddonModelReference(
            FormKey:
                "000800:Fixture.esp",
            EditorId:
                "Fixture",
            Field:
                "WorldModel.Male",
            GivenPath:
                dataRelativePath,
            DataRelativePath:
                dataRelativePath
        );
    }

    private static string CreateTempDirectory()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-armor-addon-namespace-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            path
        );

        return path;
    }

    private static string CreateData(
        string root)
    {
        string data =
            Path.Combine(
                root,
                "Data"
            );

        Directory.CreateDirectory(
            data
        );

        return data;
    }
}
