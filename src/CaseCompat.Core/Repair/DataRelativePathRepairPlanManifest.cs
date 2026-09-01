using CaseCompat.Core.Analysis;
using CaseCompat.Core.Resolution;

namespace CaseCompat.Core.Repair;

public sealed record DataRelativePathRepairPlanManifestOperation(
    int Index,
    DataRelativePathRepairPlanOperation Operation,
    string JournalChildName
);

public enum DataRelativePathRepairPlanResolvedPrefixStepKind
{
    ExactSpelling,
    CasefoldEquivalent
}

public sealed record DataRelativePathRepairPlanResolvedPrefixStep(
    int ComponentIndex,
    string RequestedComponent,
    string ParentPhysicalPath,
    bool? ParentCasefoldEnabled,
    DataRelativePathRepairPlanResolvedPrefixStepKind Kind,
    string SelectedPhysicalName,
    IReadOnlyList<string> EquivalentPhysicalNames
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
    /*
     * Schema v1 does not contain resolved-prefix evidence.
     *
     * Keep this optional and omit it from JSON while null so adding the
     * field does not change newly serialized schema-v1 manifests and old
     * schema-v1 JSON can continue to deserialize with a null value.
     */
    [System.Text.Json.Serialization.JsonIgnore(
        Condition =
            System.Text.Json.Serialization.JsonIgnoreCondition
                .WhenWritingNull)]
    public IReadOnlyList<
        DataRelativePathRepairPlanResolvedPrefixStep
    >? ResolvedPrefixSteps
    {
        get;
        init;
    }

    public const int SchemaVersion1 =
        1;

    public const int SchemaVersion2 =
        2;

    /*
     * New resolver-derived repair plans use schema v2.
     *
     * The legacy Create(...) entry point remains explicitly schema v1
     * so old-format construction and compatibility tests retain their
     * original semantics.
     */
    public const int CurrentSchemaVersion =
        SchemaVersion2;
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
        /*
         * Legacy schema-v1 creation is retained so existing tests,
         * fixtures, and persisted v1 semantics remain independently
         * representable.
         */
        return CreateCore(
            schemaVersion:
                DataRelativePathRepairPlanManifestRecord
                    .SchemaVersion1,
            planId,
            createdUtc,
            dataRoot,
            requestedPath,
            sourceSnapshot,
            initialDestinationParentSnapshot,
            operations,
            resolvedPrefixSteps:
                null
        );
    }

    public static DataRelativePathRepairPlanManifestCreation
        CreateFromResolution(
            Guid planId,
            DateTimeOffset createdUtc,
            DataRelativePathResolution resolution,
            DataRelativePathRepairSourceSnapshot sourceSnapshot,
            DataRelativePathRepairDestinationParentSnapshot
                initialDestinationParentSnapshot,
            IReadOnlyList<DataRelativePathRepairPlanOperation>
                operations)
    {
        ArgumentNullException.ThrowIfNull(
            resolution
        );

        DataRelativePathCaseMismatchTopologyState topologyState =
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                );

        if (
            topologyState !=
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch)
        {
            return new(
                State:
                    DataRelativePathRepairPlanManifestCreationState
                        .InvalidInput,
                Manifest:
                    null,
                Error:
                    "Schema-v2 manifest creation requires a " +
                    "DirectStrictCaseMismatch resolution; actual " +
                    $"topology state was {topologyState}."
            );
        }

        if (
            resolution.FailedComponentIndex is not int
                failedIndex ||
            failedIndex < 0)
        {
            return new(
                State:
                    DataRelativePathRepairPlanManifestCreationState
                        .InvalidInput,
                Manifest:
                    null,
                Error:
                    "Schema-v2 manifest creation requires a valid " +
                    "failed component index."
            );
        }

        var resolvedPrefixSteps =
            new DataRelativePathRepairPlanResolvedPrefixStep[
                failedIndex
            ];

        for (
            int index = 0;
            index < failedIndex;
            index++)
        {
            PathResolutionStep[] matchingSteps =
                resolution.Steps
                    .Where(step =>
                        step.ComponentIndex ==
                        index
                    )
                    .ToArray();

            if (matchingSteps.Length != 1)
            {
                return new(
                    State:
                        DataRelativePathRepairPlanManifestCreationState
                            .InvalidInput,
                    Manifest:
                        null,
                    Error:
                        "Schema-v2 manifest creation requires exactly " +
                        "one resolved-prefix traversal step for every " +
                        "component before the strict mismatch."
                );
            }

            PathResolutionStep step =
                matchingSteps[0];

            if (
                string.IsNullOrEmpty(
                    step.SelectedPhysicalName
                ))
            {
                return new(
                    State:
                        DataRelativePathRepairPlanManifestCreationState
                            .InvalidInput,
                    Manifest:
                        null,
                    Error:
                        "Schema-v2 manifest creation requires every " +
                        "resolved-prefix traversal step to have a " +
                        "selected physical name."
                );
            }

            DataRelativePathRepairPlanResolvedPrefixStepKind
                durableKind;

            if (
                step.Kind ==
                PathResolutionStepKind
                    .ExactSpelling)
            {
                durableKind =
                    DataRelativePathRepairPlanResolvedPrefixStepKind
                        .ExactSpelling;
            }
            else if (
                step.Kind ==
                PathResolutionStepKind
                    .CasefoldEquivalent)
            {
                if (
                    step.ParentCasefoldEnabled != true ||
                    !string.IsNullOrWhiteSpace(
                        step.ParentCasefoldError
                    ))
                {
                    return new(
                        State:
                            DataRelativePathRepairPlanManifestCreationState
                                .InvalidInput,
                        Manifest:
                            null,
                        Error:
                            "Schema-v2 CasefoldEquivalent creation " +
                            "requires successful evidence that the " +
                            "physical parent was casefold-enabled."
                    );
                }

                durableKind =
                    DataRelativePathRepairPlanResolvedPrefixStepKind
                        .CasefoldEquivalent;
            }
            else
            {
                return new(
                    State:
                        DataRelativePathRepairPlanManifestCreationState
                            .InvalidInput,
                    Manifest:
                        null,
                    Error:
                        "Schema-v2 manifest creation encountered an " +
                        "unsupported resolved-prefix traversal state."
                );
            }

            resolvedPrefixSteps[index] =
                new(
                    ComponentIndex:
                        step.ComponentIndex,
                    RequestedComponent:
                        step.RequestedComponent,
                    ParentPhysicalPath:
                        step.ParentPhysicalPath,
                    ParentCasefoldEnabled:
                        step.ParentCasefoldEnabled,
                    Kind:
                        durableKind,
                    SelectedPhysicalName:
                        step.SelectedPhysicalName,
                    EquivalentPhysicalNames:
                        step.EquivalentPhysicalNames
                            .ToArray()
                );
        }

        return CreateCore(
            schemaVersion:
                DataRelativePathRepairPlanManifestRecord
                    .SchemaVersion2,
            planId,
            createdUtc,
            resolution.DataRoot,
            resolution.RequestedPath,
            sourceSnapshot,
            initialDestinationParentSnapshot,
            operations,
            resolvedPrefixSteps
        );
    }

    private static DataRelativePathRepairPlanManifestCreation
        CreateCore(
            int schemaVersion,
            Guid planId,
            DateTimeOffset createdUtc,
            string dataRoot,
            string requestedPath,
            DataRelativePathRepairSourceSnapshot sourceSnapshot,
            DataRelativePathRepairDestinationParentSnapshot
                initialDestinationParentSnapshot,
            IReadOnlyList<DataRelativePathRepairPlanOperation>
                operations,
            IReadOnlyList<
                DataRelativePathRepairPlanResolvedPrefixStep
            >? resolvedPrefixSteps)
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
                    schemaVersion,
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
            )
            {
                ResolvedPrefixSteps =
                    resolvedPrefixSteps is null
                        ? null
                        : resolvedPrefixSteps.ToArray()
            };

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
                    .SchemaVersion1 &&
            manifest.SchemaVersion !=
                DataRelativePathRepairPlanManifestRecord
                    .SchemaVersion2)
        {
            return
                $"Unsupported plan-manifest schema version " +
                $"{manifest.SchemaVersion}.";
        }

        if (
            manifest.SchemaVersion ==
                DataRelativePathRepairPlanManifestRecord
                    .SchemaVersion1 &&
            manifest.ResolvedPrefixSteps is not null)
        {
            return
                "Plan-manifest schema version 1 must not contain " +
                "resolved-prefix evidence.";
        }

        if (
            manifest.SchemaVersion ==
                DataRelativePathRepairPlanManifestRecord
                    .SchemaVersion2 &&
            manifest.ResolvedPrefixSteps is null)
        {
            return
                "Plan-manifest schema version 2 requires " +
                "resolved-prefix evidence.";
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
                            out string finalRequestedPath))
                    {
                        return
                            "The plan requested path does not match the " +
                            "final CreateFile destination.";
                    }

                    string? requestedPathBindingError =
                        ValidateRequestedPathBinding(
                            manifest,
                            requestedPath,
                            finalRequestedPath,
                            dataRoot,
                            initialParentPath
                        );

                    if (requestedPathBindingError is not null)
                    {
                        return
                            requestedPathBindingError;
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

    private static string? ValidateRequestedPathBinding(
        DataRelativePathRepairPlanManifestRecord manifest,
        string requestedPath,
        string finalRequestedPath,
        string dataRoot,
        string initialParentPath)
    {
        const string mismatchError =
            "The plan requested path does not match the " +
            "final CreateFile destination.";

        /*
         * Schema v1 retains its original meaning exactly.
         *
         * No resolved-prefix evidence exists, so RequestedPath must
         * remain ordinally identical to the complete physical operation
         * destination relative to Data.
         */
        if (
            manifest.SchemaVersion ==
            DataRelativePathRepairPlanManifestRecord
                .SchemaVersion1)
        {
            return
                string.Equals(
                    requestedPath,
                    finalRequestedPath,
                    StringComparison.Ordinal)
                    ? null
                    : mismatchError;
        }

        if (
            manifest.SchemaVersion !=
            DataRelativePathRepairPlanManifestRecord
                .SchemaVersion2)
        {
            return
                $"Unsupported plan-manifest schema version " +
                $"{manifest.SchemaVersion}.";
        }

        IReadOnlyList<
            DataRelativePathRepairPlanResolvedPrefixStep
        >? prefixSteps =
            manifest.ResolvedPrefixSteps;

        if (prefixSteps is null)
        {
            return
                "Plan-manifest schema version 2 requires " +
                "resolved-prefix evidence.";
        }

        string[] requestedComponents =
            requestedPath.Split('/');

        string[] finalComponents =
            finalRequestedPath.Split('/');

        if (
            requestedComponents.Length !=
            finalComponents.Length)
        {
            return
                mismatchError;
        }

        string expectedParent;

        try
        {
            expectedParent =
                Path.GetFullPath(
                    dataRoot
                );
        }
        catch (Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return
                "The schema-v2 resolved-prefix Data root could not " +
                "be normalized.";
        }

        /*
         * Every persisted prefix step must be contiguous from component
         * zero and must reproduce the exact physical hierarchy that ends
         * at InitialDestinationParentSnapshot.
         */
        for (
            int index = 0;
            index < prefixSteps.Count;
            index++)
        {
            DataRelativePathRepairPlanResolvedPrefixStep? step =
                prefixSteps[index];

            if (step is null)
            {
                return
                    "Schema-v2 resolved-prefix evidence contains a " +
                    "null step.";
            }

            if (step.ComponentIndex != index)
            {
                return
                    "Schema-v2 resolved-prefix evidence is not " +
                    "contiguous from component zero.";
            }

            if (index >= requestedComponents.Length)
            {
                return
                    "Schema-v2 resolved-prefix evidence extends beyond " +
                    "the requested path.";
            }

            if (
                !string.Equals(
                    step.RequestedComponent,
                    requestedComponents[index],
                    StringComparison.Ordinal))
            {
                return
                    mismatchError;
            }

            if (
                string.IsNullOrEmpty(
                    step.SelectedPhysicalName
                ))
            {
                return
                    "Schema-v2 resolved-prefix evidence requires a " +
                    "selected physical name.";
            }

            if (
                !PathEquals(
                    step.ParentPhysicalPath,
                    expectedParent))
            {
                return
                    "Schema-v2 resolved-prefix evidence does not match " +
                    "the physical parent hierarchy.";
            }

            if (
                step.EquivalentPhysicalNames is null ||
                step.EquivalentPhysicalNames.Count != 1 ||
                !string.Equals(
                    step.EquivalentPhysicalNames[0],
                    step.SelectedPhysicalName,
                    StringComparison.Ordinal))
            {
                return
                    "Schema-v2 resolved-prefix evidence requires " +
                    "exactly one equivalent physical name matching " +
                    "the selected physical name.";
            }

            if (
                !string.Equals(
                    finalComponents[index],
                    step.SelectedPhysicalName,
                    StringComparison.Ordinal))
            {
                return
                    mismatchError;
            }

            switch (step.Kind)
            {
                case
                    DataRelativePathRepairPlanResolvedPrefixStepKind
                        .ExactSpelling:
                {
                    if (
                        !string.Equals(
                            step.RequestedComponent,
                            step.SelectedPhysicalName,
                            StringComparison.Ordinal))
                    {
                        return
                            mismatchError;
                    }

                    break;
                }

                case
                    DataRelativePathRepairPlanResolvedPrefixStepKind
                        .CasefoldEquivalent:
                {
                    if (step.ParentCasefoldEnabled != true)
                    {
                        return
                            "Schema-v2 CasefoldEquivalent evidence " +
                            "requires a casefold-enabled physical parent.";
                    }

                    /*
                     * If spelling were already ordinally exact, the
                     * resolver would have emitted ExactSpelling rather
                     * than CasefoldEquivalent.
                     */
                    if (
                        string.Equals(
                            step.RequestedComponent,
                            step.SelectedPhysicalName,
                            StringComparison.Ordinal))
                    {
                        return
                            "Schema-v2 CasefoldEquivalent evidence must " +
                            "represent different physical spelling.";
                    }

                    bool logicallyEquivalent;

                    try
                    {
                        logicallyEquivalent =
                            WindowsLogicalPath.FromRelativePath(
                                step.RequestedComponent
                            ) ==
                            WindowsLogicalPath.FromRelativePath(
                                step.SelectedPhysicalName
                            );
                    }
                    catch (ArgumentException)
                    {
                        return
                            "Schema-v2 CasefoldEquivalent evidence " +
                            "contains an invalid logical component.";
                    }

                    if (!logicallyEquivalent)
                    {
                        return
                            "Schema-v2 CasefoldEquivalent evidence is " +
                            "not Windows-logically equivalent.";
                    }

                    break;
                }

                default:
                    return
                        "Schema-v2 resolved-prefix evidence contains an " +
                        "unsupported step kind.";
            }

            try
            {
                expectedParent =
                    Path.GetFullPath(
                        Path.Combine(
                            expectedParent,
                            step.SelectedPhysicalName
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
                    "Schema-v2 resolved-prefix evidence produced an " +
                    "invalid physical hierarchy.";
            }
        }

        /*
         * Prefix evidence is complete only if replaying it reaches the
         * immutable initial destination parent exactly.
         */
        if (
            !PathEquals(
                expectedParent,
                initialParentPath))
        {
            return
                "Schema-v2 resolved-prefix evidence does not terminate " +
                "at the initial destination parent.";
        }

        /*
         * Everything below InitialDestinationParentSnapshot is the
         * repair-created suffix. It remains ordinally exact.
         */
        for (
            int index = prefixSteps.Count;
            index < requestedComponents.Length;
            index++)
        {
            if (
                !string.Equals(
                    requestedComponents[index],
                    finalComponents[index],
                    StringComparison.Ordinal))
            {
                return
                    mismatchError;
            }
        }

        return
            null;
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
