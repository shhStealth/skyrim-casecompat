using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

public enum WindowsNamespacePhysicalFileReacquisitionState
{
    Reacquired,

    InvalidParticipant,
    InvalidAnalysis,
    ParticipantNotInAnalysis,
    InvalidIncarnationObservation,
    InvalidRelativePath,
    ParticipantDataRootMismatch,

    DataRootIncarnationObservationUnavailable,
    DataRootOpenFailed,
    DataRootIncarnationUnavailable,
    DataRootIncarnationChanged,

    DirectoryIncarnationObservationUnavailable,
    DirectoryEnumerationFailed,
    ExactDirectorySpellingUnavailable,
    DirectoryInspectionFailed,
    DirectoryNotDirectory,
    DirectoryOpenFailed,
    DirectoryIdentityUnavailable,
    DirectoryIdentityChanged,
    DirectoryIncarnationUnavailable,
    DirectoryIncarnationChanged,

    FileEnumerationFailed,
    ExactFileSpellingUnavailable,
    FileInspectionFailed,
    FileNotRegularFile,
    FileOpenFailed,
    FileIdentityChanged,

    FileIncarnationUnavailable,
    FileIncarnationChanged
}

/*
 * Successful reacquisition proves that:
 *
 * - every physical path component spelling was observed exactly,
 *   ordinally, from a retained parent directory descriptor;
 * - the reopened Data root still has the generation-aware incarnation
 *   recorded by the supplied pass-1 namespace analysis;
 * - intermediate directories were inspected no-follow, opened
 *   descriptor-relatively, rebound by device/inode/mount ID, and still
 *   have the generation-aware incarnations recorded in pass 1;
 * - the final file was inspected no-follow and opened through the
 *   descriptor-safe regular-file primitive;
 * - its current generation-aware incarnation still matches the file
 *   participant evidence recorded by the same pass-1 analysis.
 *
 * Exact spelling is observational evidence at reacquisition time.
 * The namespace may still change after this result is returned, so a
 * later content-observation phase must revalidate namespace spelling
 * after content observation before treating the combined evidence as
 * current.
 */
public sealed class WindowsNamespacePhysicalFileReacquisition
    : IDisposable
{
    internal WindowsNamespacePhysicalFileReacquisition(
        WindowsNamespacePhysicalFileReacquisitionState state,
        WindowsNamespacePhysicalParticipant participant,
        WindowsNamespaceFileIncarnationObservation?
            expectedIncarnationObservation,
        LinuxOpenedChildHandle? openedFile,
        LinuxOpenedFileIncarnationResult? actualIncarnation,
        string? failedComponent,
        string? error)
    {
        State = state;
        Participant = participant;
        ExpectedIncarnationObservation =
            expectedIncarnationObservation;
        OpenedFile = openedFile;
        ActualIncarnation = actualIncarnation;
        FailedComponent = failedComponent;
        Error = error;
    }

    public WindowsNamespacePhysicalFileReacquisitionState State
    {
        get;
    }

    public WindowsNamespacePhysicalParticipant Participant
    {
        get;
    }

    public WindowsNamespaceFileIncarnationObservation?
        ExpectedIncarnationObservation
    {
        get;
    }

    public LinuxOpenedChildHandle? OpenedFile
    {
        get;
        private set;
    }

    public LinuxOpenedFileIncarnationResult? ActualIncarnation
    {
        get;
    }

    public string? FailedComponent
    {
        get;
    }

    public string? Error
    {
        get;
    }

    public bool Success =>
        State ==
            WindowsNamespacePhysicalFileReacquisitionState.Reacquired &&
        ExpectedIncarnationObservation is not null &&
        OpenedFile is not null &&
        ActualIncarnation is not null &&
        ActualIncarnation.Success;

    public void Dispose()
    {
        OpenedFile?.Dispose();
        OpenedFile = null;
    }
}
