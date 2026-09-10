using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairAliasRegistryTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            9,
            10,
            0,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void Record_ThenTryFind_RoundTripsExactly()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshesDir =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "Meshes"
                )
            ).FullName;

        string realTarget =
            Directory.CreateDirectory(
                Path.Combine(
                    meshesDir,
                    "Actors"
                )
            ).FullName;

        LinuxFileIdentityResult identity =
            LinuxFileIdentity.Inspect(
                realTarget
            );

        Assert.True(
            identity.Success,
            identity.Error
        );

        string aliasesDir =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "aliases"
                )
            ).FullName;

        using LinuxNoFollowPathHandle aliasesHandle =
            OpenRoot(
                aliasesDir
            );

        Guid planId =
            Guid.NewGuid();

        var record =
            new DataRelativePathRepairAliasRecord(
                SchemaVersion:
                    DataRelativePathRepairAliasRecord.CurrentSchemaVersion,
                PlanId:
                    planId,
                CreatedUtc:
                    T0,
                DataRoot:
                    dataRoot,
                ParentPath:
                    meshesDir,
                LinkName:
                    "actors",
                TargetName:
                    "Actors",
                TargetIdentity:
                    identity
            );

        DataRelativePathRepairAliasRegistryRecordResult recorded =
            DataRelativePathRepairAliasRegistry.Record(
                aliasesHandle,
                record
            );

        Assert.True(
            recorded.Success,
            recorded.Error
        );

        Assert.Equal(
            DataRelativePathRepairAliasRegistryRecordState.Recorded,
            recorded.State
        );

        DataRelativePathRepairAliasRegistryLookupResult found =
            DataRelativePathRepairAliasRegistry.TryFind(
                aliasesHandle,
                meshesDir,
                "actors"
            );

        Assert.True(
            found.Success,
            found.Error
        );

        Assert.Equal(
            planId,
            found.Record!.PlanId
        );

        Assert.Equal(
            "Actors",
            found.Record.TargetName
        );

        Assert.Equal(
            identity.Inode,
            found.Record.TargetIdentity.Inode
        );
    }

    [Fact]
    public void TryFind_NeverRecorded_ReturnsNotFound()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string aliasesDir =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "aliases"
                )
            ).FullName;

        using LinuxNoFollowPathHandle aliasesHandle =
            OpenRoot(
                aliasesDir
            );

        DataRelativePathRepairAliasRegistryLookupResult found =
            DataRelativePathRepairAliasRegistry.TryFind(
                aliasesHandle,
                "/some/never/recorded/parent",
                "actors"
            );

        Assert.False(
            found.Success
        );

        Assert.Equal(
            DataRelativePathRepairAliasRegistryLookupState.NotFound,
            found.State
        );
    }

    [Fact]
    public void Record_CalledTwiceForSameAlias_SecondCallReportsAlreadyRecorded()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshesDir =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "Meshes"
                )
            ).FullName;

        string realTarget =
            Directory.CreateDirectory(
                Path.Combine(
                    meshesDir,
                    "Actors"
                )
            ).FullName;

        LinuxFileIdentityResult identity =
            LinuxFileIdentity.Inspect(
                realTarget
            );

        Assert.True(
            identity.Success,
            identity.Error
        );

        string aliasesDir =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "aliases"
                )
            ).FullName;

        using LinuxNoFollowPathHandle aliasesHandle =
            OpenRoot(
                aliasesDir
            );

        var record =
            new DataRelativePathRepairAliasRecord(
                SchemaVersion:
                    DataRelativePathRepairAliasRecord.CurrentSchemaVersion,
                PlanId:
                    Guid.NewGuid(),
                CreatedUtc:
                    T0,
                DataRoot:
                    dataRoot,
                ParentPath:
                    meshesDir,
                LinkName:
                    "actors",
                TargetName:
                    "Actors",
                TargetIdentity:
                    identity
            );

        DataRelativePathRepairAliasRegistryRecordResult first =
            DataRelativePathRepairAliasRegistry.Record(
                aliasesHandle,
                record
            );

        Assert.True(
            first.Success,
            first.Error
        );

        DataRelativePathRepairAliasRegistryRecordResult second =
            DataRelativePathRepairAliasRegistry.Record(
                aliasesHandle,
                record with
                {
                    PlanId = Guid.NewGuid()
                }
            );

        Assert.False(
            second.Success
        );

        Assert.Equal(
            DataRelativePathRepairAliasRegistryRecordState.AlreadyRecorded,
            second.State
        );

        // The original record must be untouched by the second attempt.
        DataRelativePathRepairAliasRegistryLookupResult found =
            DataRelativePathRepairAliasRegistry.TryFind(
                aliasesHandle,
                meshesDir,
                "actors"
            );

        Assert.True(
            found.Success,
            found.Error
        );

        Assert.Equal(
            record.PlanId,
            found.Record!.PlanId
        );
    }

    [Fact]
    public void Record_LinkNameEqualsTargetName_RefusesWithoutWriting()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshesDir =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "Meshes"
                )
            ).FullName;

        string realTarget =
            Directory.CreateDirectory(
                Path.Combine(
                    meshesDir,
                    "Actors"
                )
            ).FullName;

        LinuxFileIdentityResult identity =
            LinuxFileIdentity.Inspect(
                realTarget
            );

        Assert.True(
            identity.Success,
            identity.Error
        );

        string aliasesDir =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "aliases"
                )
            ).FullName;

        using LinuxNoFollowPathHandle aliasesHandle =
            OpenRoot(
                aliasesDir
            );

        var record =
            new DataRelativePathRepairAliasRecord(
                SchemaVersion:
                    DataRelativePathRepairAliasRecord.CurrentSchemaVersion,
                PlanId:
                    Guid.NewGuid(),
                CreatedUtc:
                    T0,
                DataRoot:
                    dataRoot,
                ParentPath:
                    meshesDir,
                LinkName:
                    "Actors",
                TargetName:
                    "Actors",
                TargetIdentity:
                    identity
            );

        DataRelativePathRepairAliasRegistryRecordResult result =
            DataRelativePathRepairAliasRegistry.Record(
                aliasesHandle,
                record
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairAliasRegistryRecordState.InvalidRecord,
            result.State
        );

        Assert.Empty(
            Directory.GetFiles(
                aliasesDir
            )
        );
    }

    [Fact]
    public void Record_TargetIdentityDoesNotMatchParentAndTargetName_RefusesWithoutWriting()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshesDir =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "Meshes"
                )
            ).FullName;

        // Identity captured for a completely different directory than
        // the one this record claims to describe.
        string unrelatedDir =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "unrelated"
                )
            ).FullName;

        LinuxFileIdentityResult mismatchedIdentity =
            LinuxFileIdentity.Inspect(
                unrelatedDir
            );

        Assert.True(
            mismatchedIdentity.Success,
            mismatchedIdentity.Error
        );

        string aliasesDir =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "aliases"
                )
            ).FullName;

        using LinuxNoFollowPathHandle aliasesHandle =
            OpenRoot(
                aliasesDir
            );

        var record =
            new DataRelativePathRepairAliasRecord(
                SchemaVersion:
                    DataRelativePathRepairAliasRecord.CurrentSchemaVersion,
                PlanId:
                    Guid.NewGuid(),
                CreatedUtc:
                    T0,
                DataRoot:
                    dataRoot,
                ParentPath:
                    meshesDir,
                LinkName:
                    "actors",
                TargetName:
                    "Actors",
                TargetIdentity:
                    mismatchedIdentity
            );

        DataRelativePathRepairAliasRegistryRecordResult result =
            DataRelativePathRepairAliasRegistry.Record(
                aliasesHandle,
                record
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairAliasRegistryRecordState.InvalidRecord,
            result.State
        );

        Assert.Empty(
            Directory.GetFiles(
                aliasesDir
            )
        );
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

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-alias-registry-tests",
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
                    recursive: true
                );
            }
        }
    }
}
