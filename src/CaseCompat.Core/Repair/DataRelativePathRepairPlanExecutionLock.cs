using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairPlanExecutionLockState
{
    Acquired,

    InvalidInitialManifestRead,
    InitialManifestIdentityUnavailable,

    LockUnavailable,

    LockedManifestReadFailed,
    LockedManifestIdentityUnavailable,

    PlanIdChanged,
    ManifestIncarnationChanged
}

public sealed record DataRelativePathRepairPlanExecutionLockAcquisition(
    DataRelativePathRepairPlanExecutionLockState State,
    string LockChildName,
    DataRelativePathRepairPlanManifestReaderResult InitialManifestRead,
    LinuxExclusiveChildFileLockResult? LockAcquisition,
    DataRelativePathRepairPlanManifestReaderResult? LockedManifestRead,
    LinuxExclusiveChildFileLockLease? Lease,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairPlanExecutionLockState.Acquired &&
        LockedManifestRead?.Success == true &&
        Lease is not null &&
        Lease.IsHeld;
}

/*
 * Serializes cooperating CaseCompat whole-plan forward and rollback
 * executors by immutable PlanId.
 *
 * The initial manifest read is used only to discover the PlanId and
 * strong manifest incarnation that must still be present after the
 * plan lock is acquired.
 *
 * The locked re-read becomes authoritative for all subsequent plan
 * validation, preflight, and execution.
 *
 * The lock does not replace any existing per-operation descriptor,
 * journal-incarnation, or directory-lock guard.
 */
