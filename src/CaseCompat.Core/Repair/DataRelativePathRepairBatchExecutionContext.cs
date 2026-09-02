namespace CaseCompat.Core.Repair;

/*
 * Immutable batch-membership expectation for one child.
 *
 * This is metadata only. It carries no filesystem handle, ownership,
 * journal authority, or permission to mutate anything.
 */
public sealed record
    DataRelativePathRepairBatchExecutionChildExpectation(
        int Index,
        string ChildName,
        Guid PlanId,
        string ManifestSha256
    );

public enum DataRelativePathRepairBatchExecutionContextCreationState
{
    Created,

    InvalidManifest,
    CurrentChildIndexOutOfRange,
    CurrentChildMismatch
}

public sealed record
    DataRelativePathRepairBatchExecutionContextCreation(
        DataRelativePathRepairBatchExecutionContextCreationState State,
        DataRelativePathRepairBatchExecutionContext? Context,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathRepairBatchExecutionContextCreationState
                .Created &&
        Context is not null;
}

/*
 * Immutable logical context for one child of an already-verified batch.
 *
 * The context deliberately exposes only durable batch members that precede
 * CurrentChildIndex. Therefore a later reuse-authorizer cannot accidentally
 * nominate the current child or a future child as the historical owner.
 *
 * This object is NOT mutation authority.
 *
 * In particular:
 *
 *   - it does not retain the batch-directory descriptor;
 *   - it does not prove that an earlier child still has the same manifest;
 *   - it does not prove any operation journal;
 *   - it does not prove any filesystem incarnation;
 *   - it does not authorize BatchReused journal publication.
 *
 * A future mutating authorizer must reopen selected earlier children from
 * the retained batch-directory descriptor and revalidate their exact
 * PlanId / manifest SHA / operation journal at mutation time.
 */
public sealed record DataRelativePathRepairBatchExecutionContext(
    Guid BatchId,
    string DataRoot,
    string ChildManifestName,
    int CurrentChildIndex,
    DataRelativePathRepairBatchExecutionChildExpectation CurrentChild,
    IReadOnlyList<
        DataRelativePathRepairBatchExecutionChildExpectation>
        EarlierChildren
)
{
    public static DataRelativePathRepairBatchExecutionContextCreation
        Create(
            DataRelativePathRepairBatchManifestRecord manifest,
            int currentChildIndex,
            DataRelativePathRepairBatchManifestChild expectedCurrentChild)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        ArgumentNullException.ThrowIfNull(
            expectedCurrentChild
        );

        string? validationError =
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            );

        if (validationError is not null)
        {
            return Failure(
                DataRelativePathRepairBatchExecutionContextCreationState
                    .InvalidManifest,
                "The batch execution context requires a valid batch " +
                    $"manifest: {validationError}"
            );
        }

        if (
            currentChildIndex < 0 ||
            currentChildIndex >= manifest.Children.Count)
        {
            return Failure(
                DataRelativePathRepairBatchExecutionContextCreationState
                    .CurrentChildIndexOutOfRange,
                $"Current child index {currentChildIndex} is outside the " +
                    $"verified batch range 0.." +
                    $"{manifest.Children.Count - 1}."
            );
        }

        DataRelativePathRepairBatchManifestChild actualCurrentChild =
            manifest.Children[
                currentChildIndex
            ];

        if (
            !string.Equals(
                actualCurrentChild.ChildName,
                expectedCurrentChild.ChildName,
                StringComparison.Ordinal
            ) ||
            actualCurrentChild.PlanId !=
                expectedCurrentChild.PlanId ||
            !string.Equals(
                actualCurrentChild.ManifestSha256,
                expectedCurrentChild.ManifestSha256,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return Failure(
                DataRelativePathRepairBatchExecutionContextCreationState
                    .CurrentChildMismatch,
                "The requested current child does not match the exact " +
                    "batch member recorded at that durable batch index."
            );
        }

        var earlier =
            new DataRelativePathRepairBatchExecutionChildExpectation[
                currentChildIndex
            ];

        for (
            int index = 0;
            index < currentChildIndex;
            index++)
        {
            earlier[index] =
                FromManifestChild(
                    index,
                    manifest.Children[index]
                );
        }

        DataRelativePathRepairBatchExecutionContext context =
            new(
                BatchId:
                    manifest.BatchId,
                DataRoot:
                    manifest.DataRoot,
                ChildManifestName:
                    manifest.ChildManifestName,
                CurrentChildIndex:
                    currentChildIndex,
                CurrentChild:
                    FromManifestChild(
                        currentChildIndex,
                        actualCurrentChild
                    ),
                EarlierChildren:
                    Array.AsReadOnly(
                        earlier
                    )
            );

        return new(
            State:
                DataRelativePathRepairBatchExecutionContextCreationState
                    .Created,
            Context:
                context,
            Error:
                null
        );
    }

    private static
        DataRelativePathRepairBatchExecutionChildExpectation
        FromManifestChild(
            int index,
            DataRelativePathRepairBatchManifestChild child)
    {
        return new(
            Index:
                index,
            ChildName:
                child.ChildName,
            PlanId:
                child.PlanId,
            ManifestSha256:
                child.ManifestSha256
        );
    }

    private static DataRelativePathRepairBatchExecutionContextCreation
        Failure(
            DataRelativePathRepairBatchExecutionContextCreationState state,
            string error)
    {
        return new(
            State:
                state,
            Context:
                null,
            Error:
                error
        );
    }
}
