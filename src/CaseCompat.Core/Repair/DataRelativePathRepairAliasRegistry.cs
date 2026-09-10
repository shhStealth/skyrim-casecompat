using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairAliasRegistryRecordState
{
    Recorded,
    AlreadyRecorded,
    InvalidRecord,
    WriteFailed
}

public sealed record DataRelativePathRepairAliasRegistryRecordResult(
    DataRelativePathRepairAliasRegistryRecordState State,
    string ChildName,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairAliasRegistryRecordState.Recorded;
}

public enum DataRelativePathRepairAliasRegistryLookupState
{
    Found,
    NotFound,
    RecordInvalid,
    ReadFailed
}

public sealed record DataRelativePathRepairAliasRegistryLookupResult(
    DataRelativePathRepairAliasRegistryLookupState State,
    DataRelativePathRepairAliasRecord? Record,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairAliasRegistryLookupState.Found &&
        Record is not null;
}

// Durable index of every alias this project has deliberately created,
// keyed by exactly the (parent directory, link name) pair a symlink
// alias is identified by.
//
// This is a hint, not a trusted fact: TryFind only proves a record
// exists claiming this alias was created - it proves nothing about
// what is currently on disk. Every caller that wants to actually rely
// on an alias must still re-read the real, current symlink target
// fresh and confirm it matches before trusting it for anything, per
// this project's "always re-derive fresh proof immediately before
// mutating or trusting" rule.
public static class DataRelativePathRepairAliasRegistry
{
    public static DataRelativePathRepairAliasRegistryRecordResult Record(
        LinuxNoFollowPathHandle aliasesDirectory,
        DataRelativePathRepairAliasRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            aliasesDirectory
        );

        ArgumentNullException.ThrowIfNull(
            record
        );

        string? validationError =
            DataRelativePathRepairAliasRecord.Validate(
                record
            );

        if (validationError is not null)
        {
            return new(
                State:
                    DataRelativePathRepairAliasRegistryRecordState
                        .InvalidRecord,
                ChildName:
                    string.Empty,
                Error:
                    validationError
            );
        }

        string childName =
            AliasChildName(
                record.ParentPath,
                record.LinkName
            );

        DataRelativePathRepairAliasWriterResult write =
            DataRelativePathRepairAliasWriter.CreateInitial(
                aliasesDirectory,
                childName,
                record
            );

        if (write.Success)
        {
            return new(
                State:
                    DataRelativePathRepairAliasRegistryRecordState
                        .Recorded,
                ChildName:
                    childName,
                Error:
                    null
            );
        }

        if (
            write.State ==
            DataRelativePathRepairAliasWriteState.AliasAlreadyExists)
        {
            return new(
                State:
                    DataRelativePathRepairAliasRegistryRecordState
                        .AlreadyRecorded,
                ChildName:
                    childName,
                Error:
                    write.Error
            );
        }

        return new(
            State:
                DataRelativePathRepairAliasRegistryRecordState
                    .WriteFailed,
            ChildName:
                childName,
            Error:
                write.Error ??
                write.State.ToString()
        );
    }

    public static DataRelativePathRepairAliasRegistryLookupResult TryFind(
        LinuxNoFollowPathHandle aliasesDirectory,
        string parentPath,
        string linkName)
    {
        ArgumentNullException.ThrowIfNull(
            aliasesDirectory
        );

        string childName =
            AliasChildName(
                parentPath,
                linkName
            );

        DataRelativePathRepairAliasReaderResult read =
            DataRelativePathRepairAliasReader.Read(
                aliasesDirectory,
                childName
            );

        if (read.Success)
        {
            return new(
                State:
                    DataRelativePathRepairAliasRegistryLookupState.Found,
                Record:
                    read.Record,
                Error:
                    null
            );
        }

        if (
            read.State ==
            DataRelativePathRepairAliasReadState.AliasRecordUnavailable)
        {
            return new(
                State:
                    DataRelativePathRepairAliasRegistryLookupState
                        .NotFound,
                Record:
                    null,
                Error:
                    null
            );
        }

        if (
            read.State ==
            DataRelativePathRepairAliasReadState.RecordInvalid)
        {
            return new(
                State:
                    DataRelativePathRepairAliasRegistryLookupState
                        .RecordInvalid,
                Record:
                    null,
                Error:
                    read.Error
            );
        }

        return new(
            State:
                DataRelativePathRepairAliasRegistryLookupState.ReadFailed,
            Record:
                null,
            Error:
                read.Error ??
                read.State.ToString()
        );
    }

    public static string AliasChildName(
        string parentPath,
        string linkName)
    {
        ArgumentNullException.ThrowIfNull(
            parentPath
        );

        ArgumentNullException.ThrowIfNull(
            linkName
        );

        string key =
            parentPath +
            '\0' +
            linkName;

        string hash =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        key
                    )
                )
            )[..16].ToLowerInvariant();

        return $"{hash}.alias.json";
    }
}
