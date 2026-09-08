using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Independent, scoped-down durable apply journal for one targeted
// consumer-authoritative repair plan.
//
// This is intentionally not a variant of the legacy
// DataRelativePathRepairFileJournal. It supports CreateFile plans only
// (exactly one operation) and has no automated crash-forward-recovery
// layer: a durable journal still proves exactly which phase execution
// reached, but recovering from a mid-execution crash is a manual/future
// concern, not an automated reconciler.
//
// Each transition is published as a new immutable phase file rather than
// replacing an existing one in place - this reuses only the already-proven
// no-overwrite unnamed-file-publish primitives, with no new "replace
// durable content in place" primitive required.
public enum DataRelativePathTargetedConsumerCaseRepairApplyJournalState
{
    IntentRecorded,
    Prepared,
    Applied,
    RolledBack
}

public sealed record DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord(
    int SchemaVersion,
    Guid PlanId,
    DataRelativePathTargetedConsumerCaseRepairApplyJournalState State,
    DateTimeOffset RecordedUtc,
    string DataRoot,
    DataRelativePathRepairPlanOperation Operation,
    DataRelativePathRepairSourceSnapshot SourceSnapshot,
    LinuxFileIncarnationIdentity? PreparedFileIncarnationIdentity,
    LinuxFileIncarnationIdentity? AppliedFileIncarnationIdentity
)
{
    public const int SchemaVersion1 =
        1;

    public const int CurrentSchemaVersion =
        SchemaVersion1;
}

