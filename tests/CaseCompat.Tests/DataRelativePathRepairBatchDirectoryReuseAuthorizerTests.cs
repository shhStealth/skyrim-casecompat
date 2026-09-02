using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
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

            int result =
                global::RepairPlanCommand.Run(
                    [
                        "repair-plan",
                        DataRoot,
                        $"meshes/Alpha/{fileName}",
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
                        "Alpha"
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
