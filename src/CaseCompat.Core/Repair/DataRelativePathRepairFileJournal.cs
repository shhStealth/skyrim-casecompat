using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairFileJournalState
{
    IntentRecorded,
    Prepared,
    Applied,
    RollbackRequested,
    RolledBack,
    RecoveryConflict
}

public enum DataRelativePathRepairFileJournalTransitionState
{
    Transitioned,

    InvalidRecord,
    InvalidTransition,
    InvalidPreparedIdentity,
    InvalidConflictReason
}

public sealed record DataRelativePathRepairFileJournalRecord(
    int SchemaVersion,
    Guid JournalId,
    int Revision,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    DataRelativePathRepairFileJournalState State,
    string DataRoot,
    DataRelativePathRepairPlanOperation Operation,
    DataRelativePathRepairSourceSnapshot SourceSnapshot,
    DataRelativePathRepairDestinationParentSnapshot
        DestinationParentSnapshot,
    LinuxOpenedFileIdentityResult? PreparedFileIdentity,
    string? RecoveryConflictReason
)
{
    public const int CurrentSchemaVersion =
        1;

    public bool IsTerminal =>
        State is
            DataRelativePathRepairFileJournalState
                .RolledBack or
            DataRelativePathRepairFileJournalState
                .RecoveryConflict;
}

public sealed record
    DataRelativePathRepairFileJournalTransitionResult(
        DataRelativePathRepairFileJournalTransitionState State,
        DataRelativePathRepairFileJournalRecord? Record,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathRepairFileJournalTransitionState
                .Transitioned &&
        Record is not null;
}

