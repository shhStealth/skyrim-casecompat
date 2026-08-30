using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairSourceValidatorTests
{
    [Fact]
    public void Validate_UnchangedSource_Matches()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string source =
            CreateSource(
                dataRoot,
                "meshes/example/fixture.nif",
                "unchanged"
            );

        DataRelativePathRepairSourceSnapshot expected =
            Snapshot(
                source
            );

        DataRelativePathRepairSourceValidation result =
            DataRelativePathRepairSourceValidator.Validate(
                dataRoot,
                expected
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairSourceValidationState
                .Matched,
            result.State
        );

        Assert.Equal(
            LinuxNoFollowPathOpenState.Opened,
            result.OpenState
        );

        Assert.NotNull(
            result.ActualSnapshot
        );

        Assert.Null(
            result.Error
        );
    }

    [Fact]
    public void Validate_PathReplacedAfterProjection_ReportsIdentityChanged()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string source =
            CreateSource(
                dataRoot,
                "meshes/example/fixture.nif",
                "original"
            );

        DataRelativePathRepairSourceSnapshot expected =
            Snapshot(
                source
            );

        string moved =
            Path.Combine(
                Path.GetDirectoryName(
                    source
                )!,
                "original-moved.nif"
            );

        File.Move(
            source,
            moved
        );

        File.WriteAllText(
            source,
            "replacement"
        );

        DataRelativePathRepairSourceValidation result =
            DataRelativePathRepairSourceValidator.Validate(
                dataRoot,
                expected
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairSourceValidationState
                .IdentityChanged,
            result.State
        );

        Assert.NotNull(
            result.ActualSnapshot
        );
    }

    [Fact]
    public void Validate_SameFileWithDifferentSize_ReportsSizeChanged()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string source =
            CreateSource(
                dataRoot,
                "meshes/example/fixture.nif",
                "short"
            );

        DataRelativePathRepairSourceSnapshot expected =
            Snapshot(
                source
            );

        File.AppendAllText(
            source,
            "-now-longer"
        );

        DataRelativePathRepairSourceValidation result =
            DataRelativePathRepairSourceValidator.Validate(
                dataRoot,
                expected
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairSourceValidationState
                .SizeChanged,
            result.State
        );

        Assert.True(
            expected.Identity.SameObjectAs(
                result.ActualSnapshot!.Identity!
            )
        );
    }

    [Fact]
    public void Validate_SameFileSameSizeDifferentContent_ReportsHashChanged()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string source =
            CreateSource(
                dataRoot,
                "meshes/example/fixture.nif",
                "AAAA"
            );

        DataRelativePathRepairSourceSnapshot expected =
            Snapshot(
                source
            );

        File.WriteAllText(
            source,
            "BBBB"
        );

        DataRelativePathRepairSourceValidation result =
            DataRelativePathRepairSourceValidator.Validate(
                dataRoot,
                expected
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairSourceValidationState
                .HashChanged,
            result.State
        );

        Assert.Equal(
            expected.Size,
            result.ActualSnapshot!.Size
        );

        Assert.True(
            expected.Identity.SameObjectAs(
                result.ActualSnapshot.Identity!
            )
        );
    }

    [Fact]
    public void Validate_SourceOutsideDataRoot_IsRejectedBeforeOpen()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string outside =
            Path.Combine(
                temp.RootPath,
                "outside.nif"
            );

        File.WriteAllText(
            outside,
            "outside"
        );

        DataRelativePathRepairSourceSnapshot expected =
            Snapshot(
                outside
            );

        DataRelativePathRepairSourceValidation result =
            DataRelativePathRepairSourceValidator.Validate(
                dataRoot,
                expected
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairSourceValidationState
                .SourceOutsideDataRoot,
            result.State
        );

        Assert.Null(
            result.OpenState
        );

        Assert.Null(
            result.ActualSnapshot
        );
    }

    private static string CreateDataRoot(
        TemporaryDirectory temp)
    {
        return Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                "Data"
            )
        ).FullName;
    }

    private static string CreateSource(
        string dataRoot,
        string relativePath,
        string content)
    {
        string fullPath =
            Path.Combine(
                dataRoot,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                fullPath
            )!
        );

        File.WriteAllText(
            fullPath,
            content
        );

        return fullPath;
    }

    private static DataRelativePathRepairSourceSnapshot
        Snapshot(
            string physicalPath)
    {
        LinuxFileIdentityResult identity =
            LinuxFileIdentity.Inspect(
                physicalPath
            );

        Assert.True(
            identity.Success
        );

        byte[] bytes =
            File.ReadAllBytes(
                physicalPath
            );

        return new DataRelativePathRepairSourceSnapshot(
            PhysicalPath:
                Path.GetFullPath(
                    physicalPath
                ),
            Size:
                bytes.LongLength,
            Sha256:
                Convert.ToHexString(
                    SHA256.HashData(
                        bytes
                    )
                ),
            Identity:
                identity
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
                    "casecompat-source-validation-tests",
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
