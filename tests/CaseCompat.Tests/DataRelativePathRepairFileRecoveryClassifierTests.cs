using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairFileRecoveryClassifierTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            30,
            20,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void Intent_MissingDestination_IsConsistent()
    {
        using Fixture fixture =
            new();

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                fixture.Intent()
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .IntentDestinationMissing,
            result.State
        );

        Assert.True(
            result.ClassificationAvailable
        );
    }

    [Fact]
    public void Intent_PresentDestination_IsConflict()
    {
        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "external"
        );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                fixture.Intent()
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .IntentDestinationConflict,
            result.State
        );
    }

    [Fact]
    public void Prepared_MissingDestination_RequiresRepreparation()
    {
        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord prepared =
            fixture.Prepared(
                fixture.FakePreparedIdentity()
            );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                prepared
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMissing,
            result.State
        );
    }

    [Fact]
    public void Prepared_MatchingDestination_IsRecognized()
    {
        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "source"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                fixture.Prepared(
                    preparedIdentity
                )
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMatches,
            result.State
        );

        Assert.True(
            result.DestinationMatchesPreparedIdentity
        );
    }

    [Fact]
    public void Prepared_DifferentDestination_IsConflict()
    {
        using Fixture fixture =
            new();

        fixture.WriteSibling(
            "owned.nif",
            "owned"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromChild(
                "owned.nif"
            );

        fixture.WriteDestination(
            "replacement"
        );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                fixture.Prepared(
                    preparedIdentity
                )
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationConflict,
            result.State
        );

        Assert.False(
            result.DestinationMatchesPreparedIdentity
        );
    }

    [Fact]
    public void Prepared_SameInodeSameSizeDifferentContent_IsConflict()
    {
        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "source"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        fixture.OverwriteDestinationInPlace(
            "mutant"
        );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                fixture.Prepared(
                    preparedIdentity
                )
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationConflict,
            result.State
        );

        Assert.True(
            result.DestinationMatchesPreparedIdentity
        );

        Assert.False(
            result.DestinationContentMatchesSourceSnapshot
        );

        Assert.NotNull(
            result.DestinationSnapshot
        );

        Assert.True(
            result.DestinationSnapshot!.Success
        );

        Assert.Equal(
            fixture.SourceLength,
            result.DestinationSnapshot.Size
        );

        Assert.Contains(
            "SHA-256",
            result.Error
        );
    }

    [Fact]
    public void Prepared_SameInodeDifferentSize_IsConflict()
    {
        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "source"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        fixture.OverwriteDestinationInPlace(
            "source!"
        );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                fixture.Prepared(
                    preparedIdentity
                )
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationConflict,
            result.State
        );

        Assert.True(
            result.DestinationMatchesPreparedIdentity
        );

        Assert.False(
            result.DestinationContentMatchesSourceSnapshot
        );

        Assert.NotNull(
            result.DestinationSnapshot
        );

        Assert.True(
            result.DestinationSnapshot!.Success
        );

        Assert.NotEqual(
            fixture.SourceLength,
            result.DestinationSnapshot.Size
        );

        Assert.Contains(
            "size",
            result.Error,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Applied_MatchingDestination_IsConsistent()
    {
        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "source"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        DataRelativePathRepairFileJournalRecord applied =
            RequireRecord(
                DataRelativePathRepairFileJournal.MarkApplied(
                    fixture.Prepared(
                        preparedIdentity
                    ),
                    T0.AddSeconds(2)
                )
            );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                applied
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .AppliedDestinationMatches,
            result.State
        );
    }

    [Fact]
    public void Applied_MissingDestination_IsDetected()
    {
        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord applied =
            RequireRecord(
                DataRelativePathRepairFileJournal.MarkApplied(
                    fixture.Prepared(
                        fixture.FakePreparedIdentity()
                    ),
                    T0.AddSeconds(2)
                )
            );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                applied
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .AppliedDestinationMissing,
            result.State
        );
    }

    [Fact]
    public void RollbackRequested_MatchingDestination_IsPending()
    {
        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "source"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        DataRelativePathRepairFileJournalRecord
            rollbackRequested =
                fixture.RollbackRequested(
                    preparedIdentity
                );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                rollbackRequested
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMatches,
            result.State
        );
    }

    [Fact]
    public void RollbackRequested_MissingDestination_IsDetected()
    {
        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord
            rollbackRequested =
                fixture.RollbackRequested(
                    fixture.FakePreparedIdentity()
                );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                rollbackRequested
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMissing,
            result.State
        );
    }

    [Fact]
    public void RolledBack_MissingDestination_IsConsistent()
    {
        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord rolledBack =
            RequireRecord(
                DataRelativePathRepairFileJournal.MarkRolledBack(
                    fixture.RollbackRequested(
                        fixture.FakePreparedIdentity()
                    ),
                    T0.AddSeconds(4)
                )
            );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                rolledBack
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RolledBackDestinationMissing,
            result.State
        );
    }

    [Fact]
    public void RolledBack_PresentDestination_IsConflict()
    {
        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "unexpected"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        DataRelativePathRepairFileJournalRecord rolledBack =
            RequireRecord(
                DataRelativePathRepairFileJournal.MarkRolledBack(
                    fixture.RollbackRequested(
                        preparedIdentity
                    ),
                    T0.AddSeconds(4)
                )
            );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                rolledBack
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RolledBackDestinationConflict,
            result.State
        );
    }

    [Fact]
    public void RecoveryConflict_IsReturnedWithoutReinterpretingFilesystem()
    {
        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord conflict =
            RequireRecord(
                DataRelativePathRepairFileJournal
                    .MarkRecoveryConflict(
                        fixture.Prepared(
                            fixture.FakePreparedIdentity()
                        ),
                        "fixture conflict",
                        T0.AddSeconds(2)
                    )
            );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                conflict
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RecoveryConflictTerminal,
            result.State
        );

        Assert.Equal(
            "fixture conflict",
            result.Error
        );
    }

    [Fact]
    public void Prepared_SymbolicLinkDestination_IsConflict()
    {
        using Fixture fixture =
            new();

        fixture.WriteSibling(
            "target.nif",
            "target"
        );

        File.CreateSymbolicLink(
            fixture.DestinationPath,
            Path.Combine(
                fixture.ParentPath,
                "target.nif"
            )
        );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                fixture.Prepared(
                    fixture.FakePreparedIdentity()
                )
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationConflict,
            result.State
        );

        Assert.Equal(
            LinuxOpenChildReadOnlyAtState
                .ChildSymbolicLinkRejected,
            result.DestinationOpenState
        );
    }

    [Fact]
    public void DestinationParentReplaced_IsValidationFailure()
    {
        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord prepared =
            fixture.Prepared(
                fixture.FakePreparedIdentity()
            );

        string moved =
            fixture.ParentPath +
            "-original";

        Directory.Move(
            fixture.ParentPath,
            moved
        );

        Directory.CreateDirectory(
            fixture.ParentPath
        );

        DataRelativePathRepairFileRecoveryClassification result =
            DataRelativePathRepairFileRecoveryClassifier.Classify(
                prepared
            );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .DestinationParentValidationFailed,
            result.State
        );

        Assert.False(
            result.ClassificationAvailable
        );

        Assert.Equal(
            DataRelativePathRepairDestinationParentValidationState
                .IdentityChanged,
            result.ParentValidation!.State
        );
    }

    private static DataRelativePathRepairFileJournalRecord
        RequireRecord(
            DataRelativePathRepairFileJournalTransitionResult result)
    {
        Assert.True(
            result.Success,
            result.Error
        );

        return Assert.IsType<
            DataRelativePathRepairFileJournalRecord
        >(
            result.Record
        );
    }

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-recovery-classifier-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRoot =
                Path.Combine(
                    RootPath,
                    "Data"
                );

            ParentPath =
                Path.Combine(
                    DataRoot,
                    "Parent"
                );

            SourcePath =
                Path.Combine(
                    DataRoot,
                    "source.nif"
                );

            DestinationPath =
                Path.Combine(
                    ParentPath,
                    "Final.nif"
                );

            Directory.CreateDirectory(
                ParentPath
            );

            File.WriteAllText(
                SourcePath,
                "source"
            );

            ParentSnapshot =
                CaptureParentSnapshot();
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string ParentPath { get; }

        public string SourcePath { get; }

        public string DestinationPath { get; }

        public long SourceLength =>
            Encoding.UTF8.GetByteCount(
                "source"
            );

        public DataRelativePathRepairDestinationParentSnapshot
            ParentSnapshot { get; }

        public DataRelativePathRepairFileJournalRecord Intent()
        {
            DataRelativePathRepairFileJournalTransitionResult result =
                DataRelativePathRepairFileJournal.CreateIntent(
                    Guid.NewGuid(),
                    T0,
                    DataRoot,
                    new DataRelativePathRepairPlanOperation(
                        Kind:
                            DataRelativePathRepairPlanOperationKind
                                .CreateFile,
                        DestinationPath:
                            DestinationPath,
                        SourcePath:
                            SourcePath
                    ),
                    new DataRelativePathRepairSourceSnapshot(
                        PhysicalPath:
                            SourcePath,
                        Size:
                            6,
                        Sha256:
                            Convert.ToHexString(
                                SHA256.HashData(
                                    Encoding.UTF8.GetBytes(
                                        "source"
                                    )
                                )
                            ),
                        Identity:
                            new LinuxFileIdentityResult(
                                FullPath:
                                    SourcePath,
                                DeviceMajor:
                                    8U,
                                DeviceMinor:
                                    1U,
                                Inode:
                                    100UL,
                                LinkCount:
                                    1U,
                                MountId:
                                    55UL,
                                Error:
                                    null
                            )
                    ),
                    ParentSnapshot
                );

            return RequireRecord(
                result
            );
        }

        public DataRelativePathRepairFileJournalRecord Prepared(
            LinuxOpenedFileIdentityResult preparedIdentity)
        {
            return RequireRecord(
                DataRelativePathRepairFileJournal.MarkPrepared(
                    Intent(),
                    SyntheticFileJournalIncarnation.FromPhysical(
                        preparedIdentity
                    ),
                    T0.AddSeconds(1)
                )
            );
        }

        public DataRelativePathRepairFileJournalRecord
            RollbackRequested(
                LinuxOpenedFileIdentityResult preparedIdentity)
        {
            DataRelativePathRepairFileJournalRecord prepared =
                Prepared(
                    preparedIdentity
                );

            DataRelativePathRepairFileJournalRecord applied =
                RequireRecord(
                    DataRelativePathRepairFileJournal.MarkApplied(
                        prepared,
                        T0.AddSeconds(2)
                    )
                );

            return RequireRecord(
                DataRelativePathRepairFileJournal
                    .RequestRollback(
                        applied,
                        T0.AddSeconds(3)
                    )
            );
        }

        public void WriteDestination(
            string text)
        {
            File.WriteAllText(
                DestinationPath,
                text
            );
        }

        public void OverwriteDestinationInPlace(
            string text)
        {
            byte[] bytes =
                Encoding.UTF8.GetBytes(
                    text
                );

            using FileStream stream =
                new(
                    DestinationPath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None
                );

            stream.SetLength(
                bytes.LongLength
            );

            stream.Position =
                0;

            stream.Write(
                bytes
            );

            stream.Flush(
                flushToDisk:
                    true
            );
        }

        public void WriteSibling(
            string childName,
            string text)
        {
            File.WriteAllText(
                Path.Combine(
                    ParentPath,
                    childName
                ),
                text
            );
        }

        public LinuxOpenedFileIdentityResult
            PreparedIdentityFromDestination()
        {
            return PreparedIdentityFromChild(
                "Final.nif"
            );
        }

        public LinuxOpenedFileIdentityResult
            PreparedIdentityFromChild(
                string childName)
        {
            using LinuxNoFollowPathHandle parent =
                OpenParent();

            LinuxOpenChildReadOnlyAtResult opened =
                LinuxOpenChildReadOnlyAt.Open(
                    parent,
                    childName
                );

            Assert.True(
                opened.Success
            );

            using LinuxOpenedChildHandle child =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    opened.OpenedChild
                );

            LinuxOpenedFileIdentityResult identity =
                LinuxOpenedFileIdentity.Capture(
                    child
                );

            Assert.True(
                identity.Success
            );

            return identity with
            {
                LinkCount =
                    0U
            };
        }

        public LinuxOpenedFileIdentityResult
            FakePreparedIdentity()
        {
            return new LinuxOpenedFileIdentityResult(
                State:
                    LinuxOpenedFileIdentityState.Captured,
                DeviceMajor:
                    8U,
                DeviceMinor:
                    1U,
                Inode:
                    999999UL,
                LinkCount:
                    0U,
                MountId:
                    55UL,
                Errno:
                    null,
                Error:
                    null
            );
        }

        private
            DataRelativePathRepairDestinationParentSnapshot
            CaptureParentSnapshot()
        {
            using LinuxNoFollowPathHandle parent =
                OpenParent();

            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    parent
                );

            Assert.True(
                snapshot.Success
            );

            Assert.NotNull(
                snapshot.Identity
            );

            Assert.False(
                snapshot.CasefoldEnabled
            );

            Assert.NotNull(
                snapshot.RawFlags
            );

            return new
                DataRelativePathRepairDestinationParentSnapshot(
                    PhysicalPath:
                        ParentPath,
                    Identity:
                        snapshot.Identity!,
                    CasefoldEnabled:
                        snapshot.CasefoldEnabled!.Value,
                    RawFlags:
                        snapshot.RawFlags!.Value
                );
        }

        private LinuxNoFollowPathHandle OpenParent()
        {
            LinuxNoFollowPathOpenResult result =
                LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                    DataRoot,
                    "Parent"
                );

            Assert.True(
                result.Success
            );

            return Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                result.OpenedPath
            );
        }

        public void Dispose()
        {
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
