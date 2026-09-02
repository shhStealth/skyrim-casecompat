using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

/*
 * xUnit 2 cannot dynamically skip a test after its body starts, so
 * inode-generation capability detection is performed at discovery
 * time through derived FactAttribute types.
 *
 * The probes use Path.GetTempPath(), matching the filesystem used by
 * these tests. Only the explicit GenerationUnavailable state causes a
 * skip. Other probe failures are not hidden; discovery falls through
 * and the real test body is allowed to run and fail normally.
 */
public sealed class LinuxDirectoryInodeGenerationFactAttribute
    : FactAttribute
{
    public LinuxDirectoryInodeGenerationFactAttribute()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip =
                "Requires Linux directory inode-generation capture.";

            return;
        }

        string rootPath =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-directory-generation-fact-probe",
                Guid.NewGuid().ToString("N")
            );

        try
        {
            Directory.CreateDirectory(
                rootPath
            );

            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    rootPath
                );

            if (
                !opened.Success ||
                opened.OpenedPath is not
                    LinuxNoFollowPathHandle handle)
            {
                return;
            }

            using (handle)
            {
                LinuxOpenedInodeGenerationResult generation =
                    LinuxOpenedInodeGeneration.Capture(
                        handle
                    );

                if (
                    generation.State ==
                    LinuxOpenedInodeGenerationState
                        .GenerationUnavailable)
                {
                    Skip =
                        "Linux directory inode-generation capture is " +
                        "unavailable on the test temporary filesystem.";
                }
            }
        }
        catch
        {
            /*
             * Do not convert unrelated probe failures into skips or
             * discovery failures. Let the actual test exercise the
             * environment and report its normal assertion failure.
             */
        }
        finally
        {
            TryDeleteProbeRoot(
                rootPath
            );
        }
    }

    private static void TryDeleteProbeRoot(
        string rootPath)
    {
        try
        {
            if (
                Directory.Exists(
                    rootPath
                ))
            {
                Directory.Delete(
                    rootPath,
                    recursive:
                        true
                );
            }
        }
        catch
        {
            /*
             * Cleanup failure is not capability evidence and must not
             * turn discovery into a test result.
             */
        }
    }
}

public sealed class LinuxFileInodeGenerationFactAttribute
    : FactAttribute
{
    public LinuxFileInodeGenerationFactAttribute()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip =
                "Requires Linux file inode-generation capture.";

            return;
        }

        string rootPath =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-file-generation-fact-probe",
                Guid.NewGuid().ToString("N")
            );

        const string fileName =
            "Owned.bin";

        try
        {
            Directory.CreateDirectory(
                rootPath
            );

            File.WriteAllText(
                Path.Combine(
                    rootPath,
                    fileName
                ),
                "probe"
            );

            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                    rootPath,
                    fileName
                );

            if (
                !opened.Success ||
                opened.OpenedPath is not
                    LinuxNoFollowPathHandle handle)
            {
                return;
            }

            using (handle)
            {
                LinuxOpenedInodeGenerationResult generation =
                    LinuxOpenedInodeGeneration.Capture(
                        handle
                    );

                if (
                    generation.State ==
                    LinuxOpenedInodeGenerationState
                        .GenerationUnavailable)
                {
                    Skip =
                        "Linux file inode-generation capture is " +
                        "unavailable on the test temporary filesystem.";
                }
            }
        }
        catch
        {
            /*
             * Do not convert unrelated probe failures into skips or
             * discovery failures. Let the actual test exercise the
             * environment and report its normal assertion failure.
             */
        }
        finally
        {
            TryDeleteProbeRoot(
                rootPath
            );
        }
    }

    private static void TryDeleteProbeRoot(
        string rootPath)
    {
        try
        {
            if (
                Directory.Exists(
                    rootPath
                ))
            {
                Directory.Delete(
                    rootPath,
                    recursive:
                        true
                );
            }
        }
        catch
        {
            /*
             * Cleanup failure is not capability evidence and must not
             * turn discovery into a test result.
             */
        }
    }
}
