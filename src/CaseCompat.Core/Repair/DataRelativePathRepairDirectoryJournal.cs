using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryJournalState
{
    IntentRecorded,
    Prepared,
    Applied,
    RollbackRequested,
    RolledBack,
    RecoveryConflict
}

public enum DataRelativePathRepairDirectoryJournalTransitionState
{
    Transitioned,

    InvalidRecord,
    InvalidTransition,
    InvalidStagingName,
    InvalidPreparedIdentity,
    InvalidConflictReason
}

public sealed record DataRelativePathRepairDirectoryJournalRecord(
    int SchemaVersion,
    Guid JournalId,
    int Revision,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    DataRelativePathRepairDirectoryJournalState State,
    string DataRoot,
    DataRelativePathRepairPlanOperation Operation,
    DataRelativePathRepairDestinationParentSnapshot
        DestinationParentSnapshot,
    string? PreparedStagingChildName,
    LinuxFileIdentityResult? PreparedDirectoryIdentity,
    string? RecoveryConflictReason
)
{
    public const int CurrentSchemaVersion =
        1;

    public bool IsTerminal =>
        State is
            DataRelativePathRepairDirectoryJournalState
                .RolledBack or
            DataRelativePathRepairDirectoryJournalState
                .RecoveryConflict;
}

public sealed record
    DataRelativePathRepairDirectoryJournalTransitionResult(
        DataRelativePathRepairDirectoryJournalTransitionState State,
        DataRelativePathRepairDirectoryJournalRecord? Record,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathRepairDirectoryJournalTransitionState
                .Transitioned &&
        Record is not null;
}

public static class DataRelativePathRepairDirectoryJournal
{
    public static
        DataRelativePathRepairDirectoryJournalTransitionResult
        CreateIntent(
            Guid journalId,
            DateTimeOffset nowUtc,
            string dataRoot,
            DataRelativePathRepairPlanOperation operation,
            DataRelativePathRepairDestinationParentSnapshot
                destinationParentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(
            operation
        );

        ArgumentNullException.ThrowIfNull(
            destinationParentSnapshot
        );

        var record =
            new DataRelativePathRepairDirectoryJournalRecord(
                SchemaVersion:
                    DataRelativePathRepairDirectoryJournalRecord
                        .CurrentSchemaVersion,
                JournalId:
                    journalId,
                Revision:
                    0,
                CreatedUtc:
                    nowUtc,
                UpdatedUtc:
                    nowUtc,
                State:
                    DataRelativePathRepairDirectoryJournalState
                        .IntentRecorded,
                DataRoot:
                    dataRoot,
                Operation:
                    operation,
                DestinationParentSnapshot:
                    destinationParentSnapshot,
                PreparedStagingChildName:
                    null,
                PreparedDirectoryIdentity:
                    null,
                RecoveryConflictReason:
                    null
            );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        return Success(
            record
        );
    }

