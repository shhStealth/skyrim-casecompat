using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class TargetedConsumerBatchApplyTests
{
    [Fact]
    public void Run_ContestedAncestor_ReportsAliasFixDistinctlyFromPlainRename()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Two candidates share a contested ancestor ("actors" vs
        // "Actors"); only one physically exists. This exercises the
        // real Run/ApplyOne orchestration this Step wired up - the
        // aliases directory threaded through Project/Create/Execute,
        // and the AppliedDurablyViaAlias outcome distinguishing an
        // alias-based fix from a plain rename in the batch summary.
        string dataRoot =
            CreateDataRoot();

        string realActorsDirectory =
            Path.Combine(
                dataRoot,
                "Meshes",
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
                "candidatez.nif"
            ),
            "candidate Z content"
        );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidateA =
            BuildCandidate(
                dataRoot,
                "Meshes/Actors/CandidateA.nif"
            );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidateZ =
            BuildCandidate(
                dataRoot,
                "Meshes/actors/CandidateZ.nif"
            );

        using LinuxNoFollowPathHandle dataRootHandle =
            OpenRoot(
                dataRoot
            );

        string rootPath =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-batch-apply-tests",
                Guid.NewGuid().ToString("N")
            );

        string planDirectoryPath =
            Path.Combine(
                rootPath,
                "Plan"
            );

        string journalDirectoryPath =
            Path.Combine(
                rootPath,
                "Journal"
            );

        string aliasesDirectoryPath =
            Path.Combine(
                rootPath,
                "Aliases"
            );

        Directory.CreateDirectory(
            planDirectoryPath
        );

        Directory.CreateDirectory(
            journalDirectoryPath
        );

        Directory.CreateDirectory(
            aliasesDirectoryPath
        );

        using LinuxNoFollowPathHandle planDirectory =
            OpenRoot(
                planDirectoryPath
            );

        using LinuxNoFollowPathHandle journalDirectory =
            OpenRoot(
                journalDirectoryPath
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            OpenRoot(
                aliasesDirectoryPath
            );

        if (!SupportsUnnamedFilesAt(
                journalDirectory))
        {
            return;
        }

        TargetedConsumerBatchApplyRunResult result =
            TargetedConsumerBatchApply.Run(
                dataRootHandle,
                planDirectory,
                journalDirectory,
                aliasesDirectory,
                new[]
                {
                    candidateA,
                    candidateZ
                },
                new[]
                {
                    candidateA.AuthoritativeRequestedPath,
                    candidateZ.AuthoritativeRequestedPath
                }
            );

        Assert.Equal(
            2,
            result.AppliedCount
        );

        Assert.Equal(
            1,
            result.AppliedViaAliasCount
        );

        Assert.Empty(
            result.RejectionCounts
        );

        TargetedConsumerBatchApplyItemResult itemA =
            Assert.Single(
                result.Items,
                item =>
                    item.Candidate.AuthoritativeRequestedPath ==
                    "Meshes/Actors/CandidateA.nif"
            );

        Assert.Equal(
            "AppliedDurablyViaAlias",
            itemA.Outcome
        );

        TargetedConsumerBatchApplyItemResult itemZ =
            Assert.Single(
                result.Items,
                item =>
                    item.Candidate.AuthoritativeRequestedPath ==
                    "Meshes/actors/CandidateZ.nif"
            );

        Assert.Equal(
            "AppliedDurably",
            itemZ.Outcome
        );

        var aliasInfo =
            new FileInfo(
                Path.Combine(
                    dataRoot,
                    "Meshes",
                    "Actors"
                )
            );

        Assert.Equal(
            "actors",
            aliasInfo.LinkTarget
        );

        Assert.Equal(
            "candidate A content",
            File.ReadAllText(
                Path.Combine(
                    dataRoot,
                    "Meshes",
                    "Actors",
                    "CandidateA.nif"
                )
            )
        );

        Assert.Equal(
            "candidate Z content",
            File.ReadAllText(
                Path.Combine(
                    realActorsDirectory,
                    "CandidateZ.nif"
                )
            )
        );
    }

    [Fact]
    public void
        Run_ManyCandidatesSharingTwoNestedContestedAncestors_AllSucceed()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Reproduces a real bug found live on a user's install: many
        // candidates share TWO nested contested ancestors ("character"/
        // "Character", then "character assets"/"Character Assets"
        // beneath it), and every candidate's own leaf filename was
        // already exactly correct - only the two ancestor directories
        // were ever mismatched. Applying the first candidate creates
        // both alias levels as its own prerequisite steps; every other
        // candidate then reuses both. This exercises two distinct gaps
        // that only became reachable once an alias could force entry
        // into the case-insensitive intermediate scan for segments that
        // were never themselves contested:
        //
        //   1. A later segment that already has the exact requested
        //      casing (here, "Beards") must not be treated as needing a
        //      rename - a same-name "rename" always collides with
        //      itself (see DataRelativePathRepairPlanOperationKind
        //      .VerifyExistingDirectory).
        //   2. Once both ancestors resolve correctly, a leaf whose own
        //      name was never mismatched is now the SAME physical file
        //      as its own destination - there is nothing left to
        //      rename, and that must be recognized as success, not a
        //      destination conflict.
        //
        // All five candidates apply in ONE pass with zero left over,
        // matching what the fix restores: before it, this scenario
        // showed Applied=0 across repeated runs, identically, forever.
        string dataRoot =
            CreateDataRoot();

        string realDirectory =
            Path.Combine(
                dataRoot,
                "Meshes",
                "character",
                "character assets",
                "Beards"
            );

        Directory.CreateDirectory(
            realDirectory
        );

        var candidates =
            new List<
                DataRelativePathTargetedConsumerCaseRepairCandidate
            >();

        var allWinningRequestedPaths =
            new List<string?>();

        for (
            int index = 1;
            index <= 5;
            index++)
        {
            File.WriteAllText(
                Path.Combine(
                    realDirectory,
                    $"File{index}.tri"
                ),
                $"content {index}"
            );

            string requestedPath =
                "Meshes/Character/Character Assets/Beards/" +
                $"File{index}.tri";

            candidates.Add(
                BuildCandidate(
                    dataRoot,
                    requestedPath
                )
            );

            allWinningRequestedPaths.Add(
                requestedPath
            );
        }

        // Pin "character" lowercase at the first level, and "character
        // assets" lowercase at the second (beneath the SAME "Character"
        // casing the five candidates above use) - two independent,
        // genuine winning-consumer disagreements, one per nested level.
        allWinningRequestedPaths.Add(
            "Meshes/character/LowercasePinnedFile1.nif"
        );

        allWinningRequestedPaths.Add(
            "Meshes/Character/character assets/LowercasePinnedFile2.nif"
        );

        using LinuxNoFollowPathHandle dataRootHandle =
            OpenRoot(
                dataRoot
            );

        string rootPath =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-batch-apply-tests",
                Guid.NewGuid().ToString("N")
            );

        string planDirectoryPath =
            Path.Combine(
                rootPath,
                "Plan"
            );

        string journalDirectoryPath =
            Path.Combine(
                rootPath,
                "Journal"
            );

        string aliasesDirectoryPath =
            Path.Combine(
                rootPath,
                "Aliases"
            );

        Directory.CreateDirectory(
            planDirectoryPath
        );

        Directory.CreateDirectory(
            journalDirectoryPath
        );

        Directory.CreateDirectory(
            aliasesDirectoryPath
        );

        using LinuxNoFollowPathHandle planDirectory =
            OpenRoot(
                planDirectoryPath
            );

        using LinuxNoFollowPathHandle journalDirectory =
            OpenRoot(
                journalDirectoryPath
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            OpenRoot(
                aliasesDirectoryPath
            );

        if (!SupportsUnnamedFilesAt(
                journalDirectory))
        {
            return;
        }

        TargetedConsumerBatchApplyRunResult result =
            TargetedConsumerBatchApply.Run(
                dataRootHandle,
                planDirectory,
                journalDirectory,
                aliasesDirectory,
                candidates,
                allWinningRequestedPaths
            );

        Assert.Equal(
            5,
            result.AppliedCount
        );

        Assert.Equal(
            5,
            result.AppliedViaAliasCount
        );

        Assert.Empty(
            result.RejectionCounts
        );

        string fixedDirectory =
            Path.Combine(
                dataRoot,
                "Meshes",
                "Character",
                "Character Assets",
                "Beards"
            );

        for (
            int index = 1;
            index <= 5;
            index++)
        {
            Assert.Equal(
                $"content {index}",
                File.ReadAllText(
                    Path.Combine(
                        fixedDirectory,
                        $"File{index}.tri"
                    )
                )
            );
        }

        var characterAliasInfo =
            new FileInfo(
                Path.Combine(
                    dataRoot,
                    "Meshes",
                    "Character"
                )
            );

        Assert.Equal(
            "character",
            characterAliasInfo.LinkTarget
        );

        var characterAssetsAliasInfo =
            new FileInfo(
                Path.Combine(
                    dataRoot,
                    "Meshes",
                    "character",
                    "Character Assets"
                )
            );

        Assert.Equal(
            "character assets",
            characterAssetsAliasInfo.LinkTarget
        );

        // The real, original directories are untouched and unrenamed -
        // only aliases were created, never a rename of either.
        Assert.True(
            Directory.Exists(
                Path.Combine(
                    dataRoot,
                    "Meshes",
                    "character"
                )
            )
        );

        Assert.True(
            Directory.Exists(
                realDirectory
            )
        );
    }

    [Fact]
    public void
        Run_AlreadyFixedFileNoLongerACandidate_IsNotStrandedByUnrelatedFix()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Reproduces the exact stranding bug found on a real install
        // during this session: SeranaHair.nif already sits correctly
        // beneath the real, lowercase "actors" directory - its own
        // winning consumer requires exactly that casing, and nothing
        // about it needs fixing, so it is NOT included in `candidates`
        // here (mirroring how a file that already matches never
        // becomes a candidate on a real scan). CandidateA is a
        // genuinely unrelated, mismatched candidate that needs the
        // SAME ancestor under a different casing ("Actors"). Without
        // SeranaHair's requested path also feeding contested-ancestor
        // analysis, "actors" would look uncontested and CandidateA's
        // fix would rename it out from under Serana's hair.
        string dataRoot =
            CreateDataRoot();

        string realActorsDirectory =
            Path.Combine(
                dataRoot,
                "Meshes",
                "actors"
            );

        Directory.CreateDirectory(
            realActorsDirectory
        );

        File.WriteAllText(
            Path.Combine(
                realActorsDirectory,
                "SeranaHair.nif"
            ),
            "serana hair content"
        );

        File.WriteAllText(
            Path.Combine(
                realActorsDirectory,
                "candidatea.nif"
            ),
            "candidate A content"
        );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidateA =
            BuildCandidate(
                dataRoot,
                "Meshes/Actors/CandidateA.nif"
            );

        using LinuxNoFollowPathHandle dataRootHandle =
            OpenRoot(
                dataRoot
            );

        string rootPath =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-batch-apply-tests",
                Guid.NewGuid().ToString("N")
            );

        string planDirectoryPath =
            Path.Combine(
                rootPath,
                "Plan"
            );

        string journalDirectoryPath =
            Path.Combine(
                rootPath,
                "Journal"
            );

        string aliasesDirectoryPath =
            Path.Combine(
                rootPath,
                "Aliases"
            );

        Directory.CreateDirectory(
            planDirectoryPath
        );

        Directory.CreateDirectory(
            journalDirectoryPath
        );

        Directory.CreateDirectory(
            aliasesDirectoryPath
        );

        using LinuxNoFollowPathHandle planDirectory =
            OpenRoot(
                planDirectoryPath
            );

        using LinuxNoFollowPathHandle journalDirectory =
            OpenRoot(
                journalDirectoryPath
            );

        using LinuxNoFollowPathHandle aliasesDirectory =
            OpenRoot(
                aliasesDirectoryPath
            );

        if (!SupportsUnnamedFilesAt(
                journalDirectory))
        {
            return;
        }

        TargetedConsumerBatchApplyRunResult result =
            TargetedConsumerBatchApply.Run(
                dataRootHandle,
                planDirectory,
                journalDirectory,
                aliasesDirectory,
                new[]
                {
                    candidateA
                },
                new[]
                {
                    candidateA.AuthoritativeRequestedPath,
                    "Meshes/actors/SeranaHair.nif"
                }
            );

        TargetedConsumerBatchApplyItemResult itemA =
            Assert.Single(
                result.Items
            );

        Assert.Equal(
            "AppliedDurablyViaAlias",
            itemA.Outcome
        );

        // The real "actors" directory - and Serana's hair inside it -
        // must be completely untouched: no rename, no move.
        Assert.True(
            Directory.Exists(
                realActorsDirectory
            )
        );

        Assert.Equal(
            "serana hair content",
            File.ReadAllText(
                Path.Combine(
                    realActorsDirectory,
                    "SeranaHair.nif"
                )
            )
        );

        var aliasInfo =
            new FileInfo(
                Path.Combine(
                    dataRoot,
                    "Meshes",
                    "Actors"
                )
            );

        Assert.Equal(
            "actors",
            aliasInfo.LinkTarget
        );
    }

    [Fact]
    public void
        Analyze_WithoutAlreadyFixedFilesRequestedPath_WouldHaveStrandedIt()
    {
        // Contrast test proving the bug this closes was real: computing
        // contested-ancestor prefixes from candidates alone (the old,
        // narrower behavior) misses SeranaHair's still-load-bearing
        // requested path entirely, since it was never a candidate.
        IReadOnlySet<string> contestedFromCandidatesAlone =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                new[]
                {
                    "Meshes/Actors/CandidateA.nif"
                }
            );

        Assert.DoesNotContain(
            "MESHES/ACTORS",
            contestedFromCandidatesAlone
        );

        IReadOnlySet<string> contestedWithFullEvidence =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                new[]
                {
                    "Meshes/Actors/CandidateA.nif",
                    "Meshes/actors/SeranaHair.nif"
                }
            );

        Assert.Contains(
            "MESHES/ACTORS",
            contestedWithFullEvidence
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairCandidate
        BuildCandidate(
            string dataRoot,
            string requestedPath)
    {
        using LinuxNoFollowPathHandle root =
            OpenRoot(
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

    private static string CreateDataRoot()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-batch-apply-tests",
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
}
