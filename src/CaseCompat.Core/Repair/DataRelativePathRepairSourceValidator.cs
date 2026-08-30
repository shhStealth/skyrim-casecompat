namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairSourceValidator
{
    public static DataRelativePathRepairSourceValidation
        Validate(
            string dataRoot,
            DataRelativePathRepairSourceSnapshot
                expectedSnapshot)
    {
        DataRelativePathRepairSourceLeaseAcquisition
            acquisition =
                DataRelativePathRepairSourceLeaseAcquirer
                    .Acquire(
                        dataRoot,
                        expectedSnapshot
                    );

        try
        {
            return acquisition.Validation;
        }
        finally
        {
            acquisition.Lease?.Dispose();
        }
    }
}