public static class DataRelativePathRepairPlanExecutionLock
{
    public static string CreateLockChildName(
        Guid planId)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException(
                "A non-empty PlanId is required.",
                nameof(planId)
            );
        }

        return
            $".casecompat-plan-{planId:N}.execution-lock";
    }

    public static DataRelativePathRepairPlanExecutionLockAcquisition
        Acquire(
            LinuxNoFollowPathHandle journalDirectory,
            string manifestChildName,
            DataRelativePathRepairPlanManifestReaderResult
                initialManifestRead)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            initialManifestRead
        );

        if (
            !initialManifestRead.Success ||
            initialManifestRead.Manifest is null)
        {
            return Result(
                DataRelativePathRepairPlanExecutionLockState
                    .InvalidInitialManifestRead,
                string.Empty,
                initialManifestRead,
                error:
                    initialManifestRead.Error ??
                    initialManifestRead.State.ToString()
            );
        }

        LinuxFileIncarnationIdentity?
            initialManifestIdentity =
                initialManifestRead
                    .ManifestIncarnationIdentity;

        if (
            initialManifestRead.ManifestIncarnation?.Success !=
                true ||
            initialManifestIdentity is null ||
            !initialManifestIdentity.Success)
        {
            return Result(
                DataRelativePathRepairPlanExecutionLockState
                    .InitialManifestIdentityUnavailable,
                string.Empty,
                initialManifestRead,
                error:
                    initialManifestRead.ManifestIncarnation?.Error ??
                    "The initial manifest read did not retain a " +
                    "complete strong file incarnation."
            );
        }

        Guid initialPlanId =
            initialManifestRead.Manifest.PlanId;

        if (initialPlanId == Guid.Empty)
        {
            return Result(
                DataRelativePathRepairPlanExecutionLockState
                    .InvalidInitialManifestRead,
                string.Empty,
                initialManifestRead,
                error:
                    "The initial manifest has an empty PlanId."
            );
        }

        string lockChildName =
            CreateLockChildName(
                initialPlanId
            );

        LinuxExclusiveChildFileLockResult lockAcquisition =
            LinuxExclusiveChildFileLock.Acquire(
                journalDirectory,
                lockChildName
            );

        if (!lockAcquisition.Success)
        {
            return Result(
                DataRelativePathRepairPlanExecutionLockState
                    .LockUnavailable,
                lockChildName,
                initialManifestRead,
                lockAcquisition:
                    lockAcquisition,
                error:
                    lockAcquisition.Error ??
                    lockAcquisition.State.ToString()
            );
        }

        LinuxExclusiveChildFileLockLease lease =
            lockAcquisition.Lease!;

        DataRelativePathRepairPlanManifestReaderResult lockedRead =
            DataRelativePathRepairPlanManifestReader.Read(
                journalDirectory,
                manifestChildName
            );

        if (!lockedRead.Success)
        {
            lease.Dispose();

            return Result(
                DataRelativePathRepairPlanExecutionLockState
                    .LockedManifestReadFailed,
                lockChildName,
                initialManifestRead,
                lockAcquisition:
                    lockAcquisition,
                lockedManifestRead:
                    lockedRead,
                error:
                    lockedRead.Error ??
                    lockedRead.State.ToString()
            );
        }

        LinuxFileIncarnationIdentity?
            lockedManifestIdentity =
                lockedRead.ManifestIncarnationIdentity;

        if (
            lockedRead.ManifestIncarnation?.Success !=
                true ||
            lockedManifestIdentity is null ||
            !lockedManifestIdentity.Success)
        {
            lease.Dispose();

            return Result(
                DataRelativePathRepairPlanExecutionLockState
                    .LockedManifestIdentityUnavailable,
                lockChildName,
                initialManifestRead,
                lockAcquisition:
                    lockAcquisition,
                lockedManifestRead:
                    lockedRead,
                error:
                    lockedRead.ManifestIncarnation?.Error ??
                    "The locked manifest re-read did not retain a " +
                    "complete strong file incarnation."
            );
        }

        /*
         * If the PlanId changed, the lock we hold names the wrong plan.
         * Refuse before considering any plan content authoritative.
         */
        if (
            lockedRead.Manifest!.PlanId !=
            initialPlanId)
        {
            lease.Dispose();

            return Result(
                DataRelativePathRepairPlanExecutionLockState
                    .PlanIdChanged,
                lockChildName,
                initialManifestRead,
                lockAcquisition:
                    lockAcquisition,
                lockedManifestRead:
                    lockedRead,
                error:
                    "The manifest PlanId changed between the initial " +
                    "read and the locked re-read."
            );
        }

        /*
         * A manifest is immutable under CaseCompat's normal writer
         * protocol. Replacing the manifest inode between discovery and
         * lock acquisition is therefore not a resumable condition.
         *
         * Require the exact generation-aware file incarnation that the
         * initial reader observed.
         */
        if (
            !initialManifestIdentity.SameIncarnationAs(
                lockedManifestIdentity
            ))
        {
            lease.Dispose();

            return Result(
                DataRelativePathRepairPlanExecutionLockState
                    .ManifestIncarnationChanged,
                lockChildName,
                initialManifestRead,
                lockAcquisition:
                    lockAcquisition,
                lockedManifestRead:
                    lockedRead,
                error:
                    "The manifest file incarnation changed between " +
                    "the initial read and the locked re-read."
            );
        }

        return new(
            State:
                DataRelativePathRepairPlanExecutionLockState
                    .Acquired,
            LockChildName:
                lockChildName,
            InitialManifestRead:
                initialManifestRead,
            LockAcquisition:
                lockAcquisition,
            LockedManifestRead:
                lockedRead,
            Lease:
                lease,
            Error:
                null
        );
    }

    private static DataRelativePathRepairPlanExecutionLockAcquisition
        Result(
            DataRelativePathRepairPlanExecutionLockState state,
            string lockChildName,
            DataRelativePathRepairPlanManifestReaderResult
                initialManifestRead,
            LinuxExclusiveChildFileLockResult?
                lockAcquisition = null,
            DataRelativePathRepairPlanManifestReaderResult?
                lockedManifestRead = null,
            string? error = null)
    {
        return new(
            State:
                state,
            LockChildName:
                lockChildName,
            InitialManifestRead:
                initialManifestRead,
            LockAcquisition:
                lockAcquisition,
            LockedManifestRead:
                lockedManifestRead,
            Lease:
                null,
            Error:
                error
        );
    }
}
