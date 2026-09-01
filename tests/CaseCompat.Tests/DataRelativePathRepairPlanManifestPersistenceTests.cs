using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairPlanManifestPersistenceTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            9,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void Create_LinearPlan_AssignsOrderedExactJournalNames()
    {
        Guid planId =
            Guid.Parse(
                "11111111-2222-3333-4444-555555555555"
            );

        DataRelativePathRepairPlanManifestCreation creation =
            CreateManifest(
                planId
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        DataRelativePathRepairPlanManifestRecord manifest =
            creation.Manifest!;

        Assert.Equal(
            3,
            manifest.Operations.Count
        );

        Assert.Equal(
            Enumerable.Range(
                0,
                3
            ),
            manifest.Operations
                .Select(
                    entry =>
                        entry.Index
                )
        );

        Assert.Equal(
            ".casecompat-plan-" +
            "11111111222233334444555555555555-" +
            "op-0000-directory.json",
            manifest.Operations[0].JournalChildName
        );

        Assert.Equal(
            ".casecompat-plan-" +
            "11111111222233334444555555555555-" +
            "op-0001-directory.json",
            manifest.Operations[1].JournalChildName
        );

        Assert.Equal(
            ".casecompat-plan-" +
            "11111111222233334444555555555555-" +
            "op-0002-file.json",
            manifest.Operations[2].JournalChildName
        );

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_AlteredOperationJournalName_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestOperation[] operations =
            manifest.Operations
                .ToArray();

        operations[1] =
            operations[1] with
            {
                JournalChildName =
                    "different.json"
            };

        DataRelativePathRepairPlanManifestRecord changed =
            manifest with
            {
                Operations =
                    operations
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                changed
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "deterministic",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_RequestedPathMatchingFinalDestination_IsAccepted()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        Assert.Equal(
            "meshes/Fafny stash/Bishop Armor/armor.nif",
            manifest.RequestedPath
        );

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_RequestedPathDifferentFromFinalDestination_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord changed =
            manifest with
            {
                RequestedPath =
                    "meshes/Different stash/" +
                    "Bishop Armor/armor.nif"
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                changed
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "final CreateFile destination",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_RequestedPathCaseDifferenceInsideRepairSuffix_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        /*
         * A future fix may allow case-only differences in the
         * already-resolved prefix, such as requested "Meshes"
         * versus physical "meshes".
         *
         * It must not weaken the strict repair suffix. The operation
         * chain creates "Bishop Armor/armor.nif", so changing that
         * strict suffix to "bishop armor/armor.nif" must remain invalid.
         */
        DataRelativePathRepairPlanManifestRecord changed =
            manifest with
            {
                RequestedPath =
                    "Meshes/Fafny stash/" +
                    "bishop armor/armor.nif"
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                changed
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "final CreateFile destination",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Json_SchemaVersion1_OmitsOptionalResolvedPrefixEvidence()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        Assert.Equal(
            1,
            manifest.SchemaVersion
        );

        Assert.Null(
            manifest.ResolvedPrefixSteps
        );

        byte[] json =
            DataRelativePathRepairPlanManifestJson.Serialize(
                manifest
            );

        string jsonText =
            System.Text.Encoding.UTF8.GetString(
                json
            );

        /*
         * A schema-v1 document predates this property. While the value is
         * null, serialization must omit it so the v1 wire representation
         * does not acquire v2-only evidence accidentally.
         */
        Assert.DoesNotContain(
            "\"ResolvedPrefixSteps\"",
            jsonText
        );

        DataRelativePathRepairPlanManifestRecord restored =
            Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                DataRelativePathRepairPlanManifestJson.Deserialize(
                    json
                )
            );

        Assert.Equal(
            1,
            restored.SchemaVersion
        );

        Assert.Null(
            restored.ResolvedPrefixSteps
        );

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                restored
            )
        );
    }

    [Fact]
    public void Validate_SchemaVersion1_WithResolvedPrefixEvidence_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord changed =
            manifest with
            {
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "meshes",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            true,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .ExactSpelling,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        Assert.Equal(
            1,
            changed.SchemaVersion
        );

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                changed
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "schema",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_WithCasefoldPrefixEvidence_IsAccepted()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        /*
         * Physical operation chain:
         *
         *   /game/Data/meshes/Fafny stash/
         *       Bishop Armor/armor.nif
         *
         * Literal Skyrim-facing request:
         *
         *   Meshes/Fafny stash/Bishop Armor/armor.nif
         *
         * Only component 0 differs, and its durable evidence states that
         * the containing Data directory was casefold-enabled.
         */
        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    2,
                RequestedPath =
                    "Meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "Meshes",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            true,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .CasefoldEquivalent,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.Null(
            error
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_CasefoldEquivalentWithoutCasefoldParent_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    2,
                RequestedPath =
                    "Meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "Meshes",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            false,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .CasefoldEquivalent,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "casefold-enabled physical parent",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_TruncatedResolvedPrefixEvidence_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    2,
                RequestedPath =
                    "Meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                    []
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "does not terminate at the initial destination parent",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_EquivalentHierarchySplit_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    2,
                RequestedPath =
                    "Meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "Meshes",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            true,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .CasefoldEquivalent,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            [
                                "meshes",
                                "Meshes"
                            ]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "exactly one equivalent physical name",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_RequestedPrefixEvidenceTamper_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    2,
                RequestedPath =
                    "Meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "MESHES",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            true,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .CasefoldEquivalent,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "final CreateFile destination",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_ExactSpellingPrefixTamper_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        /*
         * Tamper both RequestedPath and its durable evidence together.
         *
         * This bypasses the earlier RequestedComponent-to-RequestedPath
         * binding and proves that ExactSpelling itself remains ordinal.
         */
        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion2,
                RequestedPath =
                    "MESHES/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "MESHES",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            false,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .ExactSpelling,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "final CreateFile destination",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_ParentPhysicalPathTamper_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion2,
                RequestedPath =
                    "Meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "Meshes",
                        ParentPhysicalPath:
                            "/game/OtherData",
                        ParentCasefoldEnabled:
                            true,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .CasefoldEquivalent,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "physical parent hierarchy",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_NoncontiguousComponentIndex_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion2,
                RequestedPath =
                    "Meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            1,
                        RequestedComponent:
                            "Meshes",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            true,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .CasefoldEquivalent,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "not contiguous from component zero",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_CasefoldEquivalentWithExactSpelling_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion2,
                RequestedPath =
                    "meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "meshes",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            true,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .CasefoldEquivalent,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "different physical spelling",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_NonEquivalentCasefoldNames_AreRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion2,
                RequestedPath =
                    "Mashes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "Mashes",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            true,
                        Kind:
                            DataRelativePathRepairPlanResolvedPrefixStepKind
                                .CasefoldEquivalent,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "not Windows-logically equivalent",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_SchemaVersion2_UnsupportedPrefixStepKind_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord version2 =
            manifest with
            {
                SchemaVersion =
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion2,
                RequestedPath =
                    "meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                ResolvedPrefixSteps =
                [
                    new(
                        ComponentIndex:
                            0,
                        RequestedComponent:
                            "meshes",
                        ParentPhysicalPath:
                            "/game/Data",
                        ParentCasefoldEnabled:
                            false,
                        Kind:
                            (DataRelativePathRepairPlanResolvedPrefixStepKind)
                                999,
                        SelectedPhysicalName:
                            "meshes",
                        EquivalentPhysicalNames:
                            ["meshes"]
                    )
                ]
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                version2
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "unsupported step kind",
            error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_UnsupportedSchemaVersion_IsRejected()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord changed =
            manifest with
            {
                SchemaVersion =
                    3
            };

        string? error =
            DataRelativePathRepairPlanManifest.Validate(
                changed
            );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "Unsupported plan-manifest schema version",
            error,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Json_RoundTrip_PreservesManifestEvidence()
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        byte[] json =
            DataRelativePathRepairPlanManifestJson.Serialize(
                manifest
            );

        DataRelativePathRepairPlanManifestRecord restored =
            Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                DataRelativePathRepairPlanManifestJson.Deserialize(
                    json
                )
            );

        Assert.Equal(
            manifest.SchemaVersion,
            restored.SchemaVersion
        );

        Assert.Equal(
            manifest.PlanId,
            restored.PlanId
        );

        Assert.Equal(
            manifest.CreatedUtc,
            restored.CreatedUtc
        );

        Assert.Equal(
            manifest.DataRoot,
            restored.DataRoot
        );

        Assert.Equal(
            manifest.RequestedPath,
            restored.RequestedPath
        );

        Assert.Equal(
            manifest.SourceSnapshot,
            restored.SourceSnapshot
        );

        Assert.Equal(
            manifest.InitialDestinationParentSnapshot,
            restored.InitialDestinationParentSnapshot
        );

        Assert.Equal(
            manifest.Operations.Count,
            restored.Operations.Count
        );

        for (
            int index = 0;
            index < manifest.Operations.Count;
            index++)
        {
            Assert.Equal(
                manifest.Operations[index],
                restored.Operations[index]
            );
        }

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                restored
            )
        );
    }

    [Fact]
    public void CreateInitial_ThenRead_RoundTripsDurably()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathRepairPlanManifestRecord manifest =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestWriterResult write =
            DataRelativePathRepairPlanManifestWriter.CreateInitial(
                fixture.ManifestDirectory,
                "plan.json",
                manifest
            );

        Assert.True(
            write.Success,
            write.Error
        );

        Assert.Equal(
            DataRelativePathRepairPlanManifestWriteState
                .CreatedDurably,
            write.State
        );

        DataRelativePathRepairPlanManifestReaderResult read =
            DataRelativePathRepairPlanManifestReader.Read(
                fixture.ManifestDirectory,
                "plan.json"
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            manifest.PlanId,
            read.Manifest!.PlanId
        );

        Assert.Equal(
            manifest.Operations.Count,
            read.Manifest.Operations.Count
        );

        Assert.NotNull(
            read.ManifestIncarnationIdentity
        );

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                read.Manifest
            )
        );
    }

    [Fact]
    public void CreateInitial_ExistingManifest_IsNotOverwritten()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathRepairPlanManifestRecord first =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestRecord second =
            RequireManifest(
                CreateManifest(
                    Guid.NewGuid()
                )
            );

        DataRelativePathRepairPlanManifestWriterResult firstWrite =
            DataRelativePathRepairPlanManifestWriter.CreateInitial(
                fixture.ManifestDirectory,
                "plan.json",
                first
            );

        Assert.True(
            firstWrite.Success,
            firstWrite.Error
        );

        DataRelativePathRepairPlanManifestWriterResult duplicate =
            DataRelativePathRepairPlanManifestWriter.CreateInitial(
                fixture.ManifestDirectory,
                "plan.json",
                second
            );

        Assert.False(
            duplicate.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanManifestWriteState
                .ManifestAlreadyExists,
            duplicate.State
        );

        DataRelativePathRepairPlanManifestReaderResult read =
            DataRelativePathRepairPlanManifestReader.Read(
                fixture.ManifestDirectory,
                "plan.json"
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            first.PlanId,
            read.Manifest!.PlanId
        );

        Assert.NotEqual(
            second.PlanId,
            read.Manifest.PlanId
        );
    }

    [Fact]
    public void Read_SymbolicLinkManifest_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string target =
            Path.Combine(
                fixture.RootPath,
                "target.json"
            );

        File.WriteAllText(
            target,
            "{}"
        );

        string link =
            Path.Combine(
                fixture.ManifestDirectoryPath,
                "plan.json"
            );

        File.CreateSymbolicLink(
            link,
            target
        );

        DataRelativePathRepairPlanManifestReaderResult read =
            DataRelativePathRepairPlanManifestReader.Read(
                fixture.ManifestDirectory,
                "plan.json"
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanManifestReadState
                .ManifestSymbolicLinkRejected,
            read.State
        );
    }

    private static DataRelativePathRepairPlanManifestCreation
        CreateManifest(
            Guid planId)
    {
        const string dataRoot =
            "/game/Data";

        const string source =
            "/game/Data/meshes/fafny stash/" +
            "Bishop Armor/armor.nif";

        const string initialParent =
            "/game/Data/meshes";

        var sourceSnapshot =
            new DataRelativePathRepairSourceSnapshot(
                PhysicalPath:
                    source,
                Size:
                    6,
                Sha256:
                    new string(
                        'A',
                        64
                    ),
                Identity:
                    Identity(
                        source,
                        inode:
                            100UL
                    )
            );

        var parentSnapshot =
            new DataRelativePathRepairDestinationParentSnapshot(
                PhysicalPath:
                    initialParent,
                Identity:
                    Identity(
                        initialParent,
                        inode:
                            200UL
                    ),
                CasefoldEnabled:
                    false,
                RawFlags:
                    0
            );

        DataRelativePathRepairPlanOperation[] operations =
        [
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/meshes/Fafny stash",
                SourcePath:
                    null
            ),

            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/meshes/Fafny stash/" +
                    "Bishop Armor",
                SourcePath:
                    null
            ),

            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile,
                DestinationPath:
                    "/game/Data/meshes/Fafny stash/" +
                    "Bishop Armor/armor.nif",
                SourcePath:
                    source
            )
        ];

        return DataRelativePathRepairPlanManifest.Create(
            planId,
            T0,
            dataRoot,
            "meshes/Fafny stash/Bishop Armor/armor.nif",
            sourceSnapshot,
            parentSnapshot,
            operations
        );
    }

    private static LinuxFileIdentityResult Identity(
        string path,
        ulong inode)
    {
        return new(
            FullPath:
                path,
            DeviceMajor:
                8U,
            DeviceMinor:
                1U,
            Inode:
                inode,
            LinkCount:
                1U,
            MountId:
                55UL,
            Error:
                null
        );
    }

    private static DataRelativePathRepairPlanManifestRecord
        RequireManifest(
            DataRelativePathRepairPlanManifestCreation creation)
    {
        Assert.True(
            creation.Success,
            creation.Error
        );

        return creation.Manifest!;
    }

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-plan-manifest-tests",
                    Guid.NewGuid().ToString("N")
                );

            ManifestDirectoryPath =
                Path.Combine(
                    RootPath,
                    "Manifest"
                );

            Directory.CreateDirectory(
                ManifestDirectoryPath
            );

            ManifestDirectory =
                OpenRoot(
                    ManifestDirectoryPath
                );
        }

        public string RootPath { get; }

        public string ManifestDirectoryPath { get; }

        public LinuxNoFollowPathHandle
            ManifestDirectory { get; }

        public bool SupportsUnnamedFiles()
        {
            LinuxCreateUnnamedFileAtResult probe =
                LinuxCreateUnnamedFileAt.Create(
                    ManifestDirectory
                );

            if (
                probe.State ==
                LinuxCreateUnnamedFileAtState
                    .TmpfileUnsupported)
            {
                return false;
            }

            Assert.True(
                probe.Success,
                probe.Error
            );

            probe.OpenedFile!.Dispose();

            return true;
        }

        private static LinuxNoFollowPathHandle OpenRoot(
            string path)
        {
            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    path
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            return Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
            );
        }

        public void Dispose()
        {
            ManifestDirectory.Dispose();

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
