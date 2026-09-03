using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class SkyrimWinningArmorAddonSnapshotEvidenceScannerTests
{
    [Fact]
    public void Inspect_ExactDuplicateRequestedPaths_AreGroupedOrdinally()
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            Inventory(
                Winner(
                    "ArmorA",
                    "WinnerA.esp",
                    10,
                    Reference(
                        "ArmorA",
                        "WorldModel.Male",
                        @"Meshes\Foo\Sword.nif",
                        "Meshes/Foo/Sword.nif"
                    )
                ),
                Winner(
                    "ArmorB",
                    "WinnerB.esp",
                    20,
                    Reference(
                        "ArmorB",
                        "WorldModel.Female",
                        "meshes/foo/sword-source.nif",
                        "Meshes/Foo/Sword.nif"
                    )
                )
            );

        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootLogicalName:
                    "Meshes",
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            false
                        ),
                        DirectorySpec(
                            "Meshes/Foo",
                            false
                        )
                    },
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Meshes",
                            WindowsNamespacePhysicalObjectKind.Directory
                        ),
                        ParticipantSpec(
                            "Meshes/Foo",
                            WindowsNamespacePhysicalObjectKind.Directory
                        ),
                        ParticipantSpec(
                            "Meshes/Foo/Sword.nif",
                            WindowsNamespacePhysicalObjectKind.File
                        )
                    }
            );

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult result =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                inventory,
                new[]
                {
                    analysis
                }
            );

        Assert.Same(inventory, result.Inventory);
        Assert.Equal(2, result.ReferenceCount);
        Assert.Equal(1, result.UniqueRequestedPathCount);
        Assert.Equal(1, result.AvoidedLookupCalls);

        SkyrimWinningArmorAddonSnapshotPathEvidence path =
            Assert.Single(result.Paths);

        Assert.Equal(
            "Meshes/Foo/Sword.nif",
            path.RequestedPath
        );

        Assert.Equal(2, path.AffectedReferenceCount);
        Assert.True(path.HasLookup);

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.Resolved,
            path.Lookup!.State
        );
    }

    [Fact]
    public void Inspect_DifferentRequestedCasing_RemainsDistinctGroups()
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            Inventory(
                Winner(
                    "ArmorA",
                    "Winner.esp",
                    10,
                    Reference(
                        "ArmorA",
                        "WorldModel.Male",
                        "Meshes/Sword.nif",
                        "Meshes/Sword.nif"
                    ),
                    Reference(
                        "ArmorA",
                        "WorldModel.Female",
                        "meshes/Sword.nif",
                        "meshes/Sword.nif"
                    )
                )
            );

        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootLogicalName:
                    "Meshes",
                rootCasefold:
                    true,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            false
                        )
                    },
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Meshes",
                            WindowsNamespacePhysicalObjectKind.Directory
                        ),
                        ParticipantSpec(
                            "Meshes/Sword.nif",
                            WindowsNamespacePhysicalObjectKind.File
                        )
                    }
            );

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult result =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                inventory,
                new[]
                {
                    analysis
                }
            );

        Assert.Equal(2, result.ReferenceCount);
        Assert.Equal(2, result.UniqueRequestedPathCount);
        Assert.Equal(0, result.AvoidedLookupCalls);

        Assert.Equal(
            new[]
            {
                "Meshes/Sword.nif",
                "meshes/Sword.nif"
            },
            result.Paths
                .Select(path =>
                    path.RequestedPath
                )
                .ToArray()
        );
    }

    [Fact]
    public void Inspect_GroupPreservesEveryWinningReferenceContext()
    {
        SkyrimArmorAddonModelReference first =
            Reference(
                "ArmorA",
                "WorldModel.Male",
                @"Meshes\Foo\Sword.nif",
                "Meshes/Foo/Sword.nif"
            );

        SkyrimArmorAddonModelReference second =
            Reference(
                "ArmorB",
                "FirstPersonModel.Female",
                "source-form-differs",
                "Meshes/Foo/Sword.nif"
            );

        SkyrimWinningArmorAddonInventoryResult inventory =
            Inventory(
                Winner(
                    "ArmorA",
                    "WinnerA.esp",
                    14,
                    first
                ),
                Winner(
                    "ArmorB",
                    "WinnerB.esp",
                    29,
                    second
                )
            );

        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootLogicalName:
                    "Meshes",
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            false
                        ),
                        DirectorySpec(
                            "Meshes/Foo",
                            false
                        )
                    },
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Meshes",
                            WindowsNamespacePhysicalObjectKind.Directory
                        ),
                        ParticipantSpec(
                            "Meshes/Foo",
                            WindowsNamespacePhysicalObjectKind.Directory
                        ),
                        ParticipantSpec(
                            "Meshes/Foo/Sword.nif",
                            WindowsNamespacePhysicalObjectKind.File
                        )
                    }
            );

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult result =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                inventory,
                new[]
                {
                    analysis
                }
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence path =
            Assert.Single(result.Paths);

        Assert.Collection(
            path.References,
            item =>
            {
                Assert.Equal(
                    "WinnerA.esp",
                    item.WinningPluginName
                );
                Assert.Equal(
                    14,
                    item.WinningLoadOrderIndex
                );
                Assert.Same(
                    first,
                    item.Reference
                );
                Assert.Equal(
                    @"Meshes\Foo\Sword.nif",
                    item.Reference.GivenPath
                );
            },
            item =>
            {
                Assert.Equal(
                    "WinnerB.esp",
                    item.WinningPluginName
                );
                Assert.Equal(
                    29,
                    item.WinningLoadOrderIndex
                );
                Assert.Same(
                    second,
                    item.Reference
                );
                Assert.Equal(
                    "source-form-differs",
                    item.Reference.GivenPath
                );
            }
        );
    }

    [Fact]
    public void Inspect_MissingLookup_RemainsPathLevelLookupProducedAndMissing()
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            Inventory(
                Winner(
                    "ArmorA",
                    "Winner.esp",
                    10,
                    Reference(
                        "ArmorA",
                        "WorldModel.Male",
                        "Meshes/Missing.nif",
                        "Meshes/Missing.nif"
                    )
                )
            );

        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootLogicalName:
                    "Meshes",
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            false
                        )
                    },
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Meshes",
                            WindowsNamespacePhysicalObjectKind.Directory
                        ),
                        ParticipantSpec(
                            "Meshes/Other.nif",
                            WindowsNamespacePhysicalObjectKind.File
                        )
                    }
            );

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult result =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                inventory,
                new[]
                {
                    analysis
                }
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence path =
            Assert.Single(result.Paths);

        Assert.True(path.HasLookup);

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState.LookupProduced,
            path.State
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.Missing,
            path.Lookup!.State
        );

        Assert.False(path.Lookup.Success);
    }

    [Fact]
    public void Inspect_IndeterminateLookupState_RemainsIntact()
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            Inventory(
                Winner(
                    "ArmorA",
                    "Winner.esp",
                    10,
                    Reference(
                        "ArmorA",
                        "WorldModel.Male",
                        "Meshes/Sword.nif",
                        "Meshes/Sword.nif"
                    )
                )
            );

        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootLogicalName:
                    "Meshes",
                rootCasefold:
                    true,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "meshes",
                            false
                        )
                    },
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "meshes",
                            WindowsNamespacePhysicalObjectKind.Directory
                        ),
                        ParticipantSpec(
                            "meshes/Sword.nif",
                            WindowsNamespacePhysicalObjectKind.File
                        )
                    },
                dataRootChildNames:
                    new[]
                    {
                        "meshes",
                        "Ünicode"
                    }
            );

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult result =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                inventory,
                new[]
                {
                    analysis
                }
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence path =
            Assert.Single(result.Paths);

        Assert.True(path.HasLookup);

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState
                .CasefoldEquivalenceUnknown,
            path.Lookup!.State
        );
    }

    [Fact]
    public void Inspect_InvalidRequestedPath_RemainsCompositionFailure()
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            Inventory(
                Winner(
                    "ArmorA",
                    "Winner.esp",
                    10,
                    Reference(
                        "ArmorA",
                        "WorldModel.Male",
                        "Meshes//Sword.nif",
                        "Meshes//Sword.nif"
                    )
                )
            );

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult result =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                inventory,
                Array.Empty<WindowsNamespaceAnalysis>()
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence path =
            Assert.Single(result.Paths);

        Assert.False(path.HasLookup);

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .InvalidRequestedPath,
            path.State
        );

        Assert.Null(path.RequestedRootLogicalPath);
        Assert.Null(path.SelectedAnalysis);
        Assert.Null(path.Lookup);
    }

    [Fact]
    public void Inspect_NoMatchingAnalysis_RemainsCompositionFailure()
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            Inventory(
                Winner(
                    "ArmorA",
                    "Winner.esp",
                    10,
                    Reference(
                        "ArmorA",
                        "WorldModel.Male",
                        "Meshes/Sword.nif",
                        "Meshes/Sword.nif"
                    )
                )
            );

        WindowsNamespaceAnalysis textures =
            Analysis(
                rootLogicalName:
                    "Textures",
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Textures",
                            false
                        )
                    },
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Textures",
                            WindowsNamespacePhysicalObjectKind.Directory
                        ),
                        ParticipantSpec(
                            "Textures/Example.dds",
                            WindowsNamespacePhysicalObjectKind.File
                        )
                    }
            );

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult result =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                inventory,
                new[]
                {
                    textures
                }
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence path =
            Assert.Single(result.Paths);

        Assert.False(path.HasLookup);

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .NoMatchingNamespaceAnalysis,
            path.State
        );

        Assert.Equal(0, path.MatchingAnalysisCount);
        Assert.Null(path.SelectedAnalysis);
        Assert.Null(path.Lookup);
    }

    [Fact]
    public void Inspect_EmptyWinningInventory_ProducesEmptyEvidence()
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            Inventory();

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult result =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                inventory,
                Array.Empty<WindowsNamespaceAnalysis>()
            );

        Assert.Same(inventory, result.Inventory);
        Assert.Empty(result.Paths);
        Assert.Equal(0, result.ReferenceCount);
        Assert.Equal(0, result.UniqueRequestedPathCount);
        Assert.Equal(0, result.AvoidedLookupCalls);
        Assert.True(result.WinnerSearchComplete);
    }

    private sealed record DirectorySpecValue(
        string RelativePath,
        bool? Casefold
    );

    private sealed record ParticipantSpecValue(
        string RelativePath,
        WindowsNamespacePhysicalObjectKind Kind
    );

    private static SkyrimWinningArmorAddonInventoryResult Inventory(
        params SkyrimWinningArmorAddonRecord[] winners)
    {
        return new SkyrimWinningArmorAddonInventoryResult(
            DataRoot:
                "/fixture/Data",
            RuntimeActivePluginCount:
                winners.Length,
            PluginsOpened:
                winners.Length,
            MissingPluginFiles:
                Array.Empty<string>(),
            ReadErrors:
                Array.Empty<SkyrimPluginReadError>(),
            Winners:
                winners
        );
    }

    private static SkyrimWinningArmorAddonRecord Winner(
        string formKey,
        string pluginName,
        int loadOrderIndex,
        params SkyrimArmorAddonModelReference[] references)
    {
        return new SkyrimWinningArmorAddonRecord(
            FormKey:
                formKey,
            EditorId:
                formKey,
            WinningPluginName:
                pluginName,
            WinningLoadOrderIndex:
                loadOrderIndex,
            ModelReferences:
                references
        );
    }

    private static SkyrimArmorAddonModelReference Reference(
        string formKey,
        string field,
        string givenPath,
        string dataRelativePath)
    {
        return new SkyrimArmorAddonModelReference(
            FormKey:
                formKey,
            EditorId:
                formKey,
            Field:
                field,
            GivenPath:
                givenPath,
            DataRelativePath:
                dataRelativePath
        );
    }

    private static ParticipantSpecValue ParticipantSpec(
        string relativePath,
        WindowsNamespacePhysicalObjectKind kind)
    {
        return new ParticipantSpecValue(
            relativePath,
            kind
        );
    }

    private static DirectorySpecValue DirectorySpec(
        string relativePath,
        bool? casefold)
    {
        return new DirectorySpecValue(
            relativePath,
            casefold
        );
    }

    private static WindowsNamespaceAnalysis Analysis(
        string rootLogicalName,
        bool? rootCasefold,
        IReadOnlyList<DirectorySpecValue> directories,
        IReadOnlyList<ParticipantSpecValue> participants,
        IReadOnlyList<string>? dataRootChildNames = null)
    {
        WindowsNamespacePhysicalParticipant[] physicalParticipants =
            participants
                .Select(
                    (spec, index) =>
                        CreateParticipant(
                            spec.RelativePath,
                            spec.Kind,
                            inode:
                                (ulong)(
                                    100 +
                                    index
                                )
                        )
                )
                .ToArray();

        WindowsNamespaceNode[] nodes =
            physicalParticipants
                .GroupBy(
                    participant =>
                        WindowsLogicalPath.FromRelativePath(
                            participant.RelativePath
                        )
                )
                .Select(
                    group =>
                        new WindowsNamespaceNode(
                            LogicalPath:
                                group.Key,
                            Participants:
                                group
                                    .OrderBy(
                                        participant =>
                                            participant.RelativePath,
                                        StringComparer.Ordinal
                                    )
                                    .ToArray()
                        )
                )
                .OrderBy(
                    node =>
                        node.LogicalPath.Value,
                    StringComparer.Ordinal
                )
                .ToArray();

        WindowsNamespaceDirectoryLookupObservation[] lookup =
            new[]
            {
                Lookup(
                    ".",
                    rootCasefold
                )
            }
            .Concat(
                directories.Select(
                    directory =>
                        Lookup(
                            directory.RelativePath,
                            directory.Casefold
                        )
                )
            )
            .ToArray();

        string[] completeRootChildNames =
            (
                dataRootChildNames ??
                physicalParticipants
                    .Where(
                        participant =>
                            !participant.RelativePath.Contains(
                                '/',
                                StringComparison.Ordinal
                            )
                    )
                    .Select(
                        participant =>
                            participant.Name
                    )
            )
            .OrderBy(
                name =>
                    name,
                StringComparer.Ordinal
            )
            .ToArray();

        return new WindowsNamespaceAnalysis(
            DataRootPath:
                "/fixture/Data",
            RootLogicalPath:
                WindowsLogicalPath.FromRelativePath(
                    rootLogicalName
                ),
            DirectoryLookupObservations:
                lookup,
            DirectoryIncarnationObservations:
                Array.Empty<
                    WindowsNamespaceDirectoryIncarnationObservation
                >(),
            FileIncarnationObservations:
                Array.Empty<
                    WindowsNamespaceFileIncarnationObservation
                >(),
            Nodes:
                nodes,
            Errors:
                Array.Empty<string>()
        ) with
        {
            DataRootChildNames =
                completeRootChildNames
        };
    }

    private static WindowsNamespaceDirectoryLookupObservation Lookup(
        string relativePath,
        bool? casefold)
    {
        string fullPath =
            string.Equals(
                relativePath,
                ".",
                StringComparison.Ordinal
            )
                ? "/fixture/Data"
                : "/fixture/Data/" +
                    relativePath;

        return new WindowsNamespaceDirectoryLookupObservation(
            FullPath:
                fullPath,
            RelativePath:
                relativePath,
            CasefoldEnabled:
                casefold,
            RawFlags:
                casefold is null
                    ? null
                    : casefold.Value
                        ? 0x40000000
                        : 0,
            Error:
                null
        );
    }

    private static WindowsNamespacePhysicalParticipant CreateParticipant(
        string relativePath,
        WindowsNamespacePhysicalObjectKind kind,
        ulong inode)
    {
        string normalized =
            relativePath.Replace(
                '\\',
                '/'
            );

        string name =
            normalized
                .Split('/')
                [^1];

        return new WindowsNamespacePhysicalParticipant(
            FullPath:
                "/fixture/Data/" +
                normalized,
            RelativePath:
                normalized,
            Name:
                name,
            Kind:
                kind,
            DeviceMajor:
                8,
            DeviceMinor:
                1,
            Inode:
                inode,
            MountId:
                42,
            IdentityError:
                null
        );
    }
}
