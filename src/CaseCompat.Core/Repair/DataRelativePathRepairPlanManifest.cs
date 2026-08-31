namespace CaseCompat.Core.Repair;

public sealed record DataRelativePathRepairPlanManifestOperation(
    int Index,
    DataRelativePathRepairPlanOperation Operation,
    string JournalChildName
);

public sealed record DataRelativePathRepairPlanManifestRecord(
    int SchemaVersion,
    Guid PlanId,
    DateTimeOffset CreatedUtc,
    string DataRoot,
    string RequestedPath,
    DataRelativePathRepairSourceSnapshot SourceSnapshot,
    DataRelativePathRepairDestinationParentSnapshot
        InitialDestinationParentSnapshot,
    IReadOnlyList<DataRelativePathRepairPlanManifestOperation>
        Operations
)
{
    public const int CurrentSchemaVersion =
        1;
}

public enum DataRelativePathRepairPlanManifestCreationState
{
    Created,
    InvalidInput
}

public sealed record DataRelativePathRepairPlanManifestCreation(
    DataRelativePathRepairPlanManifestCreationState State,
    DataRelativePathRepairPlanManifestRecord? Manifest,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairPlanManifestCreationState.Created &&
        Manifest is not null;
}

public static class DataRelativePathRepairPlanManifest
{
    public static DataRelativePathRepairPlanManifestCreation Create(
        Guid planId,
        DateTimeOffset createdUtc,
        string dataRoot,
        string requestedPath,
        DataRelativePathRepairSourceSnapshot sourceSnapshot,
        DataRelativePathRepairDestinationParentSnapshot
            initialDestinationParentSnapshot,
        IReadOnlyList<DataRelativePathRepairPlanOperation>
            operations)
    {
        ArgumentNullException.ThrowIfNull(
            sourceSnapshot
        );

        ArgumentNullException.ThrowIfNull(
            initialDestinationParentSnapshot
        );

        ArgumentNullException.ThrowIfNull(
            operations
        );

        DataRelativePathRepairPlanManifestOperation[] entries =
            operations
                .Select(
                    (
                        operation,
                        index
                    ) =>
                        new DataRelativePathRepairPlanManifestOperation(
                            Index:
                                index,
                            Operation:
                                operation,
                            JournalChildName:
                                CreateOperationJournalChildName(
                                    planId,
                                    index,
                                    operation.Kind
                                )
                        )
                )
                .ToArray();

        var manifest =
            new DataRelativePathRepairPlanManifestRecord(
                SchemaVersion:
                    DataRelativePathRepairPlanManifestRecord
                        .CurrentSchemaVersion,
                PlanId:
                    planId,
                CreatedUtc:
                    createdUtc,
                DataRoot:
                    dataRoot,
                RequestedPath:
                    requestedPath,
                SourceSnapshot:
                    sourceSnapshot,
                InitialDestinationParentSnapshot:
                    initialDestinationParentSnapshot,
                Operations:
                    entries
            );

        string? validationError =
            Validate(
                manifest
            );

        if (validationError is not null)
        {
            return new(
                State:
                    DataRelativePathRepairPlanManifestCreationState
                        .InvalidInput,
                Manifest:
                    null,
                Error:
                    validationError
            );
        }

        return new(
            State:
                DataRelativePathRepairPlanManifestCreationState
                    .Created,
            Manifest:
                manifest,
            Error:
                null
        );
    }

