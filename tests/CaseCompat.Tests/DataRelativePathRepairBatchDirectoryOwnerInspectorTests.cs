using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchDirectoryOwnerInspectorTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            9,
            2,
            3,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void Inspect_EarlierOwnedAppliedDirectory_AuthorizesEvidence()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        fixture.ApplyPlan(
            owner
        );

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        string[] before =
            Directory.GetFiles(
                owner.ChildDirectoryPath
            )
            .OrderBy(
                path => path,
                StringComparer.Ordinal
            )
            .ToArray();

        DataRelativePathRepairBatchDirectoryOwnerInspection inspection =
            DataRelativePathRepairBatchDirectoryOwnerInspector.Inspect(
                batch,
                context,
                owner.DestinationDirectoryPath
            );

        Assert.True(
            inspection.Success,
            inspection.Error
        );

        DataRelativePathRepairBatchDirectoryOwnerEvidence evidence =
            Assert.IsType<
                DataRelativePathRepairBatchDirectoryOwnerEvidence
            >(
                inspection.Evidence
            );

        Assert.Equal(
            context.BatchId,
            evidence.BatchId
        );

        Assert.Equal(
            0,
            evidence.OwnerChildIndex
        );

        Assert.Equal(
            owner.ChildName,
            evidence.OwnerChildName
        );

        Assert.Equal(
            context.EarlierChildren[0].PlanId,
            evidence.OwnerPlanId
        );

        Assert.Equal(
            context.EarlierChildren[0].ManifestSha256,
            evidence.OwnerManifestSha256
        );

        Assert.Equal(
            0,
            evidence.OwnerOperationIndex
        );

        Assert.False(
            string.IsNullOrWhiteSpace(
                evidence.OwnerJournalChildName
            )
        );

        Assert.NotEqual(
            Guid.Empty,
            evidence.OwnerJournalId
        );

        Assert.NotNull(
            evidence.OwnedDirectoryIncarnationIdentity
        );

        string[] after =
            Directory.GetFiles(
                owner.ChildDirectoryPath
            )
            .OrderBy(
                path => path,
                StringComparer.Ordinal
            )
            .ToArray();

        Assert.Equal(
            before,
            after
        );
    }

    [Fact]
    public void Inspect_NoMatchingEarlierDirectory_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "beta",
                requestedComponent:
                    "Beta"
            );

        fixture.ApplyPlan(
            owner
        );

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryOwnerInspection inspection =
            DataRelativePathRepairBatchDirectoryOwnerInspector.Inspect(
                batch,
                context,
                borrower.DestinationDirectoryPath
            );

        Assert.False(
            inspection.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryOwnerInspectionState
                .NoOwnedDirectoryAuthority,
            inspection.State
        );

        Assert.Null(
            inspection.Evidence
        );
    }

    [Fact]
    public void Inspect_EarlierManifestShaMismatch_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        fixture.ApplyPlan(
            owner
        );

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1,
                firstChildManifestSha256Override:
                    new string(
                        '0',
                        64
                    )
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryOwnerInspection inspection =
            DataRelativePathRepairBatchDirectoryOwnerInspector.Inspect(
                batch,
                context,
                owner.DestinationDirectoryPath
            );

        Assert.False(
            inspection.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryOwnerInspectionState
                .EarlierManifestExpectationMismatch,
            inspection.State
        );

        Assert.Equal(
            owner.ChildName,
            inspection.FailedChildName
        );

        Assert.Null(
            inspection.Evidence
        );
    }

    [Fact]
    public void Inspect_MatchingOwnerJournalAbsent_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        /*
         * Deliberately do not apply the owner.
         *
         * Its authenticated manifest contains the matching CreateDirectory
         * operation, but no durable operation journal proves ownership.
         */
        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryOwnerInspection inspection =
            DataRelativePathRepairBatchDirectoryOwnerInspector.Inspect(
                batch,
                context,
                owner.DestinationDirectoryPath
            );

        Assert.False(
            inspection.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryOwnerInspectionState
                .OwnerJournalReadFailed,
            inspection.State
        );

        Assert.Equal(
            owner.ChildName,
            inspection.FailedChildName
        );

        Assert.Null(
            inspection.Evidence
        );
    }

    [Fact]
    public void Inspect_FirstBatchChild_HasNoEarlierAuthority()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec first =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    first
                ],
                currentChildIndex:
                    0
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryOwnerInspection inspection =
            DataRelativePathRepairBatchDirectoryOwnerInspector.Inspect(
                batch,
                context,
                first.DestinationDirectoryPath
            );

        Assert.False(
            inspection.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryOwnerInspectionState
                .NoOwnedDirectoryAuthority,
            inspection.State
        );

        Assert.Null(
            inspection.Evidence
        );
    }

    [Fact]
    public void
        Inspect_EarlierBatchReusedDirectory_SkipsBorrowerAndReturnsTrueOwner()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec reused =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec current =
            fixture.CreatePlan(
                index:
                    3,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        fixture.ApplyPlan(
            owner
        );

        Guid batchId =
            Guid.NewGuid();

        /*
         * Build a context in which child 2 is the current borrower.
         * Child 1 is therefore the only earlier durable member and must
         * be the authority used to manufacture child 2's test-only
         * BatchReused journal.
         */
        DataRelativePathRepairBatchExecutionContext reusedContext =
            fixture.BuildContext(
                [
                    owner,
                    reused,
                    current
                ],
                currentChildIndex:
                    1,
                batchId:
                    batchId
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryOwnerInspection ownerInspection =
            DataRelativePathRepairBatchDirectoryOwnerInspector.Inspect(
                batch,
                reusedContext,
                owner.DestinationDirectoryPath
            );

        Assert.True(
            ownerInspection.Success,
            ownerInspection.Error
        );

        DataRelativePathRepairBatchDirectoryOwnerEvidence trueOwner =
            Assert.IsType<
                DataRelativePathRepairBatchDirectoryOwnerEvidence
            >(
                ownerInspection.Evidence
            );

        fixture.PersistBatchReusedDirectoryJournal(
            owner,
            reused,
            trueOwner,
            batchId
        );

        /*
         * Child 3 now has two earlier matching plans:
         *
         *   child 1: schema-v2 owned Applied
         *   child 2: schema-v3 BatchReused Applied
         *
         * The borrower must never become ownership authority. The
         * inspector must skip child 2 and return child 1.
         */
        DataRelativePathRepairBatchExecutionContext currentContext =
            fixture.BuildContext(
                [
                    owner,
                    reused,
                    current
                ],
                currentChildIndex:
                    2,
                batchId:
                    batchId
            );

        DataRelativePathRepairBatchDirectoryOwnerInspection inspection =
            DataRelativePathRepairBatchDirectoryOwnerInspector.Inspect(
                batch,
                currentContext,
                owner.DestinationDirectoryPath
            );

        Assert.True(
            inspection.Success,
            inspection.Error
        );

        DataRelativePathRepairBatchDirectoryOwnerEvidence evidence =
            Assert.IsType<
                DataRelativePathRepairBatchDirectoryOwnerEvidence
            >(
                inspection.Evidence
            );

        Assert.Equal(
            0,
            evidence.OwnerChildIndex
        );

        Assert.Equal(
            owner.ChildName,
            evidence.OwnerChildName
        );

        Assert.NotEqual(
            reused.ChildName,
            evidence.OwnerChildName
        );

        Assert.Equal(
            trueOwner.OwnerPlanId,
            evidence.OwnerPlanId
        );

        Assert.Equal(
            trueOwner.OwnerManifestSha256,
            evidence.OwnerManifestSha256
        );

        Assert.Equal(
            trueOwner.OwnerOperationIndex,
            evidence.OwnerOperationIndex
        );

        Assert.Equal(
            trueOwner.OwnerJournalChildName,
            evidence.OwnerJournalChildName
        );

        Assert.Equal(
            trueOwner.OwnerJournalId,
            evidence.OwnerJournalId
        );

        Assert.True(
            evidence.OwnedDirectoryIncarnationIdentity
                .SameIncarnationAs(
                    trueOwner.OwnedDirectoryIncarnationIdentity
                )
        );
    }

    private sealed record PlanSpec(
        string ChildName,
        string ChildDirectoryPath,
        string DestinationDirectoryPath
    );

    private sealed class Fixture
        : IDisposable
    {
        private Fixture(
            string rootPath,
            string dataRoot,
            string batchRoot)
        {
            RootPath =
                rootPath;

            DataRoot =
                dataRoot;

            BatchRoot =
                batchRoot;
        }

        public const string ManifestName =
            "repair-plan.json";

        public string RootPath
        {
            get;
        }

        public string DataRoot
        {
            get;
        }

        public string BatchRoot
        {
            get;
        }

        public static Fixture Create()
        {
            string rootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-batch-directory-owner-inspector-tests",
                    Guid.NewGuid().ToString("N")
                );

            string dataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        rootPath,
                        "Data"
                    )
                ).FullName;

            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            );

            string batchRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        rootPath,
                        "batch"
                    )
                ).FullName;

            return new(
                rootPath,
                dataRoot,
                batchRoot
            );
        }

        public PlanSpec CreatePlan(
            int index,
            string physicalComponent,
            string requestedComponent)
        {
            string fileName =
                $"Thing{index}.nif";

            string physicalDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        physicalComponent
                    )
                ).FullName;

            string sourcePath =
                Path.Combine(
                    physicalDirectory,
                    fileName
                );

            File.WriteAllText(
                sourcePath,
                $"owner-inspector-payload-{index}"
            );

            string childName =
                $"plan-{index:D6}";

            string childDirectoryPath =
                Directory.CreateDirectory(
                    Path.Combine(
                        BatchRoot,
                        childName
                    )
                ).FullName;

            string requestedPath =
                $"meshes/{requestedComponent}/{fileName}";

            int result =
                global::RepairPlanCommand.Run(
                    [
                        "repair-plan",
                        DataRoot,
                        requestedPath,
                        childDirectoryPath,
                        ManifestName
                    ]
                );

            Assert.Equal(
                0,
                result
            );

            return new(
                ChildName:
                    childName,
                ChildDirectoryPath:
                    childDirectoryPath,
                DestinationDirectoryPath:
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        requestedComponent
                    )
            );
        }

        public void ApplyPlan(
            PlanSpec plan)
        {
            int result =
                global::RepairApplyCommand.Run(
                    [
                        "repair-apply",
                        plan.ChildDirectoryPath,
                        ManifestName,
                        DataRoot
                    ]
                );

            Assert.Equal(
                0,
                result
            );
        }

        public void PersistBatchReusedDirectoryJournal(
            PlanSpec owner,
            PlanSpec borrower,
            DataRelativePathRepairBatchDirectoryOwnerEvidence ownerEvidence,
            Guid batchId)
        {
            using LinuxNoFollowPathHandle ownerDirectory =
                OpenRoot(
                    owner.ChildDirectoryPath
                );

            DataRelativePathRepairPlanManifestReaderResult ownerManifestRead =
                DataRelativePathRepairPlanManifestReader.Read(
                    ownerDirectory,
                    ManifestName
                );

            Assert.True(
                ownerManifestRead.Success,
                ownerManifestRead.Error
            );

            DataRelativePathRepairDirectoryJournalReaderResult
                ownerJournalRead =
                    DataRelativePathRepairDirectoryJournalReader.Read(
                        ownerDirectory,
                        ownerEvidence.OwnerJournalChildName
                    );

            Assert.True(
                ownerJournalRead.Success,
                ownerJournalRead.Error
            );

            DataRelativePathRepairDirectoryJournalRecord ownerJournal =
                Assert.IsType<
                    DataRelativePathRepairDirectoryJournalRecord
                >(
                    ownerJournalRead.Record
                );

            Assert.Equal(
                DataRelativePathRepairDirectoryJournalRecord.SchemaVersion2,
                ownerJournal.SchemaVersion
            );

            Assert.Equal(
                DataRelativePathRepairDirectoryJournalState.Applied,
                ownerJournal.State
            );

            Assert.NotNull(
                ownerJournal.PreparedDirectoryIncarnationIdentity
            );

            using LinuxNoFollowPathHandle borrowerDirectory =
                OpenRoot(
                    borrower.ChildDirectoryPath
                );

            DataRelativePathRepairPlanManifestReaderResult borrowerRead =
                DataRelativePathRepairPlanManifestReader.Read(
                    borrowerDirectory,
                    ManifestName
                );

            Assert.True(
                borrowerRead.Success,
                borrowerRead.Error
            );

            DataRelativePathRepairPlanManifestRecord borrowerManifest =
                Assert.IsType<
                    DataRelativePathRepairPlanManifestRecord
                >(
                    borrowerRead.Manifest
                );

            DataRelativePathRepairPlanManifestOperation borrowerDirectoryOp =
                borrowerManifest.Operations[0];

            Assert.Equal(
                DataRelativePathRepairPlanOperationKind.CreateDirectory,
                borrowerDirectoryOp.Operation.Kind
            );

            Assert.Equal(
                owner.DestinationDirectoryPath,
                borrowerDirectoryOp.Operation.DestinationPath
            );

            var provenance =
                new DataRelativePathRepairDirectoryBatchReuseProvenance(
                    BatchId:
                        batchId,
                    OwnerChildName:
                        ownerEvidence.OwnerChildName,
                    OwnerPlanId:
                        ownerEvidence.OwnerPlanId,
                    OwnerManifestSha256:
                        ownerEvidence.OwnerManifestSha256,
                    OwnerOperationIndex:
                        ownerEvidence.OwnerOperationIndex,
                    OwnerJournalChildName:
                        ownerEvidence.OwnerJournalChildName,
                    ReusedDirectoryIncarnationIdentity:
                        ownerEvidence.OwnedDirectoryIncarnationIdentity
                );

            DataRelativePathRepairDirectoryJournalTransitionResult creation =
                DataRelativePathRepairDirectoryJournal
                    .CreateBatchReuseApplied(
                        Guid.NewGuid(),
                        T0.AddMinutes(1),
                        DataRoot,
                        borrowerDirectoryOp.Operation,
                        borrowerManifest.InitialDestinationParentSnapshot,
                        ownerJournal.DestinationParentIncarnationIdentity,
                        provenance
                    );

            Assert.True(
                creation.Success,
                creation.Error
            );

            DataRelativePathRepairDirectoryJournalRecord reusedJournal =
                Assert.IsType<
                    DataRelativePathRepairDirectoryJournalRecord
                >(
                    creation.Record
                );

            DataRelativePathRepairDirectoryJournalWriterResult write =
                DataRelativePathRepairDirectoryJournalWriter
                    .CreateBatchReuseApplied(
                        borrowerDirectory,
                        borrowerDirectoryOp.JournalChildName,
                        reusedJournal
                    );

            Assert.True(
                write.Success,
                write.Error
            );

            DataRelativePathRepairDirectoryJournalReaderResult verify =
                DataRelativePathRepairDirectoryJournalReader.Read(
                    borrowerDirectory,
                    borrowerDirectoryOp.JournalChildName
                );

            Assert.True(
                verify.Success,
                verify.Error
            );

            Assert.Equal(
                DataRelativePathRepairDirectoryJournalRecord.SchemaVersion3,
                verify.Record!.SchemaVersion
            );

            Assert.Equal(
                DataRelativePathRepairDirectoryOwnershipDisposition.BatchReused,
                verify.Record.OwnershipDisposition
            );

            Assert.Equal(
                DataRelativePathRepairDirectoryJournalState.Applied,
                verify.Record.State
            );

            Assert.Null(
                verify.Record.PreparedDirectoryIncarnationIdentity
            );
        }

        public DataRelativePathRepairBatchExecutionContext
            BuildContext(
                IReadOnlyList<PlanSpec> plans,
                int currentChildIndex,
                string? firstChildManifestSha256Override = null,
                Guid? batchId = null)
        {
            using LinuxNoFollowPathHandle batch =
                OpenRoot(
                    BatchRoot
                );

            var children =
                new List<
                    DataRelativePathRepairBatchManifestChild
                >(
                    plans.Count
                );

            for (
                int index = 0;
                index < plans.Count;
                index++)
            {
                PlanSpec plan =
                    plans[index];

                LinuxOpenChildDirectoryReadOnlyAtResult childOpen =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        batch,
                        plan.ChildName
                    );

                Assert.True(
                    childOpen.Success,
                    childOpen.Error
                );

                using LinuxNoFollowPathHandle child =
                    Assert.IsType<
                        LinuxNoFollowPathHandle
                    >(
                        childOpen.OpenedDirectory
                    );

                DataRelativePathRepairPlanManifestReaderResult read =
                    DataRelativePathRepairPlanManifestReader.Read(
                        child,
                        ManifestName
                    );

                Assert.True(
                    read.Success,
                    read.Error
                );

                string sha256 =
                    index == 0 &&
                    firstChildManifestSha256Override is not null
                        ? firstChildManifestSha256Override
                        : read.ManifestSha256!;

                children.Add(
                    new(
                        ChildName:
                            plan.ChildName,
                        PlanId:
                            read.Manifest!.PlanId,
                        ManifestSha256:
                            sha256
                    )
                );
            }

            DataRelativePathRepairBatchManifestCreation creation =
                DataRelativePathRepairBatchManifest.Create(
                    batchId ?? Guid.NewGuid(),
                    T0,
                    DataRoot,
                    ManifestName,
                    inputPathCount:
                        plans.Count,
                    safeRejectionCount:
                        0,
                    children
                );

            Assert.True(
                creation.Success,
                creation.Error
            );

            DataRelativePathRepairBatchManifestRecord manifest =
                Assert.IsType<
                    DataRelativePathRepairBatchManifestRecord
                >(
                    creation.Manifest
                );

            DataRelativePathRepairBatchExecutionContextCreation
                contextCreation =
                    DataRelativePathRepairBatchExecutionContext.Create(
                        manifest,
                        currentChildIndex,
                        manifest.Children[
                            currentChildIndex
                        ]
                    );

            Assert.True(
                contextCreation.Success,
                contextCreation.Error
            );

            return Assert.IsType<
                DataRelativePathRepairBatchExecutionContext
            >(
                contextCreation.Context
            );
        }

        public static LinuxNoFollowPathHandle OpenRoot(
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
