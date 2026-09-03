using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

/*
 * Read-only Windows-logical namespace inventory.
 *
 * Filesystem authority for recursive traversal comes only from retained
 * Linux directory descriptors:
 *
 * - Data is opened with O_DIRECTORY | O_NOFOLLOW;
 * - directories are enumerated descriptor-relatively;
 * - direct children are inspected with statx(AT_SYMLINK_NOFOLLOW);
 * - only children proven to be directories are opened;
 * - child directories are opened with O_DIRECTORY | O_NOFOLLOW;
 * - the pre-open child identity is compared with identity captured from
 *   the retained opened descriptor before recursion.
 *
 * Physical path strings stored in the result are descriptive provenance.
 * They are not filesystem authority.
 */
public static class WindowsNamespaceAnalyzer
{
    private readonly record struct DirectoryIdentity(
        uint DeviceMajor,
        uint DeviceMinor,
        ulong Inode,
        ulong MountId
    );

    public static WindowsNamespaceAnalysis Analyze(
        string dataRootDirectory,
        string namespaceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRootDirectory
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            namespaceName
        );

        if (namespaceName.Contains('/') ||
            namespaceName.Contains('\\'))
        {
            throw new ArgumentException(
                "The initial namespace analyzer accepts " +
                "one direct Data child name.",
                nameof(namespaceName)
            );
        }

        string dataRootPath =
            Path.GetFullPath(
                dataRootDirectory
            );

        WindowsLogicalPath rootLogicalPath =
            WindowsLogicalPath.FromRelativePath(
                namespaceName
            );

        var participantsByLogicalPath =
            new Dictionary<
                WindowsLogicalPath,
                List<WindowsNamespacePhysicalParticipant>
            >();

        var errors =
            new List<string>();

        var directoryLookupObservations =
            new List<
                WindowsNamespaceDirectoryLookupObservation
            >();

        var directoryIncarnationObservations =
            new List<
                WindowsNamespaceDirectoryIncarnationObservation
            >();

        var fileIncarnationObservations =
            new List<
                WindowsNamespaceFileIncarnationObservation
            >();

