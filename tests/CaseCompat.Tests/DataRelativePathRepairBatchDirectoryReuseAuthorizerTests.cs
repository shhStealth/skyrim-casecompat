using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed partial class
    DataRelativePathRepairBatchDirectoryReuseAuthorizerTests
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
    public void
        Authorize_CurrentDestinationMatchesTrueOwner_ReturnsProvenance()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
            );

        fixture.ApplyPlan(
            owner
        );

        Guid batchId =
            Guid.NewGuid();

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1,
                batchId:
                    batchId
            );

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        DataRelativePathRepairPlanManifestOperation entry =
            borrowerManifest.Operations[0];

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        string[] before =
            Directory.GetFiles(
                borrower.ChildDirectoryPath
            )
            .OrderBy(
                path => path,
                StringComparer.Ordinal
            )
            .ToArray();

        DataRelativePathRepairBatchDirectoryReuseAuthorization result =
            DataRelativePathRepairBatchDirectoryReuseAuthorizer.Authorize(
                batch,
                context,
                parent,
                entry
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                .Authorized,
            result.State
        );

        Assert.Equal(
            owner.ChildName,
            result.OwnerEvidence!.OwnerChildName
        );

        Assert.Equal(
            batchId,
            result.Provenance!.BatchId
        );

        Assert.Equal(
            owner.ChildName,
            result.Provenance.OwnerChildName
        );

        Assert.True(
            result.OwnerEvidence.OwnedDirectoryIncarnationIdentity
                .SameIncarnationAs(
                    result.Provenance
                        .ReusedDirectoryIncarnationIdentity
                )
        );

        Assert.True(
            result.CurrentDestinationIncarnation!.Identity!
                .SameIncarnationAs(
                    result.Provenance
                        .ReusedDirectoryIncarnationIdentity
                )
        );

        string[] after =
            Directory.GetFiles(
                borrower.ChildDirectoryPath
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
    public void
        Authorize_CurrentDestinationReplaced_FailsStrongIncarnationMatch()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
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

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        string moved =
            owner.DestinationDirectoryPath +
            "-original";

        Directory.Move(
            owner.DestinationDirectoryPath,
            moved
        );

        Directory.CreateDirectory(
            owner.DestinationDirectoryPath
        );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryReuseAuthorization result =
            DataRelativePathRepairBatchDirectoryReuseAuthorizer.Authorize(
                batch,
                context,
                parent,
                borrowerManifest.Operations[0]
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                .CurrentDestinationIncarnationMismatch,
            result.State
        );

        Assert.NotNull(
            result.OwnerEvidence
        );

        Assert.NotNull(
            result.CurrentDestinationIncarnation?.Identity
        );

        Assert.False(
            result.OwnerEvidence!.OwnedDirectoryIncarnationIdentity
                .SameIncarnationAs(
                    result.CurrentDestinationIncarnation!.Identity!
                )
        );

        Assert.Null(
            result.Provenance
        );
    }

    [Fact]
    public void
        Authorize_CurrentDestinationMissing_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
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

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        Directory.Move(
            owner.DestinationDirectoryPath,
            owner.DestinationDirectoryPath +
                "-moved"
        );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryReuseAuthorization result =
            DataRelativePathRepairBatchDirectoryReuseAuthorizer.Authorize(
                batch,
                context,
                parent,
                borrowerManifest.Operations[0]
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                .CurrentDestinationOpenFailed,
            result.State
        );

        Assert.Equal(
            LinuxOpenChildDirectoryReadOnlyAtState.ChildUnavailable,
            result.CurrentDestinationOpenState
        );

        Assert.Null(
            result.Provenance
        );
    }

    [Fact]
    public void
        Authorize_LeaseForDifferentParent_IsRejectedBeforeReuse()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
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

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        DataRelativePathRepairPlanManifestOperation original =
            borrowerManifest.Operations[0];

        DataRelativePathRepairPlanManifestOperation wrongParentEntry =
            original with
            {
                Operation =
                    original.Operation with
                    {
                        DestinationPath =
                            Path.Combine(
                                fixture.DataRoot,
                                "textures",
                                "Alpha"
                            )
                    }
            };

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryReuseAuthorization result =
            DataRelativePathRepairBatchDirectoryReuseAuthorizer.Authorize(
                batch,
                context,
                parent,
                wrongParentEntry
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                .DestinationParentBindingMismatch,
            result.State
        );

        Assert.Null(
            result.OwnerInspection
        );

        Assert.Null(
            result.Provenance
        );
    }

    [Fact]
    public void
        Authorize_UnauthenticatedSiblingOperation_IsRejected()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
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

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        DataRelativePathRepairPlanManifestOperation original =
            borrowerManifest.Operations[0];

        DataRelativePathRepairPlanManifestOperation forgedSibling =
            original with
            {
                Operation =
                    original.Operation with
                    {
                        DestinationPath =
                            Path.Combine(
                                Path.GetDirectoryName(
                                    original.Operation.DestinationPath
                                )!,
                                "Beta"
                            )
                    }
            };

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryReuseAuthorization result =
            DataRelativePathRepairBatchDirectoryReuseAuthorizer.Authorize(
                batch,
                context,
                parent,
                forgedSibling
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                .CurrentOperationBindingMismatch,
            result.State
        );

        Assert.Null(
            result.OwnerInspection
        );

        Assert.Null(
            result.Provenance
        );
    }

    [Fact]
    public void
        Authorize_CurrentManifestShaMismatch_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
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
                currentChildManifestSha256Override:
                    new string(
                        '0',
                        64
                    )
            );

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryReuseAuthorization result =
            DataRelativePathRepairBatchDirectoryReuseAuthorizer.Authorize(
                batch,
                context,
                parent,
                borrowerManifest.Operations[0]
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                .CurrentManifestExpectationMismatch,
            result.State
        );

        Assert.Null(
            result.OwnerInspection
        );

        Assert.Null(
            result.Provenance
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
                    "casecompat-batch-directory-reuse-authorizer-tests",
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
            int index)
        {
            string physicalDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        "alpha"
                    )
                ).FullName;

            string fileName =
                $"Thing{index}.nif";

            File.WriteAllText(
                Path.Combine(
                    physicalDirectory,
                    fileName
                ),
                $"reuse-authorizer-payload-{index}"
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
                $"meshes/Alpha/{fileName}";

            /*
             * This fixture exercises downstream authenticated batch
             * directory-reuse behavior, not standalone planning policy.
             *
             * Shared-directory batch members may be technically
             * projectable even when standalone repair-plan correctly
             * rejects the same path as a sparse namespace split.
             *
             * Persist the exact technical batch candidate directly through
             * the ordinary immutable manifest factory/writer. No repair
             * operation is executed and no production safety bypass is
             * introduced.
             */
            var resolution =
                CaseCompat.Core.Resolution
                    .DataRelativePathResolver
                    .ResolveFile(
                        DataRoot,
                        requestedPath
                    );

            DataRelativePathRepairPlanProjection projection =
                DataRelativePathRepairPlanProjector
                    .ProjectBatchCandidate(
                        resolution
                    );

            Assert.True(
                projection.HasPlan,
                projection.Error
            );

            var creation =
                DataRelativePathRepairPlanManifest
                    .CreateFromResolution(
                        Guid.NewGuid(),
                        DateTimeOffset.UtcNow,
                        resolution,
                        projection.SourceSnapshot!,
                        projection.DestinationParentSnapshot!,
                        projection.Operations
                    );

            Assert.True(
                creation.Success,
                creation.Error
            );

            DataRelativePathRepairPlanManifestRecord
                expectedManifest =
                    Assert.IsType<
                        DataRelativePathRepairPlanManifestRecord
                    >(
                        creation.Manifest
                    );

            using (
                LinuxNoFollowPathHandle childDirectory =
                    OpenRoot(
                        childDirectoryPath
                    ))
            {
                var write =
                    DataRelativePathRepairPlanManifestWriter
                        .CreateInitial(
                            childDirectory,
                            ManifestName,
                            expectedManifest
                        );

                Assert.True(
                    write.Success,
                    write.Error
                );

                DataRelativePathRepairPlanManifestReaderResult
                    verify =
                        DataRelativePathRepairPlanManifestReader
                            .Read(
                                childDirectory,
                                ManifestName
                            );

                Assert.True(
                    verify.Success,
                    verify.Error
                );

                Assert.Equal(
                    expectedManifest.PlanId,
                    verify.Manifest!.PlanId
                );
            }

            return new(
                ChildName:
                    childName,
                ChildDirectoryPath:
                    childDirectoryPath,
                DestinationDirectoryPath:
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        "Alpha"
                    )
            );
        }

        public void ApplyPlan(
            PlanSpec plan)
        {
            const string batchManifestName =
                "batch-manifest.json";

            const string applyAuthorizationName =
                "batch-apply-authorization.json";

            string[] childNames =
                Directory
                    .EnumerateDirectories(
                        BatchRoot,
                        "plan-*",
                        SearchOption.TopDirectoryOnly
                    )
                    .Select(
                        Path.GetFileName
                    )
                    .Where(
                        name =>
                            !string.IsNullOrWhiteSpace(
                                name
                            )
                    )
                    .Select(
                        name =>
                            name!
                    )
                    .OrderBy(
                        name =>
                            name,
                        StringComparer.Ordinal
                    )
                    .ToArray();

            Assert.NotEmpty(
                childNames
            );

            using LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    BatchRoot
                );

            var manifests =
                new List<
                    DataRelativePathRepairPlanManifestRecord>(
                        childNames.Length
                    );

            var children =
                new List<
                    DataRelativePathRepairBatchManifestChild>(
                        childNames.Length
                    );

            foreach (
                string childName
                in childNames)
            {
                LinuxOpenChildDirectoryReadOnlyAtResult childOpen =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        batchDirectory,
                        childName
                    );

                Assert.True(
                    childOpen.Success,
                    childOpen.Error
                );

                using LinuxNoFollowPathHandle childDirectory =
                    Assert.IsType<
                        LinuxNoFollowPathHandle>(
                            childOpen.OpenedDirectory
                        );

                DataRelativePathRepairPlanManifestReaderResult read =
                    DataRelativePathRepairPlanManifestReader.Read(
                        childDirectory,
                        ManifestName
                    );

                Assert.True(
                    read.Success,
                    read.Error
                );

                DataRelativePathRepairPlanManifestRecord manifest =
                    Assert.IsType<
                        DataRelativePathRepairPlanManifestRecord>(
                            read.Manifest
                        );

                string manifestSha256 =
                    Assert.IsType<string>(
                        read.ManifestSha256
                    );

                manifests.Add(
                    manifest
                );

                children.Add(
                    new(
                        ChildName:
                            childName,
                        PlanId:
                            manifest.PlanId,
                        ManifestSha256:
                            manifestSha256
                    )
                );
            }

            DataRelativePathRepairBatchCoverageAuthorization coverage =
                DataRelativePathRepairBatchCoverageAuthorizer
                    .AuthorizePersistedManifests(
                        manifests
                    );

            Assert.True(
                coverage.AllAuthorized
            );

            DataRelativePathRepairBatchManifestCreation batchCreation =
                DataRelativePathRepairBatchManifest
                    .CreateCoverageAuthorized(
                        batchId:
                            Guid.NewGuid(),
                        createdUtc:
                            T0,
                        dataRoot:
                            DataRoot,
                        childManifestName:
                            ManifestName,
                        inputPathCount:
                            children.Count,
                        safeRejectionCount:
                            0,
                        children:
                            children
                    );

            Assert.True(
                batchCreation.Success,
                batchCreation.Error
            );

            DataRelativePathRepairBatchManifestRecord batchManifest =
                Assert.IsType<
                    DataRelativePathRepairBatchManifestRecord>(
                        batchCreation.Manifest
                    );

            DataRelativePathRepairBatchManifestWriterResult batchWrite =
                DataRelativePathRepairBatchManifestWriter
                    .CreateInitial(
                        batchDirectory,
                        batchManifestName,
                        batchManifest
                    );

            Assert.True(
                batchWrite.Success,
                batchWrite.Error
            );

            DataRelativePathRepairBatchManifestReaderResult batchRead =
                DataRelativePathRepairBatchManifestReader.Read(
                    batchDirectory,
                    batchManifestName
                );

            Assert.True(
                batchRead.Success,
                batchRead.Error
            );

            string batchManifestSha256 =
                Assert.IsType<string>(
                    batchRead.ManifestSha256
                );

            DataRelativePathRepairBatchApplyAuthorizationCreation
                authorizationCreation =
                    DataRelativePathRepairBatchApplyAuthorization
                        .CreateForCompletedBatch(
                            batchManifest,
                            batchManifestSha256,
                            T0
                        );

            Assert.True(
                authorizationCreation.Success,
                authorizationCreation.Error
            );

            DataRelativePathRepairBatchApplyAuthorizationWriterResult
                authorizationWrite =
                    DataRelativePathRepairBatchApplyAuthorizationWriter
                        .CreateInitial(
                            batchDirectory,
                            applyAuthorizationName,
                            authorizationCreation.Authorization!
                        );

            Assert.True(
                authorizationWrite.Success,
                authorizationWrite.Error
            );

            int currentChildIndex =
                children.FindIndex(
                    child =>
                        string.Equals(
                            child.ChildName,
                            plan.ChildName,
                            StringComparison.Ordinal
                        )
                );

            Assert.True(
                currentChildIndex >= 0
            );

            DataRelativePathRepairBatchExecutionContextCreation
                contextCreation =
                    DataRelativePathRepairBatchExecutionContext.Create(
                        batchManifest,
                        currentChildIndex,
                        batchManifest.Children[
                            currentChildIndex
                        ]
                    );

            Assert.True(
                contextCreation.Success,
                contextCreation.Error
            );

            DataRelativePathRepairBatchExecutionContext context =
                Assert.IsType<
                    DataRelativePathRepairBatchExecutionContext>(
                        contextCreation.Context
                    );

            LinuxOpenChildDirectoryReadOnlyAtResult ownerOpen =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    batchDirectory,
                    plan.ChildName
                );

            Assert.True(
                ownerOpen.Success,
                ownerOpen.Error
            );

            using LinuxNoFollowPathHandle ownerDirectory =
                Assert.IsType<
                    LinuxNoFollowPathHandle>(
                        ownerOpen.OpenedDirectory
                    );

            DataRelativePathRepairPlanForwardExecution execution =
                DataRelativePathRepairPlanForwardExecutor
                    .ExecuteExpectedBatchManifest(
                        batchDirectory,
                        context,
                        ownerDirectory,
                        DataRoot,
                        T0
                    );

            Assert.True(
                execution.Success,
                execution.Error
            );
        }

        public DataRelativePathRepairPlanManifestRecord ReadManifest(
            PlanSpec plan)
        {
            using LinuxNoFollowPathHandle child =
                OpenRoot(
                    plan.ChildDirectoryPath
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

            return Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                read.Manifest
            );
        }

        public DataRelativePathRepairValidatedDestinationParentLease
            AcquireParentLease(
                DataRelativePathRepairPlanManifestRecord manifest)
        {
            DataRelativePathRepairDestinationParentLeaseAcquisition
                acquisition =
                    DataRelativePathRepairDestinationParentLeaseAcquirer
                        .Acquire(
                            DataRoot,
                            manifest.InitialDestinationParentSnapshot
                        );

            Assert.True(
                acquisition.Success,
                acquisition.Validation.Error
            );

            DataRelativePathRepairValidatedDestinationParentLease lease =
                Assert.IsType<
                    DataRelativePathRepairValidatedDestinationParentLease
                >(
                    acquisition.Lease
                );

            Assert.True(
                lease.ActualIncarnation.Success,
                lease.ActualIncarnation.Error
            );

            Assert.NotNull(
                lease.IncarnationIdentity
            );

            return lease;
        }

        public DataRelativePathRepairBatchExecutionContext BuildContext(
            IReadOnlyList<PlanSpec> plans,
            int currentChildIndex,
            Guid? batchId = null,
            string? currentChildManifestSha256Override = null)
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

                using LinuxNoFollowPathHandle child =
                    OpenRoot(
                        plan.ChildDirectoryPath
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
                    index == currentChildIndex &&
                    currentChildManifestSha256Override is not null
                        ? currentChildManifestSha256Override
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
                    batchId ??
                        Guid.NewGuid(),
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
