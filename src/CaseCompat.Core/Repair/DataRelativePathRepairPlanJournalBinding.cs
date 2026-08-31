using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

/*
 * Exact manifest journal names identify where an operation journal
 * belongs, but the filename itself is not authority.
 *
 * Cross-bind a loaded durable operation journal to the immutable
 * manifest and independently trusted Data root before allowing its
 * state to drive either forward recovery or destructive rollback.
 */
internal static class DataRelativePathRepairPlanJournalBinding
{
    public static string? ValidateDirectory(
        DataRelativePathRepairPlanManifestOperation entry,
        DataRelativePathRepairDirectoryJournalRecord journal,
        string trustedDataRoot)
    {
        ArgumentNullException.ThrowIfNull(
            entry
        );

        ArgumentNullException.ThrowIfNull(
            journal
        );

        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                journal.DataRoot,
                out string? rootBindingError
            ))
        {
            return
                "The directory operation journal Data root does not " +
                "match the independently trusted Data root: " +
                rootBindingError;
        }

        return ValidateOperation(
            entry,
            journal.Operation
        );
    }

    public static string? ValidateFile(
        DataRelativePathRepairPlanManifestRecord manifest,
        DataRelativePathRepairPlanManifestOperation entry,
        DataRelativePathRepairFileJournalRecord journal,
        string trustedDataRoot)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        ArgumentNullException.ThrowIfNull(
            entry
        );

        ArgumentNullException.ThrowIfNull(
            journal
        );

        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                journal.DataRoot,
                out string? rootBindingError
            ))
        {
            return
                "The file operation journal Data root does not match " +
                "the independently trusted Data root: " +
                rootBindingError;
        }

        string? operationError =
            ValidateOperation(
                entry,
                journal.Operation
            );

        if (operationError is not null)
        {
            return operationError;
        }

        if (
            !SameSourceSnapshot(
                manifest.SourceSnapshot,
                journal.SourceSnapshot
            ))
        {
            return
                "The file operation journal source snapshot does not " +
                "match the immutable plan manifest source evidence.";
        }

        return null;
    }

    private static string? ValidateOperation(
        DataRelativePathRepairPlanManifestOperation entry,
        DataRelativePathRepairPlanOperation journalOperation)
    {
        DataRelativePathRepairPlanOperation expected =
            entry.Operation;

        if (journalOperation.Kind != expected.Kind)
        {
            return
                $"Operation journal {entry.JournalChildName} has kind " +
                $"{journalOperation.Kind}, but the manifest requires " +
                $"{expected.Kind}.";
        }

        if (
            !PathEquals(
                journalOperation.DestinationPath,
                expected.DestinationPath
            ))
        {
            return
                $"Operation journal {entry.JournalChildName} has a " +
                "destination that does not match the immutable plan " +
                "manifest.";
        }

        if (
            !NullablePathEquals(
                journalOperation.SourcePath,
                expected.SourcePath
            ))
        {
            return
                $"Operation journal {entry.JournalChildName} has a " +
                "source path that does not match the immutable plan " +
                "manifest.";
        }

        return null;
    }

    private static bool SameSourceSnapshot(
        DataRelativePathRepairSourceSnapshot expected,
        DataRelativePathRepairSourceSnapshot actual)
    {
        LinuxFileIdentityResult expectedIdentity =
            expected.Identity;

        LinuxFileIdentityResult actualIdentity =
            actual.Identity;

        return
            PathEquals(
                expected.PhysicalPath,
                actual.PhysicalPath
            ) &&
            expected.Size ==
                actual.Size &&
            string.Equals(
                expected.Sha256,
                actual.Sha256,
                StringComparison.OrdinalIgnoreCase
            ) &&
            PathEquals(
                expectedIdentity.FullPath,
                actualIdentity.FullPath
            ) &&
            expectedIdentity.DeviceMajor ==
                actualIdentity.DeviceMajor &&
            expectedIdentity.DeviceMinor ==
                actualIdentity.DeviceMinor &&
            expectedIdentity.Inode ==
                actualIdentity.Inode &&
            expectedIdentity.LinkCount ==
                actualIdentity.LinkCount &&
            expectedIdentity.MountId ==
                actualIdentity.MountId;
    }

    private static bool NullablePathEquals(
        string? left,
        string? right)
    {
        if (
            left is null ||
            right is null)
        {
            return
                left is null &&
                right is null;
        }

        return PathEquals(
            left,
            right
        );
    }

    private static bool PathEquals(
        string left,
        string right)
    {
        try
        {
            string normalizedLeft =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        left
                    )
                );

            string normalizedRight =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        right
                    )
                );

            return string.Equals(
                normalizedLeft,
                normalizedRight,
                StringComparison.Ordinal
            );
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return false;
        }
    }
}
