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

// Identity evidence for one CreateDirectory operation whose SourcePath is
// non-null: renaming an already-populated, differently-cased physical
// directory to the requested destination name, rather than creating a
// new empty one. Renaming the whole directory - not just the one leaf
// file a candidate happens to be fixing - keeps every sibling asset
// (chargen/race variants, related textures, anything else that mod
// shipped in the same folder) together under one correctly-cased tree
// instead of splitting them across an old, mostly-full directory and a
// new, nearly-empty one.
public sealed record DataRelativePathRepairDirectoryRenameSource(
    string DestinationPath,
    string PhysicalPath,
    LinuxFileIdentityResult Identity,
    uint InodeGeneration
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