public enum
    DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
{
    Transitioned,

    InvalidRecord,
    InvalidTransition,
    InvalidPreparedIdentity,
    InvalidAppliedIdentity
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult(
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
            State,
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord? Record,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
            .Transitioned &&
        Record is not null;
}

public static class DataRelativePathTargetedConsumerCaseRepairApplyJournal
{
    public static string IntentChildName(
        Guid planId)
    {
        return
            $"{planId:N}.apply-intent.json";
    }

    public static string PreparedChildName(
        Guid planId)
    {
        return
            $"{planId:N}.apply-prepared.json";
    }

    public static string AppliedChildName(
        Guid planId)
    {
        return
            $"{planId:N}.apply-applied.json";
    }

    public static string RolledBackChildName(
        Guid planId)
    {
        return
            $"{planId:N}.apply-rolledback.json";
    }

    public static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
        CreateIntent(
            Guid planId,
            DateTimeOffset nowUtc,
            string dataRoot,
            DataRelativePathRepairPlanOperation operation,
            DataRelativePathRepairSourceSnapshot sourceSnapshot)
    {
        ArgumentNullException.ThrowIfNull(
            operation
        );

        ArgumentNullException.ThrowIfNull(
            sourceSnapshot
        );

        var record =
            new DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord(
                SchemaVersion:
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
                        .CurrentSchemaVersion,
                PlanId:
                    planId,
                State:
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                        .IntentRecorded,
                RecordedUtc:
                    nowUtc,
                DataRoot:
                    dataRoot,
                Operation:
                    operation,
                SourceSnapshot:
                    sourceSnapshot,
                PreparedFileIncarnationIdentity:
                    null,
                AppliedFileIncarnationIdentity:
                    null
            );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        return Success(
            record
        );
    }

    public static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
        MarkPrepared(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
                record,
            LinuxFileIncarnationIdentity preparedFileIncarnationIdentity,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        ArgumentNullException.ThrowIfNull(
            preparedFileIncarnationIdentity
        );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (
            record.State !=
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .IntentRecorded)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidTransition,
                "Only an IntentRecorded journal may transition to Prepared."
            );
        }

        if (!preparedFileIncarnationIdentity.Success)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidPreparedIdentity,
                "The Prepared transition requires a complete file " +
                "incarnation identity."
            );
        }

        var next =
            record with
            {
                State =
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                        .Prepared,
                RecordedUtc =
                    nowUtc,
                PreparedFileIncarnationIdentity =
                    preparedFileIncarnationIdentity
            };

        string? nextValidationError =
            Validate(
                next
            );

        if (nextValidationError is not null)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidRecord,
                nextValidationError
            );
        }

        return Success(
            next
        );
    }

    public static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
        MarkApplied(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
                record,
            LinuxFileIncarnationIdentity appliedFileIncarnationIdentity,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        ArgumentNullException.ThrowIfNull(
            appliedFileIncarnationIdentity
        );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (
            record.State !=
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .Prepared)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidTransition,
                "Only a Prepared journal may transition to Applied."
            );
        }

        if (!appliedFileIncarnationIdentity.Success)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidAppliedIdentity,
                "The Applied transition requires a complete file " +
                "incarnation identity."
            );
        }

        var next =
            record with
            {
                State =
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                        .Applied,
                RecordedUtc =
                    nowUtc,
                AppliedFileIncarnationIdentity =
                    appliedFileIncarnationIdentity
            };

        string? nextValidationError =
            Validate(
                next
            );

        if (nextValidationError is not null)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidRecord,
                nextValidationError
            );
        }

        return Success(
            next
        );
    }

    public static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
        MarkRolledBack(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
                record,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (
            record.State !=
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .Applied)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidTransition,
                "Only an Applied journal may transition to RolledBack."
            );
        }

        var next =
            record with
            {
                State =
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                        .RolledBack,
                RecordedUtc =
                    nowUtc
            };

        string? nextValidationError =
            Validate(
                next
            );

        if (nextValidationError is not null)
        {
            return Failure(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .InvalidRecord,
                nextValidationError
            );
        }

        return Success(
            next
        );
    }

    public static string? Validate(
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        if (
            record.SchemaVersion !=
            DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
                .SchemaVersion1)
        {
            return
                "Unsupported targeted apply-journal schema version.";
        }

        if (record.PlanId == Guid.Empty)
        {
            return
                "The targeted apply journal requires a non-empty PlanId.";
        }

        if (!TryCanonicalAbsolutePath(
                record.DataRoot,
                out string dataRoot))
        {
            return
                "The targeted apply journal Data root must be a canonical " +
                "absolute path.";
        }

        if (record.Operation is null)
        {
            return
                "The targeted apply journal requires an operation.";
        }

        if (
            record.Operation.Kind !=
            DataRelativePathRepairPlanOperationKind.CreateFile)
        {
            return
                "The targeted apply journal supports CreateFile " +
                "operations only.";
        }

        if (
            !TryCanonicalAbsolutePath(
                record.Operation.DestinationPath,
                out string destinationPath) ||
            !TryRelativeUnderRoot(
                dataRoot,
                destinationPath))
        {
            return
                "The targeted apply journal destination must be a " +
                "canonical absolute path beneath the Data root.";
        }

        if (record.SourceSnapshot is null)
        {
            return
                "The targeted apply journal requires source evidence.";
        }

        if (
            !TryCanonicalAbsolutePath(
                record.SourceSnapshot.PhysicalPath,
                out string sourcePath) ||
            !TryRelativeUnderRoot(
                dataRoot,
                sourcePath))
        {
            return
                "The targeted apply journal source path must be a " +
                "canonical absolute path beneath the Data root.";
        }

        if (
            !string.Equals(
                record.Operation.SourcePath,
                sourcePath,
                StringComparison.Ordinal))
        {
            return
                "The targeted apply journal operation source path does " +
                "not match its source snapshot.";
        }

        if (record.SourceSnapshot.Size < 0)
        {
            return
                "The targeted apply journal source size cannot be " +
                "negative.";
        }

        if (!IsSha256(
                record.SourceSnapshot.Sha256))
        {
            return
                "The targeted apply journal source requires a " +
                "64-character SHA-256 value.";
        }

        LinuxFileIdentityResult? sourceIdentity =
            record.SourceSnapshot.Identity;

        if (
            sourceIdentity is null ||
            !sourceIdentity.Success ||
            sourceIdentity.DeviceMajor is null ||
            sourceIdentity.DeviceMinor is null ||
            sourceIdentity.Inode is null ||
            sourceIdentity.MountId is null ||
            !string.Equals(
                sourceIdentity.FullPath,
                sourcePath,
                StringComparison.Ordinal))
        {
            return
                "The targeted apply journal source snapshot requires " +
                "complete physical identity bound to its exact physical " +
                "path.";
        }

        switch (record.State)
        {
            case
                DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                    .IntentRecorded:
                if (
                    record.PreparedFileIncarnationIdentity is not null ||
                    record.AppliedFileIncarnationIdentity is not null)
                {
                    return
                        "An IntentRecorded targeted apply journal must not " +
                        "carry prepared or applied identity.";
                }

                break;

            case
                DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                    .Prepared:
                if (
                    record.PreparedFileIncarnationIdentity is null ||
                    !record.PreparedFileIncarnationIdentity.Success ||
                    record.AppliedFileIncarnationIdentity is not null)
                {
                    return
                        "A Prepared targeted apply journal requires " +
                        "complete prepared identity and no applied " +
                        "identity.";
                }

                break;

            case
                DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                    .Applied:
                if (
                    record.PreparedFileIncarnationIdentity is null ||
                    !record.PreparedFileIncarnationIdentity.Success ||
                    record.AppliedFileIncarnationIdentity is null ||
                    !record.AppliedFileIncarnationIdentity.Success)
                {
                    return
                        "An Applied targeted apply journal requires " +
                        "complete prepared and applied identity.";
                }

                break;

            case
                DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                    .RolledBack:
                if (
                    record.PreparedFileIncarnationIdentity is null ||
                    !record.PreparedFileIncarnationIdentity.Success ||
                    record.AppliedFileIncarnationIdentity is null ||
                    !record.AppliedFileIncarnationIdentity.Success)
                {
                    return
                        "A RolledBack targeted apply journal requires " +
                        "the complete prepared and applied identity it " +
                        "was published with.";
                }

                break;

            default:
                return
                    "Unsupported targeted apply-journal state.";
        }

        return null;
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
        string path)
    {
        string relative;

        try
        {
            relative =
                Path.GetRelativePath(
                    dataRoot,
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

        return
            relative != "." &&
            relative != ".." &&
            !relative.StartsWith(
                "../",
                StringComparison.Ordinal) &&
            !Path.IsPathRooted(
                relative
            );
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
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
        Success(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
                record)
    {
        return new(
            State:
                DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                    .Transitioned,
            Record:
                record,
            Error:
                null
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
        Failure(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                state,
            string error)
    {
        return new(
            State:
                state,
            Record:
                null,
            Error:
                error
        );
    }
}
