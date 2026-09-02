using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairPlanForwardBatchBindingTests
{
    private const string ManifestName =
        "repair-plan.json";

    private static readonly DateTimeOffset T0 =
        new(
            2026,
            9,
            2,
            3,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void
        ExecuteExpectedBatchManifest_WrongChildDescriptor_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string firstName =
            "plan-000001";

        string secondName =
            "plan-000002";

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                firstName
            )
        );

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                secondName
            )
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                temp.RootPath,
                firstName,
                secondName
            );

        DataRelativePathRepairBatchExecutionContext context =
            CreateContext(
                manifest,
                currentChildIndex:
                    0
            );

        using LinuxNoFollowPathHandle batchDirectory =
            OpenRoot(
                temp.RootPath
            );

        using LinuxNoFollowPathHandle wrongJournalDirectory =
            OpenChild(
                batchDirectory,
                secondName
            );

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor
                .ExecuteExpectedBatchManifest(
                    batchDirectory,
                    context,
                    wrongJournalDirectory,
                    temp.RootPath,
                    T0
                );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .BatchChildBindingFailed,
            execution.State
        );

        Assert.Null(
            execution.ManifestRead
        );

        Assert.Empty(
            execution.OperationResults
        );
    }

    [Fact]
    public void
        ExecuteExpectedBatchManifest_CorrectChildDescriptor_PassesBinding()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string firstName =
            "plan-000001";

        string secondName =
            "plan-000002";

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                firstName
            )
        );

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                secondName
            )
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                temp.RootPath,
                firstName,
                secondName
            );

        DataRelativePathRepairBatchExecutionContext context =
            CreateContext(
                manifest,
                currentChildIndex:
                    0
            );

        using LinuxNoFollowPathHandle batchDirectory =
            OpenRoot(
                temp.RootPath
            );

        using LinuxNoFollowPathHandle journalDirectory =
            OpenChild(
                batchDirectory,
                firstName
            );

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor
                .ExecuteExpectedBatchManifest(
                    batchDirectory,
                    context,
                    journalDirectory,
                    temp.RootPath,
                    T0
                );

        /*
         * No plan manifest is present in this focused fixture.
         *
         * ManifestReadFailed therefore proves that exact batch-child
         * descriptor binding succeeded and execution proceeded into
         * the ordinary whole-plan path.
         */
        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .ManifestReadFailed,
            execution.State
        );

        Assert.NotNull(
            execution.ManifestRead
        );

        Assert.Empty(
            execution.OperationResults
        );
    }

    private static DataRelativePathRepairBatchManifestRecord
        CreateManifest(
            string dataRoot,
            string firstName,
            string secondName)
    {
        DataRelativePathRepairBatchManifestCreation creation =
            DataRelativePathRepairBatchManifest.Create(
                Guid.NewGuid(),
                T0,
                dataRoot,
                ManifestName,
                inputPathCount:
                    2,
                safeRejectionCount:
                    0,
                [
                    new(
                        ChildName:
                            firstName,
                        PlanId:
                            Guid.NewGuid(),
                        ManifestSha256:
                            new string(
                                '1',
                                64
                            )
                    ),
                    new(
                        ChildName:
                            secondName,
                        PlanId:
                            Guid.NewGuid(),
                        ManifestSha256:
                            new string(
                                '2',
                                64
                            )
                    )
                ]
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

    private static DataRelativePathRepairBatchExecutionContext
        CreateContext(
            DataRelativePathRepairBatchManifestRecord manifest,
            int currentChildIndex)
    {
        DataRelativePathRepairBatchExecutionContextCreation creation =
            DataRelativePathRepairBatchExecutionContext.Create(
                manifest,
                currentChildIndex,
                manifest.Children[
                    currentChildIndex
                ]
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        return Assert.IsType<
            DataRelativePathRepairBatchExecutionContext
        >(
            creation.Context
        );
    }

    private static LinuxNoFollowPathHandle OpenRoot(
        string path)
    {
        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenRootReadOnly(
                path
            );

        Assert.True(
            result.Success
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
            result.OpenedPath
        );
    }

    private static LinuxNoFollowPathHandle OpenChild(
        LinuxNoFollowPathHandle parent,
        string childName)
    {
        LinuxOpenChildDirectoryReadOnlyAtResult result =
            LinuxOpenChildDirectoryReadOnlyAt.Open(
                parent,
                childName
            );

        Assert.True(
            result.Success,
            result.Error
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
            result.OpenedDirectory
        );
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-batch-child-binding-tests",
                    Guid.NewGuid()
                        .ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (
                Directory.Exists(
                    RootPath
                ))
            {
                Directory.Delete(
                    RootPath,
                    recursive:
                        true
                );
            }
        }
    }
}
