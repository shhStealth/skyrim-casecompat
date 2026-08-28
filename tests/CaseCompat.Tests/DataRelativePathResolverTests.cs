using CaseCompat.Core.Findings;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathResolverTests
{
    [Fact]
    public void ResolveFile_StrictCaseSplit_FailsWithUniqueEquivalentCandidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string physicalDirectory =
            Path.Combine(
                dataRoot,
                "meshes",
                "fafny stash",
                "Bishop Armor"
            );

        Directory.CreateDirectory(
            physicalDirectory
        );

        string physicalFile =
            Path.Combine(
                physicalDirectory,
                "Bishop_Bodysuit_1.nif"
            );

        File.WriteAllText(
            physicalFile,
            "bishop-fixture"
        );

        DataRelativePathResolution result =
            DataRelativePathResolver.ResolveFile(
                dataRoot,
                "Meshes/Fafny stash/Bishop Armor/" +
                "Bishop_Bodysuit_1.nif",
                path =>
                    InspectFixtureCasefold(
                        path,
                        dataRoot
                    )
            );

        Assert.False(
            result.LinuxResolves
        );

        Assert.Null(
            result.ResolvedPhysicalPath
        );

        Assert.Equal(
            1,
            result.FailedComponentIndex
        );

        Assert.Equal(
            PathResolutionStepKind.CasefoldEquivalent,
            result.Steps[0].Kind
        );

        Assert.Equal(
            PathResolutionStepKind.Missing,
            result.Steps[1].Kind
        );

        Assert.Equal(
            "Fafny stash",
            result.Steps[1].RequestedComponent
        );

        Assert.Contains(
            "fafny stash",
            result.Steps[1]
                .EquivalentPhysicalNames
        );

        Assert.Equal(
            1,
            result.CandidateCount
        );

        Assert.Equal(
            physicalFile,
            Assert.Single(
                result.EquivalentPhysicalCandidates
            )
        );

        EffectiveAssetReferenceFinding finding =
            CreateBishopFinding(result);

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .UnresolvedUniqueEquivalent,
            EffectiveAssetReferenceEvidenceClassifier
                .Classify(finding)
        );
    }

    [Fact]
    public void ResolveFile_RepairedCaseSplit_ResolvesWithTwoEquivalentCandidates()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshes =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        string requestedDirectory =
            Path.Combine(
                meshes,
                "Fafny stash",
                "Bishop Armor"
            );

        string alternateDirectory =
            Path.Combine(
                meshes,
                "fafny stash",
                "Bishop Armor"
            );

        Directory.CreateDirectory(
            requestedDirectory
        );

        Directory.CreateDirectory(
            alternateDirectory
        );

        string requestedFile =
            Path.Combine(
                requestedDirectory,
                "Bishop_Bodysuit_1.nif"
            );

        string alternateFile =
            Path.Combine(
                alternateDirectory,
                "Bishop_Bodysuit_1.nif"
            );

        File.WriteAllText(
            requestedFile,
            "bishop-fixture"
        );

        File.WriteAllText(
            alternateFile,
            "bishop-fixture"
        );

        DataRelativePathResolution result =
            DataRelativePathResolver.ResolveFile(
                dataRoot,
                "Meshes/Fafny stash/Bishop Armor/" +
                "Bishop_Bodysuit_1.nif",
                path =>
                    InspectFixtureCasefold(
                        path,
                        dataRoot
                    )
            );

        Assert.True(
            result.LinuxResolves
        );

        Assert.Equal(
            requestedFile,
            result.ResolvedPhysicalPath
        );

        Assert.Null(
            result.FailedComponentIndex
        );

        Assert.Equal(
            PathResolutionStepKind.CasefoldEquivalent,
            result.Steps[0].Kind
        );

        Assert.Equal(
            PathResolutionStepKind.ExactSpelling,
            result.Steps[1].Kind
        );

        Assert.Equal(
            2,
            result.CandidateCount
        );

        Assert.Contains(
            requestedFile,
            result.EquivalentPhysicalCandidates
        );

        Assert.Contains(
            alternateFile,
            result.EquivalentPhysicalCandidates
        );

        EffectiveAssetReferenceFinding finding =
            CreateBishopFinding(result);

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .LinuxResolvable,
            EffectiveAssetReferenceEvidenceClassifier
                .Classify(finding)
        );
    }

    private static EffectiveAssetReferenceFinding
        CreateBishopFinding(
            DataRelativePathResolution resolution)
    {
        return new EffectiveAssetReferenceFinding(
            ConsumerKind:
                "ArmorAddon",
            ConsumerFormKey:
                "00080E:[FB] Bishop Armor.esp",
            ConsumerEditorId:
                "000_Bishop_Bodysuit_Blue_AA",
            WinningPluginName:
                "[FB] Bishop Armor.esp",
            WinningLoadOrderIndex:
                1712,
            WinnerSearchComplete:
                true,
            ReferenceField:
                "WorldModel.Female",
            RawPath:
                @"Fafny stash\Bishop Armor\Bishop_Bodysuit_1.nif",
            RequestedPath:
                "Meshes/Fafny stash/Bishop Armor/" +
                "Bishop_Bodysuit_1.nif",
            Resolution:
                resolution
        );
    }

    private static DirectoryCasefoldResult
        InspectFixtureCasefold(
            string path,
            string dataRoot)
    {
        string fullPath =
            Path.GetFullPath(path);

        bool casefoldEnabled =
            string.Equals(
                fullPath,
                Path.GetFullPath(dataRoot),
                StringComparison.Ordinal
            );

        return new DirectoryCasefoldResult(
            FullPath: fullPath,
            Exists: true,
            CasefoldEnabled: casefoldEnabled,
            RawFlags:
                casefoldEnabled
                    ? LinuxDirectoryFlags.FsCasefoldFlag
                    : 0L,
            Error: null
        );
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-tests",
                    Guid.NewGuid()
                        .ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(
                    RootPath,
                    recursive: true
                );
            }
        }
    }
}
