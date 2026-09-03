using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

/*
 * Reacquire one regular-file participant from a specific pass-1 namespace
 * analysis by its physical Data-relative spelling.
 *
 * This deliberately does not use a multi-component lookup as authority.
 * At each retained directory descriptor it enumerates actual direct-child
 * names and requires an ordinal exact spelling match before inspecting and
 * opening that component.
 *
 * This is read-only. It performs no hashing and no repair operation.
 */
public static class WindowsNamespacePhysicalFileReacquirer
{
    public static WindowsNamespacePhysicalFileReacquisition Reacquire(
        WindowsNamespaceAnalysis analysis,
        WindowsNamespacePhysicalParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(
            analysis
        );

        ArgumentNullException.ThrowIfNull(
            participant
        );

        if (
            participant.Kind !=
                WindowsNamespacePhysicalObjectKind.File ||
            participant.IdentityError is not null ||
            participant.DeviceMajor is null ||
            participant.DeviceMinor is null ||
            participant.Inode is null ||
            participant.MountId is null)
        {
            return Result(
                WindowsNamespacePhysicalFileReacquisitionState
                    .InvalidParticipant,
                participant,
                expected:
                    null,
                error:
                    "The participant is not a regular file with a " +
                    "complete physical identity."
            );
        }

        if (!AnalysisEvidenceShapeUsable(
                analysis,
                out string? analysisError))
        {
            return Result(
                WindowsNamespacePhysicalFileReacquisitionState
                    .InvalidAnalysis,
                participant,
                expected:
                    null,
                error:
                    analysisError
            );
        }

        if (!AnalysisContainsParticipant(
                analysis,
                participant,
                out string? participantError))
        {
            return Result(
                WindowsNamespacePhysicalFileReacquisitionState
                    .ParticipantNotInAnalysis,
                participant,
                expected:
                    null,
                error:
                    participantError
            );
        }

        WindowsNamespaceFileIncarnationObservation?
            expectedIncarnationObservation =
                FindExpectedFileIncarnationObservation(
                    analysis,
                    participant,
                    out string? fileEvidenceError
                );

        if (expectedIncarnationObservation is null)
        {
            return Result(
                WindowsNamespacePhysicalFileReacquisitionState
                    .InvalidIncarnationObservation,
                participant,
                expected:
                    null,
                error:
                    fileEvidenceError
            );
        }

        string[]? components =
            SplitPhysicalRelativePath(
                participant.RelativePath
            );

        if (
            components is null ||
            components.Length == 0)
        {
            return Result(
                WindowsNamespacePhysicalFileReacquisitionState
                    .InvalidRelativePath,
                participant,
                expectedIncarnationObservation,
                error:
                    "The participant relative path is not a valid " +
                    "physical Data-relative path."
            );
        }

        if (!ParticipantBelongsToDataRoot(
                analysis.DataRootPath,
                participant,
                components,
                out string? provenanceError))
        {
            return Result(
                WindowsNamespacePhysicalFileReacquisitionState
                    .ParticipantDataRootMismatch,
                participant,
                expectedIncarnationObservation,
                error:
                    provenanceError
            );
        }

        LinuxNoFollowPathOpenResult rootOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                analysis.DataRootPath
            );

        if (
            !rootOpen.Success ||
            rootOpen.OpenedPath is null)
        {
            return Result(
                WindowsNamespacePhysicalFileReacquisitionState
                    .DataRootOpenFailed,
                participant,
                expectedIncarnationObservation,
                error:
                    rootOpen.Error ??
                    rootOpen.State.ToString()
            );
        }

        LinuxNoFollowPathHandle currentDirectory =
            rootOpen.OpenedPath;

        try
        {
            WindowsNamespaceDirectoryIncarnationObservation?
                expectedDataRootIncarnation =
                    FindExpectedDirectoryIncarnationObservation(
                        analysis,
                        ".",
                        out string? rootEvidenceError
                    );

            if (expectedDataRootIncarnation is null)
            {
                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .DataRootIncarnationObservationUnavailable,
                    participant,
                    expectedIncarnationObservation,
                    failedComponent:
                        ".",
                    error:
                        rootEvidenceError
                );
            }

