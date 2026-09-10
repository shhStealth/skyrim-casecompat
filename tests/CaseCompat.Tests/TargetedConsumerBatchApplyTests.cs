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
