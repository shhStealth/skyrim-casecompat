using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairReadHandleTests
{
    [Fact]
    public void
        ReadSideApis_AcceptDescriptorRelativeChildDirectoryHandle()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string rootPath =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-repair-read-handle-tests",
                Guid.NewGuid()
                    .ToString("N")
            );

        string dataRoot =
            Path.Combine(
                rootPath,
                "Data"
            );

        string journalPath =
            Path.Combine(
                rootPath,
                "Journal"
            );

        Directory.CreateDirectory(
            dataRoot
        );

        Directory.CreateDirectory(
            journalPath
        );

        try
        {
            LinuxNoFollowPathOpenResult rootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    rootPath
                );

            Assert.True(
                rootOpen.Success,
                rootOpen.Error
            );

            using LinuxNoFollowPathHandle root =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    rootOpen.OpenedPath
                );

            LinuxOpenChildReadOnlyAtResult journalOpen =
                LinuxOpenChildReadOnlyAt.Open(
                    root,
                    "Journal"
                );

            Assert.True(
                journalOpen.Success,
                journalOpen.Error
            );

            using LinuxOpenedChildHandle journal =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    journalOpen.OpenedChild
                );

            DataRelativePathRepairPlanManifestReaderResult
                manifestRead =
                    DataRelativePathRepairPlanManifestReader
                        .Read(
                            journal,
                            "repair-plan.json"
                        );

            Assert.False(
                manifestRead.Success
            );

            Assert.Equal(
                DataRelativePathRepairPlanManifestReadState
                    .ManifestUnavailable,
                manifestRead.State
            );

            DataRelativePathRepairDirectoryJournalReaderResult
                directoryJournalRead =
                    DataRelativePathRepairDirectoryJournalReader
                        .Read(
                            journal,
                            "missing-directory-journal.json"
                        );

            Assert.False(
                directoryJournalRead.Success
            );

            Assert.Equal(
                DataRelativePathRepairDirectoryJournalReadState
                    .JournalUnavailable,
                directoryJournalRead.State
            );

            DataRelativePathRepairFileJournalReaderResult
                fileJournalRead =
                    DataRelativePathRepairFileJournalReader
                        .Read(
                            journal,
                            "missing-file-journal.json"
                        );

            Assert.False(
                fileJournalRead.Success
            );

            Assert.Equal(
                DataRelativePathRepairFileJournalReadState
                    .JournalUnavailable,
                fileJournalRead.State
            );

            DataRelativePathRepairPlanStatusInspection inspection =
                DataRelativePathRepairPlanStatusInspector.Inspect(
                    journal,
                    "repair-plan.json",
                    dataRoot
                );

            Assert.False(
                inspection.Success
            );

            Assert.Equal(
                DataRelativePathRepairPlanStatusInspectionState
                    .ManifestReadFailed,
                inspection.State
            );

            Assert.Empty(
                inspection.OperationStatuses
            );

            /*
             * All APIs exercised above are read-only. The descriptor-
             * relative child directory must remain empty.
             */
            Assert.Empty(
                Directory.EnumerateFileSystemEntries(
                    journalPath
                )
            );
        }
        finally
        {
            if (
                Directory.Exists(
                    rootPath))
            {
                Directory.Delete(
                    rootPath,
                    recursive:
                        true
                );
            }
        }
    }
}