            LinuxOpenedDirectoryIncarnationResult
                actualDataRootIncarnation =
                    LinuxOpenedDirectoryIncarnation.Capture(
                        currentDirectory,
                        analysis.DataRootPath
                    );

            if (
                !actualDataRootIncarnation.Success ||
                actualDataRootIncarnation.Identity is null)
            {
                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .DataRootIncarnationUnavailable,
                    participant,
                    expectedIncarnationObservation,
                    failedComponent:
                        ".",
                    error:
                        actualDataRootIncarnation.Error ??
                        actualDataRootIncarnation.State.ToString()
                );
            }

            if (!MatchesExpectedDirectoryIncarnation(
                    expectedDataRootIncarnation,
                    actualDataRootIncarnation.Identity))
            {
                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .DataRootIncarnationChanged,
                    participant,
                    expectedIncarnationObservation,
                    failedComponent:
                        ".",
                    error:
                        "The reopened Data root does not match the " +
                        "generation-aware directory incarnation recorded " +
                        "during pass-1 namespace analysis."
                );
            }

            for (
                int index = 0;
                index < components.Length - 1;
                index++)
            {
                string component =
                    components[index];

                LinuxEnumerateDirectoryAtResult enumeration =
                    LinuxEnumerateDirectoryAt.Enumerate(
                        currentDirectory
                    );

                if (!enumeration.Success)
                {
                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .DirectoryEnumerationFailed,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            enumeration.Error ??
                            enumeration.State.ToString()
                    );
                }

                if (!ContainsExactName(
                        enumeration.ChildNames,
                        component))
                {
                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .ExactDirectorySpellingUnavailable,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            "The exact physical directory spelling is " +
                            "not present in the retained parent."
                    );
                }

                LinuxInspectChildAtResult inspection =
                    LinuxInspectChildAt.Inspect(
                        currentDirectory,
                        component
                    );

                if (!inspection.Success)
                {
                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .DirectoryInspectionFailed,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            inspection.Error ??
                            inspection.State.ToString()
                    );
                }

                if (
                    inspection.Kind !=
                        LinuxChildObjectKind.Directory)
                {
                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .DirectoryNotDirectory,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            "The exact physical component is no longer " +
                            "a directory."
                    );
                }

                LinuxOpenChildDirectoryReadOnlyAtResult opened =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        currentDirectory,
                        component
                    );

                if (
                    !opened.Success ||
                    opened.OpenedDirectory is null)
                {
                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .DirectoryOpenFailed,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            opened.Error ??
                            opened.State.ToString()
                    );
                }

                LinuxNoFollowPathHandle childDirectory =
                    opened.OpenedDirectory;

                LinuxOpenedDirectoryIdentityResult
                    openedIdentity =
                        LinuxOpenedDirectoryIdentity.Capture(
                            childDirectory
                        );

                if (!openedIdentity.Success)
                {
                    childDirectory.Dispose();

                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .DirectoryIdentityUnavailable,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            openedIdentity.Error ??
                            openedIdentity.State.ToString()
                    );
                }

                if (!SameDirectoryIdentity(
                        inspection,
                        openedIdentity))
                {
                    childDirectory.Dispose();

                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .DirectoryIdentityChanged,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            "The directory identity changed between " +
                            "no-follow inspection and descriptor open."
                    );
                }

                string directoryRelativePath =
                    string.Join(
                        "/",
                        components.Take(
                            index + 1
                        )
                    );

                WindowsNamespaceDirectoryIncarnationObservation?
                    expectedDirectoryIncarnation =
                        FindExpectedDirectoryIncarnationObservation(
                            analysis,
                            directoryRelativePath,
                            out string? directoryEvidenceError
                        );

                if (expectedDirectoryIncarnation is null)
                {
                    childDirectory.Dispose();

                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .DirectoryIncarnationObservationUnavailable,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            directoryEvidenceError
                    );
                }

                LinuxOpenedDirectoryIncarnationResult
                    actualDirectoryIncarnation =
                        LinuxOpenedDirectoryIncarnation.Capture(
                            childDirectory,
                            expectedDirectoryIncarnation.FullPath
                        );

                if (
                    !actualDirectoryIncarnation.Success ||
                    actualDirectoryIncarnation.Identity is null)
                {
                    childDirectory.Dispose();

                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .DirectoryIncarnationUnavailable,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            actualDirectoryIncarnation.Error ??
                            actualDirectoryIncarnation.State.ToString()
                    );
                }

                if (!MatchesExpectedDirectoryIncarnation(
                        expectedDirectoryIncarnation,
                        actualDirectoryIncarnation.Identity))
                {
                    childDirectory.Dispose();

                    return Result(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .DirectoryIncarnationChanged,
                        participant,
                        expectedIncarnationObservation,
                        failedComponent:
                            component,
                        error:
                            "The reopened directory does not match the " +
                            "generation-aware incarnation recorded for " +
                            "this physical prefix during pass-1 analysis."
                    );
                }

                /*
                 * The child descriptor is now the authority for the next
                 * component. The previous parent can be released.
                 */
                currentDirectory.Dispose();
                currentDirectory =
                    childDirectory;
            }

            string fileName =
                components[^1];

            LinuxEnumerateDirectoryAtResult fileEnumeration =
                LinuxEnumerateDirectoryAt.Enumerate(
                    currentDirectory
                );

            if (!fileEnumeration.Success)
            {
                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .FileEnumerationFailed,
                    participant,
                    expectedIncarnationObservation,
                    failedComponent:
                        fileName,
                    error:
                        fileEnumeration.Error ??
                        fileEnumeration.State.ToString()
                );
            }

            if (!ContainsExactName(
                    fileEnumeration.ChildNames,
                    fileName))
            {
                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .ExactFileSpellingUnavailable,
                    participant,
                    expectedIncarnationObservation,
                    failedComponent:
                        fileName,
                    error:
                        "The exact physical file spelling is not " +
                        "present in the retained parent."
                );
            }

            LinuxInspectChildAtResult fileInspection =
                LinuxInspectChildAt.Inspect(
                    currentDirectory,
                    fileName
                );

            if (!fileInspection.Success)
            {
                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .FileInspectionFailed,
                    participant,
                    expectedIncarnationObservation,
                    failedComponent:
                        fileName,
                    error:
                        fileInspection.Error ??
                        fileInspection.State.ToString()
                );
            }

            if (
                fileInspection.Kind !=
                    LinuxChildObjectKind.RegularFile)
            {
                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .FileNotRegularFile,
                    participant,
                    expectedIncarnationObservation,
                    failedComponent:
                        fileName,
                    error:
                        "The exact physical target is no longer a " +
                        "regular file."
                );
            }

            LinuxOpenChildRegularFileReadOnlyAtResult openedFile =
                LinuxOpenChildRegularFileReadOnlyAt.Open(
                    currentDirectory,
                    fileName
                );

            if (
                !openedFile.Success ||
                openedFile.OpenedFile is null ||
                openedFile.Identity is null)
            {
                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .FileOpenFailed,
                    participant,
                    expectedIncarnationObservation,
                    failedComponent:
                        fileName,
                    error:
                        openedFile.Error ??
                        openedFile.State.ToString()
                );
            }

            LinuxOpenedChildHandle retainedFile =
                openedFile.OpenedFile;

            if (!SameFileIdentity(
                    fileInspection,
                    openedFile.Identity))
            {
                retainedFile.Dispose();

                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .FileIdentityChanged,
                    participant,
                    expectedIncarnationObservation,
                    failedComponent:
                        fileName,
                    error:
                        "The file identity changed between no-follow " +
                        "inspection and descriptor-safe open."
                );
            }

            LinuxOpenedFileIncarnationResult actualIncarnation =
                LinuxOpenedFileIncarnation.Capture(
                    retainedFile
                );

            if (
                !actualIncarnation.Success ||
                actualIncarnation.Identity is null)
            {
                retainedFile.Dispose();

                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .FileIncarnationUnavailable,
                    participant,
                    expectedIncarnationObservation,
                    actualIncarnation:
                        actualIncarnation,
                    failedComponent:
                        fileName,
                    error:
                        actualIncarnation.Error ??
                        actualIncarnation.State.ToString()
                );
            }

            if (!MatchesExpectedIncarnation(
                    participant,
                    expectedIncarnationObservation,
                    actualIncarnation.Identity))
            {
                retainedFile.Dispose();

                return Result(
                    WindowsNamespacePhysicalFileReacquisitionState
                        .FileIncarnationChanged,
                    participant,
                    expectedIncarnationObservation,
                    actualIncarnation:
                        actualIncarnation,
                    failedComponent:
                        fileName,
                    error:
                        "The reacquired regular file does not match " +
                        "the generation-aware participant incarnation " +
                        "recorded during namespace analysis."
                );
            }

            return new WindowsNamespacePhysicalFileReacquisition(
                state:
                    WindowsNamespacePhysicalFileReacquisitionState
                        .Reacquired,
                participant:
                    participant,
                expectedIncarnationObservation:
                    expectedIncarnationObservation,
                openedFile:
                    retainedFile,
                actualIncarnation:
                    actualIncarnation,
                failedComponent:
                    null,
                error:
                    null
            );
        }
        finally
        {
            currentDirectory.Dispose();
        }
    }

    private static bool AnalysisEvidenceShapeUsable(
        WindowsNamespaceAnalysis analysis,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(
                analysis.DataRootPath))
        {
            error =
                "The supplied namespace analysis has no usable Data root.";

            return false;
        }

        if (!analysis.Complete)
        {
            error =
                "The supplied pass-1 namespace analysis is incomplete. " +
                "Physical-file reacquisition requires a complete analysis.";

            return false;
        }

        if (
            analysis.Nodes is null ||
            analysis.DirectoryIncarnationObservations is null ||
            analysis.FileIncarnationObservations is null)
        {
            error =
                "The supplied namespace analysis is missing required " +
                "participant or incarnation-evidence collections.";

            return false;
        }

        error =
            null;

        return true;
    }

    private static bool AnalysisContainsParticipant(
        WindowsNamespaceAnalysis analysis,
        WindowsNamespacePhysicalParticipant participant,
        out string? error)
    {
        int matches =
            0;

        foreach (WindowsNamespaceNode node in analysis.Nodes)
        {
            foreach (
                WindowsNamespacePhysicalParticipant candidate
                in node.Participants)
            {
                if (candidate == participant)
                {
                    matches++;
                }
            }
        }

        if (matches != 1)
        {
            error =
                matches == 0
                    ? "The participant is not present in the supplied " +
                        "pass-1 namespace analysis."
                    : "The supplied pass-1 namespace analysis contains " +
                        "the participant more than once.";

            return false;
        }

        error =
            null;

        return true;
    }

    private static WindowsNamespaceFileIncarnationObservation?
        FindExpectedFileIncarnationObservation(
            WindowsNamespaceAnalysis analysis,
            WindowsNamespacePhysicalParticipant participant,
            out string? error)
    {
        WindowsNamespaceFileIncarnationObservation? match =
            null;

        int matches =
            0;

        foreach (
            WindowsNamespaceFileIncarnationObservation observation
            in analysis.FileIncarnationObservations)
        {
            if (
                string.Equals(
                    observation.RelativePath,
                    participant.RelativePath,
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    observation.FullPath,
                    participant.FullPath,
                    StringComparison.Ordinal
                ))
            {
                match =
                    observation;

                matches++;
            }
        }

        if (matches != 1)
        {
            error =
                matches == 0
                    ? "No pass-1 file-incarnation observation describes " +
                        "this participant."
                    : "More than one pass-1 file-incarnation observation " +
                        "describes this participant.";

            return null;
        }

        if (
            match!.Error is not null ||
            match.InodeGeneration is null)
        {
            error =
                "The pass-1 file-incarnation observation for this " +
                "participant is incomplete.";

            return null;
        }

        error =
            null;

        return match;
    }

    private static WindowsNamespaceDirectoryIncarnationObservation?
        FindExpectedDirectoryIncarnationObservation(
            WindowsNamespaceAnalysis analysis,
            string relativePath,
            out string? error)
    {
        WindowsNamespaceDirectoryIncarnationObservation? match =
            null;

        int matches =
            0;

        foreach (
            WindowsNamespaceDirectoryIncarnationObservation observation
            in analysis.DirectoryIncarnationObservations)
        {
            if (string.Equals(
                    observation.RelativePath,
                    relativePath,
                    StringComparison.Ordinal))
            {
                match =
                    observation;

                matches++;
            }
        }

        if (matches != 1)
        {
            error =
                matches == 0
                    ? $"No pass-1 directory-incarnation observation " +
                        $"exists for physical prefix '{relativePath}'."
                    : $"More than one pass-1 directory-incarnation " +
                        $"observation exists for physical prefix " +
                        $"'{relativePath}'.";

            return null;
        }

        if (
            match!.Error is not null ||
            match.DeviceMajor is null ||
            match.DeviceMinor is null ||
            match.Inode is null ||
            match.MountId is null ||
            match.InodeGeneration is null)
        {
            error =
                $"The pass-1 directory-incarnation observation for " +
                $"physical prefix '{relativePath}' is incomplete.";

            return null;
        }

        string projectedFullPath;
        string observedFullPath;

        try
        {
            string fullDataRoot =
                Path.GetFullPath(
                    analysis.DataRootPath
                );

            projectedFullPath =
                relativePath == "."
                    ? fullDataRoot
                    : Path.GetFullPath(
                        Path.Combine(
                            fullDataRoot,
                            relativePath.Replace(
                                '/',
                                Path.DirectorySeparatorChar
                            )
                        )
                    );

            observedFullPath =
                Path.GetFullPath(
                    match.FullPath
                );
        }
        catch (Exception ex)
        {
            error =
                $"The pass-1 directory-incarnation provenance for " +
                $"'{relativePath}' is invalid: {ex.Message}";

            return null;
        }

        if (!string.Equals(
                projectedFullPath,
                observedFullPath,
                StringComparison.Ordinal))
        {
            error =
                $"The pass-1 directory-incarnation FullPath for " +
                $"'{relativePath}' does not correspond to the supplied " +
                "analysis Data root.";

            return null;
        }

        error =
            null;

        return match;
    }

    private static bool MatchesExpectedDirectoryIncarnation(
        WindowsNamespaceDirectoryIncarnationObservation expected,
        LinuxDirectoryIncarnationIdentity actual)
    {
        return
            actual.Success &&
            actual.PhysicalIdentity.DeviceMajor ==
                expected.DeviceMajor &&
            actual.PhysicalIdentity.DeviceMinor ==
                expected.DeviceMinor &&
            actual.PhysicalIdentity.Inode ==
                expected.Inode &&
            actual.PhysicalIdentity.MountId ==
                expected.MountId &&
            actual.InodeGeneration ==
                expected.InodeGeneration;
    }

    private static string[]? SplitPhysicalRelativePath(
        string relativePath)
    {
        if (
            string.IsNullOrWhiteSpace(
                relativePath
            ) ||
            Path.IsPathRooted(
                relativePath
            ) ||
            relativePath.Contains('\\') ||
            relativePath.Contains('\0'))
        {
            return null;
        }

        string[] components =
            relativePath.Split(
                '/',
                StringSplitOptions.None
            );

        if (
            components.Length == 0 ||
            components.Any(component =>
                string.IsNullOrEmpty(component) ||
                component is "." or ".."))
        {
            return null;
        }

        return components;
    }

    /*
     * Path strings are checked here only as provenance for the supplied
     * analysis record. Filesystem authority remains entirely descriptor
     * based below.
     */
    private static bool ParticipantBelongsToDataRoot(
        string dataRootDirectory,
        WindowsNamespacePhysicalParticipant participant,
        IReadOnlyList<string> components,
        out string? error)
    {
        string fullDataRoot;
        string participantFullPath;
        string projectedFullPath;

        try
        {
            fullDataRoot =
                Path.GetFullPath(
                    dataRootDirectory
                );

            participantFullPath =
                Path.GetFullPath(
                    participant.FullPath
                );

            projectedFullPath =
                Path.GetFullPath(
                    Path.Combine(
                        fullDataRoot,
                        Path.Combine(
                            components.ToArray()
                        )
                    )
                );
        }
        catch (Exception ex)
        {
            error =
                "The participant/Data-root provenance is invalid: " +
                ex.Message;

            return false;
        }

        if (
            !string.Equals(
                participantFullPath,
                projectedFullPath,
                StringComparison.Ordinal
            ))
        {
            error =
                "The participant FullPath does not correspond to its " +
                "physical RelativePath beneath the supplied Data root.";

            return false;
        }

        if (
            !string.Equals(
                participant.Name,
                components[^1],
                StringComparison.Ordinal
            ))
        {
            error =
                "The participant Name does not match the final physical " +
                "RelativePath component.";

            return false;
        }

        error =
            null;

        return true;
    }

    private static bool ContainsExactName(
        IReadOnlyList<string> names,
        string expectedName)
    {
        return names.Contains(
            expectedName,
            StringComparer.Ordinal
        );
    }

    private static bool SameDirectoryIdentity(
        LinuxInspectChildAtResult inspected,
        LinuxOpenedDirectoryIdentityResult opened)
    {
        return
            inspected.Success &&
            inspected.Kind ==
                LinuxChildObjectKind.Directory &&
            opened.Success &&
            inspected.DeviceMajor ==
                opened.DeviceMajor &&
            inspected.DeviceMinor ==
                opened.DeviceMinor &&
            inspected.Inode ==
                opened.Inode &&
            inspected.MountId ==
                opened.MountId;
    }

    private static bool SameFileIdentity(
        LinuxInspectChildAtResult inspected,
        LinuxOpenedFileIdentityResult opened)
    {
        return
            inspected.Success &&
            inspected.Kind ==
                LinuxChildObjectKind.RegularFile &&
            opened.Success &&
            inspected.DeviceMajor ==
                opened.DeviceMajor &&
            inspected.DeviceMinor ==
                opened.DeviceMinor &&
            inspected.Inode ==
                opened.Inode &&
            inspected.MountId ==
                opened.MountId;
    }

    private static bool MatchesExpectedIncarnation(
        WindowsNamespacePhysicalParticipant participant,
        WindowsNamespaceFileIncarnationObservation expected,
        LinuxFileIncarnationIdentity actual)
    {
        return
            actual.Success &&
            actual.PhysicalIdentity.DeviceMajor ==
                participant.DeviceMajor &&
            actual.PhysicalIdentity.DeviceMinor ==
                participant.DeviceMinor &&
            actual.PhysicalIdentity.Inode ==
                participant.Inode &&
            actual.PhysicalIdentity.MountId ==
                participant.MountId &&
            actual.InodeGeneration ==
                expected.InodeGeneration;
    }

    private static WindowsNamespacePhysicalFileReacquisition Result(
        WindowsNamespacePhysicalFileReacquisitionState state,
        WindowsNamespacePhysicalParticipant participant,
        WindowsNamespaceFileIncarnationObservation? expected,
        LinuxOpenedFileIncarnationResult? actualIncarnation = null,
        string? failedComponent = null,
        string? error = null)
    {
        return new WindowsNamespacePhysicalFileReacquisition(
            state,
            participant,
            expected,
            openedFile:
                null,
            actualIncarnation,
            failedComponent,
            error
        );
    }
}
