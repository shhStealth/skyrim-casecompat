namespace CaseCompat.Tests;

public sealed class TargetedConsumerBatchApplyConvergenceTests
{
    [Fact]
    public void
        NoProgressSincePreviousPass_IdenticalCandidateAndOutcomeCounts_ReturnsTrue()
    {
        TargetedConsumerBatchApplyPassResult previous =
            PassResult(
                passNumber: 1,
                candidateCount: 40,
                appliedCount: 0,
                rejectionCounts:
                    new()
                    {
                        ["PlanRejected:DestinationParentAmbiguous"] = 40
                    }
            );

        TargetedConsumerBatchApplyPassResult current =
            PassResult(
                passNumber: 2,
                candidateCount: 40,
                appliedCount: 0,
                rejectionCounts:
                    new()
                    {
                        ["PlanRejected:DestinationParentAmbiguous"] = 40
                    }
            );

        Assert.True(
            TargetedConsumerBatchApply.NoProgressSincePreviousPass(
                previous,
                current
            )
        );
    }

    [Fact]
    public void
        NoProgressSincePreviousPass_CandidateCountShrank_ReturnsFalse()
    {
        // A smaller candidate set on the later pass is itself evidence
        // of progress (fewer things still need fixing), even before
        // looking at applied/rejection counts.
        TargetedConsumerBatchApplyPassResult previous =
            PassResult(
                passNumber: 1,
                candidateCount: 540,
                appliedCount: 433,
                rejectionCounts:
                    new()
                    {
                        ["DurablePlanRejected:AdmissionRejected"] = 107
                    }
            );

        TargetedConsumerBatchApplyPassResult current =
            PassResult(
                passNumber: 2,
                candidateCount: 107,
                appliedCount: 107,
                rejectionCounts:
                    new()
            );

        Assert.False(
            TargetedConsumerBatchApply.NoProgressSincePreviousPass(
                previous,
                current
            )
        );
    }

    [Fact]
    public void
        NoProgressSincePreviousPass_AppliedCountDiffers_ReturnsFalse()
    {
        TargetedConsumerBatchApplyPassResult previous =
            PassResult(
                passNumber: 1,
                candidateCount: 40,
                appliedCount: 5,
                rejectionCounts:
                    new()
                    {
                        ["DurablePlanRejected:AdmissionRejected"] = 35
                    }
            );

        TargetedConsumerBatchApplyPassResult current =
            PassResult(
                passNumber: 2,
                candidateCount: 40,
                appliedCount: 10,
                rejectionCounts:
                    new()
                    {
                        ["DurablePlanRejected:AdmissionRejected"] = 30
                    }
            );

        Assert.False(
            TargetedConsumerBatchApply.NoProgressSincePreviousPass(
                previous,
                current
            )
        );
    }

    [Fact]
    public void
        NoProgressSincePreviousPass_SameTotalsButDifferentRejectionReason_ReturnsFalse()
    {
        // Same shape numerically, but the REASON changed - a different
        // subset of candidates is now stuck for a different cause. That
        // is still meaningfully different state, not a stable loop.
        TargetedConsumerBatchApplyPassResult previous =
            PassResult(
                passNumber: 1,
                candidateCount: 40,
                appliedCount: 0,
                rejectionCounts:
                    new()
                    {
                        ["DurablePlanRejected:AdmissionRejected"] = 40
                    }
            );

        TargetedConsumerBatchApplyPassResult current =
            PassResult(
                passNumber: 2,
                candidateCount: 40,
                appliedCount: 0,
                rejectionCounts:
                    new()
                    {
                        ["PlanRejected:DestinationParentAmbiguous"] = 40
                    }
            );

        Assert.False(
            TargetedConsumerBatchApply.NoProgressSincePreviousPass(
                previous,
                current
            )
        );
    }

    [Fact]
    public void
        NoProgressSincePreviousPass_SameReasonDifferentCount_ReturnsFalse()
    {
        TargetedConsumerBatchApplyPassResult previous =
            PassResult(
                passNumber: 1,
                candidateCount: 40,
                appliedCount: 0,
                rejectionCounts:
                    new()
                    {
                        ["PlanRejected:DestinationParentAmbiguous"] = 40
                    }
            );

        TargetedConsumerBatchApplyPassResult current =
            PassResult(
                passNumber: 2,
                candidateCount: 40,
                appliedCount: 0,
                rejectionCounts:
                    new()
                    {
                        ["PlanRejected:DestinationParentAmbiguous"] = 38,
                        ["DurablePlanRejected:AdmissionRejected"] = 2
                    }
            );

        Assert.False(
            TargetedConsumerBatchApply.NoProgressSincePreviousPass(
                previous,
                current
            )
        );
    }

    private static TargetedConsumerBatchApplyPassResult PassResult(
        int passNumber,
        int candidateCount,
        int appliedCount,
        Dictionary<string, int> rejectionCounts)
    {
        var runResult =
            new TargetedConsumerBatchApplyRunResult(
                Array.Empty<TargetedConsumerBatchApplyItemResult>(),
                appliedCount,
                0,
                rejectionCounts
            );

        return new TargetedConsumerBatchApplyPassResult(
            passNumber,
            candidateCount,
            runResult
        );
    }
}
