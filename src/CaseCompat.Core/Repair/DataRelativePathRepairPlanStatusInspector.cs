using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairPlanStatusInspectionState
{
    Inspected,

    ManifestReadFailed,
    ManifestDataRootMismatch,

    JournalReadFailed,
    JournalMismatch,

    UnsupportedOperationKind
}

public enum DataRelativePathRepairPlanObservedOperationState
{
    NotStarted,

    IntentRecorded,
    Prepared,
    Applied,
    RollbackRequested,
    RolledBack,
    RecoveryConflict
}

public enum DataRelativePathRepairPlanOverallStatus
{
    NotStarted,
    InProgress,
    Applied,
    RollbackInProgress,
    RolledBack,
    RecoveryConflict
}

public sealed record DataRelativePathRepairPlanOperationStatus(
    DataRelativePathRepairPlanManifestOperation Entry,
    DataRelativePathRepairPlanObservedOperationState State
);

public sealed record DataRelativePathRepairPlanStatusInspection(
    DataRelativePathRepairPlanStatusInspectionState State,
    DataRelativePathRepairPlanManifestReaderResult? ManifestRead,
    DataRelativePathRepairPlanManifestRecord? Manifest,
    IReadOnlyList<DataRelativePathRepairPlanOperationStatus>
        OperationStatuses,
    DataRelativePathRepairPlanOverallStatus? OverallStatus,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairPlanStatusInspectionState
                .Inspected &&
        Manifest is not null &&
        OverallStatus is not null;
}