    public static string? Validate(
        DataRelativePathRepairPlanManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        if (
            manifest.SchemaVersion !=
            DataRelativePathRepairPlanManifestRecord
                .CurrentSchemaVersion)
        {
            return
                $"Unsupported plan-manifest schema version " +
                $"{manifest.SchemaVersion}.";
        }

        if (manifest.PlanId == Guid.Empty)
        {
            return
                "The plan manifest requires a non-empty PlanId.";
        }

        if (
            string.IsNullOrWhiteSpace(
                manifest.DataRoot
            ))
        {
            return
                "The plan manifest requires a Data root.";
        }

        if (
            !TryNormalizeRequestedPath(
                manifest.RequestedPath,
                out string requestedPath))
        {
            return
                "The plan manifest requires a valid Data-relative " +
                "requested path.";
        }

        /*
         * DataRelativePathResolver stores RequestedPath canonically:
         * slash-separated, no empty components, and no traversal.
         *
         * Keep the durable manifest in that same canonical form so
         * one semantic path cannot acquire multiple manifest spellings.
         */
        if (
            !string.Equals(
                manifest.RequestedPath,
                requestedPath,
                StringComparison.Ordinal))
        {
            return
                "The plan manifest requested path is not in canonical " +
                "Data-relative form.";
        }

        if (manifest.SourceSnapshot is null)
        {
            return
                "The plan manifest requires source evidence.";
        }

        if (
            manifest.InitialDestinationParentSnapshot is null)
        {
            return
                "The plan manifest requires initial destination-parent " +
                "evidence.";
        }

        if (
            manifest.Operations is null ||
            manifest.Operations.Count == 0)
        {
            return
                "The plan manifest requires at least one operation.";
        }

        if (
            !TryNormalizeAbsolutePath(
                manifest.DataRoot,
                out string dataRoot))
        {
            return
                "The plan Data root must be an absolute valid path.";
        }

        if (
            !TryNormalizeAbsolutePath(
                manifest.SourceSnapshot.PhysicalPath,
                out string sourcePath))
        {
            return
                "The source snapshot path must be an absolute valid path.";
        }

        if (
            !IsStrictlyWithinRoot(
                dataRoot,
                sourcePath))
        {
            return
                "The source snapshot must be inside the plan Data root.";
        }

        if (
            manifest.SourceSnapshot.Size < 0)
        {
            return
                "The source snapshot size cannot be negative.";
        }

        if (
            !IsSha256(
                manifest.SourceSnapshot.Sha256))
        {
            return
                "The source snapshot must contain a 64-character " +
                "SHA-256 value.";
        }

        if (
            manifest.SourceSnapshot.Identity is null ||
            !manifest.SourceSnapshot.Identity.Success)
        {
            return
                "The source snapshot requires usable physical identity.";
        }

        if (
            !TryNormalizeAbsolutePath(
                manifest.SourceSnapshot.Identity.FullPath,
                out string sourceIdentityPath) ||
            !PathEquals(
                sourcePath,
                sourceIdentityPath))
        {
            return
                "The source snapshot path does not match its physical " +
                "identity path.";
        }

        DataRelativePathRepairDestinationParentSnapshot
            initialParent =
                manifest.InitialDestinationParentSnapshot;

        if (
            !TryNormalizeAbsolutePath(
                initialParent.PhysicalPath,
                out string initialParentPath))
        {
            return
                "The initial destination-parent snapshot path must be " +
                "an absolute valid path.";
        }

        if (
            !IsWithinOrEqualRoot(
                dataRoot,
                initialParentPath))
        {
            return
                "The initial destination parent must be inside or equal " +
                "to the plan Data root.";
        }

        if (
            initialParent.Identity is null ||
            !initialParent.Identity.Success)
        {
            return
                "The initial destination-parent snapshot requires usable " +
                "physical identity.";
        }

        if (
            !TryNormalizeAbsolutePath(
                initialParent.Identity.FullPath,
                out string initialParentIdentityPath) ||
            !PathEquals(
                initialParentPath,
                initialParentIdentityPath))
        {
            return
                "The initial destination-parent path does not match its " +
                "physical identity path.";
        }

        if (initialParent.CasefoldEnabled)
        {
            return
                "A direct strict-case repair plan requires the initial " +
                "destination parent to be case-sensitive.";
        }

        var seenJournalNames =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        string expectedParentPath =
            initialParentPath;

        for (
            int index = 0;
            index < manifest.Operations.Count;
            index++)
        {
            DataRelativePathRepairPlanManifestOperation? entry =
                manifest.Operations[index];

            if (entry is null)
            {
                return
                    $"Plan operation {index} is missing.";
            }

            if (entry.Index != index)
            {
                return
                    $"Plan operation index {entry.Index} is not the " +
                    $"expected contiguous index {index}.";
            }

            if (entry.Operation is null)
            {
                return
                    $"Plan operation {index} has no operation record.";
            }

            DataRelativePathRepairPlanOperation operation =
                entry.Operation;

            if (
                operation.Kind is not
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory and not
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile)
            {
                return
                    $"Plan operation {index} has unsupported kind " +
                    $"{operation.Kind}.";
            }

            string expectedJournalName =
                CreateOperationJournalChildName(
                    manifest.PlanId,
                    index,
                    operation.Kind
                );

            if (
                !string.Equals(
                    entry.JournalChildName,
                    expectedJournalName,
                    StringComparison.Ordinal))
            {
                return
                    $"Plan operation {index} journal name does not match " +
                    "the deterministic plan journal name.";
            }

            if (
                !IsValidChildName(
                    entry.JournalChildName))
            {
                return
                    $"Plan operation {index} journal name must identify " +
                    "exactly one direct child.";
            }

            if (
                !seenJournalNames.Add(
                    entry.JournalChildName))
            {
                return
                    $"Plan operation {index} reuses an operation journal " +
                    "name.";
            }

            if (
                !TryNormalizeAbsolutePath(
                    operation.DestinationPath,
                    out string destinationPath))
            {
                return
                    $"Plan operation {index} destination must be an " +
                    "absolute valid path.";
            }

            if (
                !IsStrictlyWithinRoot(
                    dataRoot,
                    destinationPath))
            {
                return
                    $"Plan operation {index} destination must be inside " +
                    "the plan Data root.";
            }

            string? parentPath =
                Path.GetDirectoryName(
                    destinationPath
                );

            if (
                string.IsNullOrEmpty(
                    parentPath) ||
                !TryNormalizeAbsolutePath(
                    parentPath,
                    out string normalizedParentPath))
            {
                return
                    $"Plan operation {index} destination parent is " +
                    "invalid.";
            }

            if (
                !PathEquals(
                    normalizedParentPath,
                    expectedParentPath))
            {
                return
                    $"Plan operation {index} is not a direct child of " +
                    "the parent established by the preceding plan step.";
            }

            bool last =
                index ==
                manifest.Operations.Count - 1;

            switch (operation.Kind)
            {
                case
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory:
                {
                    if (last)
                    {
                        return
                            "A repair plan must terminate with " +
                            "CreateFile.";
                    }

                    if (operation.SourcePath is not null)
                    {
                        return
                            $"Directory operation {index} cannot have " +
                            "a source path.";
                    }

                    expectedParentPath =
                        destinationPath;

                    break;
                }

                case
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile:
                {
                    if (!last)
                    {
                        return
                            "CreateFile must be the final repair-plan " +
                            "operation.";
                    }

                    if (
                        !TryNormalizeAbsolutePath(
                            operation.SourcePath,
                            out string operationSourcePath))
                    {
                        return
                            "The final CreateFile operation requires an " +
                            "absolute source path.";
                    }

                    if (
                        !PathEquals(
                            operationSourcePath,
                            sourcePath))
                    {
                        return
                            "The final CreateFile source path does not " +
                            "match the plan source snapshot.";
                    }

                    /*
                     * RequestedPath is not merely descriptive metadata.
                     *
                     * Bind it to the exact final destination represented
                     * by this immutable operation chain. Normalize the
                     * filesystem-derived relative path using the same
                     * component semantics as DataRelativePathResolver,
                     * then compare the canonical slash-separated form.
                     */
                    string finalRelativePath;

                    try
                    {
                        finalRelativePath =
                            Path.GetRelativePath(
                                dataRoot,
                                destinationPath
                            );
                    }
                    catch (
                        Exception ex)
                        when (
                            ex is ArgumentException or
                            NotSupportedException or
                            PathTooLongException)
                    {
                        return
                            "The final CreateFile destination could not " +
                            "be expressed relative to the plan Data root.";
                    }

                    if (
                        !TryNormalizeRequestedPath(
                            finalRelativePath,
                            out string finalRequestedPath) ||
                        !string.Equals(
                            requestedPath,
                            finalRequestedPath,
                            StringComparison.Ordinal))
                    {
                        return
                            "The plan requested path does not match the " +
                            "final CreateFile destination.";
                    }

                    break;
                }

                default:
                    return
                        $"Plan operation {index} has unsupported kind.";
            }
        }

        return null;
    }

