using CaseCompat.Core.Repair;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairBatchExecutionContextTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            9,
            2,
            2,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void Create_MiddleChild_ExposesOnlyEarlierDurableMembers()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                childCount:
                    4
            );

        DataRelativePathRepairBatchExecutionContextCreation creation =
            DataRelativePathRepairBatchExecutionContext.Create(
                manifest,
                currentChildIndex:
                    2,
                expectedCurrentChild:
                    manifest.Children[2]
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        DataRelativePathRepairBatchExecutionContext context =
            Assert.IsType<
                DataRelativePathRepairBatchExecutionContext
            >(
                creation.Context
            );

        Assert.Equal(
            manifest.BatchId,
            context.BatchId
        );

        Assert.Equal(
            manifest.DataRoot,
            context.DataRoot
        );

        Assert.Equal(
            manifest.ChildManifestName,
            context.ChildManifestName
        );

        Assert.Equal(
            2,
            context.CurrentChildIndex
        );

        Assert.Equal(
            2,
            context.CurrentChild.Index
        );

        Assert.Equal(
            manifest.Children[2].ChildName,
            context.CurrentChild.ChildName
        );

        Assert.Equal(
            manifest.Children[2].PlanId,
            context.CurrentChild.PlanId
        );

        Assert.Equal(
            manifest.Children[2].ManifestSha256,
            context.CurrentChild.ManifestSha256
        );

        Assert.Equal(
            2,
            context.EarlierChildren.Count
        );

        for (
            int index = 0;
            index < context.EarlierChildren.Count;
            index++)
        {
            DataRelativePathRepairBatchExecutionChildExpectation earlier =
                context.EarlierChildren[index];

            Assert.Equal(
                index,
                earlier.Index
            );

            Assert.Equal(
                manifest.Children[index].ChildName,
                earlier.ChildName
            );

            Assert.Equal(
                manifest.Children[index].PlanId,
                earlier.PlanId
            );

            Assert.Equal(
                manifest.Children[index].ManifestSha256,
                earlier.ManifestSha256
            );
        }

        Assert.DoesNotContain(
            context.EarlierChildren,
            child =>
                child.Index >=
                    context.CurrentChildIndex
        );
    }

    [Fact]
    public void Create_FirstChild_HasNoEarlierMembers()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                childCount:
                    2
            );

        DataRelativePathRepairBatchExecutionContextCreation creation =
            DataRelativePathRepairBatchExecutionContext.Create(
                manifest,
                currentChildIndex:
                    0,
                expectedCurrentChild:
                    manifest.Children[0]
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        Assert.Empty(
            creation.Context!.EarlierChildren
        );
    }

    [Fact]
    public void Create_CurrentChildExpectationMismatch_IsRejected()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                childCount:
                    3
            );

        DataRelativePathRepairBatchManifestChild mismatched =
            manifest.Children[1] with
            {
                PlanId =
                    Guid.NewGuid()
            };

        DataRelativePathRepairBatchExecutionContextCreation creation =
            DataRelativePathRepairBatchExecutionContext.Create(
                manifest,
                currentChildIndex:
                    1,
                expectedCurrentChild:
                    mismatched
            );

        Assert.False(
            creation.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchExecutionContextCreationState
                .CurrentChildMismatch,
            creation.State
        );

        Assert.Null(
            creation.Context
        );
    }

    [Fact]
    public void Create_CurrentChildIndexOutOfRange_IsRejected()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                childCount:
                    2
            );

        DataRelativePathRepairBatchExecutionContextCreation creation =
            DataRelativePathRepairBatchExecutionContext.Create(
                manifest,
                currentChildIndex:
                    2,
                expectedCurrentChild:
                    manifest.Children[1]
            );

        Assert.False(
            creation.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchExecutionContextCreationState
                .CurrentChildIndexOutOfRange,
            creation.State
        );

        Assert.Null(
            creation.Context
        );
    }

    [Fact]
    public void Create_InvalidBatchManifest_IsRejected()
    {
        DataRelativePathRepairBatchManifestRecord valid =
            CreateManifest(
                childCount:
                    2
            );

        DataRelativePathRepairBatchManifestRecord invalid =
            valid with
            {
                BatchId =
                    Guid.Empty
            };

        DataRelativePathRepairBatchExecutionContextCreation creation =
            DataRelativePathRepairBatchExecutionContext.Create(
                invalid,
                currentChildIndex:
                    0,
                expectedCurrentChild:
                    valid.Children[0]
            );

        Assert.False(
            creation.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchExecutionContextCreationState
                .InvalidManifest,
            creation.State
        );

        Assert.Null(
            creation.Context
        );
    }

    private static DataRelativePathRepairBatchManifestRecord
        CreateManifest(
            int childCount)
    {
        var children =
            new List<
                DataRelativePathRepairBatchManifestChild
            >(
                childCount
            );

        for (
            int index = 0;
            index < childCount;
            index++)
        {
            children.Add(
                new(
                    ChildName:
                        $"plan-{index + 1:D6}",
                    PlanId:
                        Guid.NewGuid(),
                    ManifestSha256:
                        (index + 1).ToString(
                            "X64"
                        )
                )
            );
        }

        DataRelativePathRepairBatchManifestCreation creation =
            DataRelativePathRepairBatchManifest.Create(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                "repair-plan.json",
                inputPathCount:
                    childCount,
                safeRejectionCount:
                    0,
                children
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        return Assert.IsType<
            DataRelativePathRepairBatchManifestRecord
        >(
            creation.Manifest
        );
    }
}
