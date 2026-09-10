using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathTargetedConsumerCaseRepairApplyPersistenceTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            9,
            8,
            12,
            0,
            0,
            TimeSpan.Zero
        );

    // ---- Journal transitions (pure, no IO) ----

    [Fact]
    public void CreateIntent_ValidInputs_ProducesIntentRecorded()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            result =
                CreateIntent();

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .IntentRecorded,
            result.Record!.State
        );

        Assert.Null(
            result.Record.PreparedFileIncarnationIdentity
        );

        Assert.Null(
            result.Record.AppliedFileIncarnationIdentity
        );
    }

    [Fact]
    public void MarkPrepared_FromIntentRecorded_ProducesPrepared()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            prepared =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkPrepared(
                        intent,
                        FakeIdentity(
                            inode:
                                100UL
                        ),
                        T0.AddSeconds(
                            1
                        )
                    );

        Assert.True(
            prepared.Success,
            prepared.Error
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .Prepared,
            prepared.Record!.State
        );

        Assert.NotNull(
            prepared.Record.PreparedFileIncarnationIdentity
        );
    }

    [Fact]
    public void MarkPrepared_FromNonIntentRecorded_IsRejected()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
            prepared =
                RequirePrepared();

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            secondPrepare =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkPrepared(
                        prepared,
                        FakeIdentity(
                            inode:
                                200UL
                        ),
                        T0.AddSeconds(
                            2
                        )
                    );

        Assert.False(
            secondPrepare.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                .InvalidTransition,
            secondPrepare.State
        );
    }

    [Fact]
    public void MarkApplied_FromPrepared_ProducesApplied()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
            prepared =
                RequirePrepared();

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            applied =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkApplied(
                        prepared,
                        FakeIdentity(
                            inode:
                                101UL
                        ),
                        T0.AddSeconds(
                            2
                        )
                    );

        Assert.True(
            applied.Success,
            applied.Error
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .Applied,
            applied.Record!.State
        );
    }

    [Fact]
    public void MarkRolledBack_FromApplied_ProducesRolledBack()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord applied =
            RequireApplied();

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            rolledBack =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkRolledBack(
                        applied,
                        T0.AddSeconds(
                            3
                        )
                    );

        Assert.True(
            rolledBack.Success,
            rolledBack.Error
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .RolledBack,
            rolledBack.Record!.State
        );
    }

    [Fact]
    public void MarkRolledBack_FromNonApplied_IsRejected()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            rolledBack =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkRolledBack(
                        intent,
                        T0.AddSeconds(
                            1
                        )
                    );

        Assert.False(
            rolledBack.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                .InvalidTransition,
            rolledBack.State
        );
    }

    // ---- JSON round trip ----

    [Fact]
    public void Json_RoundTrip_PreservesRecord()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord applied =
            RequireApplied();

        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationResult
            encoded =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalJson
                    .SerializeValidated(
                        applied
                    );

        Assert.True(
            encoded.Success,
            encoded.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationResult
            decoded =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalJson
                    .DeserializeValidated(
                        encoded.Bytes!
                    );

        Assert.True(
            decoded.Success,
            decoded.Error
        );

        Assert.Equal(
            applied.PlanId,
            decoded.Record!.PlanId
        );

        Assert.Equal(
            applied.State,
            decoded.Record.State
        );

        Assert.Equal(
            applied.Operation,
            decoded.Record.Operation
        );
    }

    // ---- Writer / Reader ----

    [Fact]
    public void Writer_ThenReader_RoundTripsDurably()
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

        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            write =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        fixture.JournalDirectory,
                        "phase.json",
                        intent
                    );

        Assert.True(
            write.Success,
            write.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalReaderResult
            read =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReader
                    .Read(
                        fixture.JournalDirectory,
                        "phase.json"
                    );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            intent.PlanId,
            read.Phase!.PlanId
        );
    }

    [Fact]
    public void Writer_ExistingPhase_IsNotOverwritten()
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

        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord first =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord second =
            RequireRecord(
                CreateIntent(
                    planId:
                        Guid.NewGuid()
                )
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            firstWrite =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        fixture.JournalDirectory,
                        "phase.json",
                        first
                    );

        Assert.True(
            firstWrite.Success,
            firstWrite.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            duplicate =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        fixture.JournalDirectory,
                        "phase.json",
                        second
                    );

        Assert.False(
            duplicate.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                .JournalPhaseAlreadyExists,
            duplicate.State
        );
    }

    // ---- Executor ----

    [Fact]
    public void Execute_ValidPlan_CreatesDestinationAndAllThreeJournalPhases()
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

        byte[] sourceBytesBeforeApply =
            File.ReadAllBytes(
                fixture.SourcePath
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        // Apply publishes by renaming the source's own inode, not by
        // copying its bytes - the original mismatched-case name must no
        // longer exist afterward. Two files resolving the same asset by
        // different case is exactly the directory shape this design
        // exists to eliminate.
        Assert.False(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.Equal(
            sourceBytesBeforeApply,
            File.ReadAllBytes(
                fixture.DestinationPath
            )
        );

        Assert.NotNull(
            execution.IntentJournalChildName
        );

        Assert.NotNull(
            execution.PreparedJournalChildName
        );

        Assert.NotNull(
            execution.AppliedJournalChildName
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    fixture.JournalDirectoryPath,
                    execution.AppliedJournalChildName!
                )
            )
        );
    }

    [Fact]
    public void Execute_MultiOperationPlan_CreatesDirectoryThenFile()
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

        byte[] nestedSourceBytesBeforeApply =
            File.ReadAllBytes(
                fixture.NestedSourcePath
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreateMultiOperationPlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        Assert.Single(
            execution.CreatedDirectoryPaths
        );

        Assert.True(
            Directory.Exists(
                fixture.NestedDestinationParentPath
            )
        );

        Assert.True(
            File.Exists(
                fixture.NestedDestinationPath
            )
        );

        // The source lived under a differently-cased parent directory
        // ("inner" vs "Inner"), so this exercises the cross-directory
        // rename path specifically - both the old file name and its old
        // parent's entry for it must be gone.
        Assert.False(
            File.Exists(
                fixture.NestedSourcePath
            )
        );

        Assert.Equal(
            nestedSourceBytesBeforeApply,
            File.ReadAllBytes(
                fixture.NestedDestinationPath
            )
        );

        Assert.NotNull(
            execution.AppliedJournalChildName
        );
    }

    [Fact]
    public void
        Execute_RealPipeline_ExistingDirectoryWithSiblingsMovesTogether()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // A real end-to-end run through Candidate -> Project ->
        // DurablePlan.Create -> Execute, exercising directory-rename
        // discovery against a physically-existing, differently-cased
        // directory that also contains a sibling file this candidate
        // never touches. This is the actual regression this design
        // exists to prevent: an untouched sibling being left behind in
        // an abandoned old-cased directory while only the fixed file
        // moves to a new, otherwise-empty correctly-cased one.
        string dataRoot =
            RealPipelineDataRoot();

        string oldCasedDirectory =
            Path.Combine(
                dataRoot,
                "meshes",
                "018auri"
            );

        Directory.CreateDirectory(
            oldCasedDirectory
        );

        File.WriteAllText(
            Path.Combine(
                oldCasedDirectory,
                "femalehead.tri"
            ),
            "the fixed file"
        );

        File.WriteAllText(
            Path.Combine(
                oldCasedDirectory,
                "sibling.tri"
            ),
            "an untouched sibling"
        );

        string requestedPath =
            "Meshes/018Auri/FemaleHead.tri";

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            RealPipelineCandidate(
                dataRoot,
                requestedPath
            );

        using LinuxNoFollowPathHandle root =
            RealPipelineOpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection
            projection =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        root,
                        candidate
                    );

        Assert.True(
            projection.HasPlan,
            projection.Error
        );

        // Both "meshes" (top level) and "meshes/018auri" physically
        // exist under the wrong case - both must be discovered as
        // rename sources, not just the innermost one.
        Assert.Equal(
            2,
            projection.DirectoryRenameSources.Count
        );

        Assert.Contains(
            projection.DirectoryRenameSources,
            source =>
                source.PhysicalPath ==
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
        );

        Assert.Contains(
            projection.DirectoryRenameSources,
            source =>
                source.PhysicalPath ==
                oldCasedDirectory
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creation =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    Guid.NewGuid(),
                    T0,
                    projection
                );

        Assert.True(
            creation.Success,
            creation.Error
        );

        string journalDirectoryPath =
            Path.Combine(
                Path.GetDirectoryName(
                    dataRoot
                )!,
                "Journal"
            );

        Directory.CreateDirectory(
            journalDirectoryPath
        );

        using LinuxNoFollowPathHandle journalDirectory =
            RealPipelineOpenRoot(
                journalDirectoryPath
            );

        if (!SupportsUnnamedFilesAt(
                journalDirectory))
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                root,
                journalDirectory,
                creation.Record!,
                T0
            );

        Assert.True(
            execution.Success,
            $"State={execution.State} Error={execution.Error}"
        );

        string newCasedDirectory =
            Path.Combine(
                dataRoot,
                "Meshes",
                "018Auri"
            );

        // The whole directory moved: the fixed file under its new name,
        // and the untouched sibling right along with it, both content
        // preserved exactly.
        Assert.Equal(
            "the fixed file",
            File.ReadAllText(
                Path.Combine(
                    newCasedDirectory,
                    "FemaleHead.tri"
                )
            )
        );

        Assert.Equal(
            "an untouched sibling",
            File.ReadAllText(
                Path.Combine(
                    newCasedDirectory,
                    "sibling.tri"
                )
            )
        );

        // Nothing left behind under the old name at any level.
        Assert.False(
            Directory.Exists(
                oldCasedDirectory
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            )
        );
    }

    [Fact]
    public void
        Execute_RealPipeline_ContestedAncestor_AliasesInsteadOfRenamingSharedDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // A real end-to-end run through Candidate -> Project ->
        // DurablePlan.Create -> Execute, exercising the alias path this
        // Step-4 change adds. The uncontested "meshes" top level is
        // still renamed wholesale (as always), but the contested
        // "actors"/"Actors" ancestor beneath it must become a symlink
        // alias instead of a rename - and this deliberately exercises
        // that alias immediately following (in the same plan) an
        // ancestor that was ITSELF just renamed, since the alias's own
        // reopen must not depend on a now-stale absolute path string
        // captured at plan-build time.
        string dataRoot =
            RealPipelineDataRoot();

        string realActorsDirectory =
            Path.Combine(
                dataRoot,
                "meshes",
                "actors"
            );

        Directory.CreateDirectory(
            realActorsDirectory
        );

        File.WriteAllText(
            Path.Combine(
                realActorsDirectory,
                "candidatea.nif"
            ),
            "candidate A content"
        );

        File.WriteAllText(
            Path.Combine(
                realActorsDirectory,
                "candidateb.nif"
            ),
            "candidate B content"
        );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidateA =
            RealPipelineCandidate(
                dataRoot,
                "Meshes/Actors/CandidateA.nif"
            );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidateB =
            RealPipelineCandidate(
                dataRoot,
                "Meshes/actors/CandidateB.nif"
            );

        IReadOnlySet<string> contestedAncestorPrefixes =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                new[]
                {
                    candidateA.AuthoritativeRequestedPath,
                    candidateB.AuthoritativeRequestedPath
                }
            );

        Assert.Contains(
            "MESHES/ACTORS",
            contestedAncestorPrefixes
        );

        using LinuxNoFollowPathHandle root =
            RealPipelineOpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection
            projectionA =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        root,
                        candidateA,
                        contestedAncestorPrefixes
                    );

        Assert.True(
            projectionA.HasPlan,
            projectionA.Error
        );

        Assert.Single(
            projectionA.AliasSources
        );

        Assert.Contains(
            projectionA.Operations,
            op =>
                op.Kind ==
                DataRelativePathRepairPlanOperationKind
                    .CreateAliasSymlink
        );

        // Admission's own re-projection must be told about the same
        // contested-ancestor set the original projection saw, or it will
        // recompute a plain rename instead of an alias and reject this
        // perfectly valid plan as a mismatch against itself.
        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creationA =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    Guid.NewGuid(),
                    T0,
                    projectionA,
                    contestedAncestorPrefixes
                );

        Assert.True(
            creationA.Success,
            creationA.Error
        );

        string journalDirectoryPath =
            Path.Combine(
                Path.GetDirectoryName(
                    dataRoot
                )!,
                "Journal"
            );

        string aliasesDirectoryPath =
            Path.Combine(
                Path.GetDirectoryName(
                    dataRoot
                )!,
                "Aliases"
            );

        Directory.CreateDirectory(
            journalDirectoryPath
        );

        Directory.CreateDirectory(
            aliasesDirectoryPath
        );

        using LinuxNoFollowPathHandle journalDirectory =
            RealPipelineOpenRoot(
                journalDirectoryPath
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            RealPipelineOpenRoot(
                aliasesDirectoryPath
            );

        if (
            !SupportsUnnamedFilesAt(
                journalDirectory) ||
            !SupportsUnnamedFilesAt(
                aliasesDirectory))
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairApplyExecution executionA =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                root,
                journalDirectory,
                creationA.Record!,
                T0,
                aliasesDirectory
            );

        Assert.True(
            executionA.Success,
            $"State={executionA.State} Error={executionA.Error}"
        );

        string renamedTopLevel =
            Path.Combine(
                dataRoot,
                "Meshes"
            );

        Assert.True(
            Directory.Exists(
                renamedTopLevel
            )
        );

        // The one real "actors" directory must still be exactly where it
        // was - untouched and unrenamed - now reachable beneath the
        // renamed top level under its original lowercase name.
        string realActorsAfterTopRename =
            Path.Combine(
                renamedTopLevel,
                "actors"
            );

        Assert.True(
            Directory.Exists(
                realActorsAfterTopRename
            )
        );

        var aliasInfo =
            new FileInfo(
                Path.Combine(
                    renamedTopLevel,
                    "Actors"
                )
            );

        Assert.Equal(
            "actors",
            aliasInfo.LinkTarget
        );

        // Candidate A's own file moved into place through the alias.
        Assert.False(
            File.Exists(
                Path.Combine(
                    realActorsDirectory,
                    "candidatea.nif"
                )
            )
        );

        Assert.Equal(
            "candidate A content",
            File.ReadAllText(
                Path.Combine(
                    renamedTopLevel,
                    "Actors",
                    "CandidateA.nif"
                )
            )
        );

        // Candidate B's own file, an untouched sibling in the same real
        // directory, must be completely unaffected by A's alias.
        Assert.Equal(
            "candidate B content",
            File.ReadAllText(
                Path.Combine(
                    realActorsAfterTopRename,
                    "candidateb.nif"
                )
            )
        );

        // The alias must genuinely expose the real directory's entire,
        // untouched content - not just candidate A's own moved file -
        // when read through the aliased (capitalized) name.
        //
        // Rediscovering candidate B through the full candidate-discovery
        // pipeline after this alias exists is deliberately out of scope
        // here: that pipeline's own case-insensitive namespace analyzer
        // (DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer)
        // is a separate, pre-existing component, independent of the plan
        // projector's own outer-loop alias recognition and ambiguous-match
        // collapsing (see TryCollapseAmbiguousMatches and
        // IsVerifiedKnownAlias). It is not yet alias-aware itself, and
        // treats the real directory plus its alias as an unresolved
        // multi-match conflict. This only matters for rediscovering
        // candidates in a later, separate re-scan after aliases already
        // exist on disk - a real batch run discovers every candidate
        // once, up front, before any of them apply (see
        // Execute_RealPipeline_SecondCandidateReusesExistingAliasAfterFirstApplies),
        // so it does not block the feature's actual motivating scenario.
        Assert.Equal(
            "candidate B content",
            File.ReadAllText(
                Path.Combine(
                    renamedTopLevel,
                    "Actors",
                    "candidateb.nif"
                )
            )
        );
    }

    [Fact]
    public void
        Execute_RealPipeline_SecondCandidateReusesExistingAliasAfterFirstApplies()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Three unrelated candidates share the same contested ancestor:
        // A and B both need "Actors" (capitalized); Z needs "actors"
        // (lowercase, matching the one real directory exactly) - this is
        // what makes the ancestor contested at all. "Meshes" itself is
        // already correctly cased from the start, so applying A never
        // needs to rename anything B's own SourceSnapshot depends on -
        // isolating this test from the unrelated, already-understood
        // "stale source after an ancestor rename" characteristic this
        // pipeline has always had.
        //
        // This is the actual motivating scenario for the whole alias
        // feature: a real batch run applies candidates one at a time,
        // so B's own Project call necessarily happens AFTER A's alias
        // already exists on disk. Without outer-loop alias recognition,
        // B's exact-match traversal would hit A's symlink and be wrongly
        // refused as a destination conflict.
        string dataRoot =
            RealPipelineDataRoot();

        string meshesDirectory =
            Path.Combine(
                dataRoot,
                "Meshes"
            );

        string realActorsDirectory =
            Path.Combine(
                meshesDirectory,
                "actors"
            );

        Directory.CreateDirectory(
            realActorsDirectory
        );

        File.WriteAllText(
            Path.Combine(
                realActorsDirectory,
                "candidatea.nif"
            ),
            "candidate A content"
        );

        File.WriteAllText(
            Path.Combine(
                realActorsDirectory,
                "candidateb2.nif"
            ),
            "candidate B content"
        );

        File.WriteAllText(
            Path.Combine(
                realActorsDirectory,
                "candidatez.nif"
            ),
            "candidate Z content"
        );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidateA =
            RealPipelineCandidate(
                dataRoot,
                "Meshes/Actors/CandidateA.nif"
            );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidateB =
            RealPipelineCandidate(
                dataRoot,
                "Meshes/Actors/CandidateB2.nif"
            );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidateZ =
            RealPipelineCandidate(
                dataRoot,
                "Meshes/actors/CandidateZ.nif"
            );

        IReadOnlySet<string> contestedAncestorPrefixes =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                new[]
                {
                    candidateA.AuthoritativeRequestedPath,
                    candidateB.AuthoritativeRequestedPath,
                    candidateZ.AuthoritativeRequestedPath
                }
            );

        Assert.Contains(
            "MESHES/ACTORS",
            contestedAncestorPrefixes
        );

        using LinuxNoFollowPathHandle root =
            RealPipelineOpenRoot(
                dataRoot
            );

        string journalDirectoryPath =
            Path.Combine(
                Path.GetDirectoryName(
                    dataRoot
                )!,
                "Journal"
            );

        string aliasesDirectoryPath =
            Path.Combine(
                Path.GetDirectoryName(
                    dataRoot
                )!,
                "Aliases"
            );

        Directory.CreateDirectory(
            journalDirectoryPath
        );

        Directory.CreateDirectory(
            aliasesDirectoryPath
        );

        using LinuxNoFollowPathHandle journalDirectory =
            RealPipelineOpenRoot(
                journalDirectoryPath
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            RealPipelineOpenRoot(
                aliasesDirectoryPath
            );

        if (
            !SupportsUnnamedFilesAt(
                journalDirectory) ||
            !SupportsUnnamedFilesAt(
                aliasesDirectory))
        {
            return;
        }

        // Candidate A applies first, creating the real alias.
        DataRelativePathTargetedConsumerCaseRepairPlanProjection
            projectionA =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        root,
                        candidateA,
                        contestedAncestorPrefixes,
                        aliasesDirectory
                    );

        Assert.True(
            projectionA.HasPlan,
            projectionA.Error
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creationA =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    Guid.NewGuid(),
                    T0,
                    projectionA,
                    contestedAncestorPrefixes,
                    aliasesDirectory
                );

        Assert.True(
            creationA.Success,
            creationA.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution executionA =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                root,
                journalDirectory,
                creationA.Record!,
                T0,
                aliasesDirectory
            );

        Assert.True(
            executionA.Success,
            $"State={executionA.State} Error={executionA.Error}"
        );

        var aliasInfo =
            new FileInfo(
                Path.Combine(
                    meshesDirectory,
                    "Actors"
                )
            );

        Assert.Equal(
            "actors",
            aliasInfo.LinkTarget
        );

        // Candidate B's own Project call now happens against a
        // filesystem where "Actors" already exists as A's symlink -
        // exactly the real batch-apply ordering.
        DataRelativePathTargetedConsumerCaseRepairPlanProjection
            projectionB =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        root,
                        candidateB,
                        contestedAncestorPrefixes,
                        aliasesDirectory
                    );

        Assert.True(
            projectionB.HasPlan,
            projectionB.Error
        );

        Assert.Single(
            projectionB.AliasSources
        );

        Assert.Contains(
            projectionB.Operations,
            op =>
                op.Kind ==
                DataRelativePathRepairPlanOperationKind
                    .CreateAliasSymlink
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creationB =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    Guid.NewGuid(),
                    T0,
                    projectionB,
                    contestedAncestorPrefixes,
                    aliasesDirectory
                );

        Assert.True(
            creationB.Success,
            creationB.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution executionB =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                root,
                journalDirectory,
                creationB.Record!,
                T0,
                aliasesDirectory
            );

        // The alias operation is a reuse (idempotent no-op), not a
        // conflict - B's own apply must succeed, not fail with
        // AliasAlreadyExists.
        Assert.True(
            executionB.Success,
            $"State={executionB.State} Error={executionB.Error}"
        );

        Assert.Equal(
            "candidate B content",
            File.ReadAllText(
                Path.Combine(
                    meshesDirectory,
                    "Actors",
                    "CandidateB2.nif"
                )
            )
        );

        // The alias itself is still exactly one symlink - B's apply did
        // not create a second, competing one.
        var aliasInfoAfterB =
            new FileInfo(
                Path.Combine(
                    meshesDirectory,
                    "Actors"
                )
            );

        Assert.Equal(
            "actors",
            aliasInfoAfterB.LinkTarget
        );

        Assert.Equal(
            "candidate A content",
            File.ReadAllText(
                Path.Combine(
                    meshesDirectory,
                    "Actors",
                    "CandidateA.nif"
                )
            )
        );
    }

    [Fact]
    public void
        Execute_RealPipeline_TwoRealDirectoriesStillRefuseAsAmbiguous()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Both "actors" and "Actors" physically exist as real,
        // populated, non-symlink directories - the genuine "someone
        // already manually created both" shape, distinct from the
        // alias shape. A requested casing that matches neither exactly
        // must still be refused as ambiguous, never guessed at, even
        // with alias recognition wired in.
        string dataRoot =
            RealPipelineDataRoot();

        string meshesDirectory =
            Path.Combine(
                dataRoot,
                "Meshes"
            );

        string lowercaseActors =
            Path.Combine(
                meshesDirectory,
                "actors"
            );

        string capitalizedActors =
            Path.Combine(
                meshesDirectory,
                "Actors"
            );

        Directory.CreateDirectory(
            lowercaseActors
        );

        Directory.CreateDirectory(
            capitalizedActors
        );

        File.WriteAllText(
            Path.Combine(
                lowercaseActors,
                "candidatea.nif"
            ),
            "lower content"
        );

        File.WriteAllText(
            Path.Combine(
                capitalizedActors,
                "upper.nif"
            ),
            "upper content"
        );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            RealPipelineCandidate(
                dataRoot,
                "Meshes/ACTORS/CandidateA.nif"
            );

        using LinuxNoFollowPathHandle root =
            RealPipelineOpenRoot(
                dataRoot
            );

        string aliasesDirectoryPath =
            Path.Combine(
                Path.GetDirectoryName(
                    dataRoot
                )!,
                "Aliases"
            );

        Directory.CreateDirectory(
            aliasesDirectoryPath
        );

        using LinuxNoFollowPathHandle aliasesDirectory =
            RealPipelineOpenRoot(
                aliasesDirectoryPath
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection
            projection =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        root,
                        candidate,
                        contestedAncestorPrefixes:
                            null,
                        aliasesDirectory:
                            aliasesDirectory
                    );

        Assert.False(
            projection.HasPlan
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                .DestinationParentAmbiguous,
            projection.State
        );
    }

    [Fact]
    public void Execute_RequiredDirectoryAlreadyExists_RefusesWithoutCreatingFile()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreateMultiOperationPlan();

        Directory.CreateDirectory(
            fixture.NestedDestinationParentPath
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                .DirectoryAlreadyExists,
            execution.State
        );

        Assert.False(
            File.Exists(
                fixture.NestedDestinationPath
            )
        );

        // Refusing before the rename must leave the source exactly
        // where it was.
        Assert.True(
            File.Exists(
                fixture.NestedSourcePath
            )
        );

        Assert.Empty(
            Directory.GetFiles(
                fixture.JournalDirectoryPath
            )
        );
    }

    [Fact]
    public void Execute_DestinationAlreadyExists_RefusesWithoutWritingIntentJournal()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        File.WriteAllText(
            fixture.DestinationPath,
            "already here"
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                .DestinationExists,
            execution.State
        );

        Assert.Null(
            execution.IntentJournalChildName
        );

        // Refusing before the rename must leave the source exactly
        // where it was.
        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.Empty(
            Directory.GetFiles(
                fixture.JournalDirectoryPath
            )
        );
    }

    // ---- Executor: CreateAliasSymlink ----

    [Fact]
    public void
        Execute_AliasOperation_CreatesSymlinkAndAppliesNestedFileInsideRealTarget()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (
            !fixture.SupportsUnnamedFiles() ||
            !fixture.SupportsUnnamedFilesAtAliasesDirectory())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreateAliasPlan(
                out string realTargetDirectoryPath,
                out string aliasLinkPath,
                out string finalSourcePath,
                out string finalDestinationPath
            );

        byte[] sourceBytesBeforeApply =
            File.ReadAllBytes(
                finalSourcePath
            );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0,
                fixture.AliasesDirectory
            );

        Assert.True(
            execution.Success,
            $"State={execution.State} Error={execution.Error}"
        );

        var linkInfo =
            new FileInfo(
                aliasLinkPath
            );

        Assert.True(
            linkInfo.LinkTarget is not null
        );

        Assert.Equal(
            Path.GetFileName(
                realTargetDirectoryPath
            ),
            linkInfo.LinkTarget
        );

        Assert.True(
            File.Exists(
                finalDestinationPath
            )
        );

        Assert.Equal(
            sourceBytesBeforeApply,
            File.ReadAllBytes(
                finalDestinationPath
            )
        );

        Assert.False(
            File.Exists(
                finalSourcePath
            )
        );

        // The alias's own registry entry must be durably recorded so a
        // later scan can recognize this specific symlink as one this
        // project created itself.
        DataRelativePathRepairAliasRegistryLookupResult lookup =
            DataRelativePathRepairAliasRegistry.TryFind(
                fixture.AliasesDirectory,
                Path.GetDirectoryName(
                    aliasLinkPath
                )!,
                Path.GetFileName(
                    aliasLinkPath
                )
            );

        Assert.True(
            lookup.Success,
            lookup.Error
        );

        Assert.Equal(
            Path.GetFileName(
                realTargetDirectoryPath
            ),
            lookup.Record!.TargetName
        );
    }

    [Fact]
    public void Execute_AliasOperation_AliasesDirectoryNotProvided_Refuses()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreateAliasPlan(
                out _,
                out string aliasLinkPath,
                out string finalSourcePath,
                out _
            );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                .AliasesDirectoryRequired,
            execution.State
        );

        Assert.False(
            File.Exists(
                aliasLinkPath
            )
        );

        Assert.True(
            File.Exists(
                finalSourcePath
            )
        );
    }

    [Fact]
    public void Execute_AliasOperation_AliasNameAlreadyExists_Refuses()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (
            !fixture.SupportsUnnamedFiles() ||
            !fixture.SupportsUnnamedFilesAtAliasesDirectory())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreateAliasPlan(
                out _,
                out string aliasLinkPath,
                out string finalSourcePath,
                out _
            );

        Directory.CreateDirectory(
            aliasLinkPath
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0,
                fixture.AliasesDirectory
            );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                .AliasAlreadyExists,
            execution.State
        );

        Assert.True(
            File.Exists(
                finalSourcePath
            )
        );

        Assert.Empty(
            Directory.GetFiles(
                fixture.JournalDirectoryPath
            )
        );
    }

    [Fact]
    public void
        Execute_AliasOperation_TargetIdentityChangedSincePlanning_Refuses()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (
            !fixture.SupportsUnnamedFiles() ||
            !fixture.SupportsUnnamedFilesAtAliasesDirectory())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreateAliasPlan(
                out string realTargetDirectoryPath,
                out _,
                out string finalSourcePath,
                out _
            );

        // Replace the alias target with a freshly-created directory of
        // the exact same name: same path, different inode. The source
        // file for the final CreateFile step lives inside this same
        // directory, so it is moved out and back via rename (which never
        // changes a file's own inode or generation) to isolate the
        // identity change to the target directory alone.
        string siblingContent =
            File.ReadAllText(
                Path.Combine(
                    realTargetDirectoryPath,
                    "sibling.dat"
                )
            );

        string temporarySourcePath =
            Path.Combine(
                fixture.SubDirectoryPath,
                "newfile.dat.tmp"
            );

        File.Move(
            finalSourcePath,
            temporarySourcePath
        );

        Directory.Delete(
            realTargetDirectoryPath,
            recursive:
                true
        );

        Directory.CreateDirectory(
            realTargetDirectoryPath
        );

        File.WriteAllText(
            Path.Combine(
                realTargetDirectoryPath,
                "sibling.dat"
            ),
            siblingContent
        );

        File.Move(
            temporarySourcePath,
            finalSourcePath
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0,
                fixture.AliasesDirectory
            );

        Assert.False(
            execution.Success
        );

        Assert.True(
            execution.State is
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasTargetIdentityMismatch or
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .AliasTargetGenerationMismatch,
            $"Unexpected execution state: {execution.State}"
        );
    }

    [Fact]
    public void Execute_SourceIdentityChangedSinceThePlanWasProjected_Refuses()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        byte[] sourceBytes =
            File.ReadAllBytes(
                fixture.SourcePath
            );

        File.Delete(
            fixture.SourcePath
        );

        File.WriteAllBytes(
            fixture.SourcePath,
            sourceBytes
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.False(
            execution.Success
        );

        /*
         * Deleting and recreating the source file always changes its
         * inode generation. Whether the device/inode number itself also
         * changes depends on the underlying filesystem's reuse timing,
         * so either freshness check firing is correct evidence that the
         * executor caught the tampered source.
         */
        Assert.True(
            execution.State is
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceIdentityMismatch or
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceGenerationMismatch,
            $"Unexpected execution state: {execution.State}"
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );
    }

    // ---- Rollback ----

    [Fact]
    public void Rollback_AfterSuccessfulApply_RemovesDestinationAndWritesRolledBackJournal()
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

        byte[] sourceBytesBeforeApply =
            File.ReadAllBytes(
                fixture.SourcePath
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            rollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            1
                        )
                    );

        Assert.True(
            rollback.Success,
            rollback.Error
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        // Rollback is the same rename in reverse, not a delete: the
        // original mismatched-case name must be restored with its
        // exact original content.
        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.Equal(
            sourceBytesBeforeApply,
            File.ReadAllBytes(
                fixture.SourcePath
            )
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    fixture.JournalDirectoryPath,
                    rollback.RolledBackJournalChildName!
                )
            )
        );
    }

    [Fact]
    public void Rollback_OriginalLocationOccupied_RefusesWithoutRenamingEitherFile()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        // Simulate something re-populating the original mismatched-case
        // name after apply - e.g. a mod update re-deploying its loose
        // file.
        File.WriteAllText(
            fixture.SourcePath,
            "reappeared after apply"
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            rollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            1
                        )
                    );

        Assert.False(
            rollback.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                .OriginalLocationOccupied,
            rollback.State
        );

        // Neither file should have moved: the reappeared original stays
        // put, and the correctly-cased destination stays put too.
        Assert.Equal(
            "reappeared after apply",
            File.ReadAllText(
                fixture.SourcePath
            )
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );
    }

    [Fact]
    public void Rollback_DestinationChangedSinceApply_RefusesToRemove()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        byte[] destinationBytes =
            File.ReadAllBytes(
                fixture.DestinationPath
            );

        File.Delete(
            fixture.DestinationPath
        );

        File.WriteAllBytes(
            fixture.DestinationPath,
            destinationBytes
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            rollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            1
                        )
                    );

        Assert.False(
            rollback.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                .DestinationIdentityMismatch,
            rollback.State
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        // Refusing before the rename-back must leave the original name
        // vacant, exactly as the successful apply left it.
        Assert.False(
            File.Exists(
                fixture.SourcePath
            )
        );
    }

    [Fact]
    public void Rollback_AlreadyRolledBack_IsRefusedIdempotently()
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

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            firstRollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            1
                        )
                    );

        Assert.True(
            firstRollback.Success,
            firstRollback.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            secondRollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            2
                        )
                    );

        Assert.False(
            secondRollback.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                .AlreadyRolledBack,
            secondRollback.State
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
        CreateIntent(
            Guid? planId = null)
    {
        const string dataRoot =
            "/game/Data";

        const string source =
            "/game/Data/sub/target.dat";

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
                    new LinuxFileIdentityResult(
                        FullPath:
                            source,
                        DeviceMajor:
                            8U,
                        DeviceMinor:
                            1U,
                        Inode:
                            100UL,
                        LinkCount:
                            1U,
                        MountId:
                            44UL,
                        Error:
                            null
                    )
            );

        var operation =
            new DataRelativePathRepairPlanOperation(
                Kind:
                    DataRelativePathRepairPlanOperationKind.CreateFile,
                DestinationPath:
                    "/game/Data/sub/Target.DAT",
                SourcePath:
                    source
            );

        return
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .CreateIntent(
                    planId ??
                    Guid.Parse(
                        "e2f6c1f2-2222-4444-8888-abcdefabcdef"
                    ),
                    T0,
                    dataRoot,
                    operation,
                    sourceSnapshot
                );
    }

    private static LinuxFileIncarnationIdentity FakeIdentity(
        ulong inode)
    {
        return new(
            PhysicalIdentity:
                new LinuxOpenedFileIdentityResult(
                    State:
                        LinuxOpenedFileIdentityState.Captured,
                    DeviceMajor:
                        8U,
                    DeviceMinor:
                        1U,
                    Inode:
                        inode,
                    LinkCount:
                        1U,
                    MountId:
                        44UL,
                    Errno:
                        null,
                    Error:
                        null
                ),
            InodeGeneration:
                123U
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
        RequirePrepared()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        return RequireRecord(
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .MarkPrepared(
                    intent,
                    FakeIdentity(
                        inode:
                            100UL
                    ),
                    T0.AddSeconds(
                        1
                    )
                )
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
        RequireApplied()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
            prepared =
                RequirePrepared();

        return RequireRecord(
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .MarkApplied(
                    prepared,
                    FakeIdentity(
                        inode:
                            101UL
                    ),
                    T0.AddSeconds(
                        2
                    )
                )
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
        RequireRecord(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
                result)
    {
        Assert.True(
            result.Success,
            result.Error
        );

        return result.Record!;
    }

    // ---- Real-pipeline helpers (Candidate -> Project), independent of
    // the hand-built Fixture plans above ----

    private static string RealPipelineDataRoot()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-targeted-apply-real-pipeline-tests",
                Guid.NewGuid().ToString("N"),
                "Data"
            );

        Directory.CreateDirectory(
            path
        );

        return Path.GetFullPath(
            path
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairCandidate
        RealPipelineCandidate(
            string dataRoot,
            string requestedPath)
    {
        using LinuxNoFollowPathHandle root =
            RealPipelineOpenRoot(
                dataRoot
            );

        string rootLogical =
            requestedPath
                .Split('/')[0]
                .ToUpperInvariant();

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
            current =
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                    .Analyze(
                        root,
                        rootLogical,
                        requestedPath
                    );

        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier.Classify(
                requestedPath.ToUpperInvariant(),
                new[]
                {
                    requestedPath
                }
            );

        DataRelativePathTargetedConsumerCaseRepairCandidateProjection
            projection =
                DataRelativePathTargetedConsumerCaseRepairCandidateProjector
                    .Project(
                        dataRoot,
                        consumer,
                        current
                    );

        Assert.True(
            projection.HasCandidate,
            projection.Error
        );

        return Assert.IsType<
            DataRelativePathTargetedConsumerCaseRepairCandidate
        >(
            projection.Candidate
        );
    }

    private static LinuxNoFollowPathHandle RealPipelineOpenRoot(
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

    private static bool SupportsUnnamedFilesAt(
        LinuxNoFollowPathHandle directory)
    {
        LinuxCreateUnnamedFileAtResult probe =
            LinuxCreateUnnamedFileAt.Create(
                directory
            );

        if (
            probe.State ==
            LinuxCreateUnnamedFileAtState.TmpfileUnsupported)
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

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-targeted-apply-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRootPath =
                Path.Combine(
                    RootPath,
                    "Data"
                );

            SubDirectoryPath =
                Path.Combine(
                    DataRootPath,
                    "sub"
                );

            JournalDirectoryPath =
                Path.Combine(
                    RootPath,
                    "Journal"
                );

            AliasesDirectoryPath =
                Path.Combine(
                    RootPath,
                    "Aliases"
                );

            Directory.CreateDirectory(
                SubDirectoryPath
            );

            Directory.CreateDirectory(
                JournalDirectoryPath
            );

            Directory.CreateDirectory(
                AliasesDirectoryPath
            );

            SourcePath =
                Path.Combine(
                    SubDirectoryPath,
                    "target.dat"
                );

            DestinationPath =
                Path.Combine(
                    SubDirectoryPath,
                    "Target.DAT"
                );

            File.WriteAllBytes(
                SourcePath,
                Encoding.ASCII.GetBytes(
                    "hello!"
                )
            );

            NestedSourceParentPath =
                Path.Combine(
                    SubDirectoryPath,
                    "inner"
                );

            Directory.CreateDirectory(
                NestedSourceParentPath
            );

            NestedSourcePath =
                Path.Combine(
                    NestedSourceParentPath,
                    "target2.dat"
                );

            NestedDestinationParentPath =
                Path.Combine(
                    SubDirectoryPath,
                    "Inner"
                );

            NestedDestinationPath =
                Path.Combine(
                    NestedDestinationParentPath,
                    "Target2.DAT"
                );

            File.WriteAllBytes(
                NestedSourcePath,
                Encoding.ASCII.GetBytes(
                    "nested!"
                )
            );

            DataRoot =
                OpenRoot(
                    DataRootPath
                );

            JournalDirectory =
                OpenRoot(
                    JournalDirectoryPath
                );

            AliasesDirectory =
                OpenRoot(
                    AliasesDirectoryPath
                );
        }

        public string RootPath { get; }

        public string DataRootPath { get; }

        public string SubDirectoryPath { get; }

        public string JournalDirectoryPath { get; }

        public string AliasesDirectoryPath { get; }

        public string SourcePath { get; }

        public string DestinationPath { get; }

        public string NestedSourceParentPath { get; }

        public string NestedSourcePath { get; }

        public string NestedDestinationParentPath { get; }

        public string NestedDestinationPath { get; }

        public LinuxNoFollowPathHandle DataRoot { get; }

        public LinuxNoFollowPathHandle JournalDirectory { get; }

        public LinuxNoFollowPathHandle AliasesDirectory { get; }

        public bool SupportsUnnamedFiles()
        {
            return SupportsUnnamedFilesAt(
                JournalDirectory
            );
        }

        public bool SupportsUnnamedFilesAtAliasesDirectory()
        {
            return SupportsUnnamedFilesAt(
                AliasesDirectory
            );
        }

        private static bool SupportsUnnamedFilesAt(
            LinuxNoFollowPathHandle directory)
        {
            LinuxCreateUnnamedFileAtResult probe =
                LinuxCreateUnnamedFileAt.Create(
                    directory
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

        public DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
            CreatePlan()
        {
            byte[] sourceBytes =
                File.ReadAllBytes(
                    SourcePath
                );

            string sourceSha256 =
                Convert.ToHexString(
                    SHA256.HashData(
                        sourceBytes
                    )
                );

            LinuxFileIdentityResult sourceIdentity =
                LinuxFileIdentity.Inspect(
                    SourcePath
                );

            Assert.True(
                sourceIdentity.Success,
                sourceIdentity.Error
            );

            LinuxFileIdentityResult subDirectoryIdentity =
                LinuxFileIdentity.Inspect(
                    SubDirectoryPath
                );

            Assert.True(
                subDirectoryIdentity.Success,
                subDirectoryIdentity.Error
            );

            uint sourceInodeGeneration =
                CaptureInodeGeneration(
                    SubDirectoryPath,
                    "target.dat"
                );

            var sourceSnapshot =
                new DataRelativePathRepairSourceSnapshot(
                    PhysicalPath:
                        SourcePath,
                    Size:
                        sourceBytes.Length,
                    Sha256:
                        sourceSha256,
                    Identity:
                        sourceIdentity
                );

            var parentSnapshot =
                new DataRelativePathRepairDestinationParentSnapshot(
                    PhysicalPath:
                        SubDirectoryPath,
                    Identity:
                        subDirectoryIdentity,
                    CasefoldEnabled:
                        false,
                    RawFlags:
                        0
                );

            DataRelativePathRepairPlanOperation[] operations =
            [
                new(
                    Kind:
                        DataRelativePathRepairPlanOperationKind.CreateFile,
                    DestinationPath:
                        DestinationPath,
                    SourcePath:
                        SourcePath
                )
            ];

            var record =
                new DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
                    SchemaVersion:
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                            .SchemaVersion1,
                    PlanId:
                        Guid.NewGuid(),
                    CreatedUtc:
                        T0,
                    DataRoot:
                        DataRootPath,
                    RequestedPath:
                        "sub/Target.DAT",
                    SourceSnapshot:
                        sourceSnapshot,
                    SourceInodeGeneration:
                        sourceInodeGeneration,
                    InitialDestinationParentSnapshot:
                        parentSnapshot,
                    Operations:
                        operations,
                    DirectoryRenameSources:
                        Array.Empty<
                            DataRelativePathRepairDirectoryRenameSource
                        >(),
                    AliasSources:
                        Array.Empty<
                            DataRelativePathRepairAliasSource
                        >()
                );

            string? validationError =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan
                    .Validate(
                        record
                    );

            Assert.Null(
                validationError
            );

            return record;
        }

        public DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
            CreateMultiOperationPlan()
        {
            byte[] sourceBytes =
                File.ReadAllBytes(
                    NestedSourcePath
                );

            string sourceSha256 =
                Convert.ToHexString(
                    SHA256.HashData(
                        sourceBytes
                    )
                );

            LinuxFileIdentityResult sourceIdentity =
                LinuxFileIdentity.Inspect(
                    NestedSourcePath
                );

            Assert.True(
                sourceIdentity.Success,
                sourceIdentity.Error
            );

            LinuxFileIdentityResult subDirectoryIdentity =
                LinuxFileIdentity.Inspect(
                    SubDirectoryPath
                );

            Assert.True(
                subDirectoryIdentity.Success,
                subDirectoryIdentity.Error
            );

            uint sourceInodeGeneration =
                CaptureInodeGeneration(
                    NestedSourceParentPath,
                    "target2.dat"
                );

            var sourceSnapshot =
                new DataRelativePathRepairSourceSnapshot(
                    PhysicalPath:
                        NestedSourcePath,
                    Size:
                        sourceBytes.Length,
                    Sha256:
                        sourceSha256,
                    Identity:
                        sourceIdentity
                );

            var parentSnapshot =
                new DataRelativePathRepairDestinationParentSnapshot(
                    PhysicalPath:
                        SubDirectoryPath,
                    Identity:
                        subDirectoryIdentity,
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
                        NestedDestinationParentPath,
                    SourcePath:
                        null
                ),
                new(
                    Kind:
                        DataRelativePathRepairPlanOperationKind.CreateFile,
                    DestinationPath:
                        NestedDestinationPath,
                    SourcePath:
                        NestedSourcePath
                )
            ];

            var record =
                new DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
                    SchemaVersion:
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                            .SchemaVersion1,
                    PlanId:
                        Guid.NewGuid(),
                    CreatedUtc:
                        T0,
                    DataRoot:
                        DataRootPath,
                    RequestedPath:
                        "sub/Inner/Target2.DAT",
                    SourceSnapshot:
                        sourceSnapshot,
                    SourceInodeGeneration:
                        sourceInodeGeneration,
                    InitialDestinationParentSnapshot:
                        parentSnapshot,
                    Operations:
                        operations,
                    DirectoryRenameSources:
                        Array.Empty<
                            DataRelativePathRepairDirectoryRenameSource
                        >(),
                    AliasSources:
                        Array.Empty<
                            DataRelativePathRepairAliasSource
                        >()
                );

            string? validationError =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan
                    .Validate(
                        record
                    );

            Assert.Null(
                validationError
            );

            return record;
        }

        public DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
            CreateAliasPlan(
                out string realTargetDirectoryPath,
                out string aliasLinkPath,
                out string finalSourcePath,
                out string finalDestinationPath)
        {
            realTargetDirectoryPath =
                Path.Combine(
                    SubDirectoryPath,
                    "Actors"
                );

            Directory.CreateDirectory(
                realTargetDirectoryPath
            );

            File.WriteAllText(
                Path.Combine(
                    realTargetDirectoryPath,
                    "sibling.dat"
                ),
                "an untouched sibling"
            );

            finalSourcePath =
                Path.Combine(
                    realTargetDirectoryPath,
                    "newfile.dat"
                );

            File.WriteAllBytes(
                finalSourcePath,
                Encoding.ASCII.GetBytes(
                    "aliased content"
                )
            );

            aliasLinkPath =
                Path.Combine(
                    SubDirectoryPath,
                    "actors"
                );

            finalDestinationPath =
                Path.Combine(
                    aliasLinkPath,
                    "NewFile.DAT"
                );

            LinuxFileIdentityResult subDirectoryIdentity =
                LinuxFileIdentity.Inspect(
                    SubDirectoryPath
                );

            Assert.True(
                subDirectoryIdentity.Success,
                subDirectoryIdentity.Error
            );

            LinuxFileIdentityResult realTargetIdentity =
                LinuxFileIdentity.Inspect(
                    realTargetDirectoryPath
                );

            Assert.True(
                realTargetIdentity.Success,
                realTargetIdentity.Error
            );

            uint realTargetInodeGeneration =
                CaptureDirectoryInodeGeneration(
                    SubDirectoryPath,
                    "Actors"
                );

            LinuxFileIdentityResult sourceIdentity =
                LinuxFileIdentity.Inspect(
                    finalSourcePath
                );

            Assert.True(
                sourceIdentity.Success,
                sourceIdentity.Error
            );

            byte[] sourceBytes =
                File.ReadAllBytes(
                    finalSourcePath
                );

            string sourceSha256 =
                Convert.ToHexString(
                    SHA256.HashData(
                        sourceBytes
                    )
                );

            uint sourceInodeGeneration =
                CaptureInodeGeneration(
                    realTargetDirectoryPath,
                    "newfile.dat"
                );

            var sourceSnapshot =
                new DataRelativePathRepairSourceSnapshot(
                    PhysicalPath:
                        finalSourcePath,
                    Size:
                        sourceBytes.Length,
                    Sha256:
                        sourceSha256,
                    Identity:
                        sourceIdentity
                );

            var parentSnapshot =
                new DataRelativePathRepairDestinationParentSnapshot(
                    PhysicalPath:
                        SubDirectoryPath,
                    Identity:
                        subDirectoryIdentity,
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
                            .CreateAliasSymlink,
                    DestinationPath:
                        aliasLinkPath,
                    SourcePath:
                        realTargetDirectoryPath
                ),
                new(
                    Kind:
                        DataRelativePathRepairPlanOperationKind.CreateFile,
                    DestinationPath:
                        finalDestinationPath,
                    SourcePath:
                        finalSourcePath
                )
            ];

            DataRelativePathRepairAliasSource[] aliasSources =
            [
                new(
                    DestinationPath:
                        aliasLinkPath,
                    PhysicalPath:
                        realTargetDirectoryPath,
                    Identity:
                        realTargetIdentity,
                    InodeGeneration:
                        realTargetInodeGeneration
                )
            ];

            var record =
                new DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
                    SchemaVersion:
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                            .SchemaVersion1,
                    PlanId:
                        Guid.NewGuid(),
                    CreatedUtc:
                        T0,
                    DataRoot:
                        DataRootPath,
                    RequestedPath:
                        "sub/actors/NewFile.DAT",
                    SourceSnapshot:
                        sourceSnapshot,
                    SourceInodeGeneration:
                        sourceInodeGeneration,
                    InitialDestinationParentSnapshot:
                        parentSnapshot,
                    Operations:
                        operations,
                    DirectoryRenameSources:
                        Array.Empty<
                            DataRelativePathRepairDirectoryRenameSource
                        >(),
                    AliasSources:
                        aliasSources
                );

            string? validationError =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan
                    .Validate(
                        record
                    );

            Assert.Null(
                validationError
            );

            return record;
        }

        private static uint CaptureDirectoryInodeGeneration(
            string parentPath,
            string childName)
        {
            LinuxNoFollowPathHandle parent =
                OpenRoot(
                    parentPath
                );

            try
            {
                LinuxOpenChildReadOnlyAtResult opened =
                    LinuxOpenChildReadOnlyAt.Open(
                        parent,
                        childName
                    );

                Assert.True(
                    opened.Success,
                    opened.Error
                );

                using LinuxOpenedChildHandle child =
                    opened.OpenedChild!;

                LinuxOpenedInodeGenerationResult generation =
                    LinuxOpenedInodeGeneration.Capture(
                        child
                    );

                Assert.True(
                    generation.Success,
                    generation.Error
                );

                return generation.Generation!.Value;
            }
            finally
            {
                parent.Dispose();
            }
        }

        private static uint CaptureInodeGeneration(
            string parentPath,
            string childName)
        {
            LinuxNoFollowPathHandle parent =
                OpenRoot(
                    parentPath
                );

            try
            {
                LinuxOpenChildRegularFileReadOnlyAtResult opened =
                    LinuxOpenChildRegularFileReadOnlyAt.Open(
                        parent,
                        childName
                    );

                Assert.True(
                    opened.Success,
                    opened.Error
                );

                using LinuxOpenedChildHandle child =
                    opened.OpenedFile!;

                LinuxOpenedFileIncarnationResult incarnation =
                    LinuxOpenedFileIncarnation.Capture(
                        child
                    );

                Assert.True(
                    incarnation.Success,
                    incarnation.Error
                );

                return incarnation.Identity!.InodeGeneration;
            }
            finally
            {
                parent.Dispose();
            }
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
            DataRoot.Dispose();

            JournalDirectory.Dispose();

            AliasesDirectory.Dispose();

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
