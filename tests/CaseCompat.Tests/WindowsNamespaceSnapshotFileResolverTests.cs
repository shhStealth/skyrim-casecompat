using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class WindowsNamespaceSnapshotFileResolverTests
{
    [Fact]
    public void Resolve_ExactPhysicalSpellings_Resolves()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            casefold:
                                false
                        ),
                        DirectorySpec(
                            "Meshes/Foo",
                            casefold:
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

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes/Foo/Sword.nif"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.Resolved,
            result.State
        );

        Assert.Equal(
            "Meshes/Foo/Sword.nif",
            result.ResolvedPhysicalRelativePath
        );

        Assert.All(
            result.Steps,
            step =>
                Assert.Equal(
                    WindowsNamespaceSnapshotFileLookupStepKind
                        .ExactSpelling,
                    step.Kind
                )
        );
    }

    [Fact]
    public void Resolve_AsciiCasefoldEquivalentRoot_Resolves()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    true,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "meshes",
                            casefold:
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
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes/Sword.nif"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            "meshes/Sword.nif",
            result.ResolvedPhysicalRelativePath
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupStepKind
                .CasefoldEquivalent,
            result.Steps[0].Kind
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupStepKind
                .ExactSpelling,
            result.Steps[1].Kind
        );
    }

    [Fact]
    public void Resolve_StrictParentWrongCase_IsMissing()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "meshes",
                            casefold:
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
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes/Sword.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.Missing,
            result.State
        );

        Assert.Equal(
            0,
            result.FailedComponentIndex
        );

        Assert.Contains(
            "meshes",
            Assert.Single(
                result.Steps
            ).WindowsEquivalentPhysicalNames
        );
    }

    [Fact]
    public void Resolve_UnknownParentCasefold_IsIndeterminate()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    null,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "meshes",
                            casefold:
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
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes/Sword.nif"
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.CasefoldUnknown,
            result.State
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupStepKind
                .CasefoldUnknown,
            Assert.Single(
                result.Steps
            ).Kind
        );
    }

    [Fact]
    public void Resolve_MultipleAsciiCasefoldEquivalents_IsAmbiguous()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    true,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            casefold:
                                false
                        ),
                        DirectorySpec(
                            "meshes",
                            casefold:
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
                            "meshes",
                            WindowsNamespacePhysicalObjectKind.Directory
                        )
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "MESHES/file.nif"
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState
                .AmbiguousEquivalent,
            result.State
        );

        Assert.Equal(
            0,
            result.FailedComponentIndex
        );
    }

    [Fact]
    public void Resolve_CasefoldedRootWithUnrepresentedNonAsciiSibling_IsIndeterminate()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    true,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "meshes",
                            casefold:
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

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes/Sword.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState
                .CasefoldEquivalenceUnknown,
            result.State
        );

        Assert.Equal(
            0,
            result.FailedComponentIndex
        );

        WindowsNamespaceSnapshotFileLookupStep step =
            Assert.Single(
                result.Steps
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupStepKind
                .CasefoldEquivalenceUnknown,
            step.Kind
        );

        Assert.Equal(
            ".",
            step.ParentPhysicalRelativePath
        );

        Assert.Contains(
            "meshes",
            step.WindowsEquivalentPhysicalNames
        );
    }

    [Fact]
    public void Resolve_NonAsciiCasefoldDependency_IsIndeterminate()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            casefold:
                                true
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
                            "Meshes/Ünicode.nif",
                            WindowsNamespacePhysicalObjectKind.File
                        )
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes/ünicode.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState
                .CasefoldEquivalenceUnknown,
            result.State
        );

        Assert.Equal(
            1,
            result.FailedComponentIndex
        );

        Assert.Equal(
            2,
            result.Steps.Count
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupStepKind
                .ExactSpelling,
            result.Steps[0].Kind
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupStepKind
                .CasefoldEquivalenceUnknown,
            result.Steps[1].Kind
        );

        Assert.Equal(
            "Meshes",
            result.Steps[1].ParentPhysicalRelativePath
        );
    }

    [Fact]
    public void Resolve_IntermediateRegularFile_IsNotDirectory()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    false,
                directories:
                    Array.Empty<DirectorySpecValue>(),
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Meshes",
                            WindowsNamespacePhysicalObjectKind.File
                        )
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes/Foo.nif"
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.NotDirectory,
            result.State
        );

        Assert.Equal(
            0,
            result.FailedComponentIndex
        );
    }

    [Fact]
    public void Resolve_FinalDirectory_IsNotFile()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            casefold:
                                false
                        )
                    },
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Meshes",
                            WindowsNamespacePhysicalObjectKind.Directory
                        )
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes"
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.NotFile,
            result.State
        );
    }

    [Fact]
    public void Resolve_UnsupportedSelectedObject_FailsClosed()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    false,
                directories:
                    Array.Empty<DirectorySpecValue>(),
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Meshes",
                            WindowsNamespacePhysicalObjectKind.SymbolicLink
                        )
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes"
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.UnsupportedObject,
            result.State
        );
    }

    [Fact]
    public void Resolve_IncompleteAnalysis_IsRejected()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            casefold:
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
                    },
                errors:
                    new[]
                    {
                        "Synthetic pass-one failure."
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes/Sword.nif"
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.IncompleteAnalysis,
            result.State
        );

        Assert.Empty(
            result.Steps
        );
    }

    [Fact]
    public void Resolve_RequestOutsideAnalyzedNamespace_IsRejected()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            casefold:
                                false
                        )
                    },
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Meshes",
                            WindowsNamespacePhysicalObjectKind.Directory
                        )
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Textures/Foo.dds"
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState
                .RequestOutsideAnalyzedNamespace,
            result.State
        );
    }

    [Theory]
    [InlineData("../Meshes/Foo.nif")]
    [InlineData("Meshes/../Foo.nif")]
    [InlineData("/Meshes/Foo.nif")]
    [InlineData("Meshes/Foo.nif/")]
    [InlineData("Meshes//Foo.nif")]
    [InlineData(@"Meshes\\Foo.nif")]
    public void Resolve_TraversalOrRootedRequest_IsInvalid(
        string requestedPath)
    {
        WindowsNamespaceAnalysis analysis =
            Analysis(
                rootCasefold:
                    false,
                directories:
                    new[]
                    {
                        DirectorySpec(
                            "Meshes",
                            casefold:
                                false
                        )
                    },
                participants:
                    new[]
                    {
                        ParticipantSpec(
                            "Meshes",
                            WindowsNamespacePhysicalObjectKind.Directory
                        )
                    }
            );

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                requestedPath
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.InvalidRequestedPath,
            result.State
        );
    }

    [Fact]
    public void Resolve_MalformedParticipantNodeCorrelation_IsRejected()
    {
        WindowsNamespacePhysicalParticipant participant =
            CreateParticipant(
                "Meshes/Sword.nif",
                WindowsNamespacePhysicalObjectKind.File,
                inode:
                    100
            );

        WindowsNamespaceAnalysis analysis =
            new WindowsNamespaceAnalysis(
                DataRootPath:
                    "/fixture/Data",
                RootLogicalPath:
                    WindowsLogicalPath.FromRelativePath(
                        "Meshes"
                    ),
                DirectoryLookupObservations:
                    new[]
                    {
                        Lookup(
                            ".",
                            casefold:
                                false
                        )
                    },
                DirectoryIncarnationObservations:
                    Array.Empty<
                        WindowsNamespaceDirectoryIncarnationObservation
                    >(),
                FileIncarnationObservations:
                    Array.Empty<
                        WindowsNamespaceFileIncarnationObservation
                    >(),
                Nodes:
                    new[]
                    {
                        new WindowsNamespaceNode(
                            LogicalPath:
                                WindowsLogicalPath.FromRelativePath(
                                    "Meshes/Other.nif"
                                ),
                            Participants:
                                new[]
                                {
                                    participant
                                }
                        )
                    },
                Errors:
                    Array.Empty<string>()
            ) with
            {
                DataRootChildNames =
                    new[]
                    {
                        "Meshes"
                    }
            };

        WindowsNamespaceSnapshotFileLookup result =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                analysis,
                "Meshes/Sword.nif"
            );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState
                .InvalidSnapshotEvidence,
            result.State
        );
    }

    private sealed record ParticipantSpecValue(
        string RelativePath,
        WindowsNamespacePhysicalObjectKind Kind
    );

    private sealed record DirectorySpecValue(
        string RelativePath,
        bool? Casefold
    );

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
        bool? rootCasefold,
        IReadOnlyList<DirectorySpecValue> directories,
        IReadOnlyList<ParticipantSpecValue> participants,
        IReadOnlyList<string>? errors = null,
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
                    "Meshes"
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
                errors ??
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