    public static
        DataRelativePathRepairDirectoryJournalTransitionResult
        MarkPrepared(
            DataRelativePathRepairDirectoryJournalRecord record,
            string stagingChildName,
            LinuxFileIdentityResult preparedDirectoryIdentity,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        ArgumentNullException.ThrowIfNull(
            preparedDirectoryIdentity
        );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (
            record.State !=
            DataRelativePathRepairDirectoryJournalState
                .IntentRecorded)
        {
            return InvalidTransition(
                record,
                DataRelativePathRepairDirectoryJournalState
                    .Prepared
            );
        }

        if (
            !IsValidStagingName(
                record,
                stagingChildName
            ))
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidStagingName,
                "Prepared state requires a valid direct-child " +
                "staging name that differs from the final " +
                "destination directory name."
            );
        }

        if (
            !HasCompleteDirectoryIdentity(
                preparedDirectoryIdentity
            ))
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidPreparedIdentity,
                "Prepared state requires a complete directory " +
                "identity including device, inode, and mount ID."
            );
        }

        return Transition(
            record,
            nowUtc,
            DataRelativePathRepairDirectoryJournalState
                .Prepared,
            preparedStagingChildName:
                stagingChildName,
            preparedDirectoryIdentity:
                preparedDirectoryIdentity
        );
    }

    public static
        DataRelativePathRepairDirectoryJournalTransitionResult
        Reprepare(
            DataRelativePathRepairDirectoryJournalRecord record,
            string stagingChildName,
            LinuxFileIdentityResult preparedDirectoryIdentity,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        ArgumentNullException.ThrowIfNull(
            preparedDirectoryIdentity
        );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (
            record.State !=
            DataRelativePathRepairDirectoryJournalState
                .Prepared)
        {
            return InvalidTransition(
                record,
                DataRelativePathRepairDirectoryJournalState
                    .Prepared
            );
        }

        if (
            !IsValidStagingName(
                record,
                stagingChildName
            ))
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidStagingName,
                "Re-prepared state requires a valid direct-child " +
                "staging name that differs from the final " +
                "destination directory name."
            );
        }

        if (
            !HasCompleteDirectoryIdentity(
                preparedDirectoryIdentity
            ))
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidPreparedIdentity,
                "Re-prepared state requires a complete directory " +
                "identity including device, inode, and mount ID."
            );
        }

        return Transition(
            record,
            nowUtc,
            DataRelativePathRepairDirectoryJournalState
                .Prepared,
            preparedStagingChildName:
                stagingChildName,
            preparedDirectoryIdentity:
                preparedDirectoryIdentity
        );
    }

    public static
        DataRelativePathRepairDirectoryJournalTransitionResult
        MarkApplied(
            DataRelativePathRepairDirectoryJournalRecord record,
            DateTimeOffset nowUtc)
    {
        return TransitionFrom(
            record,
            nowUtc,
            expectedState:
                DataRelativePathRepairDirectoryJournalState
                    .Prepared,
            newState:
                DataRelativePathRepairDirectoryJournalState
                    .Applied
        );
    }

    public static
        DataRelativePathRepairDirectoryJournalTransitionResult
        RequestRollback(
            DataRelativePathRepairDirectoryJournalRecord record,
            DateTimeOffset nowUtc)
    {
        return TransitionFrom(
            record,
            nowUtc,
            expectedState:
                DataRelativePathRepairDirectoryJournalState
                    .Applied,
            newState:
                DataRelativePathRepairDirectoryJournalState
                    .RollbackRequested
        );
    }

    public static
        DataRelativePathRepairDirectoryJournalTransitionResult
        MarkRolledBack(
            DataRelativePathRepairDirectoryJournalRecord record,
            DateTimeOffset nowUtc)
    {
        return TransitionFrom(
            record,
            nowUtc,
            expectedState:
                DataRelativePathRepairDirectoryJournalState
                    .RollbackRequested,
            newState:
                DataRelativePathRepairDirectoryJournalState
                    .RolledBack
        );
    }

    public static
        DataRelativePathRepairDirectoryJournalTransitionResult
        MarkRecoveryConflict(
            DataRelativePathRepairDirectoryJournalRecord record,
            string reason,
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
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (
            record.State is not
                DataRelativePathRepairDirectoryJournalState
                    .Prepared and not
                DataRelativePathRepairDirectoryJournalState
                    .Applied and not
                DataRelativePathRepairDirectoryJournalState
                    .RollbackRequested)
        {
            return InvalidTransition(
                record,
                DataRelativePathRepairDirectoryJournalState
                    .RecoveryConflict
            );
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidConflictReason,
                "A recovery conflict requires a non-empty reason."
            );
        }

        return Transition(
            record,
            nowUtc,
            DataRelativePathRepairDirectoryJournalState
                .RecoveryConflict,
            recoveryConflictReason:
                reason
        );
    }

    public static string? Validate(
        DataRelativePathRepairDirectoryJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        if (
            record.SchemaVersion !=
            DataRelativePathRepairDirectoryJournalRecord
                .CurrentSchemaVersion)
        {
            return
                "The directory journal schema version is unsupported.";
        }

        if (record.JournalId == Guid.Empty)
        {
            return
                "The directory journal ID cannot be empty.";
        }

        if (record.Revision < 0)
        {
            return
                "The directory journal revision cannot be negative.";
        }

        if (
            record.UpdatedUtc <
            record.CreatedUtc)
        {
            return
                "UpdatedUtc cannot precede CreatedUtc.";
        }

        if (
            string.IsNullOrWhiteSpace(
                record.DataRoot
            ) ||
            !Path.IsPathRooted(
                record.DataRoot
            ))
        {
            return
                "The directory journal Data root must be an " +
                "absolute path.";
        }

        if (
            record.Operation.Kind !=
            DataRelativePathRepairPlanOperationKind
                .CreateDirectory)
        {
            return
                "This journal model supports CreateDirectory " +
                "operations only.";
        }

        if (
            string.IsNullOrWhiteSpace(
                record.Operation.DestinationPath
            ))
        {
            return
                "The directory operation requires a destination path.";
        }

        if (
            record.Operation.SourcePath is not null)
        {
            return
                "A CreateDirectory operation cannot contain a " +
                "source path.";
        }

        if (
            string.IsNullOrWhiteSpace(
                record.DestinationParentSnapshot
                    .PhysicalPath
            ))
        {
            return
                "The destination-parent snapshot requires a " +
                "physical path.";
        }

        string? pathBindingError =
            ValidatePathBindings(
                record
            );

        if (pathBindingError is not null)
        {
            return pathBindingError;
        }

        if (
            !HasCompleteDirectoryIdentity(
                record.DestinationParentSnapshot
                    .Identity
            ))
        {
            return
                "The destination-parent snapshot requires a " +
                "complete physical identity including mount ID.";
        }

        if (
            record.DestinationParentSnapshot
                .CasefoldEnabled)
        {
            return
                "The destination parent must remain strict for a " +
                "case-repair directory journal.";
        }

        bool requiresPreparedEvidence =
            record.State !=
            DataRelativePathRepairDirectoryJournalState
                .IntentRecorded;

        if (requiresPreparedEvidence)
        {
            if (
                !IsValidStagingName(
                    record,
                    record.PreparedStagingChildName
                ))
            {
                return
                    "This journal state requires a valid recorded " +
                    "staging child name.";
            }

            if (
                record.PreparedDirectoryIdentity is null ||
                !HasCompleteDirectoryIdentity(
                    record.PreparedDirectoryIdentity
                ))
            {
                return
                    "This journal state requires the complete " +
                    "prepared-directory identity.";
            }
        }
        else
        {
            if (
                record.PreparedStagingChildName is not null ||
                record.PreparedDirectoryIdentity is not null)
            {
                return
                    "IntentRecorded cannot already contain prepared " +
                    "directory evidence.";
            }
        }

        if (
            record.State ==
            DataRelativePathRepairDirectoryJournalState
                .RecoveryConflict)
        {
            if (
                string.IsNullOrWhiteSpace(
                    record.RecoveryConflictReason
                ))
            {
                return
                    "RecoveryConflict requires a reason.";
            }
        }
        else if (
            record.RecoveryConflictReason is not null)
        {
            return
                "Only RecoveryConflict may contain a " +
                "recovery-conflict reason.";
        }

        return null;
    }

    private static
        DataRelativePathRepairDirectoryJournalTransitionResult
        TransitionFrom(
            DataRelativePathRepairDirectoryJournalRecord record,
            DateTimeOffset nowUtc,
            DataRelativePathRepairDirectoryJournalState expectedState,
            DataRelativePathRepairDirectoryJournalState newState)
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
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (record.State != expectedState)
        {
            return InvalidTransition(
                record,
                newState
            );
        }

        return Transition(
            record,
            nowUtc,
            newState
        );
    }

    private static
        DataRelativePathRepairDirectoryJournalTransitionResult
        Transition(
            DataRelativePathRepairDirectoryJournalRecord record,
            DateTimeOffset nowUtc,
            DataRelativePathRepairDirectoryJournalState state,
            string? preparedStagingChildName = null,
            LinuxFileIdentityResult?
                preparedDirectoryIdentity = null,
            string? recoveryConflictReason = null)
    {
        if (nowUtc < record.UpdatedUtc)
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidRecord,
                "A directory journal transition cannot move " +
                "time backwards."
            );
        }

        var updated =
            record with
            {
                Revision =
                    checked(
                        record.Revision + 1
                    ),
                UpdatedUtc =
                    nowUtc,
                State =
                    state,
                PreparedStagingChildName =
                    preparedStagingChildName ??
                    record.PreparedStagingChildName,
                PreparedDirectoryIdentity =
                    preparedDirectoryIdentity ??
                    record.PreparedDirectoryIdentity,
                RecoveryConflictReason =
                    recoveryConflictReason
            };

        string? validationError =
            Validate(
                updated
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathRepairDirectoryJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        return Success(
            updated
        );
    }

    private static
        DataRelativePathRepairDirectoryJournalTransitionResult
        InvalidTransition(
            DataRelativePathRepairDirectoryJournalRecord record,
            DataRelativePathRepairDirectoryJournalState destination)
    {
        return Failure(
            DataRelativePathRepairDirectoryJournalTransitionState
                .InvalidTransition,
            $"Cannot transition directory journal from " +
            $"{record.State} to {destination}."
        );
    }

    private static string? ValidatePathBindings(
        DataRelativePathRepairDirectoryJournalRecord record)
    {
        try
        {
            string dataRoot =
                NormalizePath(
                    record.DataRoot
                );

            string destination =
                NormalizePath(
                    record.Operation.DestinationPath
                );

            string destinationParent =
                NormalizePath(
                    record.DestinationParentSnapshot
                        .PhysicalPath
                );

            if (
                !IsStrictDescendant(
                    dataRoot,
                    destination
                ))
            {
                return
                    "The destination directory must be inside the " +
                    "journal Data root.";
            }

            if (
                !IsAtOrBelow(
                    dataRoot,
                    destinationParent
                ))
            {
                return
                    "The destination-parent snapshot must be " +
                    "inside the journal Data root.";
            }

            string? actualParent =
                Path.GetDirectoryName(
                    destination
                );

            if (
                actualParent is null ||
                !string.Equals(
                    NormalizePath(
                        actualParent
                    ),
                    destinationParent,
                    StringComparison.Ordinal
                ))
            {
                return
                    "This directory journal requires the " +
                    "destination-parent snapshot to identify the " +
                    "direct physical parent of the destination " +
                    "directory.";
            }

            string childName =
                Path.GetFileName(
                    destination
                );

            if (!IsValidChildName(childName))
            {
                return
                    "The destination must identify exactly one " +
                    "direct directory child beneath the recorded " +
                    "destination parent.";
            }

            return null;
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return
                "The directory journal contains an invalid " +
                $"filesystem path: {ex.Message}";
        }
    }

    private static bool IsValidStagingName(
        DataRelativePathRepairDirectoryJournalRecord record,
        string? stagingChildName)
    {
        if (!IsValidChildName(stagingChildName))
        {
            return false;
        }

        string finalChildName =
            Path.GetFileName(
                record.Operation.DestinationPath
            );

        return
            !string.Equals(
                stagingChildName,
                finalChildName,
                StringComparison.Ordinal
            );
    }

    private static bool HasCompleteDirectoryIdentity(
        LinuxFileIdentityResult identity)
    {
        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null &&
            identity.MountId is not null;
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

    private static string NormalizePath(
        string path)
    {
        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(
                path
            )
        );
    }

    private static bool IsAtOrBelow(
        string root,
        string candidate)
    {
        return
            string.Equals(
                root,
                candidate,
                StringComparison.Ordinal
            ) ||
            IsStrictDescendant(
                root,
                candidate
            );
    }

    private static bool IsStrictDescendant(
        string root,
        string candidate)
    {
        string relative =
            Path.GetRelativePath(
                root,
                candidate
            );

        if (
            relative == "." ||
            Path.IsPathRooted(
                relative
            ) ||
            relative == "..")
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

    private static
        DataRelativePathRepairDirectoryJournalTransitionResult
        Success(
            DataRelativePathRepairDirectoryJournalRecord record)
    {
        return new(
            State:
                DataRelativePathRepairDirectoryJournalTransitionState
                    .Transitioned,
            Record:
                record,
            Error:
                null
        );
    }

    private static
        DataRelativePathRepairDirectoryJournalTransitionResult
        Failure(
            DataRelativePathRepairDirectoryJournalTransitionState
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
