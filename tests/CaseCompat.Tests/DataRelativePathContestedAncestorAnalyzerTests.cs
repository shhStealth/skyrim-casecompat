using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class DataRelativePathContestedAncestorAnalyzerTests
{
    [Fact]
    public void Analyze_TwoDisagreeingRequestedPaths_FlagsSharedAncestorContested()
    {
        IReadOnlySet<string> contested =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                new[]
                {
                    "Meshes/Actors/CandidateA.nif",
                    "Meshes/actors/CandidateB.nif"
                }
            );

        Assert.Contains(
            "MESHES/ACTORS",
            contested
        );

        Assert.DoesNotContain(
            "MESHES",
            contested
        );
    }

    [Fact]
    public void Analyze_AllRequestedPathsAgree_FlagsNothingContested()
    {
        IReadOnlySet<string> contested =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                new[]
                {
                    "Meshes/Actors/CandidateA.nif",
                    "Meshes/Actors/CandidateB.nif"
                }
            );

        Assert.Empty(
            contested
        );
    }

    [Fact]
    public void
        Analyze_AlreadyFixedPathNoLongerACandidate_StillPinsItsAncestorAsContested()
    {
        // The actual stranding bug this analyzer exists to close: a file
        // that has already been fixed (or always matched) stops being a
        // "candidate" - a mismatch-only view would never see it again.
        // But its own winning consumer's requested path still requires
        // its ancestor's current casing exactly as much as a mismatched
        // candidate's does. This test supplies ONLY that plain
        // requested-path string - not a Candidate object, and not
        // anything that would appear in a "candidates" list - proving
        // the analyzer's contested-ness detection does not depend on
        // the file still being broken.
        string alreadyFixedFileRequestedPath =
            "Meshes/actors/SeranaHair.nif";

        string unrelatedMismatchedCandidateRequestedPath =
            "Meshes/Actors/SomeArmor.nif";

        IReadOnlySet<string> contested =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                new[]
                {
                    alreadyFixedFileRequestedPath,
                    unrelatedMismatchedCandidateRequestedPath
                }
            );

        Assert.Contains(
            "MESHES/ACTORS",
            contested
        );
    }

    [Fact]
    public void Analyze_NullAndEmptyEntries_AreIgnored()
    {
        IReadOnlySet<string> contested =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                new[]
                {
                    "Meshes/Actors/CandidateA.nif",
                    null,
                    string.Empty,
                    "Meshes/actors/CandidateB.nif"
                }
            );

        Assert.Contains(
            "MESHES/ACTORS",
            contested
        );
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsNoContestedPrefixes()
    {
        IReadOnlySet<string> contested =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                Array.Empty<string?>()
            );

        Assert.Empty(
            contested
        );
    }
}
