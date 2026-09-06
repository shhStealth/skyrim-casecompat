using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairApplyAggregateNamespaceBatchCommandTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

    private const string PlanManifestName =
        "repair-plan.json";

    private const string ApplyAuthorizationName =
        "batch-apply-authorization.json";

    private static readonly DateTimeOffset SidecarT0 =
        new(
            2026,
            9,
            6,
            20,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void
        Run_RequiresBatchSidecarAndDataArguments()
    {
        int exitCode =
            global::RepairApplyAggregateNamespaceBatchCommand.Run(
                [
                    "repair-apply-aggregate-namespace-batch"
                ]
            );

        Assert.Equal(
            2,
            exitCode
        );
    }

    [Fact]
    public void
        Run_RejectsInvalidExplicitManifestChildNameBeforeFilesystemOpen()
    {
        int exitCode =
            global::RepairApplyAggregateNamespaceBatchCommand.Run(
                [
                    "repair-apply-aggregate-namespace-batch",
                    "/does/not/need/to/exist/batch",
                    "/does/not/need/to/exist/sidecar.json",
                    "../repair-plan.json",
                    "/does/not/need/to/exist/Data"
                ]
            );

        Assert.Equal(
            2,
            exitCode
        );
    }

    [Fact]
    public void
        Source_RequiresExactV4Policy3BeforeZeroChildSuccess()
    {
        string sourcePath =
            Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "src",
                    "CaseCompat.Cli",
                    "RepairApplyAggregateNamespaceBatchCommand.cs"
                )
            );

        string source =
            File.ReadAllText(
                sourcePath
            );

        int policyGate =
            source.IndexOf(
                "bool requiresAggregateApplyAuthority",
                StringComparison.Ordinal
            );

        int zeroChildSuccess =
            source.IndexOf(
                "manifest.Children.Count == 0",
                StringComparison.Ordinal
            );

        Assert.True(
            policyGate >= 0
        );

        Assert.True(
            zeroChildSuccess >= 0
        );

        Assert.True(
            policyGate < zeroChildSuccess,
            "The exact schema-v4 / policy-v3 public-command gate must " +
            "precede the zero-child success path."
        );
    }

    [Fact]
    public void
        Run_RejectsInvalidSidecarPathBeforeFilesystemOpen()
    {
        int exitCode =
            global::RepairApplyAggregateNamespaceBatchCommand.Run(
                [
                    "repair-apply-aggregate-namespace-batch",
                    "/does/not/need/to/exist/batch",
                    "\0",
                    "/does/not/need/to/exist/Data"
                ]
            );

        Assert.Equal(
            2,
            exitCode
        );
    }

    [Fact]
    public void
        Run_FreshExactV4Policy3MultiChild_PublishesAuthorityAppliesAllAndKeepsSidecarInvocationOnly()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2
            );

        if (!fixture.Supported)
        {
            return;
        }

        Assert.False(
            fixture.ApplyAuthorizationExists()
        );

        fixture.AssertNoOperationJournals(
            childIndex:
                0
        );

        fixture.AssertNoOperationJournals(
            childIndex:
                1
        );

        int result =
            fixture.RunApply();

        Assert.Equal(
            0,
            result
        );

        Assert.True(
            fixture.ApplyAuthorizationExists()
        );

        fixture.AssertAuthorizationExactBatchBound();

        for (
            int index = 0;
            index < 2;
            index++)
        {
            Assert.True(
                fixture.AnyOperationJournalExists(
                    index
                )
            );

            Assert.True(
                File.Exists(
                    fixture.DestinationPaths[index]
                )
            );

            Assert.Equal(
                fixture.SourceContents[index],
                File.ReadAllText(
                    fixture.DestinationPaths[index]
                )
            );
        }

        fixture.AssertSidecarInvocationOnly();
    }

    [Fact]
    public void
        Run_SidecarShaMismatch_FailsBeforeDurableAuthorityOrJournal()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!fixture.Supported)
        {
            return;
        }

        byte[] originalSidecar =
            File.ReadAllBytes(
                fixture.SidecarPath
            );

        fixture.RewriteSidecar(
            SidecarT0.AddSeconds(
                1
            )
        );

        Assert.False(
            originalSidecar.AsSpan().SequenceEqual(
                File.ReadAllBytes(
                    fixture.SidecarPath
                )
            )
        );

        int result =
            fixture.RunApply();

        Assert.NotEqual(
            0,
            result
        );

        Assert.False(
            fixture.ApplyAuthorizationExists()
        );

        fixture.AssertNoOperationJournals(
            childIndex:
                0
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPaths[0]
            )
        );

        fixture.AssertSidecarInvocationOnly();
    }

    [Fact]
    public void
        Run_SourceReplacementBeforeInitialAuthorization_FailsBeforeDurableAuthorityOrJournal()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!fixture.Supported)
        {
            return;
        }

        fixture.ReplaceSourceIdentity(
            childIndex:
                0
        );

        int result =
            fixture.RunApply();

        Assert.NotEqual(
            0,
            result
        );

        Assert.False(
            fixture.ApplyAuthorizationExists()
        );

        fixture.AssertNoOperationJournals(
            childIndex:
                0
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPaths[0]
            )
        );

        fixture.AssertSidecarInvocationOnly();
    }

    [Fact]
    public void
        Run_ExistingDurableAuthorityStartedChild_ReusesJournalWithoutWholeBatchCurrentC4A()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!fixture.Supported)
        {
            return;
        }

        Assert.Equal(
            0,
            fixture.RunApply()
        );

        Assert.True(
            fixture.AnyOperationJournalExists(
                childIndex:
                    0
            )
        );

        byte[] authorizationBefore =
            File.ReadAllBytes(
                fixture.ApplyAuthorizationPath
            );

        /*
         * The completed first child has legitimately changed the current
         * namespace. Whole-batch pre-apply C4A therefore no longer describes
         * current state and must not be demanded merely to reuse existing
         * durable authority for journal-owned recovery/idempotence.
         */
        Assert.False(
            fixture.AuthorizeWholeBatchCurrent()
                .AllAuthorized
        );

        bool publicationHookRan =
            false;

        int second =
            fixture.RunApply(
                (
                    _,
                    _
                ) =>
                {
                    publicationHookRan =
                        true;
                }
            );

        Assert.Equal(
            0,
            second
        );

        Assert.False(
            publicationHookRan
        );

        Assert.Equal(
            authorizationBefore,
            File.ReadAllBytes(
                fixture.ApplyAuthorizationPath
            )
        );

        Assert.Equal(
            fixture.SourceContents[0],
            File.ReadAllText(
                fixture.DestinationPaths[0]
            )
        );

        fixture.AssertAuthorizationExactBatchBound();
        fixture.AssertSidecarInvocationOnly();
    }

    [Fact]
    public void
        Run_LaterUnstartedChildSourceReplacement_RejectsRealCandidateBeforeFirstJournal()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2
            );

        if (!fixture.Supported)
        {
            return;
        }

        /*
         * Establish exactly the authority the public command would establish,
         * then cross only child 0's first journal boundary. This leaves child
         * 1 genuinely unstarted without introducing a production test hook.
         */
        fixture.EstablishDurableAuthorization();

        DataRelativePathRepairPlanForwardExecution first =
            fixture.ExecuteAggregateChild(
                childIndex:
                    0
            );

        Assert.True(
            first.Success,
            first.Error
        );

        Assert.True(
            fixture.AnyOperationJournalExists(
                childIndex:
                    0
            )
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPaths[0]
            )
        );

        fixture.AssertNoOperationJournals(
            childIndex:
                1
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPaths[1]
            )
        );

        fixture.ReplaceSourceIdentity(
            childIndex:
                1
        );

        bool publicationHookRan =
            false;

        (int ExitCode, string StandardError) captured =
            CaptureStandardError(
                () =>
                    fixture.RunApply(
                        (
                            _,
                            _
                        ) =>
                        {
                            publicationHookRan =
                                true;
                        }
                    )
            );

        Assert.NotEqual(
            0,
            captured.ExitCode
        );

        Assert.False(
            publicationHookRan
        );

        Assert.Contains(
            "candidate 1 (SourceGenerationBindingFailed)",
            captured.StandardError,
            StringComparison.Ordinal
        );

        fixture.AssertNoOperationJournals(
            childIndex:
                1
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPaths[1]
            )
        );

        Assert.True(
            fixture.ApplyAuthorizationExists()
        );

        fixture.AssertAuthorizationExactBatchBound();
        fixture.AssertSidecarInvocationOnly();
    }

    private static (int ExitCode, string StandardError)
        CaptureStandardError(
            Func<int> action)
    {
        TextWriter original =
            Console.Error;

        using var writer =
            new StringWriter();

        try
        {
            Console.SetError(
                writer
            );

            int result =
                action();

            return (
                result,
                writer.ToString()
            );
        }
        finally
        {
            Console.SetError(
                original
            );
        }
    }

    private sealed class Fixture
        : IDisposable
    {
        private const string RequestedPrefix =
            "meshes/Actors/Character/Character Assets/FaceParts/";

        private Fixture(
            int fileCount)
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-c4c-aggregate-apply-command-tests",
                    Guid.NewGuid().ToString("N")
                );

            _ =
                Directory.CreateDirectory(
                    RootPath
                );

            DataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Data"
                    )
                ).FullName;

            string meshes =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes"
                    )
                ).FullName;

            _ =
                Directory.CreateDirectory(
                    Path.Combine(
                        meshes,
                        "Actors",
                        "Character",
                        "Character Assets"
                    )
                );

            AlternateFaceParts =
                Directory.CreateDirectory(
                    Path.Combine(
                        meshes,
                        "actors",
                        "character",
                        "character assets",
                        "faceparts"
                    )
                ).FullName;

            DestinationFaceParts =
                Path.Combine(
                    meshes,
                    "Actors",
                    "Character",
                    "Character Assets",
                    "FaceParts"
                );

            EvidenceDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Evidence"
                    )
                ).FullName;

            BatchDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Batch"
                    )
                ).FullName;

            SidecarPath =
                Path.Combine(
                    EvidenceDirectory,
                    global::AggregateNamespaceManifestCommand
                        .DefaultManifestName
                );

            PathList =
                Path.Combine(
                    RootPath,
                    "paths.txt"
                );

            ApplyAuthorizationPath =
                Path.Combine(
                    BatchDirectory,
                    ApplyAuthorizationName
                );

            SourcePaths =
                new string[fileCount];

            SourceContents =
                new string[fileCount];

            DestinationPaths =
                new string[fileCount];

            RequestedPaths =
                new string[fileCount];

            for (
                int index = 0;
                index < fileCount;
                index++)
            {
                string fileName =
                    index == 0
                        ? "MaleHeadbrows.tri"
                        : $"Fixture{index}.tri";

                SourcePaths[index] =
                    Path.Combine(
                        AlternateFaceParts,
                        fileName
                    );

                SourceContents[index] =
                    $"c4c-command-lifecycle-{index}";

                DestinationPaths[index] =
                    Path.Combine(
                        DestinationFaceParts,
                        fileName
                    );

                RequestedPaths[index] =
                    RequestedPrefix +
                    fileName;
            }

            Supported =
                HasDistinctActorsRoots(
                    meshes
                );

            if (!Supported)
            {
                return;
            }

            for (
                int index = 0;
                index < fileCount;
                index++)
            {
                File.WriteAllText(
                    SourcePaths[index],
                    SourceContents[index]
                );
            }

            File.WriteAllText(
                PathList,
                string.Join(
                    Environment.NewLine,
                    RequestedPaths
                ) +
                Environment.NewLine
            );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string AlternateFaceParts { get; }

        public string DestinationFaceParts { get; }

        public string EvidenceDirectory { get; }

        public string BatchDirectory { get; }

        public string SidecarPath { get; }

        public string PathList { get; }

        public string ApplyAuthorizationPath { get; }

        public string[] SourcePaths { get; }

        public string[] SourceContents { get; }

        public string[] DestinationPaths { get; }

        public string[] RequestedPaths { get; }

        public bool Supported { get; }

        public DataRelativePathRepairBatchManifestRecord Batch
        {
            get;
            private set;
        } = null!;

        public DataRelativePathRepairPlanManifestRecord[] Children
        {
            get;
            private set;
        } = [];

        public string BatchManifestSha256
        {
            get;
            private set;
        } = string.Empty;

        public static Fixture Create(
            int fileCount = 1)
        {
            var fixture =
                new Fixture(
                    fileCount
                );

            if (fixture.Supported)
            {
                fixture.PrepareArtifacts();
            }

            return fixture;
        }

        public int RunApply(
            Action<
                LinuxNoFollowPathHandle,
                DataRelativePathRepairBatchApplyAuthorizationRecord>?
                    beforeAuthorizationPublish = null)
        {
            string[] args =
                [
                    "repair-apply-aggregate-namespace-batch",
                    BatchDirectory,
                    SidecarPath,
                    DataRoot
                ];

            return beforeAuthorizationPublish is null
                ? global::RepairApplyAggregateNamespaceBatchCommand.Run(
                    args
                )
                : global::RepairApplyAggregateNamespaceBatchCommand.Run(
                    args,
                    beforeAuthorizationPublish
                );
        }

        public bool ApplyAuthorizationExists()
        {
            return File.Exists(
                ApplyAuthorizationPath
            );
        }

        public void RewriteSidecar(
            DateTimeOffset createdUtc)
        {
            byte[] before =
                File.ReadAllBytes(
                    SidecarPath
                );

            File.Delete(
                SidecarPath
            );

            int result =
                global::AggregateNamespaceManifestCommand.Run(
                    [
                        "aggregate-namespace-manifest",
                        DataRoot,
                        "Meshes",
                        EvidenceDirectory
                    ],
                    createdUtcOverride:
                        createdUtc,
                    afterManifestPublishBeforeRead:
                        null
                );

            Assert.Equal(
                0,
                result
            );

            Assert.True(
                File.Exists(
                    SidecarPath
                )
            );

            Assert.False(
                before.AsSpan().SequenceEqual(
                    File.ReadAllBytes(
                        SidecarPath
                    )
                )
            );
        }

        public void ReplaceSourceIdentity(
            int childIndex)
        {
            string backup =
                Path.Combine(
                    RootPath,
                    $"source-{childIndex:D2}.old"
                );

            File.Move(
                SourcePaths[childIndex],
                backup
            );

            File.WriteAllText(
                SourcePaths[childIndex],
                SourceContents[childIndex]
            );
        }

        public DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
            AuthorizeWholeBatchCurrent()
        {
            using LinuxNoFollowPathHandle dataRoot =
                OpenDirectory(
                    DataRoot
                );

            DataRelativePathAggregateNamespaceManifestReaderResult
                evidence =
                    ReadEvidence();

            return
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
                    .Authorize(
                        dataRoot,
                        Batch,
                        Children,
                        evidence
                    );
        }

        public void EstablishDurableAuthorization()
        {
            Assert.False(
                ApplyAuthorizationExists()
            );

            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
                current =
                    AuthorizeWholeBatchCurrent();

            Assert.True(
                current.AllAuthorized,
                current.Error
            );

            DataRelativePathRepairBatchApplyAuthorizationCreation
                creation =
                    DataRelativePathRepairBatchApplyAuthorization
                        .CreateForCompletedBatch(
                            Batch,
                            BatchManifestSha256,
                            DateTimeOffset.UtcNow
                        );

            Assert.True(
                creation.Success,
                creation.Error
            );

            using LinuxNoFollowPathHandle batchDirectory =
                OpenDirectory(
                    BatchDirectory
                );

            DataRelativePathRepairBatchApplyAuthorizationWriterResult
                write =
                    DataRelativePathRepairBatchApplyAuthorizationWriter
                        .CreateInitial(
                            batchDirectory,
                            ApplyAuthorizationName,
                            creation.Authorization!
                        );

            Assert.True(
                write.Success,
                write.Error
            );

            AssertAuthorizationExactBatchBound();
        }

        public DataRelativePathRepairPlanForwardExecution
            ExecuteAggregateChild(
                int childIndex)
        {
            using LinuxNoFollowPathHandle batchDirectory =
                OpenDirectory(
                    BatchDirectory
                );

            DataRelativePathRepairBatchExecutionContextCreation
                contextCreation =
                    DataRelativePathRepairBatchExecutionContext.Create(
                        Batch,
                        childIndex,
                        Batch.Children[childIndex]
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

            LinuxOpenChildDirectoryReadOnlyAtResult childOpen =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    batchDirectory,
                    Batch.Children[childIndex].ChildName
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

            return
                DataRelativePathRepairPlanForwardExecutor
                    .ExecuteExpectedAggregateNamespaceBatchManifest(
                        batchDirectory,
                        context,
                        childDirectory,
                        DataRoot,
                        ReadEvidence(),
                        DateTimeOffset.UtcNow
                    );
        }

        public bool AnyOperationJournalExists(
            int childIndex)
        {
            string childDirectory =
                ChildDirectoryPath(
                    childIndex
                );

            return Children[childIndex]
                .Operations
                .Any(
                    entry =>
                        File.Exists(
                            Path.Combine(
                                childDirectory,
                                entry.JournalChildName
                            )
                        )
                );
        }

        public void AssertNoOperationJournals(
            int childIndex)
        {
            string childDirectory =
                ChildDirectoryPath(
                    childIndex
                );

            foreach (
                DataRelativePathRepairPlanManifestOperation entry
                in Children[childIndex].Operations)
            {
                Assert.False(
                    File.Exists(
                        Path.Combine(
                            childDirectory,
                            entry.JournalChildName
                        )
                    ),
                    $"Unexpected operation journal: {entry.JournalChildName}"
                );
            }
        }

        public void AssertAuthorizationExactBatchBound()
        {
            using LinuxNoFollowPathHandle batchDirectory =
                OpenDirectory(
                    BatchDirectory
                );

            DataRelativePathRepairBatchApplyAuthorizationReaderResult
                read =
                    DataRelativePathRepairBatchApplyAuthorizationReader.Read(
                        batchDirectory,
                        ApplyAuthorizationName
                    );

            Assert.True(
                read.Success,
                read.Error
            );

            DataRelativePathRepairBatchApplyAuthorizationRecord authorization =
                Assert.IsType<
                    DataRelativePathRepairBatchApplyAuthorizationRecord>(
                        read.Authorization
                    );

            Assert.Equal(
                Batch.BatchId,
                authorization.BatchId
            );

            Assert.Equal(
                DataRoot,
                authorization.DataRoot
            );

            Assert.Equal(
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion3,
                authorization.CoveragePolicyVersion
            );

            Assert.Equal(
                BatchManifestSha256,
                authorization.BatchManifestSha256,
                ignoreCase:
                    true
            );

            string durableBytesAsText =
                File.ReadAllText(
                    ApplyAuthorizationPath
                );

            Assert.False(
                durableBytesAsText.Contains(
                    SidecarPath,
                    StringComparison.Ordinal
                )
            );

            Assert.False(
                durableBytesAsText.Contains(
                    EvidenceDirectory,
                    StringComparison.Ordinal
                )
            );

            Assert.False(
                durableBytesAsText.Contains(
                    global::AggregateNamespaceManifestCommand
                        .DefaultManifestName,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }

        public void AssertSidecarInvocationOnly()
        {
            Assert.True(
                File.Exists(
                    SidecarPath
                )
            );

            byte[] sidecarBytes =
                File.ReadAllBytes(
                    SidecarPath
                );

            foreach (
                string file
                in Directory.EnumerateFiles(
                    BatchDirectory,
                    "*",
                    SearchOption.AllDirectories
                ))
            {
                Assert.False(
                    string.Equals(
                        Path.GetFileName(
                            file
                        ),
                        global::AggregateNamespaceManifestCommand
                            .DefaultManifestName,
                        StringComparison.OrdinalIgnoreCase
                    ),
                    $"A sidecar-named file was persisted inside the batch: {file}"
                );

                Assert.False(
                    File.ReadAllBytes(
                        file
                    )
                    .AsSpan()
                    .SequenceEqual(
                        sidecarBytes
                    ),
                    $"Exact sidecar bytes were copied into the batch: {file}"
                );
            }

            if (ApplyAuthorizationExists())
            {
                AssertAuthorizationExactBatchBound();
            }
        }

        private void PrepareArtifacts()
        {
            int sidecarResult =
                global::AggregateNamespaceManifestCommand.Run(
                    [
                        "aggregate-namespace-manifest",
                        DataRoot,
                        "Meshes",
                        EvidenceDirectory
                    ],
                    createdUtcOverride:
                        SidecarT0,
                    afterManifestPublishBeforeRead:
                        null
                );

            Assert.Equal(
                0,
                sidecarResult
            );

            Assert.True(
                File.Exists(
                    SidecarPath
                )
            );

            int plannerResult =
                global::RepairPlanAggregateNamespaceBatchCommand.Run(
                    [
                        "repair-plan-aggregate-namespace-batch",
                        DataRoot,
                        PathList,
                        SidecarPath,
                        BatchDirectory,
                        PlanManifestName
                    ]
                );

            Assert.Equal(
                0,
                plannerResult
            );

            Assert.False(
                ApplyAuthorizationExists()
            );

            using LinuxNoFollowPathHandle batchDirectory =
                OpenDirectory(
                    BatchDirectory
                );

            DataRelativePathRepairBatchCompletionInspection completion =
                DataRelativePathRepairBatchCompletionInspector.Inspect(
                    batchDirectory,
                    BatchManifestName,
                    PlanManifestName,
                    DataRoot
                );

            Assert.True(
                completion.Success,
                completion.Error
            );

            Assert.Null(
                completion.ApplyAuthorizationRead
            );

            Batch =
                Assert.IsType<
                    DataRelativePathRepairBatchManifestRecord>(
                        completion.Manifest
                    );

            Assert.Equal(
                DataRelativePathRepairBatchManifestRecord.SchemaVersion4,
                Batch.SchemaVersion
            );

            Assert.Equal(
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion3,
                Batch.CoveragePolicyVersion
            );

            Assert.Equal(
                SourcePaths.Length,
                Batch.Children.Count
            );

            Assert.NotNull(
                completion.BatchManifestRead
            );

            BatchManifestSha256 =
                Assert.IsType<string>(
                    completion.BatchManifestRead!
                        .ManifestSha256
                );

            Children =
                completion.Children
                    .Select(
                        child =>
                            Assert.IsType<
                                DataRelativePathRepairPlanManifestRecord>(
                                    child.Inspection.Manifest
                                )
                    )
                    .ToArray();

            Assert.Equal(
                SourcePaths.Length,
                Children.Length
            );

            for (
                int index = 0;
                index < Children.Length;
                index++)
            {
                Assert.Equal(
                    DataRelativePathRepairPlanManifestRecord.SchemaVersion4,
                    Children[index].SchemaVersion
                );

                Assert.False(
                    File.Exists(
                        DestinationPaths[index]
                    )
                );
            }
        }

        private DataRelativePathAggregateNamespaceManifestReaderResult
            ReadEvidence()
        {
            using LinuxNoFollowPathHandle evidenceDirectory =
                OpenDirectory(
                    EvidenceDirectory
                );

            DataRelativePathAggregateNamespaceManifestReaderResult read =
                DataRelativePathAggregateNamespaceManifestReader.Read(
                    evidenceDirectory,
                    global::AggregateNamespaceManifestCommand
                        .DefaultManifestName
                );

            Assert.True(
                read.Success,
                read.Error
            );

            return read;
        }

        private string ChildDirectoryPath(
            int childIndex)
        {
            return Path.Combine(
                BatchDirectory,
                Batch.Children[childIndex].ChildName
            );
        }

        private static bool HasDistinctActorsRoots(
            string meshes)
        {
            string[] names =
                Directory.EnumerateDirectories(
                    meshes
                )
                .Select(
                    Path.GetFileName
                )
                .Where(
                    name =>
                        name is not null
                )
                .Cast<string>()
                .ToArray();

            return
                names.Contains(
                    "Actors",
                    StringComparer.Ordinal
                ) &&
                names.Contains(
                    "actors",
                    StringComparer.Ordinal
                );
        }

        private static LinuxNoFollowPathHandle OpenDirectory(
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
                LinuxNoFollowPathHandle>(
                    opened.OpenedPath
                );
        }

        public void Dispose()
        {
            if (
                Directory.Exists(
                    RootPath))
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
