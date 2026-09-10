using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Independent durable child shape for the targeted consumer-authoritative
// repair lifecycle.
//
// This is intentionally a new targeted schema-v1 record. It is not a new
// version of the legacy repair-plan manifest.
//
// SourceInodeGeneration is durable because SourceSnapshot predates
// generation-aware incarnation evidence. Persisting only the snapshot would
// lose the planning-to-apply source-incarnation boundary once the aggregate
// sidecar is removed.
public sealed record
    DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
        int SchemaVersion,
        Guid PlanId,
        DateTimeOffset CreatedUtc,
        string DataRoot,
        string RequestedPath,
        DataRelativePathRepairSourceSnapshot SourceSnapshot,
        uint SourceInodeGeneration,
        DataRelativePathRepairDestinationParentSnapshot
            InitialDestinationParentSnapshot,
        IReadOnlyList<DataRelativePathRepairPlanOperation> Operations,
        IReadOnlyList<DataRelativePathRepairDirectoryRenameSource>
            DirectoryRenameSources,
        IReadOnlyList<DataRelativePathRepairAliasSource>
            AliasSources
    )
{
    public const int SchemaVersion1 =
        1;

    public const int CurrentSchemaVersion =
        SchemaVersion1;
}

public enum
    DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
{
    Created,
    InvalidInput,
    AdmissionRejected
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation(
        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
            State,
        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult?
            Admission,
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord? Record,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                .Created &&
        Admission?.Admitted == true &&
        Record is not null;
}

// C4E-4D2C1 targeted persistence-boundary projection.
//
// It deliberately performs C4E-4D2B admission again at the exact boundary
// where an in-memory repair becomes eligible for durable representation.
//
// The resulting record contains no resolver traversal evidence, provider
// candidate set, namespace coverage evidence, aggregate sidecar evidence,
// batch authority, execution authority, or mutation authority.
public static class
    DataRelativePathTargetedConsumerCaseRepairDurablePlan
{
    public static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
        Create(
            LinuxNoFollowPathHandle trustedDataRoot,
            Guid planId,
            DateTimeOffset createdUtc,
            DataRelativePathTargetedConsumerCaseRepairPlanProjection
                projection,
            IReadOnlySet<string>? contestedAncestorPrefixes = null,
            LinuxNoFollowPathHandle? aliasesDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(
            trustedDataRoot
        );

        ArgumentNullException.ThrowIfNull(
            projection
        );

        if (planId == Guid.Empty)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                    .InvalidInput,
                error:
                    "A targeted durable repair requires a non-empty PlanId."
            );
        }

        DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult
            admission =
                DataRelativePathTargetedConsumerCaseRepairPlanAdmission
                    .Admit(
                        trustedDataRoot,
                        projection,
                        contestedAncestorPrefixes,
                        aliasesDirectory
                    );

        if (
            !admission.Admitted ||
            admission.RevalidatedProjection is null ||
            admission.SourceGenerationBinding?.Success != true ||
            admission.SourceGenerationBinding.SourceInodeGeneration is null)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                    .AdmissionRejected,
                admission,
                error:
                    admission.Error ??
                    admission.State.ToString()
            );
        }

        DataRelativePathTargetedConsumerCaseRepairPlanProjection canonical =
            admission.RevalidatedProjection;

        uint sourceGeneration =
            admission.SourceGenerationBinding
                .SourceInodeGeneration
                .Value;

        if (
            sourceGeneration !=
            canonical.Candidate.SourceInodeGeneration)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                    .AdmissionRejected,
                admission,
                error:
                    "Fresh persistence-boundary source generation does not " +
                    "match the canonical targeted candidate generation."
            );
        }

        if (canonical.DestinationParentSnapshot is null)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                    .InvalidInput,
                admission,
                error:
                    "The canonical targeted plan lacks destination-parent " +
                    "evidence."
            );
        }

        var record =
            new DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
                SchemaVersion:
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                        .CurrentSchemaVersion,
                PlanId:
                    planId,
                CreatedUtc:
                    createdUtc,
                DataRoot:
                    trustedDataRoot.FullPath,
                RequestedPath:
                    canonical.Candidate.AuthoritativeRequestedPath,
                SourceSnapshot:
                    canonical.Candidate.SourceSnapshot,
                SourceInodeGeneration:
                    sourceGeneration,
                InitialDestinationParentSnapshot:
                    canonical.DestinationParentSnapshot,
                Operations:
                    canonical.Operations.ToArray(),
                DirectoryRenameSources:
                    canonical.DirectoryRenameSources.ToArray(),
                AliasSources:
                    canonical.AliasSources.ToArray()
            );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                    .InvalidInput,
                admission,
                error:
                    validationError
            );
        }

        return new(
            State:
                DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                    .Created,
            Admission:
                admission,
            Record:
                record,
            Error:
                null
        );
    }

    public static string? Validate(
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        if (
            record.SchemaVersion !=
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                .SchemaVersion1)
        {
            return
                "Unsupported targeted durable-plan schema version.";
        }

        if (record.PlanId == Guid.Empty)
        {
            return
                "The targeted durable plan requires a non-empty PlanId.";
        }

        if (!TryCanonicalAbsolutePath(
                record.DataRoot,
                out string dataRoot))
        {
            return
                "The targeted durable plan Data root must be a canonical " +
                "absolute path.";
        }

        if (!TryCanonicalRequestedPath(
                record.RequestedPath,
                out string requestedPath))
        {
            return
                "The targeted durable plan requires a canonical " +
                "Data-relative requested path.";
        }

        string? sourceError =
            ValidateSourceSnapshot(
                dataRoot,
                record.SourceSnapshot,
                out string sourcePath,
                out string sourceRelative
            );

        if (sourceError is not null)
        {
            return sourceError;
        }

        WindowsLogicalPath requestedLogical;
        WindowsLogicalPath sourceLogical;

        try
        {
            requestedLogical =
                WindowsLogicalPath.FromRelativePath(
                    requestedPath
                );

            sourceLogical =
                WindowsLogicalPath.FromRelativePath(
                    sourceRelative
                );
        }
        catch (ArgumentException ex)
        {
            return
                "The targeted durable source/requested logical path " +
                $"evidence is invalid: {ex.Message}";
        }

        if (
            requestedLogical != sourceLogical ||
            string.Equals(
                requestedPath,
                sourceRelative,
                StringComparison.Ordinal))
        {
            return
                "The targeted durable source must be Windows-logically " +
                "equivalent to, but ordinally different from, the " +
                "authoritative requested path.";
        }

        string? parentError =
            ValidateDestinationParentSnapshot(
                dataRoot,
                record.InitialDestinationParentSnapshot,
                out string destinationParentPath,
                out string destinationParentRelative
            );

        if (parentError is not null)
        {
            return parentError;
        }

        string[] requestedComponents =
            requestedPath.Split('/');

        string[] parentComponents;

        if (destinationParentRelative.Length == 0)
        {
            parentComponents =
                [];
        }
        else
        {
            parentComponents =
                destinationParentRelative.Split('/');
        }

        if (
            parentComponents.Length >=
            requestedComponents.Length)
        {
            return
                "The initial destination parent must end before the final " +
                "requested file component.";
        }

        for (
            int index = 0;
            index < parentComponents.Length;
            index++)
        {
            if (!string.Equals(
                    parentComponents[index],
                    requestedComponents[index],
                    StringComparison.Ordinal))
            {
                return
                    "The initial destination parent is not the exact " +
                    "authoritative requested prefix.";
            }
        }

        if (
            record.Operations is null ||
            record.Operations.Count !=
                requestedComponents.Length -
                parentComponents.Length)
        {
            return
                "The targeted durable operation count does not exactly " +
                "represent the missing authoritative requested suffix.";
        }

        string expectedParent =
            destinationParentPath;

        for (
            int operationIndex = 0;
            operationIndex < record.Operations.Count;
            operationIndex++)
        {
            DataRelativePathRepairPlanOperation? operation =
                record.Operations[
                    operationIndex
                ];

            if (operation is null)
            {
                return
                    "The targeted durable operation sequence contains a " +
                    "null operation.";
            }

            int componentIndex =
                parentComponents.Length +
                operationIndex;

            bool isFinal =
                operationIndex ==
                record.Operations.Count - 1;

            bool matchesExpectedKind =
                isFinal
                    ? operation.Kind ==
                        DataRelativePathRepairPlanOperationKind.CreateFile
                    : operation.Kind is
                        DataRelativePathRepairPlanOperationKind
                            .CreateDirectory or
                        DataRelativePathRepairPlanOperationKind
                            .CreateAliasSymlink;

            if (!matchesExpectedKind)
            {
                return
                    "The targeted durable operation sequence must contain " +
                    "zero or more CreateDirectory/CreateAliasSymlink " +
                    "operations followed by exactly one final CreateFile " +
                    "operation.";
            }

            string expectedDestination;

            try
            {
                expectedDestination =
                    Path.GetFullPath(
                        Path.Combine(
                            expectedParent,
                            requestedComponents[
                                componentIndex
                            ]
                        )
                    );
            }
            catch (Exception ex)
                when (
                    ex is ArgumentException or
                    NotSupportedException or
                    PathTooLongException)
            {
                return
                    "The targeted durable requested suffix produced an " +
                    $"invalid destination path: {ex.Message}";
            }

            if (
                !TryCanonicalAbsolutePath(
                    operation.DestinationPath,
                    out string operationDestination) ||
                !string.Equals(
                    operationDestination,
                    expectedDestination,
                    StringComparison.Ordinal) ||
                !TryRelativeUnderRoot(
                    dataRoot,
                    operationDestination,
                    allowRoot:
                        false,
                    out _))
            {
                return
                    "A targeted durable operation destination does not " +
                    "match the exact authoritative requested suffix.";
            }

            if (isFinal)
            {
                if (
                    !string.Equals(
                        operation.SourcePath,
                        sourcePath,
                        StringComparison.Ordinal))
                {
                    return
                        "The final targeted CreateFile operation must bind " +
                        "to the exact durable source snapshot path.";
                }
            }
            else if (
                operation.Kind ==
                DataRelativePathRepairPlanOperationKind
                    .CreateAliasSymlink)
            {
                string? aliasSourceError =
                    ValidateAliasSource(
                        dataRoot,
                        operationDestination,
                        operation.SourcePath,
                        record.AliasSources
                    );

                if (aliasSourceError is not null)
                {
                    return aliasSourceError;
                }
            }
            else if (operation.SourcePath is not null)
            {
                string? renameSourceError =
                    ValidateDirectoryRenameSource(
                        dataRoot,
                        operationDestination,
                        operation.SourcePath,
                        record.DirectoryRenameSources
                    );

                if (renameSourceError is not null)
                {
                    return renameSourceError;
                }
            }

            expectedParent =
                operationDestination;
        }

        int expectedRenameSourceCount =
            record.Operations
                .Take(
                    record.Operations.Count - 1
                )
                .Count(
                    op =>
                        op.Kind ==
                        DataRelativePathRepairPlanOperationKind
                            .CreateDirectory &&
                        op.SourcePath is not null
                );

        if (
            record.DirectoryRenameSources is null ||
            record.DirectoryRenameSources.Count !=
                expectedRenameSourceCount)
        {
            return
                "The targeted durable plan's directory-rename sources do " +
                "not exactly match its rename-flavored CreateDirectory " +
                "operations.";
        }

        int expectedAliasSourceCount =
            record.Operations
                .Take(
                    record.Operations.Count - 1
                )
                .Count(
                    op =>
                        op.Kind ==
                        DataRelativePathRepairPlanOperationKind
                            .CreateAliasSymlink
                );

        if (
            record.AliasSources is null ||
            record.AliasSources.Count !=
                expectedAliasSourceCount)
        {
            return
                "The targeted durable plan's alias sources do not " +
                "exactly match its CreateAliasSymlink operations.";
        }

        return null;
    }

    private static string? ValidateDirectoryRenameSource(
        string dataRoot,
        string operationDestination,
        string operationSourcePath,
        IReadOnlyList<DataRelativePathRepairDirectoryRenameSource>?
            directoryRenameSources)
    {
        DataRelativePathRepairDirectoryRenameSource? match =
            directoryRenameSources?
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.DestinationPath,
                            operationDestination,
                            StringComparison.Ordinal
                        )
                );

        if (match is null)
        {
            return
                "A rename-flavored targeted CreateDirectory operation has " +
                "no matching directory-rename source evidence.";
        }

        if (
            !TryCanonicalAbsolutePath(
                match.PhysicalPath,
                out string physicalPath) ||
            !string.Equals(
                physicalPath,
                operationSourcePath,
                StringComparison.Ordinal) ||
            !TryRelativeUnderRoot(
                dataRoot,
                physicalPath,
                allowRoot:
                    false,
                out _))
        {
            return
                "A targeted directory-rename source's physical path does " +
                "not exactly match its operation's source path beneath " +
                "the Data root.";
        }

        LinuxFileIdentityResult? identity =
            match.Identity;

        if (
            identity is null ||
            !identity.Success ||
            identity.DeviceMajor is null ||
            identity.DeviceMinor is null ||
            identity.Inode is null ||
            identity.MountId is null ||
            !string.Equals(
                identity.FullPath,
                physicalPath,
                StringComparison.Ordinal))
        {
            return
                "A targeted directory-rename source requires complete " +
                "physical identity bound to its exact physical path.";
        }

        return null;
    }

    private static string? ValidateAliasSource(
        string dataRoot,
        string operationDestination,
        string? operationSourcePath,
        IReadOnlyList<DataRelativePathRepairAliasSource>?
            aliasSources)
    {
        if (operationSourcePath is null)
        {
            return
                "A CreateAliasSymlink operation requires a source path " +
                "identifying the real directory it points at.";
        }

        DataRelativePathRepairAliasSource? match =
            aliasSources?
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.DestinationPath,
                            operationDestination,
                            StringComparison.Ordinal
                        )
                );

        if (match is null)
        {
            return
                "A CreateAliasSymlink operation has no matching alias " +
                "source evidence.";
        }

        if (
            !TryCanonicalAbsolutePath(
                match.PhysicalPath,
                out string physicalPath) ||
            !string.Equals(
                physicalPath,
                operationSourcePath,
                StringComparison.Ordinal) ||
            !TryRelativeUnderRoot(
                dataRoot,
                physicalPath,
                allowRoot:
                    false,
                out _))
        {
            return
                "An alias source's physical path does not exactly match " +
                "its operation's source path beneath the Data root.";
        }

        LinuxFileIdentityResult? identity =
            match.Identity;

        if (
            identity is null ||
            !identity.Success ||
            identity.DeviceMajor is null ||
            identity.DeviceMinor is null ||
            identity.Inode is null ||
            identity.MountId is null ||
            !string.Equals(
                identity.FullPath,
                physicalPath,
                StringComparison.Ordinal))
        {
            return
                "An alias source requires complete physical identity " +
                "bound to its exact physical path.";
        }

        if (
            string.Equals(
                Path.GetFileName(
                    operationDestination
                ),
                Path.GetFileName(
                    physicalPath
                ),
                StringComparison.Ordinal))
        {
            return
                "An alias's link name and target name must not be " +
                "identical.";
        }

        return null;
    }

    private static string? ValidateSourceSnapshot(
        string dataRoot,
        DataRelativePathRepairSourceSnapshot? snapshot,
        out string sourcePath,
        out string sourceRelative)
    {
        sourcePath =
            string.Empty;

        sourceRelative =
            string.Empty;

        if (snapshot is null)
        {
            return
                "The targeted durable plan requires source evidence.";
        }

        if (
            !TryCanonicalAbsolutePath(
                snapshot.PhysicalPath,
                out sourcePath) ||
            !TryRelativeUnderRoot(
                dataRoot,
                sourcePath,
                allowRoot:
                    false,
                out sourceRelative))
        {
            return
                "The targeted durable source path must be a canonical " +
                "absolute file path beneath the Data root.";
        }

        if (snapshot.Size < 0)
        {
            return
                "The targeted durable source size cannot be negative.";
        }

        if (!IsSha256(
                snapshot.Sha256))
        {
            return
                "The targeted durable source requires a 64-character " +
                "SHA-256 value.";
        }

        LinuxFileIdentityResult? identity =
            snapshot.Identity;

        if (
            identity is null ||
            !identity.Success ||
            identity.DeviceMajor is null ||
            identity.DeviceMinor is null ||
            identity.Inode is null ||
            identity.MountId is null ||
            !string.Equals(
                identity.FullPath,
                sourcePath,
                StringComparison.Ordinal))
        {
            return
                "The targeted durable source snapshot requires complete " +
                "physical identity bound to its exact physical path.";
        }

        return null;
    }

    private static string? ValidateDestinationParentSnapshot(
        string dataRoot,
        DataRelativePathRepairDestinationParentSnapshot? snapshot,
        out string parentPath,
        out string parentRelative)
    {
        parentPath =
            string.Empty;

        parentRelative =
            string.Empty;

        if (snapshot is null)
        {
            return
                "The targeted durable plan requires initial " +
                "destination-parent evidence.";
        }

        if (
            !TryCanonicalAbsolutePath(
                snapshot.PhysicalPath,
                out parentPath) ||
            !TryRelativeUnderRoot(
                dataRoot,
                parentPath,
                allowRoot:
                    true,
                out parentRelative))
        {
            return
                "The targeted durable destination parent must be a " +
                "canonical path at or beneath the Data root.";
        }

        LinuxFileIdentityResult? identity =
            snapshot.Identity;

        if (
            identity is null ||
            !identity.Success ||
            identity.DeviceMajor is null ||
            identity.DeviceMinor is null ||
            identity.Inode is null ||
            identity.MountId is null ||
            !string.Equals(
                identity.FullPath,
                parentPath,
                StringComparison.Ordinal))
        {
            return
                "The targeted durable destination-parent snapshot requires " +
                "complete physical identity bound to its exact path.";
        }

        if (snapshot.CasefoldEnabled)
        {
            return
                "The targeted durable destination parent must be " +
                "case-sensitive.";
        }

        return null;
    }

    private static bool TryCanonicalRequestedPath(
        string? value,
        out string requestedPath)
    {
        requestedPath =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                value) ||
            Path.IsPathRooted(
                value) ||
            value.Contains('\\') ||
            value.Contains('\0'))
        {
            return false;
        }

        string[] components =
            value.Split(
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

        try
        {
            _ =
                WindowsLogicalPath.FromRelativePath(
                    value
                );
        }
        catch (ArgumentException)
        {
            return false;
        }

        requestedPath =
            string.Join(
                '/',
                components
            );

        return string.Equals(
            requestedPath,
            value,
            StringComparison.Ordinal
        );
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
                character =>
                    (
                        character >= '0' &&
                        character <= '9'
                    ) ||
                    (
                        character >= 'a' &&
                        character <= 'f'
                    ) ||
                    (
                        character >= 'A' &&
                        character <= 'F'
                    )
            );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
        Result(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanCreationState
                state,
            DataRelativePathTargetedConsumerCaseRepairPlanAdmissionResult?
                admission = null,
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord?
                record = null,
            string? error = null)
    {
        return new(
            State:
                state,
            Admission:
                admission,
            Record:
                record,
            Error:
                error
        );
    }
}
