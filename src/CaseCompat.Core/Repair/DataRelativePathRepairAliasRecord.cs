using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Independent durable record of one deliberately-created symlink alias.
//
// An alias exists only because a genuine, contested ancestor-casing
// disagreement was found: two or more winning consumers require
// different casings for the same shared directory, and there is no
// single rename that satisfies both. Rather than guessing, a symlink
// is created at the missing casing, pointing at the one real,
// physically-existing directory - so both casings resolve to exactly
// the same content, with no directory ever duplicated or split.
//
// This record is the source of truth that lets later scans recognize
// "this specific symlink is one we created ourselves, safe to follow"
// - as opposed to any other, arbitrary symlink, which this project
// continues to refuse everywhere else. It is a hint, never a trusted
// fact by itself: any caller that finds a matching record here is
// still expected to re-read the symlink's actual current target fresh
// and confirm it still matches before relying on it for anything.
public sealed record DataRelativePathRepairAliasRecord(
    int SchemaVersion,
    Guid PlanId,
    DateTimeOffset CreatedUtc,
    string DataRoot,
    string ParentPath,
    string LinkName,
    string TargetName,
    LinuxFileIdentityResult TargetIdentity
)
{
    public const int SchemaVersion1 =
        1;

    public const int CurrentSchemaVersion =
        SchemaVersion1;

    public static string? Validate(
        DataRelativePathRepairAliasRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        if (record.SchemaVersion != SchemaVersion1)
        {
            return
                "Unsupported alias record schema version.";
        }

        if (record.PlanId == Guid.Empty)
        {
            return
                "An alias record requires a non-empty PlanId.";
        }

        if (!TryCanonicalAbsolutePath(
                record.DataRoot,
                out string dataRoot))
        {
            return
                "An alias record's Data root must be a canonical " +
                "absolute path.";
        }

        if (!TryCanonicalAbsolutePath(
                record.ParentPath,
                out string parentPath))
        {
            return
                "An alias record's parent path must be a canonical " +
                "absolute path.";
        }

        if (!TryRelativeUnderRoot(
                dataRoot,
                parentPath,
                allowRoot:
                    true,
                out _))
        {
            return
                "An alias record's parent path must be the Data root " +
                "or a descendant of it.";
        }

        if (!IsValidBareName(record.LinkName))
        {
            return
                "An alias record's link name must be a bare sibling " +
                "name and cannot be '.', '..', or contain path " +
                "separators or NUL.";
        }

        if (!IsValidBareName(record.TargetName))
        {
            return
                "An alias record's target name must be a bare " +
                "sibling name and cannot be '.', '..', or contain " +
                "path separators or NUL.";
        }

        if (string.Equals(
                record.LinkName,
                record.TargetName,
                StringComparison.Ordinal))
        {
            return
                "An alias record's link name and target name must " +
                "not be identical.";
        }

        LinuxFileIdentityResult identity =
            record.TargetIdentity;

        if (
            !identity.Success ||
            identity.DeviceMajor is null ||
            identity.DeviceMinor is null ||
            identity.Inode is null ||
            identity.MountId is null)
        {
            return
                "An alias record's target identity must be a complete, " +
                "successfully-captured identity.";
        }

        string expectedTargetPath =
            Path.Combine(
                parentPath,
                record.TargetName
            );

        if (!string.Equals(
                identity.FullPath,
                expectedTargetPath,
                StringComparison.Ordinal))
        {
            return
                "An alias record's target identity does not match its " +
                "own recorded parent path and target name.";
        }

        return null;
    }

    private static bool IsValidBareName(
        string? name)
    {
        if (
            string.IsNullOrEmpty(
                name
            ) ||
            name is "." or "..")
        {
            return false;
        }

        return
            !name.Contains('/') &&
            !name.Contains('\\') &&
            !name.Contains('\0');
    }

    private static bool TryCanonicalAbsolutePath(
        string? value,
        out string canonical)
    {
        canonical =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                value) ||
            !Path.IsPathFullyQualified(
                value))
        {
            return false;
        }

        try
        {
            canonical =
                Path.GetFullPath(
                    value
                );
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            canonical =
                string.Empty;

            return false;
        }

        return string.Equals(
            canonical,
            value,
            StringComparison.Ordinal
        );
    }

    private static bool TryRelativeUnderRoot(
        string dataRoot,
        string path,
        bool allowRoot,
        out string relative)
    {
        relative =
            string.Empty;

        string observed;

        try
        {
            observed =
                Path.GetRelativePath(
                    dataRoot,
                    path
                )
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/'
                );

            if (
                Path.AltDirectorySeparatorChar !=
                Path.DirectorySeparatorChar)
            {
                observed =
                    observed.Replace(
                        Path.AltDirectorySeparatorChar,
                        '/'
                    );
            }
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

        if (observed == ".")
        {
            if (!allowRoot)
            {
                return false;
            }

            relative =
                string.Empty;

            return true;
        }

        if (
            string.IsNullOrWhiteSpace(
                observed) ||
            observed == ".." ||
            observed.StartsWith(
                "../",
                StringComparison.Ordinal) ||
            Path.IsPathRooted(
                observed))
        {
            return false;
        }

        string[] components =
            observed.Split(
                '/',
                StringSplitOptions.None
            );

        if (
            components.Length == 0 ||
            components.Any(
                component =>
                    string.IsNullOrEmpty(
                        component) ||
                    component is "." or ".."))
        {
            return false;
        }

        relative =
            observed;

        return true;
    }
}
