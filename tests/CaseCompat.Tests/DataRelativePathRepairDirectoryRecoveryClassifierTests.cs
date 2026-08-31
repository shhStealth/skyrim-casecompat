using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDirectoryRecoveryClassifierTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            2,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void Intent_MissingFinal_IsConsistent()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                fixture.CreateIntent()
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .IntentFinalMissing,
            result.State
        );
    }

    [Fact]
    public void Intent_PresentFinal_IsConflict()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Final"
        );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                fixture.CreateIntent()
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .IntentFinalConflict,
            result.State
        );
    }

    [Fact]
    public void Prepared_StagingMatchesFinalMissing_IsConsistent()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureIdentity(
                ".stage"
            );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                fixture.Prepared(
                    identity
                )
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedStagingMatchesFinalMissing,
            result.State
        );

        Assert.True(
            result.StagingMatchesPreparedIdentity
        );
    }

    [Fact]
    public void Prepared_BothMissing_RequiresReprepare()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                fixture.Prepared(
                    SyntheticDirectoryJournalIncarnation.FromPhysical(
                        fixture.SyntheticIdentity()
                    )
                )
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedBothMissing,
            result.State
        );
    }

    [Fact]
    public void Prepared_FinalMatchesStagingMissing_RecognizesPublishedDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureIdentity(
                ".stage"
            );

        Directory.Move(
            fixture.PathFor(
                ".stage"
            ),
            fixture.PathFor(
                "Final"
            )
        );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                fixture.Prepared(
                    identity
                )
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedFinalMatchesStagingMissing,
            result.State
        );

        Assert.True(
            result.FinalMatchesPreparedIdentity
        );
    }

    [Fact]
    public void Prepared_BothNamesPresent_IsConflict()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureIdentity(
                ".stage"
            );

        fixture.CreateDirectory(
            "Final"
        );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                fixture.Prepared(
                    identity
                )
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedConflict,
            result.State
        );
    }

    [Fact]
    public void Prepared_WrongStagingIdentity_IsConflict()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                fixture.Prepared(
                    SyntheticDirectoryJournalIncarnation.FromPhysical(
                        fixture.SyntheticIdentity()
                    )
                )
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedConflict,
            result.State
        );
    }

    [Fact]
    public void Prepared_SymbolicLinkStaging_IsConflict()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Target"
        );

        Directory.CreateSymbolicLink(
            fixture.PathFor(
                ".stage"
            ),
            fixture.PathFor(
                "Target"
            )
        );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                fixture.Prepared(
                    SyntheticDirectoryJournalIncarnation.FromPhysical(
                        fixture.SyntheticIdentity()
                    )
                )
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedConflict,
            result.State
        );
    }

    [Fact]
    public void Applied_FinalMatches_IsConsistent()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDirectoryJournalRecord applied =
            fixture.AppliedWithFinal();

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                applied
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMatches,
            result.State
        );

        Assert.True(
            result.FinalMatchesPreparedIdentity
        );
    }

    [Fact]
    public void Applied_FinalMissing_IsDetected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDirectoryJournalRecord prepared =
            fixture.Prepared(
                SyntheticDirectoryJournalIncarnation.FromPhysical(
                    fixture.SyntheticIdentity()
                )
            );

        DataRelativePathRepairDirectoryJournalRecord applied =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkApplied(
                    prepared,
                    T0.AddSeconds(2)
                )
            );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                applied
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMissing,
            result.State
        );
    }

    [Fact]
    public void Applied_StagingStillPresent_IsConflict()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".stage"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureIdentity(
                ".stage"
            );

        DataRelativePathRepairDirectoryJournalRecord applied =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkApplied(
                    fixture.Prepared(
                        identity
                    ),
                    T0.AddSeconds(2)
                )
            );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                applied
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedConflict,
            result.State
        );
    }

    [Fact]
    public void RollbackRequested_FinalMatches_IsPending()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDirectoryJournalRecord applied =
            fixture.AppliedWithFinal();

        DataRelativePathRepairDirectoryJournalRecord requested =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.RequestRollback(
                    applied,
                    T0.AddSeconds(3)
                )
            );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                requested
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .RollbackRequestedFinalMatches,
            result.State
        );
    }

    [Fact]
    public void RollbackRequested_FinalMissing_IsDetected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDirectoryJournalRecord prepared =
            fixture.Prepared(
                SyntheticDirectoryJournalIncarnation.FromPhysical(
                    fixture.SyntheticIdentity()
                )
            );

        DataRelativePathRepairDirectoryJournalRecord applied =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkApplied(
                    prepared,
                    T0.AddSeconds(2)
                )
            );

        DataRelativePathRepairDirectoryJournalRecord requested =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.RequestRollback(
                    applied,
                    T0.AddSeconds(3)
                )
            );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                requested
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .RollbackRequestedFinalMissing,
            result.State
        );
    }

    [Fact]
    public void RolledBack_BothMissing_IsConsistent()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDirectoryJournalRecord prepared =
            fixture.Prepared(
                SyntheticDirectoryJournalIncarnation.FromPhysical(
                    fixture.SyntheticIdentity()
                )
            );

        DataRelativePathRepairDirectoryJournalRecord applied =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkApplied(
                    prepared,
                    T0.AddSeconds(2)
                )
            );

        DataRelativePathRepairDirectoryJournalRecord requested =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.RequestRollback(
                    applied,
                    T0.AddSeconds(3)
                )
            );

        DataRelativePathRepairDirectoryJournalRecord rolledBack =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkRolledBack(
                    requested,
                    T0.AddSeconds(4)
                )
            );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                rolledBack
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .RolledBackBothMissing,
            result.State
        );
    }

    [Fact]
    public void RecoveryConflict_IsReturnedWithoutFilesystemInspection()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDirectoryJournalRecord prepared =
            fixture.Prepared(
                SyntheticDirectoryJournalIncarnation.FromPhysical(
                    fixture.SyntheticIdentity()
                )
            );

        DataRelativePathRepairDirectoryJournalRecord conflict =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal
                    .MarkRecoveryConflict(
                        prepared,
                        "test conflict",
                        T0.AddSeconds(2)
                    )
            );

        /*
         * If the classifier attempted parent acquisition after
         * seeing terminal RecoveryConflict, this would fail.
         */
        fixture.DisposeParent();
        Directory.Delete(
            fixture.ParentPath
        );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                conflict
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .RecoveryConflictTerminal,
            result.State
        );

        Assert.Equal(
            "test conflict",
            result.Error
        );
    }

    [Fact]
    public void ParentMountIdMismatch_IsValidationFailure()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDirectoryJournalRecord intent =
            fixture.CreateIntent();

        LinuxFileIdentityResult identity =
            intent.DestinationParentSnapshot.Identity;

        Assert.NotNull(
            identity.MountId
        );

        ulong mismatchedMountId =
            checked(
                identity.MountId!.Value + 1UL
            );

        LinuxDirectoryIncarnationIdentity parentIncarnation =
            intent.DestinationParentIncarnationIdentity;

        DataRelativePathRepairDirectoryJournalRecord mismatched =
            intent with
            {
                DestinationParentSnapshot =
                    intent.DestinationParentSnapshot with
                    {
                        Identity =
                            identity with
                            {
                                MountId =
                                    mismatchedMountId
                            }
                    },
                DestinationParentIncarnationIdentity =
                    parentIncarnation with
                    {
                        PhysicalIdentity =
                            parentIncarnation.PhysicalIdentity with
                            {
                                MountId =
                                    mismatchedMountId
                            }
                    }
            };

        Assert.Null(
            DataRelativePathRepairDirectoryJournal.Validate(
                mismatched
            )
        );

        var result =
            DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                mismatched
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .DestinationParentValidationFailed,
            result.State
        );
    }

    private static
        DataRelativePathRepairDirectoryJournalRecord RequireRecord(
            DataRelativePathRepairDirectoryJournalTransitionResult
                result)
    {
        Assert.True(
            result.Success,
            result.Error
        );

        return Assert.IsType<
            DataRelativePathRepairDirectoryJournalRecord
        >(
            result.Record
        );
    }

    private sealed class Fixture
        : IDisposable
    {
        private bool _parentDisposed;

        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-directory-recovery-classifier-tests",
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

            Directory.CreateDirectory(
                ParentPath
            );

            Parent =
                OpenRoot(
                    ParentPath
                );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string ParentPath { get; }

        public LinuxNoFollowPathHandle Parent { get; }

        public string PathFor(
            string childName)
        {
            return Path.Combine(
                ParentPath,
                childName
            );
        }

        public void CreateDirectory(
            string childName)
        {
            Directory.CreateDirectory(
                PathFor(
                    childName
                )
            );
        }

        public DataRelativePathRepairDirectoryJournalRecord
            CreateIntent()
        {
            DataRelativePathRepairDirectoryJournalTransitionResult
                result =
                    DataRelativePathRepairDirectoryJournal.CreateIntent(
                        Guid.NewGuid(),
                        T0,
                        DataRoot,
                        new DataRelativePathRepairPlanOperation(
                            Kind:
                                DataRelativePathRepairPlanOperationKind
                                    .CreateDirectory,
                            DestinationPath:
                                PathFor(
                                    "Final"
                                ),
                            SourcePath:
                                null
                        ),
                        CaptureParentSnapshot()
                    ,
                        LiveDirectoryJournalIncarnation.Capture(
                            Parent
                        ));

            return RequireRecord(
                result
            );
        }

        public DataRelativePathRepairDirectoryJournalRecord Prepared(
            LinuxDirectoryIncarnationIdentity identity)
        {
            return RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    CreateIntent(),
                    ".stage",
                    identity,
                    T0.AddSeconds(1)
                )
            );
        }

        public DataRelativePathRepairDirectoryJournalRecord
            AppliedWithFinal()
        {
            CreateDirectory(
                ".stage"
            );

            LinuxDirectoryIncarnationIdentity identity =
                CaptureIdentity(
                    ".stage"
                );

            DataRelativePathRepairDirectoryJournalRecord prepared =
                Prepared(
                    identity
                );

            Directory.Move(
                PathFor(
                    ".stage"
                ),
                PathFor(
                    "Final"
                )
            );

            return RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkApplied(
                    prepared,
                    T0.AddSeconds(2)
                )
            );
        }

        public LinuxFileIdentityResult SyntheticIdentity()
        {
            DataRelativePathRepairDestinationParentSnapshot parent =
                CaptureParentSnapshot();

            return new(
                FullPath:
                    PathFor(
                        ".stage"
                    ),
                DeviceMajor:
                    parent.Identity.DeviceMajor,
                DeviceMinor:
                    parent.Identity.DeviceMinor,
                Inode:
                    ulong.MaxValue - 100UL,
                LinkCount:
                    2U,
                MountId:
                    parent.Identity.MountId,
                Error:
                    null
            );
        }

        public LinuxDirectoryIncarnationIdentity CaptureIdentity(
            string childName)
        {
            LinuxOpenChildReadOnlyAtResult opened =
                LinuxOpenChildReadOnlyAt.Open(
                    Parent,
                    childName
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            using LinuxOpenedChildHandle child =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    opened.OpenedChild
                );

            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    child,
                    PathFor(
                        childName
                    )
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            Assert.NotNull(
                snapshot.Identity
            );

            Assert.NotNull(
                snapshot.Identity!.MountId
            );

            return LiveDirectoryJournalIncarnation.Capture(

                child,

                PathFor(

                    childName

                )

            );
        }

        public void DisposeParent()
        {
            if (_parentDisposed)
            {
                return;
            }

            Parent.Dispose();
            _parentDisposed =
                true;
        }

        private
            DataRelativePathRepairDestinationParentSnapshot
            CaptureParentSnapshot()
        {
            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    Parent
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            Assert.NotNull(
                snapshot.Identity
            );

            Assert.NotNull(
                snapshot.Identity!.MountId
            );

            Assert.NotNull(
                snapshot.CasefoldEnabled
            );

            Assert.NotNull(
                snapshot.RawFlags
            );

            Assert.False(
                snapshot.CasefoldEnabled!.Value
            );

            return new(
                PhysicalPath:
                    ParentPath,
                Identity:
                    snapshot.Identity,
                CasefoldEnabled:
                    snapshot.CasefoldEnabled.Value,
                RawFlags:
                    snapshot.RawFlags!.Value
            );
        }

        private static LinuxNoFollowPathHandle OpenRoot(
            string path)
        {
            LinuxNoFollowPathOpenResult result =
                LinuxNoFollowPath.OpenRootReadOnly(
                    path
                );

            Assert.True(
                result.Success,
                result.Error
            );

            return Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                result.OpenedPath
            );
        }

        public void Dispose()
        {
            DisposeParent();

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