public static class DataRelativePathRepairFileJournal
{
    public static DataRelativePathRepairFileJournalTransitionResult
        CreateIntent(
            Guid journalId,
            DateTimeOffset nowUtc,
            string dataRoot,
            DataRelativePathRepairPlanOperation operation,
            DataRelativePathRepairSourceSnapshot sourceSnapshot,
            DataRelativePathRepairDestinationParentSnapshot
                destinationParentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(
            operation
        );

        ArgumentNullException.ThrowIfNull(
            sourceSnapshot
        );

        ArgumentNullException.ThrowIfNull(
            destinationParentSnapshot
        );

        var record =
            new DataRelativePathRepairFileJournalRecord(
                SchemaVersion:
                    DataRelativePathRepairFileJournalRecord
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
                    DataRelativePathRepairFileJournalState
                        .IntentRecorded,
                DataRoot:
                    dataRoot,
                Operation:
                    operation,
                SourceSnapshot:
                    sourceSnapshot,
                DestinationParentSnapshot:
                    destinationParentSnapshot,
                PreparedFileIdentity:
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
                DataRelativePathRepairFileJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        return Success(
            record
        );
    }

    public static DataRelativePathRepairFileJournalTransitionResult
        MarkPrepared(
            DataRelativePathRepairFileJournalRecord record,
            LinuxOpenedFileIdentityResult preparedFileIdentity,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        ArgumentNullException.ThrowIfNull(
            preparedFileIdentity
        );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathRepairFileJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (
            record.State !=
            DataRelativePathRepairFileJournalState
                .IntentRecorded)
        {
            return InvalidTransition(
                record,
                DataRelativePathRepairFileJournalState
                    .Prepared
            );
        }

        if (
            !IsValidPreparedIdentity(
                preparedFileIdentity
            ))
        {
            return Failure(
                DataRelativePathRepairFileJournalTransitionState
                    .InvalidPreparedIdentity,
                "Prepared state requires the successfully " +
                "captured identity of an unnamed regular file " +
                "whose link count is zero."
            );
        }

        return Transition(
            record,
            nowUtc,
            DataRelativePathRepairFileJournalState
                .Prepared,
            preparedFileIdentity:
                preparedFileIdentity
        );
    }

    public static DataRelativePathRepairFileJournalTransitionResult
        Reprepare(
            DataRelativePathRepairFileJournalRecord record,
            LinuxOpenedFileIdentityResult preparedFileIdentity,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        ArgumentNullException.ThrowIfNull(
            preparedFileIdentity
        );

        string? validationError =
            Validate(
                record
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathRepairFileJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (
            record.State !=
            DataRelativePathRepairFileJournalState
                .Prepared)
        {
            return InvalidTransition(
                record,
                DataRelativePathRepairFileJournalState
                    .Prepared
            );
        }

        if (
            !IsValidPreparedIdentity(
                preparedFileIdentity
            ))
        {
            return Failure(
                DataRelativePathRepairFileJournalTransitionState
                    .InvalidPreparedIdentity,
                "Re-prepared state requires the successfully " +
                "captured identity of a new unnamed regular file " +
                "whose link count is zero."
            );
        }

        return Transition(
            record,
            nowUtc,
            DataRelativePathRepairFileJournalState
                .Prepared,
            preparedFileIdentity:
                preparedFileIdentity
        );
    }

    public static DataRelativePathRepairFileJournalTransitionResult
        MarkApplied(
            DataRelativePathRepairFileJournalRecord record,
            DateTimeOffset nowUtc)
    {
        return TransitionFrom(
            record,
            nowUtc,
            expectedState:
                DataRelativePathRepairFileJournalState
                    .Prepared,
            newState:
                DataRelativePathRepairFileJournalState
                    .Applied
        );
    }

    public static DataRelativePathRepairFileJournalTransitionResult
        RequestRollback(
            DataRelativePathRepairFileJournalRecord record,
            DateTimeOffset nowUtc)
    {
        return TransitionFrom(
            record,
            nowUtc,
            expectedState:
                DataRelativePathRepairFileJournalState
                    .Applied,
            newState:
                DataRelativePathRepairFileJournalState
                    .RollbackRequested
        );
    }

    public static DataRelativePathRepairFileJournalTransitionResult
        MarkRolledBack(
            DataRelativePathRepairFileJournalRecord record,
            DateTimeOffset nowUtc)
    {
        return TransitionFrom(
            record,
            nowUtc,
            expectedState:
                DataRelativePathRepairFileJournalState
                    .RollbackRequested,
            newState:
                DataRelativePathRepairFileJournalState
                    .RolledBack
        );
    }

    public static DataRelativePathRepairFileJournalTransitionResult
        MarkRecoveryConflict(
            DataRelativePathRepairFileJournalRecord record,
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
                DataRelativePathRepairFileJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        if (
            record.State is not
                DataRelativePathRepairFileJournalState
                    .Prepared and not
                DataRelativePathRepairFileJournalState
                    .Applied and not
                DataRelativePathRepairFileJournalState
                    .RollbackRequested)
        {
            return InvalidTransition(
                record,
                DataRelativePathRepairFileJournalState
                    .RecoveryConflict
            );
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Failure(
                DataRelativePathRepairFileJournalTransitionState
                    .InvalidConflictReason,
                "A recovery conflict requires a non-empty reason."
            );
        }

        return Transition(
            record,
            nowUtc,
            DataRelativePathRepairFileJournalState
                .RecoveryConflict,
            recoveryConflictReason:
                reason
        );
    }

    public static string? Validate(
        DataRelativePathRepairFileJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        if (
            record.SchemaVersion !=
            DataRelativePathRepairFileJournalRecord
                .CurrentSchemaVersion)
        {
            return
                "The journal schema version is unsupported.";
        }

        if (record.JournalId == Guid.Empty)
        {
            return
                "The journal ID cannot be empty.";
        }

        if (record.Revision < 0)
        {
            return
                "The journal revision cannot be negative.";
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
                "The journal Data root must be an absolute path.";
        }

        if (
            record.Operation.Kind !=
            DataRelativePathRepairPlanOperationKind
                .CreateFile)
        {
            return
                "This journal model supports CreateFile " +
                "operations only.";
        }

        if (
            string.IsNullOrWhiteSpace(
                record.Operation.DestinationPath
            ))
        {
            return
                "The file operation requires a destination path.";
        }

        if (
            string.IsNullOrWhiteSpace(
                record.Operation.SourcePath
            ))
        {
            return
                "The file operation requires a source path.";
        }

        if (
            string.IsNullOrWhiteSpace(
                record.SourceSnapshot.PhysicalPath
            ))
        {
            return
                "The source snapshot requires a physical path.";
        }

        if (record.SourceSnapshot.Size < 0)
        {
            return
                "The source snapshot size cannot be negative.";
        }

        if (
            !IsSha256(
                record.SourceSnapshot.Sha256
            ))
        {
            return
                "The source snapshot requires a 64-character " +
                "SHA-256 value.";
        }

        if (
            !HasPhysicalIdentity(
                record.SourceSnapshot.Identity
            ))
        {
            return
                "The source snapshot requires a complete " +
                "physical identity.";
        }

        if (
            string.IsNullOrWhiteSpace(
                record.DestinationParentSnapshot
                    .PhysicalPath
            ))
        {
            return
                "The destination-parent snapshot requires " +
                "a physical path.";
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
            !HasPhysicalIdentity(
                record.DestinationParentSnapshot
                    .Identity
            ))
        {
            return
                "The destination-parent snapshot requires " +
                "a complete physical identity.";
        }

        if (
            record.DestinationParentSnapshot
                .CasefoldEnabled)
        {
            return
                "The destination parent must remain strict " +
                "for a case-repair file journal.";
        }

        bool requiresPreparedIdentity =
            record.State !=
            DataRelativePathRepairFileJournalState
                .IntentRecorded;

        if (
            requiresPreparedIdentity &&
            !IsValidPreparedIdentity(
                record.PreparedFileIdentity
            ))
        {
            return
                "This journal state requires the recorded " +
                "zero-link prepared-file identity.";
        }

        if (
            !requiresPreparedIdentity &&
            record.PreparedFileIdentity is not null)
        {
            return
                "IntentRecorded cannot already contain a " +
                "prepared-file identity.";
        }

        if (
            record.State ==
            DataRelativePathRepairFileJournalState
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
        DataRelativePathRepairFileJournalTransitionResult
        TransitionFrom(
            DataRelativePathRepairFileJournalRecord record,
            DateTimeOffset nowUtc,
            DataRelativePathRepairFileJournalState expectedState,
            DataRelativePathRepairFileJournalState newState)
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
                DataRelativePathRepairFileJournalTransitionState
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
        DataRelativePathRepairFileJournalTransitionResult
        Transition(
            DataRelativePathRepairFileJournalRecord record,
            DateTimeOffset nowUtc,
            DataRelativePathRepairFileJournalState state,
            LinuxOpenedFileIdentityResult?
                preparedFileIdentity = null,
            string? recoveryConflictReason = null)
    {
        if (nowUtc < record.UpdatedUtc)
        {
            return Failure(
                DataRelativePathRepairFileJournalTransitionState
                    .InvalidRecord,
                "A journal transition cannot move time backwards."
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
                PreparedFileIdentity =
                    preparedFileIdentity ??
                    record.PreparedFileIdentity,
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
                DataRelativePathRepairFileJournalTransitionState
                    .InvalidRecord,
                validationError
            );
        }

        return Success(
            updated
        );
    }

    private static
        DataRelativePathRepairFileJournalTransitionResult
        InvalidTransition(
            DataRelativePathRepairFileJournalRecord record,
            DataRelativePathRepairFileJournalState destination)
    {
        return Failure(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidTransition,
            $"Cannot transition journal from {record.State} " +
            $"to {destination}."
        );
    }

    private static string? ValidatePathBindings(
        DataRelativePathRepairFileJournalRecord record)
    {
        try
        {
            string dataRoot =
                NormalizePath(
                    record.DataRoot
                );

            string operationSource =
                NormalizePath(
                    record.Operation.SourcePath!
                );

            string snapshotSource =
                NormalizePath(
                    record.SourceSnapshot.PhysicalPath
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
                !string.Equals(
                    operationSource,
                    snapshotSource,
                    StringComparison.Ordinal
                ))
            {
                return
                    "The operation source path must match the " +
                    "source snapshot physical path.";
            }

            if (
                !IsAtOrBelow(
                    dataRoot,
                    snapshotSource
                ))
            {
                return
                    "The source snapshot must be inside the " +
                    "journal Data root.";
            }

            if (
                !IsStrictDescendant(
                    dataRoot,
                    destination
                ))
            {
                return
                    "The destination file must be inside the " +
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
                    "This file journal requires the destination-" +
                    "parent snapshot to identify the direct " +
                    "physical parent of the destination file.";
            }

            string childName =
                Path.GetFileName(
                    destination
                );

            if (
                string.IsNullOrEmpty(
                    childName
                ) ||
                childName is "." or ".." ||
                childName.Contains('/') ||
                childName.Contains('\\') ||
                childName.Contains('\0'))
            {
                return
                    "The destination must identify exactly one " +
                    "direct file child beneath the recorded " +
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
                "The journal contains an invalid filesystem " +
                $"path: {ex.Message}";
        }
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
        if (
            string.Equals(
                root,
                candidate,
                StringComparison.Ordinal
            ))
        {
            return true;
        }

        string prefix =
            root.EndsWith(
                Path.DirectorySeparatorChar
            )
                ? root
                : root +
                    Path.DirectorySeparatorChar;

        return candidate.StartsWith(
            prefix,
            StringComparison.Ordinal
        );
    }

    private static bool IsStrictDescendant(
        string root,
        string candidate)
    {
        return
            !string.Equals(
                root,
                candidate,
                StringComparison.Ordinal
            ) &&
            IsAtOrBelow(
                root,
                candidate
            );
    }

    private static bool HasPhysicalIdentity(
        LinuxFileIdentityResult identity)
    {
        ArgumentNullException.ThrowIfNull(
            identity
        );

        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null;
    }

    private static bool IsValidPreparedIdentity(
        LinuxOpenedFileIdentityResult? identity)
    {
        return
            identity is not null &&
            identity.Success &&
            identity.LinkCount == 0U;
    }

    private static bool IsSha256(
        string value)
    {
        return
            value.Length == 64 &&
            value.All(
                Uri.IsHexDigit
            );
    }

    private static
        DataRelativePathRepairFileJournalTransitionResult
        Success(
            DataRelativePathRepairFileJournalRecord record)
    {
        return new
            DataRelativePathRepairFileJournalTransitionResult(
                State:
                    DataRelativePathRepairFileJournalTransitionState
                        .Transitioned,
                Record:
                    record,
                Error:
                    null
            );
    }

    private static
        DataRelativePathRepairFileJournalTransitionResult
        Failure(
            DataRelativePathRepairFileJournalTransitionState state,
            string error)
    {
        return new
            DataRelativePathRepairFileJournalTransitionResult(
                State:
                    state,
                Record:
                    null,
                Error:
                    error
            );
    }
}
