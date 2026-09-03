using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class SkyrimWinningArmorAddonSnapshotConsumerProjectorTests
{
    [Fact]
    public void Project_DuplicateConsumersShareSingleInterpretationInstance()
    {
        SkyrimArmorAddonModelReference first =
            Reference(
                "ArmorA",
                "WorldModel.Male",
                "Meshes/Sword.nif"
            );

        SkyrimArmorAddonModelReference second =
            Reference(
                "ArmorB",
                "WorldModel.Female",
                "Meshes/Sword.nif"
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence path =
            Path(
                "Meshes/Sword.nif",
                WindowsNamespaceSnapshotFileLookupState.Resolved,
                Context(
                    "WinnerA.esp",
                    10,
                    first
                ),
                Context(
                    "WinnerB.esp",
                    20,
                    second
                )
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerProjector.Project(
                Scan(
                    path
                )
            );

        Assert.Equal(
            1,
            result.PathInterpretationCount
        );

        Assert.Equal(
            2,
            result.ConsumerCount
        );

        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Assert.Single(
                result.PathInterpretations
            );

        Assert.Same(
            interpretation,
            result.Consumers[0].PathInterpretation
        );

        Assert.Same(
            interpretation,
            result.Consumers[1].PathInterpretation
        );

        Assert.Equal(
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .LooseResolved,
            interpretation.State
        );
    }

    [Fact]
    public void Project_DifferentPathsReceiveDistinctInterpretations()
    {
        SkyrimWinningArmorAddonSnapshotPathEvidence firstPath =
            Path(
                "Meshes/A.nif",
                WindowsNamespaceSnapshotFileLookupState.Resolved,
                Context(
                    "WinnerA.esp",
                    1,
                    Reference(
                        "ArmorA",
                        "Male",
                        "Meshes/A.nif"
                    )
                )
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence secondPath =
            Path(
                "Meshes/B.nif",
                WindowsNamespaceSnapshotFileLookupState.Missing,
                Context(
                    "WinnerB.esp",
                    2,
                    Reference(
                        "ArmorB",
                        "Male",
                        "Meshes/B.nif"
                    )
                )
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerProjector.Project(
                Scan(
                    firstPath,
                    secondPath
                )
            );

        Assert.Equal(
            2,
            result.PathInterpretationCount
        );

        Assert.Equal(
            2,
            result.ConsumerCount
        );

        Assert.NotSame(
            result.PathInterpretations[0],
            result.PathInterpretations[1]
        );

        Assert.Same(
            result.PathInterpretations[0],
            result.Consumers[0].PathInterpretation
        );

        Assert.Same(
            result.PathInterpretations[1],
            result.Consumers[1].PathInterpretation
        );
    }

    [Fact]
    public void Project_PreservesOriginalReferenceAndWinningContext()
    {
        SkyrimArmorAddonModelReference reference =
            Reference(
                "ArmorA",
                "WorldModel.Male",
                "Meshes/Exact.nif",
                givenPath:
                    "meshes\\Exact.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerProjector.Project(
                Scan(
                    Path(
                        "Meshes/Exact.nif",
                        WindowsNamespaceSnapshotFileLookupState.Resolved,
                        Context(
                            "Winning Plugin.esp",
                            314,
                            reference
                        )
                    )
                )
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection consumer =
            Assert.Single(
                result.Consumers
            );

        Assert.Equal(
            "Winning Plugin.esp",
            consumer.WinningPluginName
        );

        Assert.Equal(
            314,
            consumer.WinningLoadOrderIndex
        );

        Assert.Same(
            reference,
            consumer.Reference
        );

        Assert.Equal(
            "ArmorA",
            consumer.Reference.FormKey
        );

        Assert.Equal(
            "WorldModel.Male",
            consumer.Reference.Field
        );

        Assert.Equal(
            "meshes\\Exact.nif",
            consumer.Reference.GivenPath
        );

        Assert.Equal(
            "Meshes/Exact.nif",
            consumer.Reference.DataRelativePath
        );
    }

    [Fact]
    public void Project_PreservesPathAndReferenceOrder()
    {
        SkyrimArmorAddonModelReference first =
            Reference(
                "Armor1",
                "Male",
                "Meshes/Z.nif"
            );

        SkyrimArmorAddonModelReference second =
            Reference(
                "Armor2",
                "Female",
                "Meshes/Z.nif"
            );

        SkyrimArmorAddonModelReference third =
            Reference(
                "Armor3",
                "Male",
                "Meshes/A.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerProjector.Project(
                Scan(
                    Path(
                        "Meshes/Z.nif",
                        WindowsNamespaceSnapshotFileLookupState.Resolved,
                        Context(
                            "First.esp",
                            1,
                            first
                        ),
                        Context(
                            "Second.esp",
                            2,
                            second
                        )
                    ),
                    Path(
                        "Meshes/A.nif",
                        WindowsNamespaceSnapshotFileLookupState.Missing,
                        Context(
                            "Third.esp",
                            3,
                            third
                        )
                    )
                )
            );

        Assert.Equal(
            new[]
            {
                "Meshes/Z.nif",
                "Meshes/A.nif"
            },
            result.PathInterpretations
                .Select(
                    item =>
                        item.Evidence.RequestedPath
                )
                .ToArray()
        );

        Assert.Equal(
            new[]
            {
                first,
                second,
                third
            },
            result.Consumers
                .Select(
                    consumer =>
                        consumer.Reference
                )
                .ToArray()
        );
    }

    [Fact]
    public void Project_ResolvedAndMissingStatesRemainConsumerVisible()
    {
        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerProjector.Project(
                Scan(
                    Path(
                        "Meshes/Exists.nif",
                        WindowsNamespaceSnapshotFileLookupState.Resolved,
                        Context(
                            "Resolved.esp",
                            1,
                            Reference(
                                "ArmorResolved",
                                "Male",
                                "Meshes/Exists.nif"
                            )
                        )
                    ),
                    Path(
                        "Meshes/Missing.nif",
                        WindowsNamespaceSnapshotFileLookupState.Missing,
                        Context(
                            "Missing.esp",
                            2,
                            Reference(
                                "ArmorMissing",
                                "Male",
                                "Meshes/Missing.nif"
                            )
                        )
                    )
                )
            );

        Assert.Equal(
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .LooseResolved,
            result.Consumers[0].State
        );

        Assert.Equal(
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .LooseUnresolved,
            result.Consumers[1].State
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.Resolved,
            result.Consumers[0]
                .PathInterpretation
                .SnapshotState
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.Missing,
            result.Consumers[1]
                .PathInterpretation
                .SnapshotState
        );
    }

    [Fact]
    public void Project_MalformedPathEvidenceFailsClosedButRetainsConsumer()
    {
        SkyrimArmorAddonModelReference mismatchedReference =
            Reference(
                "ArmorA",
                "Male",
                "Meshes/Other.nif"
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence malformed =
            Path(
                "Meshes/Expected.nif",
                WindowsNamespaceSnapshotFileLookupState.Resolved,
                Context(
                    "Winner.esp",
                    10,
                    mismatchedReference
                )
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerProjector.Project(
                Scan(
                    malformed
                )
            );

        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Assert.Single(
                result.PathInterpretations
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection consumer =
            Assert.Single(
                result.Consumers
            );

        Assert.False(
            interpretation.EvidenceStructureValid
        );

        Assert.Equal(
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .IndeterminateEvidence,
            interpretation.State
        );

        Assert.False(
            string.IsNullOrWhiteSpace(
                interpretation.InterpretationError
            )
        );

        Assert.Same(
            interpretation,
            consumer.PathInterpretation
        );

        Assert.Same(
            mismatchedReference,
            consumer.Reference
        );
    }

    [Fact]
    public void Project_IncompleteWinnerSearchRemainsAggregateOnly()
    {
        SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan =
            Scan(
                searchComplete:
                    false,
                Path(
                    "Meshes/Sword.nif",
                    WindowsNamespaceSnapshotFileLookupState.Resolved,
                    Context(
                        "Winner.esp",
                        7,
                        Reference(
                            "ArmorA",
                            "Male",
                            "Meshes/Sword.nif"
                        )
                    )
                )
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerProjector.Project(
                scan
            );

        Assert.False(
            result.WinnerSearchComplete
        );

        SkyrimWinningArmorAddonSnapshotConsumerProjection consumer =
            Assert.Single(
                result.Consumers
            );

        Assert.Equal(
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .LooseResolved,
            consumer.State
        );

        Assert.True(
            consumer.PathInterpretation.Definitive
        );

        Assert.True(
            consumer.PathInterpretation.LooseResolves
        );
    }

    [Fact]
    public void Project_EmptyScanProducesEmptyProjection()
    {
        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerProjector.Project(
                Scan()
            );

        Assert.Empty(
            result.PathInterpretations
        );

        Assert.Empty(
            result.Consumers
        );

        Assert.Equal(
            0,
            result.PathInterpretationCount
        );

        Assert.Equal(
            0,
            result.ConsumerCount
        );

        Assert.True(
            result.WinnerSearchComplete
        );
    }

    private static SkyrimWinningArmorAddonSnapshotEvidenceScanResult Scan(
        params SkyrimWinningArmorAddonSnapshotPathEvidence[] paths)
    {
        return Scan(
            searchComplete:
                true,
            paths
        );
    }

    private static SkyrimWinningArmorAddonSnapshotEvidenceScanResult Scan(
        bool searchComplete,
        params SkyrimWinningArmorAddonSnapshotPathEvidence[] paths)
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
                RuntimeActivePluginCount:
                    searchComplete
                        ? 1
                        : 2,
                PluginsOpened:
                    searchComplete
                        ? 1
                        : 1,
                MissingPluginFiles:
                    searchComplete
                        ? Array.Empty<string>()
                        : new[]
                        {
                            "Missing.esp"
                        },
                ReadErrors:
                    Array.Empty<
                        SkyrimPluginReadError
                    >(),
                Winners:
                    Array.Empty<
                        SkyrimWinningArmorAddonRecord
                    >()
            );

        return new SkyrimWinningArmorAddonSnapshotEvidenceScanResult(
            Inventory:
                inventory,
            Paths:
                paths
        );
    }

    private static SkyrimWinningArmorAddonSnapshotReferenceContext Context(
        string pluginName,
        int loadOrderIndex,
        SkyrimArmorAddonModelReference reference)
    {
        return new SkyrimWinningArmorAddonSnapshotReferenceContext(
            WinningPluginName:
                pluginName,
            WinningLoadOrderIndex:
                loadOrderIndex,
            Reference:
                reference
        );
    }

    private static SkyrimArmorAddonModelReference Reference(
        string formKey,
        string field,
        string requestedPath,
        string? givenPath = null)
    {
        return new SkyrimArmorAddonModelReference(
            FormKey:
                formKey,
            EditorId:
                formKey + "Editor",
            Field:
                field,
            GivenPath:
                givenPath ??
                requestedPath,
            DataRelativePath:
                requestedPath
        );
    }

    private static SkyrimWinningArmorAddonSnapshotPathEvidence Path(
        string requestedPath,
        WindowsNamespaceSnapshotFileLookupState state,
        params SkyrimWinningArmorAddonSnapshotReferenceContext[] references)
    {
        WindowsNamespaceAnalysis analysis =
            Analysis();

        WindowsNamespaceSnapshotFileLookup lookup =
            Lookup(
                analysis,
                state,
                requestedPath
            );

        return new SkyrimWinningArmorAddonSnapshotPathEvidence(
            RequestedPath:
                requestedPath,
            References:
                references,
            RequestedRootLogicalPath:
                WindowsLogicalPath.FromRelativePath(
                    "Meshes"
                ),
            State:
                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .LookupProduced,
            MatchingAnalysisCount:
                1,
            SelectedAnalysis:
                analysis,
            Lookup:
                lookup,
            Error:
                null
        );
    }

    private static WindowsNamespaceSnapshotFileLookup Lookup(
        WindowsNamespaceAnalysis analysis,
        WindowsNamespaceSnapshotFileLookupState state,
        string requestedPath)
    {
        WindowsNamespacePhysicalParticipant? resolved =
            state ==
            WindowsNamespaceSnapshotFileLookupState.Resolved
                ? Participant(
                    requestedPath
                )
                : null;

        return new WindowsNamespaceSnapshotFileLookup(
            Analysis:
                analysis,
            RequestedRelativePath:
                requestedPath,
            RequestedLogicalPath:
                WindowsLogicalPath.FromRelativePath(
                    requestedPath
                ),
            State:
                state,
            ResolvedParticipant:
                resolved,
            FailedComponentIndex:
                state ==
                WindowsNamespaceSnapshotFileLookupState.Resolved
                    ? null
                    : 1,
            Steps:
                Array.Empty<
                    WindowsNamespaceSnapshotFileLookupStep
                >(),
            Error:
                state ==
                WindowsNamespaceSnapshotFileLookupState.Resolved
                    ? null
                    : "fixture"
        );
    }

    private static WindowsNamespaceAnalysis Analysis()
    {
        return new WindowsNamespaceAnalysis(
            DataRootPath:
                "/fixture/Data",
            RootLogicalPath:
                WindowsLogicalPath.FromRelativePath(
                    "Meshes"
                ),
            DirectoryLookupObservations:
                Array.Empty<
                    WindowsNamespaceDirectoryLookupObservation
                >(),
            DirectoryIncarnationObservations:
                Array.Empty<
                    WindowsNamespaceDirectoryIncarnationObservation
                >(),
            FileIncarnationObservations:
                Array.Empty<
                    WindowsNamespaceFileIncarnationObservation
                >(),
            Nodes:
                Array.Empty<
                    WindowsNamespaceNode
                >(),
            Errors:
                Array.Empty<string>()
        ) with
        {
            DataRootChildNames =
                Array.Empty<string>()
        };
    }

    private static WindowsNamespacePhysicalParticipant Participant(
        string requestedPath)
    {
        string normalized =
            requestedPath.Replace(
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
                WindowsNamespacePhysicalObjectKind.File,
            DeviceMajor:
                8,
            DeviceMinor:
                1,
            Inode:
                100,
            MountId:
                42,
            IdentityError:
                null
        );
    }
}
