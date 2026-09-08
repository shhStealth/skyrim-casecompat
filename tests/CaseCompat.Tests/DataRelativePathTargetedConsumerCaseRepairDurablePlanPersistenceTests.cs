using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathTargetedConsumerCaseRepairDurablePlanPersistenceTests
{
    [Fact]
    public void CreateInitial_ThenRead_RoundTripsDurably()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            CreateRecord();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult
            write =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriter
                    .CreateInitial(
                        fixture.PlanDirectory,
                        "plan.json",
                        plan
                    );

        Assert.True(
            write.Success,
            write.Error
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                .CreatedDurably,
            write.State
        );

        Assert.NotNull(
            write.WrittenIncarnationIdentity
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
            read =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .Read(
                        fixture.PlanDirectory,
                        "plan.json"
                    );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            plan.PlanId,
            read.Plan!.PlanId
        );

        Assert.Equal(
            plan.Operations.Count,
            read.Plan.Operations.Count
        );

        Assert.NotNull(
            read.PlanIncarnationIdentity
        );

        Assert.Null(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                read.Plan
            )
        );
    }

    [Fact]
    public void Read_ReturnsSha256OfExactPersistedPlanBytes()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            CreateRecord();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult
            write =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriter
                    .CreateInitial(
                        fixture.PlanDirectory,
                        "plan.json",
                        plan
                    );

        Assert.True(
            write.Success,
            write.Error
        );

        /*
         * Add semantically insignificant JSON whitespace after durable
         * publication.
         *
         * A canonical re-serialization hash would ignore this
         * byte-level difference. The reader contract must instead bind
         * the exact persisted byte sequence that it actually
         * deserializes.
         */
        string planPath =
            Path.Combine(
                fixture.PlanDirectoryPath,
                "plan.json"
            );

        File.AppendAllText(
            planPath,
            "\n \t"
        );

        byte[] exactBytes =
            File.ReadAllBytes(
                planPath
            );

        string expectedSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    exactBytes
                )
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
            read =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .Read(
                        fixture.PlanDirectory,
                        "plan.json"
                    );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            plan.PlanId,
            read.Plan!.PlanId
        );

        Assert.Equal(
            exactBytes.LongLength,
            read.Length
        );

        Assert.Equal(
            expectedSha256,
            read.PlanSha256
        );
    }

    [Fact]
    public void CreateInitial_ExistingPlan_IsNotOverwritten()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord first =
            CreateRecord();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord second =
            CreateRecord() with
            {
                PlanId =
                    Guid.Parse(
                        "99999999-8888-7777-6666-555555555555"
                    )
            };

        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult
            firstWrite =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriter
                    .CreateInitial(
                        fixture.PlanDirectory,
                        "plan.json",
                        first
                    );

        Assert.True(
            firstWrite.Success,
            firstWrite.Error
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult
            duplicate =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriter
                    .CreateInitial(
                        fixture.PlanDirectory,
                        "plan.json",
                        second
                    );

        Assert.False(
            duplicate.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                .PlanAlreadyExists,
            duplicate.State
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
            read =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .Read(
                        fixture.PlanDirectory,
                        "plan.json"
                    );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            first.PlanId,
            read.Plan!.PlanId
        );

        Assert.NotEqual(
            second.PlanId,
            read.Plan.PlanId
        );
    }

    [Fact]
    public void Read_SymbolicLinkPlan_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string target =
            Path.Combine(
                fixture.RootPath,
                "target.json"
            );

        File.WriteAllText(
            target,
            "{}"
        );

        string link =
            Path.Combine(
                fixture.PlanDirectoryPath,
                "plan.json"
            );

        File.CreateSymbolicLink(
            link,
            target
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
            read =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .Read(
                        fixture.PlanDirectory,
                        "plan.json"
                    );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                .PlanSymbolicLinkRejected,
            read.State
        );
    }

    [Fact]
    public void Read_OversizePlan_IsRejectedBeforeOpen()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string oversizePath =
            Path.Combine(
                fixture.PlanDirectoryPath,
                "plan.json"
            );

        File.WriteAllBytes(
            oversizePath,
            new byte[
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .MaxPlanSizeBytes +
                1
            ]
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
            read =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .Read(
                        fixture.PlanDirectory,
                        "plan.json"
                    );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                .PlanTooLarge,
            read.State
        );
    }

    [Fact]
    public void Read_MissingPlan_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
            read =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .Read(
                        fixture.PlanDirectory,
                        "missing.json"
                    );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                .PlanUnavailable,
            read.State
        );
    }

    [Fact]
    public void CreateInitial_InvalidPlan_IsRejectedBeforeAnyFileIsCreated()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord invalid =
            CreateRecord() with
            {
                SchemaVersion =
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                        .SchemaVersion1 +
                    1
            };

        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult
            write =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriter
                    .CreateInitial(
                        fixture.PlanDirectory,
                        "plan.json",
                        invalid
                    );

        Assert.False(
            write.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                .InvalidPlan,
            write.State
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    fixture.PlanDirectoryPath,
                    "plan.json"
                )
            )
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
        CreateRecord()
    {
        const string dataRoot =
            "/game/Data";

        const string source =
            "/game/Data/meshes/actors/character/file.nif";

        const string requested =
            "Meshes/Actors/Character/File.NIF";

        var sourceSnapshot =
            new DataRelativePathRepairSourceSnapshot(
                PhysicalPath:
                    source,
                Size:
                    6,
                Sha256:
                    new string(
                        'A',
                        64
                    ),
                Identity:
                    Identity(
                        source,
                        inode:
                            100UL
                    )
            );

        var parentSnapshot =
            new DataRelativePathRepairDestinationParentSnapshot(
                PhysicalPath:
                    dataRoot,
                Identity:
                    Identity(
                        dataRoot,
                        inode:
                            200UL
                    ),
                CasefoldEnabled:
                    false,
                RawFlags:
                    0
            );

        DataRelativePathRepairPlanOperation[] operations =
        [
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/Meshes",
                SourcePath:
                    null
            ),
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/Meshes/Actors",
                SourcePath:
                    null
            ),
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/Meshes/Actors/Character",
                SourcePath:
                    null
            ),
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile,
                DestinationPath:
                    "/game/Data/Meshes/Actors/Character/File.NIF",
                SourcePath:
                    source
            )
        ];

        var record =
            new DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
                SchemaVersion:
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                        .SchemaVersion1,
                PlanId:
                    Guid.Parse(
                        "718d85ad-07f3-4dc3-8f08-b9e5ded7bf77"
                    ),
                CreatedUtc:
                    new DateTimeOffset(
                        2026,
                        9,
                        8,
                        4,
                        0,
                        0,
                        TimeSpan.Zero
                    ),
                DataRoot:
                    dataRoot,
                RequestedPath:
                    requested,
                SourceSnapshot:
                    sourceSnapshot,
                SourceInodeGeneration:
                    12345U,
                InitialDestinationParentSnapshot:
                    parentSnapshot,
                Operations:
                    operations
            );

        Assert.Null(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                record
            )
        );

        return record;
    }

    private static LinuxFileIdentityResult Identity(
        string fullPath,
        ulong inode)
    {
        return new(
            FullPath:
                fullPath,
            DeviceMajor:
                8U,
            DeviceMinor:
                1U,
            Inode:
                inode,
            LinkCount:
                1U,
            MountId:
                44UL,
            Error:
                null
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
                    "casecompat-targeted-plan-tests",
                    Guid.NewGuid().ToString("N")
                );

            PlanDirectoryPath =
                Path.Combine(
                    RootPath,
                    "Plan"
                );

            Directory.CreateDirectory(
                PlanDirectoryPath
            );

            PlanDirectory =
                OpenRoot(
                    PlanDirectoryPath
                );
        }

        public string RootPath { get; }

        public string PlanDirectoryPath { get; }

        public LinuxNoFollowPathHandle PlanDirectory { get; }

        public bool SupportsUnnamedFiles()
        {
            LinuxCreateUnnamedFileAtResult probe =
                LinuxCreateUnnamedFileAt.Create(
                    PlanDirectory
                );

            if (
                probe.State ==
                LinuxCreateUnnamedFileAtState
                    .TmpfileUnsupported)
            {
                return false;
            }

            Assert.True(
                probe.Success,
                probe.Error
            );

            probe.OpenedFile!.Dispose();

            return true;
        }

        private static LinuxNoFollowPathHandle OpenRoot(
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
            PlanDirectory.Dispose();

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