public static class DataRelativePathRepairPlanStatusInspector
{
    public static DataRelativePathRepairPlanStatusInspection Inspect(
        LinuxNoFollowPathHandle journalDirectory,
        string manifestChildName,
        string trustedDataRoot)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        return Inspect(
            (ILinuxOpenedHandle)journalDirectory,
            manifestChildName,
            trustedDataRoot
        );
    }

    public static DataRelativePathRepairPlanStatusInspection Inspect(
        ILinuxOpenedHandle journalDirectory,
        string manifestChildName,
        string trustedDataRoot)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        DataRelativePathRepairPlanManifestReaderResult manifestRead =
            DataRelativePathRepairPlanManifestReader.Read(
                journalDirectory,
                manifestChildName
            );

        if (!manifestRead.Success)
        {
            return Failure(
                DataRelativePathRepairPlanStatusInspectionState
                    .ManifestReadFailed,
                manifestRead,
                manifest:
                    null,
                [],
                manifestRead.Error ??
                    manifestRead.State.ToString()
            );
        }

        DataRelativePathRepairPlanManifestRecord manifest =
            manifestRead.Manifest!;

        /*
         * Status is read-only, but it must still avoid presenting an
         * unrelated manifest as authoritative for another installation.
         *
         * Use the same independently supplied trusted-root binding as
         * forward and rollback execution.
         */
        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                manifest.DataRoot,
                out string? rootBindingError
            ))
        {
            return Failure(
                DataRelativePathRepairPlanStatusInspectionState
                    .ManifestDataRootMismatch,
                manifestRead,
                manifest,
                [],
                rootBindingError
            );
        }

        var statuses =
            new List<
                DataRelativePathRepairPlanOperationStatus
            >(
                manifest.Operations.Count
            );

        foreach (
            DataRelativePathRepairPlanManifestOperation entry
            in manifest.Operations)
        {
            switch (entry.Operation.Kind)
            {
                case
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory:
                {
                    DataRelativePathRepairDirectoryJournalReaderResult
                        read =
                            DataRelativePathRepairDirectoryJournalReader
                                .Read(
                                    journalDirectory,
                                    entry.JournalChildName
                                );

                    if (
                        read.State ==
                        DataRelativePathRepairDirectoryJournalReadState
                            .JournalUnavailable)
                    {
                        statuses.Add(
                            new(
                                Entry:
                                    entry,
                                State:
                                    DataRelativePathRepairPlanObservedOperationState
                                        .NotStarted
                            )
                        );

                        break;
                    }

                    if (!read.Success)
                    {
                        return Failure(
                            DataRelativePathRepairPlanStatusInspectionState
                                .JournalReadFailed,
                            manifestRead,
                            manifest,
                            statuses,
                            $"Operation {entry.Index} journal " +
                            $"{entry.JournalChildName} could not be read: " +
                            (read.Error ??
                             read.State.ToString())
                        );
                    }

                    DataRelativePathRepairDirectoryJournalRecord journal =
                        read.Record!;

                    string? bindingError =
                        DataRelativePathRepairPlanJournalBinding
                            .ValidateDirectory(
                                entry,
                                journal,
                                trustedDataRoot
                            );

                    if (bindingError is not null)
                    {
                        return Failure(
                            DataRelativePathRepairPlanStatusInspectionState
                                .JournalMismatch,
                            manifestRead,
                            manifest,
                            statuses,
                            $"Operation {entry.Index} journal does not " +
                            $"belong to this plan: {bindingError}"
                        );
                    }

                    statuses.Add(
                        new(
                            Entry:
                                entry,
                            State:
                                Map(
                                    journal.State
                                )
                        )
                    );

                    break;
                }

                case
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile:
                {
                    DataRelativePathRepairFileJournalReaderResult read =
                        DataRelativePathRepairFileJournalReader.Read(
                            journalDirectory,
                            entry.JournalChildName
                        );

                    if (
                        read.State ==
                        DataRelativePathRepairFileJournalReadState
                            .JournalUnavailable)
                    {
                        statuses.Add(
                            new(
                                Entry:
                                    entry,
                                State:
                                    DataRelativePathRepairPlanObservedOperationState
                                        .NotStarted
                            )
                        );

                        break;
                    }

                    if (!read.Success)
                    {
                        return Failure(
                            DataRelativePathRepairPlanStatusInspectionState
                                .JournalReadFailed,
                            manifestRead,
                            manifest,
                            statuses,
                            $"Operation {entry.Index} journal " +
                            $"{entry.JournalChildName} could not be read: " +
                            (read.Error ??
                             read.State.ToString())
                        );
                    }

                    DataRelativePathRepairFileJournalRecord journal =
                        read.Record!;

                    string? bindingError =
                        DataRelativePathRepairPlanJournalBinding
                            .ValidateFile(
                                manifest,
                                entry,
                                journal,
                                trustedDataRoot
                            );

                    if (bindingError is not null)
                    {
                        return Failure(
                            DataRelativePathRepairPlanStatusInspectionState
                                .JournalMismatch,
                            manifestRead,
                            manifest,
                            statuses,
                            $"Operation {entry.Index} journal does not " +
                            $"belong to this plan: {bindingError}"
                        );
                    }

                    statuses.Add(
                        new(
                            Entry:
                                entry,
                            State:
                                Map(
                                    journal.State
                                )
                        )
                    );

                    break;
                }

                default:
                    return Failure(
                        DataRelativePathRepairPlanStatusInspectionState
                            .UnsupportedOperationKind,
                        manifestRead,
                        manifest,
                        statuses,
                        $"Operation {entry.Index} has unsupported kind " +
                        $"{entry.Operation.Kind}."
                    );
            }
        }

        return new(
            State:
                DataRelativePathRepairPlanStatusInspectionState
                    .Inspected,
            ManifestRead:
                manifestRead,
            Manifest:
                manifest,
            OperationStatuses:
                statuses,
            OverallStatus:
                ClassifyOverall(
                    statuses
                ),
            Error:
                null
        );
    }

    private static
        DataRelativePathRepairPlanObservedOperationState
        Map(
            DataRelativePathRepairDirectoryJournalState state)
    {
        return state switch
        {
            DataRelativePathRepairDirectoryJournalState
                .IntentRecorded =>
                    DataRelativePathRepairPlanObservedOperationState
                        .IntentRecorded,

            DataRelativePathRepairDirectoryJournalState
                .Prepared =>
                    DataRelativePathRepairPlanObservedOperationState
                        .Prepared,

            DataRelativePathRepairDirectoryJournalState
                .Applied =>
                    DataRelativePathRepairPlanObservedOperationState
                        .Applied,

            DataRelativePathRepairDirectoryJournalState
                .RollbackRequested =>
                    DataRelativePathRepairPlanObservedOperationState
                        .RollbackRequested,

            DataRelativePathRepairDirectoryJournalState
                .RolledBack =>
                    DataRelativePathRepairPlanObservedOperationState
                        .RolledBack,

            DataRelativePathRepairDirectoryJournalState
                .RecoveryConflict =>
                    DataRelativePathRepairPlanObservedOperationState
                        .RecoveryConflict,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    "Unsupported directory journal state."
                )
        };
    }

    private static
        DataRelativePathRepairPlanObservedOperationState
        Map(
            DataRelativePathRepairFileJournalState state)
    {
        return state switch
        {
            DataRelativePathRepairFileJournalState
                .IntentRecorded =>
                    DataRelativePathRepairPlanObservedOperationState
                        .IntentRecorded,

            DataRelativePathRepairFileJournalState
                .Prepared =>
                    DataRelativePathRepairPlanObservedOperationState
                        .Prepared,

            DataRelativePathRepairFileJournalState
                .Applied =>
                    DataRelativePathRepairPlanObservedOperationState
                        .Applied,

            DataRelativePathRepairFileJournalState
                .RollbackRequested =>
                    DataRelativePathRepairPlanObservedOperationState
                        .RollbackRequested,

            DataRelativePathRepairFileJournalState
                .RolledBack =>
                    DataRelativePathRepairPlanObservedOperationState
                        .RolledBack,

            DataRelativePathRepairFileJournalState
                .RecoveryConflict =>
                    DataRelativePathRepairPlanObservedOperationState
                        .RecoveryConflict,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    "Unsupported file journal state."
                )
        };
    }

    private static DataRelativePathRepairPlanOverallStatus
        ClassifyOverall(
            IReadOnlyList<
                DataRelativePathRepairPlanOperationStatus
            > statuses)
    {
        if (
            statuses.All(status =>
                status.State ==
                DataRelativePathRepairPlanObservedOperationState
                    .NotStarted
            ))
        {
            return
                DataRelativePathRepairPlanOverallStatus
                    .NotStarted;
        }

        if (
            statuses.Any(status =>
                status.State ==
                DataRelativePathRepairPlanObservedOperationState
                    .RecoveryConflict
            ))
        {
            return
                DataRelativePathRepairPlanOverallStatus
                    .RecoveryConflict;
        }

        if (
            statuses.All(status =>
                status.State ==
                DataRelativePathRepairPlanObservedOperationState
                    .Applied
            ))
        {
            return
                DataRelativePathRepairPlanOverallStatus
                    .Applied;
        }

        if (
            IsCompletedRollback(
                statuses
            ))
        {
            return
                DataRelativePathRepairPlanOverallStatus
                    .RolledBack;
        }

        if (
            statuses.Any(status =>
                status.State is
                    DataRelativePathRepairPlanObservedOperationState
                        .RollbackRequested or
                    DataRelativePathRepairPlanObservedOperationState
                        .RolledBack
            ))
        {
            return
                DataRelativePathRepairPlanOverallStatus
                    .RollbackInProgress;
        }

        return
            DataRelativePathRepairPlanOverallStatus
                .InProgress;
    }

    /*
     * A successfully rolled-back partial forward plan has a durable
     * RolledBack prefix followed by an untouched suffix whose journals
     * were never created.
     *
     * Example:
     *
     *     RolledBack, NotStarted, NotStarted
     *
     * That is a completed rollback, not rollback-in-progress.
     *
     * Preserve ordering here.  A NotStarted entry followed later by a
     * RolledBack entry is not the same valid shape and must not be
     * promoted to RolledBack by this descriptive status classifier.
     */
    private static bool IsCompletedRollback(
        IReadOnlyList<
            DataRelativePathRepairPlanOperationStatus
        > statuses)
    {
        bool sawRolledBack =
            false;

        bool sawUntouchedSuffix =
            false;

        foreach (
            DataRelativePathRepairPlanOperationStatus status
            in statuses)
        {
            switch (status.State)
            {
                case
                    DataRelativePathRepairPlanObservedOperationState
                        .RolledBack:
                    if (sawUntouchedSuffix)
                    {
                        return false;
                    }

                    sawRolledBack =
                        true;

                    break;

                case
                    DataRelativePathRepairPlanObservedOperationState
                        .NotStarted:
                    sawUntouchedSuffix =
                        true;

                    break;

                default:
                    return false;
            }
        }

        return sawRolledBack;
    }

    private static DataRelativePathRepairPlanStatusInspection
        Failure(
            DataRelativePathRepairPlanStatusInspectionState state,
            DataRelativePathRepairPlanManifestReaderResult?
                manifestRead,
            DataRelativePathRepairPlanManifestRecord? manifest,
            IReadOnlyList<
                DataRelativePathRepairPlanOperationStatus
            > operationStatuses,
            string? error)
    {
        return new(
            State:
                state,
            ManifestRead:
                manifestRead,
            Manifest:
                manifest,
            OperationStatuses:
                operationStatuses,
            OverallStatus:
                null,
            Error:
                error
        );
    }
}
