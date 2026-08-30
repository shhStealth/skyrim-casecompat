using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairPlanProjectionState
{
    NotDirectStrictCaseMismatch,
    ProjectionInvariantViolation,

    SourceUnavailable,
    SourceSymbolicLinkRejected,
    SourceNotFile,
    SourceIdentityUnavailable,
    SourceSnapshotFailed,

    ExistingHierarchyChanged,
    DestinationParentOutsideDataRoot,
    DestinationParentUnavailable,
    DestinationParentSymbolicLinkRejected,
    DestinationParentNotDirectory,
    DestinationParentOpenFailed,
    DestinationParentSnapshotFailed,
    DestinationParentCasefoldNotStrict,
    DestinationInspectionFailed,
    DestinationConflict,

    Projected
}

public enum DataRelativePathRepairPlanOperationKind
{
    CreateDirectory,
    CreateFile
}

public sealed record DataRelativePathRepairSourceSnapshot(
    string PhysicalPath,
    long Size,
    string Sha256,
    LinuxFileIdentityResult Identity
);

public sealed record
    DataRelativePathRepairDestinationParentSnapshot(
        string PhysicalPath,
        LinuxFileIdentityResult Identity,
        bool CasefoldEnabled,
        long RawFlags
    );

public sealed record DataRelativePathRepairPlanOperation(
    DataRelativePathRepairPlanOperationKind Kind,
    string DestinationPath,
    string? SourcePath
);

public sealed record DataRelativePathRepairPlanProjection(
    DataRelativePathRepairPlanProjectionState State,
    DataRelativePathCaseMismatchTopologyState TopologyState,
    DataRelativePathResolution Resolution,
    DataRelativePathRepairSourceSnapshot? SourceSnapshot,
    DataRelativePathRepairDestinationParentSnapshot?
        DestinationParentSnapshot,
    IReadOnlyList<DataRelativePathRepairPlanOperation> Operations,
    string? Error
)
{
    public bool HasPlan =>
        State ==
            DataRelativePathRepairPlanProjectionState
                .Projected &&
        SourceSnapshot is not null &&
        DestinationParentSnapshot is not null &&
        Operations.Count > 0;
}
