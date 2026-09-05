namespace CaseCompat.Core.Repair;

/*
 * Classification of every observed physical representation of one
 * Windows-logical aggregate namespace leaf.
 *
 * This type grants no repair, persistence, or execution authority.
 */
public enum DataRelativePathAggregateLogicalLeafState
{
    UniqueRepresentation,
    EquivalentContentMultipleRepresentations,
    ConflictingContentMultipleRepresentations
}

/*
 * One logical leaf together with the complete physical representation set
 * supplied by an aggregate namespace discovery layer.
 *
 * This primitive deliberately does not choose a repair source. Source
 * selection and authorization belong to later, separately versioned policy.
 */
public sealed record DataRelativePathAggregateLogicalLeaf(
    string WindowsLogicalPath,
    IReadOnlyList<DataRelativePathRepairSourceSnapshot>
        PhysicalRepresentations,
    DataRelativePathAggregateLogicalLeafState State
);

public static class DataRelativePathAggregateLogicalLeafClassifier
{
    public static DataRelativePathAggregateLogicalLeaf Classify(
        string windowsLogicalPath,
        IReadOnlyList<DataRelativePathRepairSourceSnapshot>
            physicalRepresentations)
    {
        if (string.IsNullOrWhiteSpace(windowsLogicalPath))
        {
            throw new ArgumentException(
                "A Windows-logical path is required.",
                nameof(windowsLogicalPath)
            );
        }

        ArgumentNullException.ThrowIfNull(
            physicalRepresentations
        );

        if (physicalRepresentations.Count == 0)
        {
            throw new ArgumentException(
                "At least one physical representation is required.",
                nameof(physicalRepresentations)
            );
        }

        if (physicalRepresentations.Any(
                representation =>
                    representation is null))
        {
            throw new ArgumentException(
                "Physical representations must not contain null entries.",
                nameof(physicalRepresentations)
            );
        }

        DataRelativePathRepairSourceSnapshot[] snapshots =
            physicalRepresentations.ToArray();

        if (snapshots.Length == 1)
        {
            return new(
                WindowsLogicalPath:
                    windowsLogicalPath,
                PhysicalRepresentations:
                    snapshots,
                State:
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation
            );
        }

        DataRelativePathRepairSourceSnapshot first =
            snapshots[0];

        bool equivalentContent =
            snapshots
                .Skip(1)
                .All(snapshot =>
                    snapshot.Size ==
                        first.Size &&
                    string.Equals(
                        snapshot.Sha256,
                        first.Sha256,
                        StringComparison.OrdinalIgnoreCase
                    )
                );

        return new(
            WindowsLogicalPath:
                windowsLogicalPath,
            PhysicalRepresentations:
                snapshots,
            State:
                equivalentContent
                    ? DataRelativePathAggregateLogicalLeafState
                        .EquivalentContentMultipleRepresentations
                    : DataRelativePathAggregateLogicalLeafState
                        .ConflictingContentMultipleRepresentations
        );
    }
}
