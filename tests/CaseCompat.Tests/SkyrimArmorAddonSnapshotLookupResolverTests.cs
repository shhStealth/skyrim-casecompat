using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class SkyrimArmorAddonSnapshotLookupResolverTests
{
    [Fact]
    public void Resolve_ExactRootMatch_ProducesResolvedLookup()
    {
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

        SkyrimArmorAddonModelReference reference =
            Reference(
                "Meshes/Foo/Sword.nif"
            );

        SkyrimArmorAddonSnapshotLookupEvidence result =
            SkyrimArmorAddonSnapshotLookupResolver.Resolve(
                reference,
                new[]
                {
                    analysis
                }
            );

        Assert.True(result.HasLookup);
        Assert.Same(reference, result.Reference);
        Assert.Same(analysis, result.SelectedAnalysis);

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState.LookupProduced,
            result.State
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.Resolved,
            result.Lookup!.State
        );
    }

    [Fact]
    public void Resolve_RootCaseDiffers_WindowsLogicalMatchSelectsAnalysis()
    {
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

        SkyrimArmorAddonSnapshotLookupEvidence result =
            SkyrimArmorAddonSnapshotLookupResolver.Resolve(
                Reference(
                    "meshes/Foo/Sword.nif"
                ),
                new[]
                {
                    analysis
                }
            );

        Assert.True(result.HasLookup);

        Assert.Equal(
            "MESHES",
            result.RequestedRootLogicalPath!.Value.Value
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.Resolved,
            result.Lookup!.State
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupStepKind.CasefoldEquivalent,
            result.Lookup.Steps[0].Kind
        );
    }

    [Fact]
    public void Resolve_PassesOriginalRequestedSpellingToCheckpoint10A()
    {
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

        const string requested =
            @"Meshes\Foo\Sword.nif";

        SkyrimArmorAddonSnapshotLookupEvidence result =
            SkyrimArmorAddonSnapshotLookupResolver.Resolve(
                Reference(
                    requested
                ),
                new[]
                {
                    analysis
                }
            );

        Assert.True(result.HasLookup);

        Assert.Equal(
            requested,
            result.Reference.DataRelativePath
        );

        Assert.Equal(
            requested,
            result.Lookup!.RequestedRelativePath
        );
    }

    [Fact]
    public void Resolve_MalformedRequestedPath_FailsBeforeSelection()
    {
        SkyrimArmorAddonSnapshotLookupEvidence result =
            SkyrimArmorAddonSnapshotLookupResolver.Resolve(
                Reference(
                    "Meshes//Sword.nif"
                ),
                Array.Empty<WindowsNamespaceAnalysis>()
            );

        Assert.False(result.HasLookup);

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState.InvalidRequestedPath,
            result.State
        );

        Assert.Null(result.RequestedRootLogicalPath);
        Assert.Equal(0, result.MatchingAnalysisCount);
        Assert.Null(result.SelectedAnalysis);
        Assert.Null(result.Lookup);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Resolve_NoMatchingRoot_FailsClosed()
    {
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

        SkyrimArmorAddonSnapshotLookupEvidence result =
            SkyrimArmorAddonSnapshotLookupResolver.Resolve(
                Reference(
                    "Meshes/Sword.nif"
                ),
                new[]
                {
                    textures
                }
            );

        Assert.False(result.HasLookup);

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .NoMatchingNamespaceAnalysis,
            result.State
        );

        Assert.Equal(0, result.MatchingAnalysisCount);
        Assert.Null(result.SelectedAnalysis);
        Assert.Null(result.Lookup);
    }

    [Fact]
    public void Resolve_DuplicateMatchingRoots_FailsClosed()
    {
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
                            "Meshes/Sword.nif",
                            WindowsNamespacePhysicalObjectKind.File
                        )
                    }
            );

        SkyrimArmorAddonSnapshotLookupEvidence result =
            SkyrimArmorAddonSnapshotLookupResolver.Resolve(
                Reference(
                    "Meshes/Sword.nif"
                ),
                new[]
                {
                    analysis,
                    analysis
                }
            );

        Assert.False(result.HasLookup);

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .AmbiguousMatchingNamespaceAnalysis,
            result.State
        );

        Assert.Equal(2, result.MatchingAnalysisCount);
        Assert.Null(result.SelectedAnalysis);
        Assert.Null(result.Lookup);
    }

    [Fact]
    public void Resolve_MissingLookup_RemainsLookupProducedAndMissing()
    {
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

        SkyrimArmorAddonSnapshotLookupEvidence result =
            SkyrimArmorAddonSnapshotLookupResolver.Resolve(
                Reference(
                    "Meshes/Foo/Missing.nif"
                ),
                new[]
                {
                    analysis
                }
            );

        Assert.True(result.HasLookup);

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState.LookupProduced,
            result.State
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.Missing,
            result.Lookup!.State
        );

        Assert.False(result.Lookup.Success);
    }

    [Fact]
    public void Resolve_IndeterminateCasefoldLookup_RemainsIntact()
    {
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

        SkyrimArmorAddonSnapshotLookupEvidence result =
            SkyrimArmorAddonSnapshotLookupResolver.Resolve(
                Reference(
                    "Meshes/Sword.nif"
                ),
                new[]
                {
                    analysis
                }
            );

        Assert.True(result.HasLookup);

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState.LookupProduced,
            result.State
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState
                .CasefoldEquivalenceUnknown,
            result.Lookup!.State
        );

        Assert.False(result.Lookup.Success);
    }

    private sealed record DirectorySpecValue(
        string RelativePath,
        bool? Casefold
    );

    private sealed record ParticipantSpecValue(
        string RelativePath,
        WindowsNamespacePhysicalObjectKind Kind
    );

    private static SkyrimArmorAddonModelReference Reference(
        string dataRelativePath)
    {
        return new SkyrimArmorAddonModelReference(
            FormKey:
                "00000001:Fixture.esp",
            EditorId:
                "FixtureArmorAddon",
            Field:
                "MaleWorldModel",
            GivenPath:
                dataRelativePath,
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
