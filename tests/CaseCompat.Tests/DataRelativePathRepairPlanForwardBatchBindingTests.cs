using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairPlanForwardBatchBindingTests
{
    private const string ManifestName =
        "repair-plan.json";

    private const string BatchManifestName =
        "batch-manifest.json";

    private const string ApplyAuthorizationName =
        "batch-apply-authorization.json";

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
        ExecuteExpectedBatchManifest_WrongChildDescriptor_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string firstName =
            "plan-000001";

        string secondName =
            "plan-000002";

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                firstName
            )
        );

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                secondName
            )
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                temp.RootPath,
                firstName,
                secondName
            );

        DataRelativePathRepairBatchExecutionContext context =
            CreateContext(
                manifest,
                currentChildIndex:
                    0
            );

        using LinuxNoFollowPathHandle batchDirectory =
            OpenRoot(
                temp.RootPath
            );

        using LinuxNoFollowPathHandle wrongJournalDirectory =
            OpenChild(
                batchDirectory,
                secondName
            );

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor
                .ExecuteExpectedBatchManifest(
                    batchDirectory,
                    context,
                    wrongJournalDirectory,
                    temp.RootPath,
                    T0
                );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .BatchChildBindingFailed,
            execution.State
        );

        Assert.Null(
            execution.ManifestRead
        );

        Assert.Empty(
            execution.OperationResults
        );
    }

    [Fact]
    public void
        ExecuteExpectedBatchManifest_CorrectChildDescriptor_PassesBinding()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string firstName =
            "plan-000001";

        string secondName =
            "plan-000002";

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                firstName
            )
        );

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                secondName
            )
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                temp.RootPath,
                firstName,
                secondName
            );

        DataRelativePathRepairBatchExecutionContext context =
            CreateContext(
                manifest,
                currentChildIndex:
                    0
            );

        using LinuxNoFollowPathHandle batchDirectory =
            OpenRoot(
                temp.RootPath
            );

        using LinuxNoFollowPathHandle journalDirectory =
            OpenChild(
                batchDirectory,
                firstName
            );

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor
                .ExecuteExpectedBatchManifest(
                    batchDirectory,
                    context,
                    journalDirectory,
                    temp.RootPath,
                    T0
                );

        /*
         * No plan manifest is present in this focused fixture.
         *
         * ManifestReadFailed therefore proves that exact batch-child
         * descriptor binding succeeded and execution proceeded into
         * the ordinary whole-plan path.
         */
        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .ManifestReadFailed,
            execution.State
        );

        Assert.NotNull(
            execution.ManifestRead
        );

        Assert.Empty(
            execution.OperationResults
        );
    }

    [Fact]
    public void
        ExecuteExpectedBatchManifest_CoverageV2UnstartedLaterChild_SourceCoverageDrift_FailsClosed()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CoverageV2Fixture fixture =
            CoverageV2Fixture.Create();

        /*
         * Publish the exact durable authorization that permits this
         * coverage-v2 batch to cross its first mutation boundary.
         */
        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        {
            DataRelativePathRepairBatchApplyAuthorizationCreation
                authorizationCreation =
                    DataRelativePathRepairBatchApplyAuthorization
                        .CreateForCompletedBatch(
                            fixture.BatchManifest,
                            fixture.BatchManifestSha256,
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
                            ApplyAuthorizationName,
                            authorizationCreation.Authorization!
                        );

            Assert.True(
                authorizationWrite.Success,
                authorizationWrite.Error
            );
        }

        /*
         * Child 1 legitimately starts and reaches durable success.
         */
        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        using (
            LinuxNoFollowPathHandle firstChildDirectory =
                OpenChild(
                    batchDirectory,
                    fixture.Plans[0].ChildName
                ))
        {
            DataRelativePathRepairPlanForwardExecution firstExecution =
                DataRelativePathRepairPlanForwardExecutor
                    .ExecuteExpectedBatchManifest(
                        batchDirectory,
                        fixture.Context,
                        firstChildDirectory,
                        fixture.DataRoot,
                        T0
                    );

            Assert.True(
                firstExecution.Success,
                firstExecution.Error
            );
        }

        Assert.True(
            File.Exists(
                fixture.Plans[0].DestinationPath
            )
        );

        Assert.False(
            File.Exists(
                fixture.Plans[1].DestinationPath
            )
        );

        /*
         * After child 1 has started/completed, the original physical source
         * namespace gains content that is not represented by any durable
         * batch child.
         *
         * The existing batch authorization remains valid provenance for the
         * mutation boundary already crossed. It must NOT silently authorize
         * an unstarted later child against this changed namespace.
         */
        string unplannedSource =
            Path.Combine(
                fixture.DataRoot,
                "meshes",
                "alpha",
                "Unplanned.nif"
            );

        File.WriteAllText(
            unplannedSource,
            "appeared-after-child-1"
        );

        DataRelativePathRepairBatchExecutionContext secondContext =
            CreateContext(
                fixture.BatchManifest,
                currentChildIndex:
                    1
            );

        DataRelativePathRepairPlanForwardExecution secondExecution;

        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        using (
            LinuxNoFollowPathHandle secondChildDirectory =
                OpenChild(
                    batchDirectory,
                    fixture.Plans[1].ChildName
                ))
        {
            secondExecution =
                DataRelativePathRepairPlanForwardExecutor
                    .ExecuteExpectedBatchManifest(
                        batchDirectory,
                        secondContext,
                        secondChildDirectory,
                        fixture.DataRoot,
                        T0
                    );
        }

        /*
         * An unstarted later child must freshly re-prove aggregate physical
         * coverage before its first operation journal can be published.
         *
         * The durable batch authorization proves the mutation boundary that
         * was legitimately crossed earlier; it does not authorize this
         * unstarted child against a subsequently changed source namespace.
         */
        Assert.False(
            secondExecution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .BatchChildBindingFailed,
            secondExecution.State
        );

        Assert.Contains(
            "coverage",
            secondExecution.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase
        );

        Assert.False(
            File.Exists(
                fixture.Plans[1].DestinationPath
            )
        );

        string secondChildPath =
            fixture.Plans[1].ChildDirectoryPath;

        string[] secondChildEntries =
            Directory
                .EnumerateFileSystemEntries(
                    secondChildPath
                )
                .Select(
                    Path.GetFileName
                )
                .Where(name =>
                    name is not null
                )
                .Cast<string>()
                .OrderBy(
                    name =>
                        name,
                    StringComparer.Ordinal
                )
                .ToArray();

        string expectedExecutionLock =
            DataRelativePathRepairPlanExecutionLock
                .CreateLockChildName(
                    fixture.Plans[1].Manifest.PlanId
                );

        /*
         * Entering the locked executor legitimately leaves its persistent
         * per-PlanId execution-lock entry behind.
         *
         * Fresh aggregate coverage must still fail before any operation
         * journal is published, so these are the only two durable child
         * entries permitted here.
         */
        string[] expectedChildEntries =
        [
            expectedExecutionLock,
            ManifestName
        ];

        Assert.Equal(
            expectedChildEntries
                .OrderBy(
                    name =>
                        name,
                    StringComparer.Ordinal
                )
                .ToArray(),
            secondChildEntries
        );
    }

    [Fact]
    public void
        ExecuteExpectedBatchManifest_CoverageV2StartedChild_SourceCoverageDrift_ValidAuthorizationResumes()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CoverageV2Fixture fixture =
            CoverageV2Fixture.Create();

        /*
         * Publish the exact durable batch authorization first. The recovery
         * attempt below must still authenticate this immutable boundary even
         * though the child will already have an operation journal.
         */
        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        {
            DataRelativePathRepairBatchApplyAuthorizationCreation
                authorizationCreation =
                    DataRelativePathRepairBatchApplyAuthorization
                        .CreateForCompletedBatch(
                            fixture.BatchManifest,
                            fixture.BatchManifestSha256,
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
                            ApplyAuthorizationName,
                            authorizationCreation.Authorization!
                        );

            Assert.True(
                authorizationWrite.Success,
                authorizationWrite.Error
            );
        }

        PersistedCoveragePlan firstPlan =
            fixture.Plans[0];

        DataRelativePathRepairPlanManifestOperation firstEntry =
            firstPlan.Manifest.Operations[0];

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateDirectory,
            firstEntry.Operation.Kind
        );

        Guid firstJournalId;
        int firstRevision;

        /*
         * Create a genuine durable operation-0 prefix without running the
         * whole-plan orchestrator. This makes the child a recovery case.
         */
        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        using (
            LinuxNoFollowPathHandle firstChildDirectory =
                OpenChild(
                    batchDirectory,
                    firstPlan.ChildName
                ))
        {
            DataRelativePathRepairDirectoryExecution firstExecution =
                DataRelativePathRepairDirectoryExecutor.Execute(
                    firstChildDirectory,
                    firstEntry.JournalChildName,
                    firstEntry.Operation,
                    firstPlan.Manifest.InitialDestinationParentSnapshot,
                    fixture.DataRoot,
                    T0.AddSeconds(5)
                );

            if (
                firstExecution.ForwardRecovery?.Publication?.State ==
                LinuxPublishOwnedDirectoryAtState.NoReplaceUnsupported)
            {
                return;
            }

            Assert.True(
                firstExecution.Success,
                firstExecution.Error
            );

            DataRelativePathRepairDirectoryJournalReaderResult before =
                DataRelativePathRepairDirectoryJournalReader.Read(
                    firstChildDirectory,
                    firstEntry.JournalChildName
                );

            Assert.True(
                before.Success,
                before.Error
            );

            firstJournalId =
                before.Record!.JournalId;

            firstRevision =
                before.Record.Revision;
        }

        Assert.False(
            File.Exists(
                firstPlan.DestinationPath
            )
        );

        /*
         * Aggregate coverage is no longer true after this point.
         *
         * An unstarted child would have to fail closed here. This child,
         * however, already owns a durable operation prefix, so normal
         * recovery semantics must own namespace interpretation after the
         * exact batch authorization is rebound.
         */
        string unplannedSource =
            Path.Combine(
                Path.GetDirectoryName(
                    firstPlan.Manifest.SourceSnapshot.PhysicalPath
                )!,
                "appeared-after-start.nif"
            );

        File.WriteAllText(
            unplannedSource,
            "post-start-coverage-drift"
        );

        DataRelativePathRepairPlanForwardExecution execution;

        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        using (
            LinuxNoFollowPathHandle firstChildDirectory =
                OpenChild(
                    batchDirectory,
                    firstPlan.ChildName
                ))
        {
            execution =
                DataRelativePathRepairPlanForwardExecutor
                    .ExecuteExpectedBatchManifest(
                        batchDirectory,
                        fixture.Context,
                        firstChildDirectory,
                        fixture.DataRoot,
                        T0.AddSeconds(10)
                    );
        }

        Assert.True(
            execution.Success,
            execution.Error
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .AppliedDurably,
            execution.State
        );

        Assert.True(
            File.Exists(
                firstPlan.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                unplannedSource
            )
        );

        /*
         * Recovery must not rewrite the already-Applied first operation.
         */
        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        using (
            LinuxNoFollowPathHandle firstChildDirectory =
                OpenChild(
                    batchDirectory,
                    firstPlan.ChildName
                ))
        {
            DataRelativePathRepairDirectoryJournalReaderResult after =
                DataRelativePathRepairDirectoryJournalReader.Read(
                    firstChildDirectory,
                    firstEntry.JournalChildName
                );

            Assert.True(
                after.Success,
                after.Error
            );

            Assert.Equal(
                firstJournalId,
                after.Record!.JournalId
            );

            Assert.Equal(
                firstRevision,
                after.Record.Revision
            );
        }
    }

    [Fact]
    public void
        ExecuteExpectedBatchManifest_CoverageV2StartedChild_MissingApplyAuthorization_FailsClosed()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CoverageV2Fixture fixture =
            CoverageV2Fixture.Create();

        PersistedCoveragePlan firstPlan =
            fixture.Plans[0];

        DataRelativePathRepairPlanManifestOperation firstEntry =
            firstPlan.Manifest.Operations[0];

        /*
         * Deliberately start the child without publishing batch authority.
         * The existing operation journal is recovery authority only after
         * the immutable coverage-v2 batch boundary is authenticated.
         */
        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        using (
            LinuxNoFollowPathHandle firstChildDirectory =
                OpenChild(
                    batchDirectory,
                    firstPlan.ChildName
                ))
        {
            DataRelativePathRepairDirectoryExecution firstExecution =
                DataRelativePathRepairDirectoryExecutor.Execute(
                    firstChildDirectory,
                    firstEntry.JournalChildName,
                    firstEntry.Operation,
                    firstPlan.Manifest.InitialDestinationParentSnapshot,
                    fixture.DataRoot,
                    T0.AddSeconds(5)
                );

            if (
                firstExecution.ForwardRecovery?.Publication?.State ==
                LinuxPublishOwnedDirectoryAtState.NoReplaceUnsupported)
            {
                return;
            }

            Assert.True(
                firstExecution.Success,
                firstExecution.Error
            );
        }

        Assert.False(
            File.Exists(
                Path.Combine(
                    fixture.BatchRoot,
                    ApplyAuthorizationName
                )
            )
        );

        Assert.False(
            File.Exists(
                firstPlan.DestinationPath
            )
        );

        DataRelativePathRepairPlanForwardExecution execution;

        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        using (
            LinuxNoFollowPathHandle firstChildDirectory =
                OpenChild(
                    batchDirectory,
                    firstPlan.ChildName
                ))
        {
            execution =
                DataRelativePathRepairPlanForwardExecutor
                    .ExecuteExpectedBatchManifest(
                        batchDirectory,
                        fixture.Context,
                        firstChildDirectory,
                        fixture.DataRoot,
                        T0.AddSeconds(10)
                    );
        }

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .BatchChildBindingFailed,
            execution.State
        );

        Assert.NotNull(
            execution.ManifestRead
        );

        Assert.Empty(
            execution.OperationResults
        );

        Assert.False(
            File.Exists(
                firstPlan.DestinationPath
            )
        );
    }

    [Fact]
    public void
        ExecuteExpectedBatchManifest_CoverageV2StartedChild_WrongApplyAuthorizationBinding_FailsClosed()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CoverageV2Fixture fixture =
            CoverageV2Fixture.Create();

        PersistedCoveragePlan firstPlan =
            fixture.Plans[0];

        DataRelativePathRepairPlanManifestOperation firstEntry =
            firstPlan.Manifest.Operations[0];

        /*
         * Establish genuine durable child progress first.
         */
        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        using (
            LinuxNoFollowPathHandle firstChildDirectory =
                OpenChild(
                    batchDirectory,
                    firstPlan.ChildName
                ))
        {
            DataRelativePathRepairDirectoryExecution firstExecution =
                DataRelativePathRepairDirectoryExecutor.Execute(
                    firstChildDirectory,
                    firstEntry.JournalChildName,
                    firstEntry.Operation,
                    firstPlan.Manifest.InitialDestinationParentSnapshot,
                    fixture.DataRoot,
                    T0.AddSeconds(5)
                );

            if (
                firstExecution.ForwardRecovery?.Publication?.State ==
                LinuxPublishOwnedDirectoryAtState.NoReplaceUnsupported)
            {
                return;
            }

            Assert.True(
                firstExecution.Success,
                firstExecution.Error
            );
        }

        /*
         * Publish a structurally valid authorization whose batch-manifest
         * SHA binding is deliberately wrong.
         */
        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        {
            string wrongBatchManifestSha256 =
                fixture.BatchManifestSha256[0] == '0'
                    ? $"1{fixture.BatchManifestSha256[1..]}"
                    : $"0{fixture.BatchManifestSha256[1..]}";

            DataRelativePathRepairBatchApplyAuthorizationCreation
                authorizationCreation =
                    DataRelativePathRepairBatchApplyAuthorization
                        .CreateForCompletedBatch(
                            fixture.BatchManifest,
                            wrongBatchManifestSha256,
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
                            ApplyAuthorizationName,
                            authorizationCreation.Authorization!
                        );

            Assert.True(
                authorizationWrite.Success,
                authorizationWrite.Error
            );
        }

        Assert.False(
            File.Exists(
                firstPlan.DestinationPath
            )
        );

        DataRelativePathRepairPlanForwardExecution execution;

        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        using (
            LinuxNoFollowPathHandle firstChildDirectory =
                OpenChild(
                    batchDirectory,
                    firstPlan.ChildName
                ))
        {
            execution =
                DataRelativePathRepairPlanForwardExecutor
                    .ExecuteExpectedBatchManifest(
                        batchDirectory,
                        fixture.Context,
                        firstChildDirectory,
                        fixture.DataRoot,
                        T0.AddSeconds(10)
                    );
        }

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .BatchChildBindingFailed,
            execution.State
        );

        Assert.NotNull(
            execution.ManifestRead
        );

        Assert.Empty(
            execution.OperationResults
        );

        Assert.False(
            File.Exists(
                firstPlan.DestinationPath
            )
        );
    }

    [Fact]
    public void
        ExecuteExpectedBatchManifest_CoverageV2MissingApplyAuthorization_FailsClosed()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CoverageV2Fixture fixture =
            CoverageV2Fixture.Create();

        Assert.False(
            File.Exists(
                Path.Combine(
                    fixture.BatchRoot,
                    ApplyAuthorizationName
                )
            )
        );

        using LinuxNoFollowPathHandle batchDirectory =
            OpenRoot(
                fixture.BatchRoot
            );

        using LinuxNoFollowPathHandle childDirectory =
            OpenChild(
                batchDirectory,
                fixture.Plans[0].ChildName
            );

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor
                .ExecuteExpectedBatchManifest(
                    batchDirectory,
                    fixture.Context,
                    childDirectory,
                    fixture.DataRoot,
                    T0
                );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .BatchChildBindingFailed,
            execution.State
        );

        Assert.NotNull(
            execution.ManifestRead
        );

        Assert.Empty(
            execution.OperationResults
        );

        Assert.False(
            File.Exists(
                fixture.Plans[0].DestinationPath
            )
        );

        Assert.False(
            File.Exists(
                fixture.Plans[1].DestinationPath
            )
        );
    }

    [Fact]
    public void
        ExecuteExpectedBatchManifest_CoverageV2WrongApplyAuthorizationBinding_FailsClosed()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CoverageV2Fixture fixture =
            CoverageV2Fixture.Create();

        using (
            LinuxNoFollowPathHandle batchDirectory =
                OpenRoot(
                    fixture.BatchRoot
                ))
        {
            DataRelativePathRepairBatchApplyAuthorizationCreation
                authorizationCreation =
                    DataRelativePathRepairBatchApplyAuthorization
                        .CreateForCompletedBatch(
                            fixture.BatchManifest,
                            new string(
                                'A',
                                64
                            ),
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
                            ApplyAuthorizationName,
                            authorizationCreation.Authorization!
                        );

            Assert.True(
                authorizationWrite.Success,
                authorizationWrite.Error
            );
        }

        using LinuxNoFollowPathHandle retainedBatchDirectory =
            OpenRoot(
                fixture.BatchRoot
            );

        using LinuxNoFollowPathHandle childDirectory =
            OpenChild(
                retainedBatchDirectory,
                fixture.Plans[0].ChildName
            );

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor
                .ExecuteExpectedBatchManifest(
                    retainedBatchDirectory,
                    fixture.Context,
                    childDirectory,
                    fixture.DataRoot,
                    T0
                );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .BatchChildBindingFailed,
            execution.State
        );

        Assert.NotNull(
            execution.ManifestRead
        );

        Assert.Empty(
            execution.OperationResults
        );

        Assert.False(
            File.Exists(
                fixture.Plans[0].DestinationPath
            )
        );

        Assert.False(
            File.Exists(
                fixture.Plans[1].DestinationPath
            )
        );
    }

    private sealed record PersistedCoveragePlan(
        string ChildName,
        string ChildDirectoryPath,
        string DestinationPath,
        DataRelativePathRepairPlanManifestRecord Manifest,
        string ManifestSha256
    );

    private sealed class CoverageV2Fixture
        : IDisposable
    {
        private CoverageV2Fixture(
            TemporaryDirectory temp,
            string dataRoot,
            string batchRoot,
            PersistedCoveragePlan[] plans,
            DataRelativePathRepairBatchManifestRecord batchManifest,
            string batchManifestSha256,
            DataRelativePathRepairBatchExecutionContext context)
        {
            Temp =
                temp;

            DataRoot =
                dataRoot;

            BatchRoot =
                batchRoot;

            Plans =
                plans;

            BatchManifest =
                batchManifest;

            BatchManifestSha256 =
                batchManifestSha256;

            Context =
                context;
        }

        public TemporaryDirectory Temp { get; }

        public string DataRoot { get; }

        public string BatchRoot { get; }

        public IReadOnlyList<PersistedCoveragePlan> Plans { get; }

        public DataRelativePathRepairBatchManifestRecord
            BatchManifest { get; }

        public string BatchManifestSha256 { get; }

        public DataRelativePathRepairBatchExecutionContext
            Context { get; }

        public static CoverageV2Fixture Create()
        {
            var temp =
                new TemporaryDirectory();

            try
            {
                string dataRoot =
                    Directory.CreateDirectory(
                        Path.Combine(
                            temp.RootPath,
                            "Data"
                        )
                    ).FullName;

                string batchRoot =
                    Directory.CreateDirectory(
                        Path.Combine(
                            temp.RootPath,
                            "batch"
                        )
                    ).FullName;

                string physicalDirectory =
                    Directory.CreateDirectory(
                        Path.Combine(
                            dataRoot,
                            "meshes",
                            "alpha"
                        )
                    ).FullName;

                /*
                 * Two physical leaves make either child independently unsafe
                 * under the standalone sparse-branch rule.
                 *
                 * Together, however, they form complete aggregate coverage
                 * for the schema-v2 batch.
                 */
                PersistedCoveragePlan first =
                    CreatePlan(
                        dataRoot,
                        batchRoot,
                        physicalDirectory,
                        index:
                            1
                    );

                PersistedCoveragePlan second =
                    CreatePlan(
                        dataRoot,
                        batchRoot,
                        physicalDirectory,
                        index:
                            2
                    );

                PersistedCoveragePlan[] plans =
                [
                    first,
                    second
                ];

                DataRelativePathRepairBatchCoverageAuthorization coverage =
                    DataRelativePathRepairBatchCoverageAuthorizer
                        .AuthorizePersistedManifests(
                            plans
                                .Select(plan =>
                                    plan.Manifest
                                )
                                .ToArray()
                        );

                Assert.True(
                    coverage.AllAuthorized
                );

                DataRelativePathRepairBatchManifestCreation
                    batchCreation =
                        DataRelativePathRepairBatchManifest
                            .CreateCoverageAuthorized(
                                batchId:
                                    Guid.NewGuid(),
                                createdUtc:
                                    T0,
                                dataRoot:
                                    dataRoot,
                                childManifestName:
                                    ManifestName,
                                inputPathCount:
                                    plans.Length,
                                safeRejectionCount:
                                    0,
                                children:
                                    plans
                                        .Select(plan =>
                                            new DataRelativePathRepairBatchManifestChild(
                                                ChildName:
                                                    plan.ChildName,
                                                PlanId:
                                                    plan.Manifest.PlanId,
                                                ManifestSha256:
                                                    plan.ManifestSha256
                                            )
                                        )
                                        .ToArray()
                            );

                Assert.True(
                    batchCreation.Success,
                    batchCreation.Error
                );

                DataRelativePathRepairBatchManifestRecord batchManifest =
                    Assert.IsType<
                        DataRelativePathRepairBatchManifestRecord
                    >(
                        batchCreation.Manifest
                    );

                string batchManifestSha256;

                using (
                    LinuxNoFollowPathHandle batchDirectory =
                        OpenRoot(
                            batchRoot
                        ))
                {
                    DataRelativePathRepairBatchManifestWriterResult write =
                        DataRelativePathRepairBatchManifestWriter
                            .CreateInitial(
                                batchDirectory,
                                BatchManifestName,
                                batchManifest
                            );

                    Assert.True(
                        write.Success,
                        write.Error
                    );

                    DataRelativePathRepairBatchManifestReaderResult read =
                        DataRelativePathRepairBatchManifestReader.Read(
                            batchDirectory,
                            BatchManifestName
                        );

                    Assert.True(
                        read.Success,
                        read.Error
                    );

                    batchManifestSha256 =
                        Assert.IsType<string>(
                            read.ManifestSha256
                        );
                }

                DataRelativePathRepairBatchExecutionContext context =
                    CreateContext(
                        batchManifest,
                        currentChildIndex:
                            0
                    );

                return new(
                    temp,
                    dataRoot,
                    batchRoot,
                    plans,
                    batchManifest,
                    batchManifestSha256,
                    context
                );
            }
            catch
            {
                temp.Dispose();
                throw;
            }
        }

        private static PersistedCoveragePlan CreatePlan(
            string dataRoot,
            string batchRoot,
            string physicalDirectory,
            int index)
        {
            string fileName =
                $"Thing{index}.nif";

            string sourcePath =
                Path.Combine(
                    physicalDirectory,
                    fileName
                );

            File.WriteAllText(
                sourcePath,
                $"coverage-v2-{index}"
            );

            string requestedPath =
                $"meshes/Alpha/{fileName}";

            string destinationPath =
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Alpha",
                    fileName
                );

            var resolution =
                CaseCompat.Core.Resolution
                    .DataRelativePathResolver
                    .ResolveFile(
                        dataRoot,
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

            DataRelativePathRepairPlanManifestCreation creation =
                DataRelativePathRepairPlanManifest
                    .CreateFromResolution(
                        Guid.NewGuid(),
                        T0,
                        resolution,
                        projection.SourceSnapshot!,
                        projection.DestinationParentSnapshot!,
                        projection.Operations
                    );

            Assert.True(
                creation.Success,
                creation.Error
            );

            DataRelativePathRepairPlanManifestRecord manifest =
                Assert.IsType<
                    DataRelativePathRepairPlanManifestRecord
                >(
                    creation.Manifest
                );

            string childName =
                $"plan-{index:D6}";

            string childDirectoryPath =
                Directory.CreateDirectory(
                    Path.Combine(
                        batchRoot,
                        childName
                    )
                ).FullName;

            string manifestSha256;

            using (
                LinuxNoFollowPathHandle childDirectory =
                    OpenRoot(
                        childDirectoryPath
                    ))
            {
                DataRelativePathRepairPlanManifestWriterResult write =
                    DataRelativePathRepairPlanManifestWriter
                        .CreateInitial(
                            childDirectory,
                            ManifestName,
                            manifest
                        );

                Assert.True(
                    write.Success,
                    write.Error
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

                manifestSha256 =
                    Assert.IsType<string>(
                        read.ManifestSha256
                    );
            }

            return new(
                ChildName:
                    childName,
                ChildDirectoryPath:
                    childDirectoryPath,
                DestinationPath:
                    destinationPath,
                Manifest:
                    manifest,
                ManifestSha256:
                    manifestSha256
            );
        }

        public void Dispose()
        {
            Temp.Dispose();
        }
    }

    private static DataRelativePathRepairBatchManifestRecord
        CreateManifest(
            string dataRoot,
            string firstName,
            string secondName)
    {
        DataRelativePathRepairBatchManifestCreation creation =
            DataRelativePathRepairBatchManifest.Create(
                Guid.NewGuid(),
                T0,
                dataRoot,
                ManifestName,
                inputPathCount:
                    2,
                safeRejectionCount:
                    0,
                [
                    new(
                        ChildName:
                            firstName,
                        PlanId:
                            Guid.NewGuid(),
                        ManifestSha256:
                            new string(
                                '1',
                                64
                            )
                    ),
                    new(
                        ChildName:
                            secondName,
                        PlanId:
                            Guid.NewGuid(),
                        ManifestSha256:
                            new string(
                                '2',
                                64
                            )
                    )
                ]
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        return Assert.IsType<
            DataRelativePathRepairBatchManifestRecord
        >(
            creation.Manifest
        );
    }

    private static DataRelativePathRepairBatchExecutionContext
        CreateContext(
            DataRelativePathRepairBatchManifestRecord manifest,
            int currentChildIndex)
    {
        DataRelativePathRepairBatchExecutionContextCreation creation =
            DataRelativePathRepairBatchExecutionContext.Create(
                manifest,
                currentChildIndex,
                manifest.Children[
                    currentChildIndex
                ]
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        return Assert.IsType<
            DataRelativePathRepairBatchExecutionContext
        >(
            creation.Context
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
            result.Success
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
            result.OpenedPath
        );
    }

    private static LinuxNoFollowPathHandle OpenChild(
        LinuxNoFollowPathHandle parent,
        string childName)
    {
        LinuxOpenChildDirectoryReadOnlyAtResult result =
            LinuxOpenChildDirectoryReadOnlyAt.Open(
                parent,
                childName
            );

        Assert.True(
            result.Success,
            result.Error
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
            result.OpenedDirectory
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
                    "casecompat-batch-child-binding-tests",
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