    public static string CreateOperationJournalChildName(
        Guid planId,
        int index,
        DataRelativePathRepairPlanOperationKind kind)
    {
        string kindToken =
            kind switch
            {
                DataRelativePathRepairPlanOperationKind
                    .CreateDirectory =>
                        "directory",

                DataRelativePathRepairPlanOperationKind
                    .CreateFile =>
                        "file",

                _ =>
                    "invalid"
            };

        return
            $".casecompat-plan-{planId:N}-" +
            $"op-{index:D4}-{kindToken}.json";
    }

    private static bool TryNormalizeRequestedPath(
        string? path,
        out string normalized)
    {
        normalized =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                path
            ) ||
            path.Contains('\0') ||
            Path.IsPathRooted(
                path
            ) ||
            path.StartsWith('\\'))
        {
            return false;
        }

        string[] components =
            path.Split(
                ['/', '\\'],
                StringSplitOptions.RemoveEmptyEntries
            );

        if (
            components.Length == 0 ||
            components.Any(component =>
                component is "." or ".."
            ))
        {
            return false;
        }

        normalized =
            string.Join(
                '/',
                components
            );

        return true;
    }

    private static bool IsSha256(
        string? value)
    {
        return
            value is not null &&
            value.Length == 64 &&
            value.All(
                Uri.IsHexDigit
            );
    }

    private static bool IsValidChildName(
        string? childName)
    {
        if (
            string.IsNullOrEmpty(
                childName
            ) ||
            childName is "." or "..")
        {
            return false;
        }

        return
            !childName.Contains('/') &&
            !childName.Contains('\\') &&
            !childName.Contains('\0');
    }

    private static bool TryNormalizeAbsolutePath(
        string? path,
        out string normalized)
    {
        normalized =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                path
            ) ||
            path.Contains('\0') ||
            !Path.IsPathFullyQualified(
                path
            ))
        {
            return false;
        }

        try
        {
            normalized =
                TrimTrailingSeparators(
                    Path.GetFullPath(
                        path
                    )
                );

            return true;
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

    private static string TrimTrailingSeparators(
        string path)
    {
        string root =
            Path.GetPathRoot(
                path
            ) ??
            string.Empty;

        if (
            string.Equals(
                path,
                root,
                StringComparison.Ordinal))
        {
            return path;
        }

        return path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
    }

    private static bool PathEquals(
        string left,
        string right)
    {
        return string.Equals(
            left,
            right,
            StringComparison.Ordinal
        );
    }

    private static bool IsWithinOrEqualRoot(
        string root,
        string path)
    {
        if (
            PathEquals(
                root,
                path))
        {
            return true;
        }

        return IsStrictlyWithinRoot(
            root,
            path
        );
    }

    private static bool IsStrictlyWithinRoot(
        string root,
        string path)
    {
        string relative;

        try
        {
            relative =
                Path.GetRelativePath(
                    root,
                    path
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

        if (
            relative == "." ||
            relative == ".." ||
            Path.IsPathFullyQualified(
                relative))
        {
            return false;
        }

        string parentPrefix =
            ".." +
            Path.DirectorySeparatorChar;

        string alternateParentPrefix =
            ".." +
            Path.AltDirectorySeparatorChar;

        return
            !relative.StartsWith(
                parentPrefix,
                StringComparison.Ordinal
            ) &&
            !relative.StartsWith(
                alternateParentPrefix,
                StringComparison.Ordinal
            );
    }
}