        LinuxNoFollowPathOpenResult rootOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                dataRootPath
            );

        if (
            !rootOpen.Success ||
            rootOpen.OpenedPath is null)
        {
            errors.Add(
                $"The Data root could not be opened safely " +
                $"({rootOpen.State}): " +
                (
                    rootOpen.Error ??
                    "no additional error"
                )
            );

            return BuildAnalysis(
                dataRootPath,
                rootLogicalPath,
                participantsByLogicalPath,
                directoryLookupObservations,
                directoryIncarnationObservations,
                fileIncarnationObservations,
                errors
            );
        }

        using LinuxNoFollowPathHandle dataRoot =
            rootOpen.OpenedPath;

        LinuxOpenedDirectoryIncarnationResult
            dataRootIncarnation =
                LinuxOpenedDirectoryIncarnation.Capture(
                    dataRoot,
                    dataRootPath
                );

        AddDirectoryIncarnationObservation(
            directoryIncarnationObservations,
            fullPath:
                dataRootPath,
            relativePath:
                ".",
            dataRootIncarnation
        );

        if (!dataRootIncarnation.Success)
        {
            errors.Add(
                $"Data: directory incarnation unavailable " +
                $"({dataRootIncarnation.State}): " +
                (
                    dataRootIncarnation.Error ??
                    "no additional error"
                )
            );
        }

        LinuxOpenedDirectorySnapshotResult
            dataRootLookupSnapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    dataRoot,
                    dataRootPath
                );

        AddDirectoryLookupObservation(
            directoryLookupObservations,
            fullPath:
                dataRootPath,
            relativePath:
                ".",
            dataRootLookupSnapshot
        );

        if (!dataRootLookupSnapshot.Success)
        {
            errors.Add(
                $"Data: directory lookup semantics unavailable " +
                $"({dataRootLookupSnapshot.State}): " +
                (
                    dataRootLookupSnapshot.Error ??
                    "no additional error"
                )
            );
        }

        LinuxEnumerateDirectoryAtResult
            rootEnumeration =
                LinuxEnumerateDirectoryAt.Enumerate(
                    dataRoot
                );

        if (!rootEnumeration.Success)
        {
            errors.Add(
                $"The Data root could not be enumerated " +
                $"descriptor-relatively " +
                $"({rootEnumeration.State}): " +
                (
                    rootEnumeration.Error ??
                    "no additional error"
                )
            );

            return BuildAnalysis(
                dataRootPath,
                rootLogicalPath,
                participantsByLogicalPath,
                directoryLookupObservations,
                directoryIncarnationObservations,
                fileIncarnationObservations,
                errors
            );
        }

        var visitedDirectories =
            new HashSet<DirectoryIdentity>();

        bool foundRepresentative =
            false;

        bool retainedDirectoryRepresentative =
            false;

        foreach (
            string childName
            in rootEnumeration.ChildNames)
        {
            if (!WindowsEquivalent(
                    childName,
                    namespaceName))
            {
                continue;
            }

            foundRepresentative =
                true;

            LinuxInspectChildAtResult inspection =
                LinuxInspectChildAt.Inspect(
                    dataRoot,
                    childName
                );

            if (!inspection.Success)
            {
                errors.Add(
                    $"Data/{childName}: child inspection failed " +
                    $"({inspection.State}): " +
                    (
                        inspection.Error ??
                        "no additional error"
                    )
                );

                continue;
            }

            string relativePath =
                childName;

            WindowsNamespacePhysicalParticipant
                participant =
                    CreateParticipant(
                        dataRootPath,
                        relativePath,
                        childName,
                        inspection
                    );

            AddParticipant(
                participantsByLogicalPath,
                rootLogicalPath,
                participant
            );

            switch (inspection.Kind)
            {
                case LinuxChildObjectKind.Directory:
                {
                    LinuxOpenChildDirectoryReadOnlyAtResult
                        opened =
                            LinuxOpenChildDirectoryReadOnlyAt
                                .Open(
                                    dataRoot,
                                    childName
                                );

                    if (
                        !opened.Success ||
                        opened.OpenedDirectory is null)
                    {
                        errors.Add(
                            $"Data/{childName}: directory open failed " +
                            $"after inspection ({opened.State}): " +
                            (
                                opened.Error ??
                                "no additional error"
                            )
                        );

                        continue;
                    }

                    using LinuxNoFollowPathHandle directory =
                        opened.OpenedDirectory;

                    LinuxOpenedDirectoryIdentityResult
                        openedIdentity =
                            LinuxOpenedDirectoryIdentity
                                .Capture(
                                    directory
                                );

                    if (!openedIdentity.Success)
                    {
                        errors.Add(
                            $"Data/{childName}: opened-directory " +
                            $"identity capture failed " +
                            $"({openedIdentity.State}): " +
                            (
                                openedIdentity.Error ??
                                "no additional error"
                            )
                        );

                        continue;
                    }

                    if (!SameDirectoryIdentity(
                            inspection,
                            openedIdentity))
                    {
                        errors.Add(
                            $"Data/{childName}: physical directory " +
                            "identity changed between inspection and " +
                            "descriptor-relative open."
                        );

                        continue;
                    }

                    LinuxOpenedDirectoryIncarnationResult
                        directoryIncarnation =
                            LinuxOpenedDirectoryIncarnation.Capture(
                                directory,
                                participant.FullPath
                            );

                    AddDirectoryIncarnationObservation(
                        directoryIncarnationObservations,
                        participant.FullPath,
                        relativePath,
                        directoryIncarnation
                    );

                    if (!directoryIncarnation.Success)
                    {
                        errors.Add(
                            $"Data/{childName}: directory incarnation " +
                            $"unavailable ({directoryIncarnation.State}): " +
                            (
                                directoryIncarnation.Error ??
                                "no additional error"
                            )
                        );
                    }

                    LinuxOpenedDirectorySnapshotResult
                        lookupSnapshot =
                            LinuxOpenedDirectorySnapshot.Capture(
                                directory,
                                participant.FullPath
                            );

                    AddDirectoryLookupObservation(
                        directoryLookupObservations,
                        participant.FullPath,
                        relativePath,
                        lookupSnapshot
                    );

                    if (!lookupSnapshot.Success)
                    {
                        errors.Add(
                            $"Data/{childName}: directory lookup " +
                            $"semantics unavailable " +
                            $"({lookupSnapshot.State}): " +
                            (
                                lookupSnapshot.Error ??
                                "no additional error"
                            )
                        );
                    }

                    retainedDirectoryRepresentative =
                        true;

                    DirectoryIdentity identity =
                        ToDirectoryIdentity(
                            openedIdentity
                        );

                    if (!visitedDirectories.Add(
                            identity))
                    {
                        errors.Add(
                            $"Data/{childName}: duplicate physical " +
                            "directory identity encountered."
                        );

                        continue;
                    }

                    AnalyzeDirectory(
                        directory,
                        dataRootPath,
                        relativePath,
                        rootLogicalPath,
                        participantsByLogicalPath,
                        directoryLookupObservations,
                        directoryIncarnationObservations,
                        fileIncarnationObservations,
                        errors,
                        visitedDirectories
                    );

                    break;
                }

                case LinuxChildObjectKind.RegularFile:
                    errors.Add(
                        $"Data/{childName}: Windows-logical " +
                        "namespace root is a regular file rather " +
                        "than a directory."
                    );
                    break;

                case LinuxChildObjectKind.SymbolicLink:
                    errors.Add(
                        $"Symbolic link is unsupported within " +
                        $"analyzed namespace: Data/{childName}"
                    );
                    break;

                case LinuxChildObjectKind.Other:
                    errors.Add(
                        $"Unsupported filesystem object within " +
                        $"analyzed namespace: Data/{childName}"
                    );
                    break;

                default:
                    errors.Add(
                        $"Data/{childName}: unknown child object kind."
                    );
                    break;
            }
        }

        if (!foundRepresentative)
        {
            errors.Add(
                $"No physical representative was found for " +
                $"Windows-logical namespace " +
                $"{rootLogicalPath}."
            );
        }
        else if (!retainedDirectoryRepresentative)
        {
            errors.Add(
                $"No safely retained physical directory " +
                $"representative was available for " +
                $"Windows-logical namespace " +
                $"{rootLogicalPath}."
            );
        }

        return BuildAnalysis(
            dataRootPath,
            rootLogicalPath,
            participantsByLogicalPath,
            directoryLookupObservations,
            directoryIncarnationObservations,
            fileIncarnationObservations,
            errors
        ) with
        {
            DataRootChildNames =
                rootEnumeration.ChildNames
                    .OrderBy(
                        childName =>
                            childName,
                        StringComparer.Ordinal
                    )
                    .ToArray()
        };
    }

    private static void AnalyzeDirectory(
        LinuxNoFollowPathHandle directory,
        string dataRootPath,
        string physicalRelativePath,
        WindowsLogicalPath logicalPath,
        Dictionary<
            WindowsLogicalPath,
            List<WindowsNamespacePhysicalParticipant>
        > participantsByLogicalPath,
        List<WindowsNamespaceDirectoryLookupObservation>
            directoryLookupObservations,
        List<WindowsNamespaceDirectoryIncarnationObservation>
            directoryIncarnationObservations,
        List<WindowsNamespaceFileIncarnationObservation>
            fileIncarnationObservations,
        List<string> errors,
        HashSet<DirectoryIdentity> visitedDirectories)
    {
        LinuxEnumerateDirectoryAtResult enumeration =
            LinuxEnumerateDirectoryAt.Enumerate(
                directory
            );

        if (!enumeration.Success)
        {
            errors.Add(
                $"{physicalRelativePath}: directory enumeration " +
                $"failed ({enumeration.State}): " +
                (
                    enumeration.Error ??
                    "no additional error"
                )
            );

            return;
        }

        foreach (
            string childName
            in enumeration.ChildNames)
        {
            LinuxInspectChildAtResult inspection =
                LinuxInspectChildAt.Inspect(
                    directory,
                    childName
                );

            string childRelativePath =
                CombineRelativePath(
                    physicalRelativePath,
                    childName
                );

            if (!inspection.Success)
            {
                errors.Add(
                    $"{childRelativePath}: child inspection failed " +
                    $"({inspection.State}): " +
                    (
                        inspection.Error ??
                        "no additional error"
                    )
                );

                continue;
            }

            WindowsLogicalPath childLogicalPath =
                WindowsLogicalPath.FromRelativePath(
                    $"{logicalPath.Value}/{childName}"
                );

            WindowsNamespacePhysicalParticipant
                participant =
                    CreateParticipant(
                        dataRootPath,
                        childRelativePath,
                        childName,
                        inspection
                    );

            AddParticipant(
                participantsByLogicalPath,
                childLogicalPath,
                participant
            );

            switch (inspection.Kind)
            {
                case LinuxChildObjectKind.Directory:
                {
                    LinuxOpenChildDirectoryReadOnlyAtResult
                        opened =
                            LinuxOpenChildDirectoryReadOnlyAt
                                .Open(
                                    directory,
                                    childName
                                );

                    if (
                        !opened.Success ||
                        opened.OpenedDirectory is null)
                    {
                        errors.Add(
                            $"{childRelativePath}: directory open " +
                            $"failed after inspection " +
                            $"({opened.State}): " +
                            (
                                opened.Error ??
                                "no additional error"
                            )
                        );

                        continue;
                    }

                    using LinuxNoFollowPathHandle childDirectory =
                        opened.OpenedDirectory;

                    LinuxOpenedDirectoryIdentityResult
                        openedIdentity =
                            LinuxOpenedDirectoryIdentity
                                .Capture(
                                    childDirectory
                                );

                    if (!openedIdentity.Success)
                    {
                        errors.Add(
                            $"{childRelativePath}: opened-directory " +
                            $"identity capture failed " +
                            $"({openedIdentity.State}): " +
                            (
                                openedIdentity.Error ??
                                "no additional error"
                            )
                        );

                        continue;
                    }

                    if (!SameDirectoryIdentity(
                            inspection,
                            openedIdentity))
                    {
                        errors.Add(
                            $"{childRelativePath}: physical directory " +
                            "identity changed between inspection and " +
                            "descriptor-relative open."
                        );

                        continue;
                    }

                    LinuxOpenedDirectoryIncarnationResult
                        directoryIncarnation =
                            LinuxOpenedDirectoryIncarnation.Capture(
                                childDirectory,
                                participant.FullPath
                            );

                    AddDirectoryIncarnationObservation(
                        directoryIncarnationObservations,
                        participant.FullPath,
                        childRelativePath,
                        directoryIncarnation
                    );

                    if (!directoryIncarnation.Success)
                    {
                        errors.Add(
                            $"{childRelativePath}: directory incarnation " +
                            $"unavailable ({directoryIncarnation.State}): " +
                            (
                                directoryIncarnation.Error ??
                                "no additional error"
                            )
                        );
                    }

                    LinuxOpenedDirectorySnapshotResult
                        lookupSnapshot =
                            LinuxOpenedDirectorySnapshot.Capture(
                                childDirectory,
                                participant.FullPath
                            );

                    AddDirectoryLookupObservation(
                        directoryLookupObservations,
                        participant.FullPath,
                        childRelativePath,
                        lookupSnapshot
                    );

                    if (!lookupSnapshot.Success)
                    {
                        errors.Add(
                            $"{childRelativePath}: directory lookup " +
                            $"semantics unavailable " +
                            $"({lookupSnapshot.State}): " +
                            (
                                lookupSnapshot.Error ??
                                "no additional error"
                            )
                        );
                    }

                    DirectoryIdentity identity =
                        ToDirectoryIdentity(
                            openedIdentity
                        );

                    if (!visitedDirectories.Add(
                            identity))
                    {
                        errors.Add(
                            $"{childRelativePath}: duplicate physical " +
                            "directory identity encountered."
                        );

                        continue;
                    }

                    AnalyzeDirectory(
                        childDirectory,
                        dataRootPath,
                        childRelativePath,
                        childLogicalPath,
                        participantsByLogicalPath,
                        directoryLookupObservations,
                        directoryIncarnationObservations,
                        fileIncarnationObservations,
                        errors,
                        visitedDirectories
                    );

                    break;
                }

                case LinuxChildObjectKind.RegularFile:
                {
                    ObserveRegularFileIncarnation(
                        directory,
                        childName,
                        childRelativePath,
                        participant,
                        inspection,
                        fileIncarnationObservations,
                        errors
                    );

                    break;
                }

                case LinuxChildObjectKind.SymbolicLink:
                    errors.Add(
                        $"Symbolic link is unsupported within " +
                        $"analyzed namespace: " +
                        childRelativePath
                    );
                    break;

                case LinuxChildObjectKind.Other:
                    errors.Add(
                        $"Unsupported filesystem object within " +
                        $"analyzed namespace: " +
                        childRelativePath
                    );
                    break;

                default:
                    errors.Add(
                        $"{childRelativePath}: unknown child " +
                        "object kind."
                    );
                    break;
            }
        }
    }

    private static void ObserveRegularFileIncarnation(
        LinuxNoFollowPathHandle parentDirectory,
        string childName,
        string relativePath,
        WindowsNamespacePhysicalParticipant participant,
        LinuxInspectChildAtResult inspection,
        List<WindowsNamespaceFileIncarnationObservation> observations,
        List<string> errors)
    {
        LinuxOpenChildRegularFileReadOnlyAtResult opened =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                parentDirectory,
                childName
            );

        if (
            !opened.Success ||
            opened.OpenedFile is null ||
            opened.Identity is null)
        {
            string error =
                opened.Error ??
                opened.State.ToString();

            observations.Add(
                new WindowsNamespaceFileIncarnationObservation(
                    FullPath:
                        participant.FullPath,
                    RelativePath:
                        relativePath,
                    InodeGeneration:
                        null,
                    Error:
                        error
                )
            );

            errors.Add(
                $"{relativePath}: regular-file descriptor acquisition " +
                $"failed ({opened.State}): {error}"
            );

            return;
        }

        using LinuxOpenedChildHandle openedFile =
            opened.OpenedFile;

        if (!SameFileIdentity(
                inspection,
                opened.Identity))
        {
            const string error =
                "The safely opened regular file does not match the " +
                "physical identity observed before opening.";

            observations.Add(
                new WindowsNamespaceFileIncarnationObservation(
                    FullPath:
                        participant.FullPath,
                    RelativePath:
                        relativePath,
                    InodeGeneration:
                        null,
                    Error:
                        error
                )
            );

            errors.Add(
                $"{relativePath}: {error}"
            );

            return;
        }

        LinuxOpenedFileIncarnationResult incarnation =
            LinuxOpenedFileIncarnation.Capture(
                openedFile
            );

        if (
            !incarnation.Success ||
            incarnation.Identity is null)
        {
            string error =
                incarnation.Error ??
                incarnation.State.ToString();

            observations.Add(
                new WindowsNamespaceFileIncarnationObservation(
                    FullPath:
                        participant.FullPath,
                    RelativePath:
                        relativePath,
                    InodeGeneration:
                        null,
                    Error:
                        error
                )
            );

            errors.Add(
                $"{relativePath}: regular-file incarnation unavailable " +
                $"({incarnation.State}): {error}"
            );

            return;
        }

        observations.Add(
            new WindowsNamespaceFileIncarnationObservation(
                FullPath:
                    participant.FullPath,
                RelativePath:
                    relativePath,
                InodeGeneration:
                    incarnation.Identity.InodeGeneration,
                Error:
                    null
            )
        );
    }

    private static WindowsNamespacePhysicalParticipant
        CreateParticipant(
            string dataRootPath,
            string relativePath,
            string childName,
            LinuxInspectChildAtResult inspection)
    {
        WindowsNamespacePhysicalObjectKind kind =
            inspection.Kind switch
            {
                LinuxChildObjectKind.Directory =>
                    WindowsNamespacePhysicalObjectKind
                        .Directory,

                LinuxChildObjectKind.RegularFile =>
                    WindowsNamespacePhysicalObjectKind
                        .File,

                LinuxChildObjectKind.SymbolicLink =>
                    WindowsNamespacePhysicalObjectKind
                        .SymbolicLink,

                LinuxChildObjectKind.Other =>
                    WindowsNamespacePhysicalObjectKind
                        .Other,

                _ =>
                    WindowsNamespacePhysicalObjectKind
                        .Other
            };

        return new WindowsNamespacePhysicalParticipant(
            FullPath:
                Path.GetFullPath(
                    Path.Combine(
                        dataRootPath,
                        relativePath
                    )
                ),
            RelativePath:
                relativePath,
            Name:
                childName,
            Kind:
                kind,
            DeviceMajor:
                inspection.DeviceMajor,
            DeviceMinor:
                inspection.DeviceMinor,
            Inode:
                inspection.Inode,
            MountId:
                inspection.MountId,
            IdentityError:
                inspection.Error
        );
    }

    private static DirectoryIdentity ToDirectoryIdentity(
        LinuxOpenedDirectoryIdentityResult identity)
    {
        if (
            !identity.Success ||
            identity.DeviceMajor is null ||
            identity.DeviceMinor is null ||
            identity.Inode is null ||
            identity.MountId is null)
        {
            throw new InvalidOperationException(
                "A complete opened-directory identity is required."
            );
        }

        return new DirectoryIdentity(
            DeviceMajor:
                identity.DeviceMajor.Value,
            DeviceMinor:
                identity.DeviceMinor.Value,
            Inode:
                identity.Inode.Value,
            MountId:
                identity.MountId.Value
        );
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
            inspected.DeviceMajor is not null &&
            inspected.DeviceMinor is not null &&
            inspected.Inode is not null &&
            inspected.MountId is not null &&
            opened.DeviceMajor is not null &&
            opened.DeviceMinor is not null &&
            opened.Inode is not null &&
            opened.MountId is not null &&
            inspected.DeviceMajor ==
                opened.DeviceMajor &&
            inspected.DeviceMinor ==
                opened.DeviceMinor &&
            inspected.Inode ==
                opened.Inode &&
            inspected.MountId ==
                opened.MountId;
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

    private static void AddDirectoryIncarnationObservation(
        List<WindowsNamespaceDirectoryIncarnationObservation> observations,
        string fullPath,
        string relativePath,
        LinuxOpenedDirectoryIncarnationResult incarnation)
    {
        LinuxDirectoryIncarnationIdentity? identity =
            incarnation.Identity;

        observations.Add(
            new WindowsNamespaceDirectoryIncarnationObservation(
                FullPath:
                    fullPath,
                RelativePath:
                    relativePath,
                DeviceMajor:
                    identity?.PhysicalIdentity.DeviceMajor,
                DeviceMinor:
                    identity?.PhysicalIdentity.DeviceMinor,
                Inode:
                    identity?.PhysicalIdentity.Inode,
                MountId:
                    identity?.PhysicalIdentity.MountId,
                InodeGeneration:
                    identity?.InodeGeneration,
                Error:
                    incarnation.Success
                        ? null
                        : incarnation.Error ??
                            incarnation.State.ToString()
            )
        );
    }

    private static void AddDirectoryLookupObservation(
        List<WindowsNamespaceDirectoryLookupObservation> observations,
        string fullPath,
        string relativePath,
        LinuxOpenedDirectorySnapshotResult snapshot)
    {
        observations.Add(
            new WindowsNamespaceDirectoryLookupObservation(
                FullPath:
                    fullPath,
                RelativePath:
                    relativePath,
                CasefoldEnabled:
                    snapshot.CasefoldEnabled,
                RawFlags:
                    snapshot.RawFlags,
                Error:
                    snapshot.Success
                        ? null
                        : snapshot.Error ??
                            snapshot.State.ToString()
            )
        );
    }

    private static string CombineRelativePath(
        string parent,
        string child)
    {
        return
            parent.TrimEnd('/', '\\') +
            "/" +
            child;
    }

    private static void AddParticipant(
        Dictionary<
            WindowsLogicalPath,
            List<WindowsNamespacePhysicalParticipant>
        > participantsByLogicalPath,
        WindowsLogicalPath logicalPath,
        WindowsNamespacePhysicalParticipant participant)
    {
        if (!participantsByLogicalPath.TryGetValue(
                logicalPath,
                out List<
                    WindowsNamespacePhysicalParticipant
                >? participants))
        {
            participants = [];

            participantsByLogicalPath.Add(
                logicalPath,
                participants
            );
        }

        participants.Add(
            participant
        );
    }

    private static bool WindowsEquivalent(
        string first,
        string second)
    {
        return
            WindowsLogicalPath.FromRelativePath(
                first
            ) ==
            WindowsLogicalPath.FromRelativePath(
                second
            );
    }

    private static WindowsNamespaceAnalysis BuildAnalysis(
        string dataRootPath,
        WindowsLogicalPath rootLogicalPath,
        Dictionary<
            WindowsLogicalPath,
            List<WindowsNamespacePhysicalParticipant>
        > participantsByLogicalPath,
        List<WindowsNamespaceDirectoryLookupObservation>
            directoryLookupObservations,
        List<WindowsNamespaceDirectoryIncarnationObservation>
            directoryIncarnationObservations,
        List<WindowsNamespaceFileIncarnationObservation>
            fileIncarnationObservations,
        List<string> errors)
    {
        WindowsNamespaceNode[] nodes =
            participantsByLogicalPath
                .Select(pair =>
                    new WindowsNamespaceNode(
                        LogicalPath:
                            pair.Key,
                        Participants:
                            pair.Value
                                .OrderBy(
                                    participant =>
                                        participant.RelativePath,
                                    StringComparer.Ordinal
                                )
                                .ToArray()
                    )
                )
                .OrderBy(
                    node =>
                        node.LogicalPath.Value,
                    StringComparer.Ordinal
                )
                .ToArray();

        return new WindowsNamespaceAnalysis(
            DataRootPath:
                dataRootPath,
            RootLogicalPath:
                rootLogicalPath,
            DirectoryLookupObservations:
                directoryLookupObservations
                    .OrderBy(
                        observation =>
                            observation.RelativePath,
                        StringComparer.Ordinal
                    )
                    .ToArray(),
            DirectoryIncarnationObservations:
                directoryIncarnationObservations
                    .OrderBy(
                        observation =>
                            observation.RelativePath,
                        StringComparer.Ordinal
                    )
                    .ToArray(),
            FileIncarnationObservations:
                fileIncarnationObservations
                    .OrderBy(
                        observation =>
                            observation.RelativePath,
                        StringComparer.Ordinal
                    )
                    .ToArray(),
            Nodes:
                nodes,
            Errors:
                errors.ToArray()
        );
    }
}
