using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class WindowsNamespacePhysicalFileReacquirerTests
{
    [LinuxFileInodeGenerationFact]
    public void Reacquire_UnchangedParticipant_ReturnsSameIncarnation()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, _) =
                CreateDataFile(
                    root,
                    "Data",
                    "Meshes/Armor/Sword.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            WindowsNamespaceFileIncarnationObservation expected =
                SingleFileObservation(
                    analysis
                );

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    analysis,
                    participant
                );

            Assert.True(
                result.Success,
                result.Error
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .Reacquired,
                result.State
            );

            Assert.Equal(
                expected,
                result.ExpectedIncarnationObservation
            );

            Assert.NotNull(
                result.OpenedFile
            );

            Assert.True(
                result.ActualIncarnation!.Success,
                result.ActualIncarnation.Error
            );

            Assert.Equal(
                participant.DeviceMajor,
                result.ActualIncarnation
                    .Identity!
                    .PhysicalIdentity
                    .DeviceMajor
            );

            Assert.Equal(
                participant.DeviceMinor,
                result.ActualIncarnation
                    .Identity!
                    .PhysicalIdentity
                    .DeviceMinor
            );

            Assert.Equal(
                participant.Inode,
                result.ActualIncarnation
                    .Identity!
                    .PhysicalIdentity
                    .Inode
            );

            Assert.Equal(
                participant.MountId,
                result.ActualIncarnation
                    .Identity!
                    .PhysicalIdentity
                    .MountId
            );

            Assert.Equal(
                expected.InodeGeneration,
                result.ActualIncarnation
                    .Identity!
                    .InodeGeneration
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_RecreatedFile_IsRejectedByIncarnation()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, string file) =
                CreateDataFile(
                    root,
                    "Data",
                    "Meshes/Example.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            File.Delete(
                file
            );

            File.WriteAllText(
                file,
                "after"
            );

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    analysis,
                    participant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .FileIncarnationChanged,
                result.State
            );

            Assert.Null(
                result.OpenedFile
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_AlteredAnalysisDataRoot_IsRejectedByProvenance()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, _) =
                CreateDataFile(
                    root,
                    "DataOne",
                    "Meshes/Example.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            string secondData =
                Path.Combine(
                    root,
                    "DataTwo"
                );

            Directory.CreateDirectory(
                secondData
            );

            WindowsNamespaceAnalysis alteredAnalysis =
                analysis with
                {
                    DataRootPath =
                        secondData
                };

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    alteredAnalysis,
                    participant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .ParticipantDataRootMismatch,
                result.State
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_IncompleteAnalysis_IsRejected()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, _) =
                CreateDataFile(
                    root,
                    "Data",
                    "Meshes/Sword.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            WindowsNamespaceAnalysis incompleteAnalysis =
                analysis with
                {
                    Errors =
                        new[]
                        {
                            "Synthetic pass-1 analysis failure."
                        }
                };

            Assert.False(
                incompleteAnalysis.Complete
            );

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    incompleteAnalysis,
                    participant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .InvalidAnalysis,
                result.State
            );

            Assert.Null(
                result.ExpectedIncarnationObservation
            );

            Assert.Contains(
                "incomplete",
                result.Error!,
                StringComparison.OrdinalIgnoreCase
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_ParticipantFromDifferentAnalysis_IsRejected()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string firstData, _) =
                CreateDataFile(
                    root,
                    "DataOne",
                    "Meshes/First.nif"
                );

            (string secondData, _) =
                CreateDataFile(
                    root,
                    "DataTwo",
                    "Meshes/Second.nif"
                );

            WindowsNamespaceAnalysis firstAnalysis =
                AnalyzeComplete(
                    firstData
                );

            WindowsNamespaceAnalysis secondAnalysis =
                AnalyzeComplete(
                    secondData
                );

            WindowsNamespacePhysicalParticipant secondParticipant =
                SingleFileParticipant(
                    secondAnalysis
                );

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    firstAnalysis,
                    secondParticipant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .ParticipantNotInAnalysis,
                result.State
            );

            Assert.Null(
                result.ExpectedIncarnationObservation
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_MissingExactDirectorySpelling_IsRejected()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, _) =
                CreateDataFile(
                    root,
                    "Data",
                    "Meshes/Armor/Sword.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            string meshes =
                Path.Combine(
                    data,
                    "Meshes"
                );

            Directory.Move(
                Path.Combine(
                    meshes,
                    "Armor"
                ),
                Path.Combine(
                    meshes,
                    "armor"
                )
            );

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    analysis,
                    participant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .ExactDirectorySpellingUnavailable,
                result.State
            );

            Assert.Equal(
                "Armor",
                result.FailedComponent
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_MissingExactFileSpelling_IsRejected()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, string file) =
                CreateDataFile(
                    root,
                    "Data",
                    "Meshes/Sword.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            File.Move(
                file,
                Path.Combine(
                    Path.GetDirectoryName(
                        file
                    )!,
                    "sword.NIF"
                )
            );

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    analysis,
                    participant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .ExactFileSpellingUnavailable,
                result.State
            );

            Assert.Equal(
                "Sword.nif",
                result.FailedComponent
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_MissingDataRootIncarnationEvidence_IsRejected()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, _) =
                CreateDataFile(
                    root,
                    "Data",
                    "Meshes/Sword.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            WindowsNamespaceAnalysis incompleteAnalysis =
                analysis with
                {
                    DirectoryIncarnationObservations =
                        analysis.DirectoryIncarnationObservations
                            .Where(
                                observation =>
                                    observation.RelativePath != "."
                            )
                            .ToArray()
                };

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    incompleteAnalysis,
                    participant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .DataRootIncarnationObservationUnavailable,
                result.State
            );

            Assert.Equal(
                ".",
                result.FailedComponent
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_MissingDirectoryIncarnationEvidence_IsRejected()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, _) =
                CreateDataFile(
                    root,
                    "Data",
                    "Meshes/Sword.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            WindowsNamespaceAnalysis incompleteAnalysis =
                analysis with
                {
                    DirectoryIncarnationObservations =
                        analysis.DirectoryIncarnationObservations
                            .Where(
                                observation =>
                                    observation.RelativePath
                                        .Replace('\\', '/') !=
                                    "Meshes"
                            )
                            .ToArray()
                };

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    incompleteAnalysis,
                    participant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .DirectoryIncarnationObservationUnavailable,
                result.State
            );

            Assert.Equal(
                "Meshes",
                result.FailedComponent
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_ReplacedDataRoot_IsRejectedEvenWhenFileSurvives()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, string file) =
                CreateDataFile(
                    root,
                    "Data",
                    "Meshes/Sword.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            string retiredData =
                Path.Combine(
                    root,
                    "RetiredData"
                );

            Directory.Move(
                data,
                retiredData
            );

            Directory.CreateDirectory(
                Path.Combine(
                    data,
                    "Meshes"
                )
            );

            File.Move(
                Path.Combine(
                    retiredData,
                    "Meshes",
                    "Sword.nif"
                ),
                file
            );

            WindowsNamespaceAnalysis currentAnalysis =
                AnalyzeComplete(
                    data
                );

            AssertSameFileIncarnation(
                analysis,
                currentAnalysis
            );

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    analysis,
                    participant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .DataRootIncarnationChanged,
                result.State
            );

            Assert.Equal(
                ".",
                result.FailedComponent
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Reacquire_ReplacedIntermediateDirectory_IsRejectedEvenWhenFileSurvives()
    {
        string root =
            CreateTempDirectory();

        try
        {
            (string data, string file) =
                CreateDataFile(
                    root,
                    "Data",
                    "Meshes/Armor/Sword.nif"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeComplete(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                SingleFileParticipant(
                    analysis
                );

            string armor =
                Path.GetDirectoryName(
                    file
                )!;

            string meshes =
                Path.GetDirectoryName(
                    armor
                )!;

            string retiredArmor =
                Path.Combine(
                    meshes,
                    "RetiredArmor"
                );

            Directory.Move(
                armor,
                retiredArmor
            );

            Directory.CreateDirectory(
                armor
            );

            File.Move(
                Path.Combine(
                    retiredArmor,
                    "Sword.nif"
                ),
                file
            );

            WindowsNamespaceAnalysis currentAnalysis =
                AnalyzeComplete(
                    data
                );

            AssertSameFileIncarnation(
                analysis,
                currentAnalysis
            );

            using WindowsNamespacePhysicalFileReacquisition result =
                WindowsNamespacePhysicalFileReacquirer.Reacquire(
                    analysis,
                    participant
                );

            Assert.False(
                result.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .DirectoryIncarnationChanged,
                result.State
            );

            Assert.Equal(
                "Armor",
                result.FailedComponent
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    private static WindowsNamespaceAnalysis AnalyzeComplete(
        string dataRoot)
    {
        WindowsNamespaceAnalysis analysis =
            WindowsNamespaceAnalyzer.Analyze(
                dataRoot,
                "Meshes"
            );

        Assert.True(
            analysis.Complete,
            string.Join(
                Environment.NewLine,
                analysis.Errors
            )
        );

        return analysis;
    }

    private static WindowsNamespacePhysicalParticipant
        SingleFileParticipant(
            WindowsNamespaceAnalysis analysis)
    {
        return Assert.Single(
            analysis.Nodes
                .SelectMany(
                    node =>
                        node.Participants
                ),
            candidate =>
                candidate.Kind ==
                    WindowsNamespacePhysicalObjectKind.File
        );
    }

    private static WindowsNamespaceFileIncarnationObservation
        SingleFileObservation(
            WindowsNamespaceAnalysis analysis)
    {
        return Assert.Single(
            analysis.FileIncarnationObservations
        );
    }

    private static void AssertSameFileIncarnation(
        WindowsNamespaceAnalysis expectedAnalysis,
        WindowsNamespaceAnalysis actualAnalysis)
    {
        WindowsNamespacePhysicalParticipant expectedParticipant =
            SingleFileParticipant(
                expectedAnalysis
            );

        WindowsNamespacePhysicalParticipant actualParticipant =
            SingleFileParticipant(
                actualAnalysis
            );

        WindowsNamespaceFileIncarnationObservation expectedObservation =
            SingleFileObservation(
                expectedAnalysis
            );

        WindowsNamespaceFileIncarnationObservation actualObservation =
            SingleFileObservation(
                actualAnalysis
            );

        Assert.Equal(
            expectedParticipant.DeviceMajor,
            actualParticipant.DeviceMajor
        );

        Assert.Equal(
            expectedParticipant.DeviceMinor,
            actualParticipant.DeviceMinor
        );

        Assert.Equal(
            expectedParticipant.Inode,
            actualParticipant.Inode
        );

        Assert.Equal(
            expectedParticipant.MountId,
            actualParticipant.MountId
        );

        Assert.Equal(
            expectedObservation.InodeGeneration,
            actualObservation.InodeGeneration
        );
    }

    private static (string DataRoot, string FilePath) CreateDataFile(
        string root,
        string dataName,
        string relativeFilePath)
    {
        string data =
            Path.Combine(
                root,
                dataName
            );

        string file =
            Path.Combine(
                data,
                relativeFilePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                file
            )!
        );

        File.WriteAllText(
            file,
            "fixture"
        );

        return (
            data,
            file
        );
    }

    private static string CreateTempDirectory()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-physical-reacquire-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            path
        );

        return path;
    }
}
