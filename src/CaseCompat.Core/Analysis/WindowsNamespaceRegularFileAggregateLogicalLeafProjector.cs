using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

/*
 * Pure compatibility projection from complete stable regular-file content
 * evidence into the dormant aggregate logical-leaf classification model.
 *
 * This performs no filesystem access, hashing, reacquisition, source
 * selection, persistence, repair planning, or execution.
 *
 * DataRelativePathRepairSourceSnapshot currently carries the older
 * LinuxFileIdentityResult shape. The generation-aware source evidence remains
 * in WindowsNamespaceRegularFileContentAnalysis and is deliberately not
 * converted into repair authority here.
 */
public static class
    WindowsNamespaceRegularFileAggregateLogicalLeafProjector
{
    public static IReadOnlyList<DataRelativePathAggregateLogicalLeaf> Project(
        WindowsNamespaceRegularFileContentAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(
            analysis
        );

        ValidateAnalysisShape(
            analysis
        );

        if (!analysis.Complete)
        {
            throw new InvalidOperationException(
                "Aggregate logical-leaf projection requires complete " +
                "regular-file content analysis."
            );
        }

        WindowsNamespaceRegularFileContentNodeAnalysis[] orderedNodes =
            analysis.Nodes
                .OrderBy(
                    node =>
                        node.LogicalPath.Value,
                    StringComparer.Ordinal
                )
                .ToArray();

        var logicalPaths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        var leaves =
            new List<DataRelativePathAggregateLogicalLeaf>(
                orderedNodes.Length
            );

        foreach (
            WindowsNamespaceRegularFileContentNodeAnalysis node
            in orderedNodes)
        {
            string logicalPath =
                node.LogicalPath.Value;

            if (!logicalPaths.Add(
                    logicalPath))
            {
                throw new InvalidOperationException(
                    $"Windows-logical leaf '{logicalPath}' occurs more " +
                    "than once in the regular-file content analysis."
                );
            }

            WindowsNamespacePhysicalFileContentEvidence[] orderedFiles =
                node.Files
                    .OrderBy(
                        file =>
                            file.Participant.FullPath,
                        StringComparer.Ordinal
                    )
                    .ToArray();

            var physicalPaths =
                new HashSet<string>(
                    StringComparer.Ordinal
                );

            var snapshots =
                new List<DataRelativePathRepairSourceSnapshot>(
                    orderedFiles.Length
                );

            foreach (
                WindowsNamespacePhysicalFileContentEvidence evidence
                in orderedFiles)
            {
                DataRelativePathRepairSourceSnapshot snapshot =
                    CreateSnapshot(
                        logicalPath,
                        evidence
                    );

                if (!physicalPaths.Add(
                        snapshot.PhysicalPath))
                {
                    throw new InvalidOperationException(
                        $"Windows-logical leaf '{logicalPath}' contains " +
                        $"duplicate physical path " +
                        $"'{snapshot.PhysicalPath}'."
                    );
                }

                snapshots.Add(
                    snapshot
                );
            }

            leaves.Add(
                DataRelativePathAggregateLogicalLeafClassifier
                    .Classify(
                        logicalPath,
                        snapshots.ToArray()
                    )
            );
        }

        return leaves.ToArray();
    }

    private static void ValidateAnalysisShape(
        WindowsNamespaceRegularFileContentAnalysis analysis)
    {
        if (analysis.Nodes is null)
        {
            throw new InvalidOperationException(
                "Regular-file content analysis has a null node collection."
            );
        }

        if (analysis.Errors is null)
        {
            throw new InvalidOperationException(
                "Regular-file content analysis has a null error collection."
            );
        }

        foreach (
            WindowsNamespaceRegularFileContentNodeAnalysis? node
            in analysis.Nodes)
        {
            if (node is null)
            {
                throw new InvalidOperationException(
                    "Regular-file content analysis contains a null node."
                );
            }

            if (string.IsNullOrWhiteSpace(
                    node.LogicalPath.Value))
            {
                throw new InvalidOperationException(
                    "Regular-file content analysis contains an empty " +
                    "Windows-logical path."
                );
            }

            if (node.Files is null)
            {
                throw new InvalidOperationException(
                    $"Windows-logical leaf '{node.LogicalPath.Value}' " +
                    "has a null physical-file evidence collection."
                );
            }

            foreach (
                WindowsNamespacePhysicalFileContentEvidence? evidence
                in node.Files)
            {
                if (evidence is null)
                {
                    throw new InvalidOperationException(
                        $"Windows-logical leaf '{node.LogicalPath.Value}' " +
                        "contains null physical-file evidence."
                    );
                }

                if (evidence.Participant is null)
                {
                    throw new InvalidOperationException(
                        $"Windows-logical leaf '{node.LogicalPath.Value}' " +
                        "contains evidence with a null participant."
                    );
                }
            }
        }
    }

    private static DataRelativePathRepairSourceSnapshot CreateSnapshot(
        string logicalPath,
        WindowsNamespacePhysicalFileContentEvidence evidence)
    {
        if (!evidence.Success)
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' contains physical " +
                "file evidence that is not stable."
            );
        }

        WindowsNamespacePhysicalParticipant participant =
            evidence.Participant;

        if (
            participant.Kind !=
                WindowsNamespacePhysicalObjectKind.File ||
            participant.IdentityError is not null ||
            participant.DeviceMajor is null ||
            participant.DeviceMinor is null ||
            participant.Inode is null ||
            participant.MountId is null ||
            string.IsNullOrWhiteSpace(
                participant.FullPath))
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' contains a " +
                "participant without complete regular-file identity."
            );
        }

        if (
            evidence.Size is not long size ||
            size < 0)
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' contains invalid " +
                $"size evidence for '{participant.FullPath}'."
            );
        }

        string? sha256 =
            evidence.Sha256;

        if (!IsSha256(
                sha256))
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' contains invalid " +
                $"SHA-256 evidence for '{participant.FullPath}'."
            );
        }

        LinuxOpenedFileIncarnationResult? post =
            evidence.PostObservationIncarnation;

        if (
            post is null ||
            !post.Success ||
            post.Identity is null ||
            !post.Identity.Success ||
            post.PhysicalIdentity is null)
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' lacks complete " +
                $"post-observation incarnation evidence for " +
                $"'{participant.FullPath}'."
            );
        }

        LinuxOpenedFileIdentityResult descriptorIdentity =
            post.Identity.PhysicalIdentity;

        if (
            !HasCompleteDescriptorIdentity(
                descriptorIdentity) ||
            !HasCompleteDescriptorIdentity(
                post.PhysicalIdentity))
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' lacks complete " +
                $"post-observation physical identity, including link count, " +
                $"for '{participant.FullPath}'."
            );
        }

        if (!SameDescriptorIdentity(
                descriptorIdentity,
                post.PhysicalIdentity))
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' contains inconsistent " +
                $"post-observation physical identity for " +
                $"'{participant.FullPath}'."
            );
        }

        if (
            participant.DeviceMajor !=
                descriptorIdentity.DeviceMajor ||
            participant.DeviceMinor !=
                descriptorIdentity.DeviceMinor ||
            participant.Inode !=
                descriptorIdentity.Inode ||
            participant.MountId !=
                descriptorIdentity.MountId)
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' contains " +
                $"post-observation identity that does not match its " +
                $"physical participant for '{participant.FullPath}'."
            );
        }

        var compatibilityIdentity =
            new LinuxFileIdentityResult(
                FullPath:
                    participant.FullPath,
                DeviceMajor:
                    descriptorIdentity.DeviceMajor,
                DeviceMinor:
                    descriptorIdentity.DeviceMinor,
                Inode:
                    descriptorIdentity.Inode,
                LinkCount:
                    descriptorIdentity.LinkCount,
                MountId:
                    descriptorIdentity.MountId,
                Error:
                    null
            );

        return new DataRelativePathRepairSourceSnapshot(
            PhysicalPath:
                participant.FullPath,
            Size:
                size,
            Sha256:
                sha256!,
            Identity:
                compatibilityIdentity
        );
    }

    private static bool HasCompleteDescriptorIdentity(
        LinuxOpenedFileIdentityResult identity)
    {
        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null &&
            identity.LinkCount is not null &&
            identity.MountId is not null &&
            identity.Error is null;
    }

    private static bool SameDescriptorIdentity(
        LinuxOpenedFileIdentityResult left,
        LinuxOpenedFileIdentityResult right)
    {
        return
            left.DeviceMajor ==
                right.DeviceMajor &&
            left.DeviceMinor ==
                right.DeviceMinor &&
            left.Inode ==
                right.Inode &&
            left.LinkCount ==
                right.LinkCount &&
            left.MountId ==
                right.MountId;
    }

    private static bool IsSha256(
        string? value)
    {
        return
            value is not null &&
            value.Length == 64 &&
            value.All(character =>
                (
                    character >= '0' &&
                    character <= '9'
                ) ||
                (
                    character >= 'a' &&
                    character <= 'f'
                ) ||
                (
                    character >= 'A' &&
                    character <= 'F'
                )
            );
    }
}
