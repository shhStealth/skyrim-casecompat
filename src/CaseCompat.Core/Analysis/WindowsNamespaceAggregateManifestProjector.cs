using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

/*
 * Pure projection of one complete Windows namespace analysis and its
 * complete stable regular-file content evidence into schema-v1 durable
 * aggregate namespace evidence.
 *
 * This class performs no filesystem access, hashing, reacquisition,
 * persistence, source selection, repair planning, or execution.
 *
 * The content-analysis result does not retain its originating
 * WindowsNamespaceAnalysis. Project(...) therefore cross-binds every logical
 * leaf, participant, pass-1 file incarnation, and stable before/after
 * incarnation back to the supplied namespace analysis before constructing
 * the manifest.
 *
 * This grants no repair or execution authority.
 */
public static class WindowsNamespaceAggregateManifestProjector
{
    private sealed record ExpectedRegularFileNode(
        WindowsNamespaceNode Node,
        WindowsNamespaceNodeTopology Topology
    );

    public static DataRelativePathAggregateNamespaceManifestRecord Project(
        WindowsNamespaceAnalysis namespaceAnalysis,
        WindowsNamespaceRegularFileContentAnalysis contentAnalysis,
        DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(
            namespaceAnalysis
        );

        ArgumentNullException.ThrowIfNull(
            contentAnalysis
        );

        ValidateNamespaceShape(
            namespaceAnalysis
        );

        ValidateContentShape(
            contentAnalysis
        );

        if (!namespaceAnalysis.Complete)
        {
            throw new InvalidOperationException(
                "Aggregate manifest projection requires a complete " +
                "Windows namespace analysis."
            );
        }

        if (!contentAnalysis.Complete)
        {
            throw new InvalidOperationException(
                "Aggregate manifest projection requires complete stable " +
                "regular-file content analysis."
            );
        }

        Dictionary<string, ExpectedRegularFileNode> expectedNodes =
            BuildExpectedRegularFileNodes(
                namespaceAnalysis
            );

        Dictionary<
            string,
            WindowsNamespaceFileIncarnationObservation
        > namespaceFileIncarnations =
            BuildNamespaceFileIncarnations(
                namespaceAnalysis
            );

        int expectedPhysicalFileCount =
            expectedNodes.Values
                .Sum(expected =>
                    expected.Node.Participants.Count);

        if (
            namespaceFileIncarnations.Count !=
                expectedPhysicalFileCount)
        {
            throw new InvalidOperationException(
                "The supplied namespace analysis file-incarnation evidence " +
                "does not exactly cover every supported regular-file " +
                "participant."
            );
        }

        Dictionary<
            string,
            WindowsNamespaceRegularFileContentNodeAnalysis
        > contentNodes =
            BuildContentNodes(
                contentAnalysis
            );

        if (contentNodes.Count != expectedNodes.Count)
        {
            throw new InvalidOperationException(
                "Regular-file content analysis does not exactly cover the " +
                "regular-file logical leaves in the supplied namespace " +
                "analysis."
            );
        }

        foreach (
            KeyValuePair<string, ExpectedRegularFileNode> pair
            in expectedNodes)
        {
            string logicalPath =
                pair.Key;

            ExpectedRegularFileNode expected =
                pair.Value;

            if (
                !contentNodes.TryGetValue(
                    logicalPath,
                    out WindowsNamespaceRegularFileContentNodeAnalysis?
                        contentNode))
            {
                throw new InvalidOperationException(
                    $"Regular-file content analysis is missing namespace " +
                    $"logical leaf '{logicalPath}'."
                );
            }

            if (contentNode.Topology != expected.Topology)
            {
                throw new InvalidOperationException(
                    $"Regular-file content logical leaf '{logicalPath}' " +
                    $"does not retain the topology observed by the supplied " +
                    "namespace analysis."
                );
            }

            ValidateContentNodeBinding(
                logicalPath,
                expected.Node,
                contentNode,
                namespaceFileIncarnations
            );
        }

        foreach (string logicalPath in contentNodes.Keys)
        {
            if (!expectedNodes.ContainsKey(
                    logicalPath))
            {
                throw new InvalidOperationException(
                    $"Regular-file content analysis contains unexpected " +
                    $"logical leaf '{logicalPath}' that is absent from the " +
                    "supplied namespace analysis."
                );
            }
        }

        IReadOnlyList<DataRelativePathAggregateLogicalLeaf>
            aggregateLeaves =
                WindowsNamespaceRegularFileAggregateLogicalLeafProjector
                    .Project(
                        contentAnalysis
                    );

        if (aggregateLeaves.Count != expectedNodes.Count)
        {
            throw new InvalidOperationException(
                "Aggregate logical-leaf projection does not exactly cover " +
                "the supplied namespace regular-file leaves."
            );
        }

        var manifestLeaves =
            new List<
                DataRelativePathAggregateNamespaceManifestLogicalLeaf
            >(
                aggregateLeaves.Count
            );

        foreach (
            DataRelativePathAggregateLogicalLeaf leaf
            in aggregateLeaves.OrderBy(
                leaf =>
                    leaf.WindowsLogicalPath,
                StringComparer.Ordinal
            ))
        {
            if (
                !contentNodes.TryGetValue(
                    leaf.WindowsLogicalPath,
                    out WindowsNamespaceRegularFileContentNodeAnalysis?
                        contentNode))
            {
                throw new InvalidOperationException(
                    $"Projected aggregate logical leaf " +
                    $"'{leaf.WindowsLogicalPath}' cannot be joined back to " +
                    "stable content evidence."
                );
            }

            Dictionary<
                string,
                WindowsNamespacePhysicalFileContentEvidence
            > evidenceByPhysicalPath =
                new(
                    StringComparer.Ordinal
                );

            foreach (
                WindowsNamespacePhysicalFileContentEvidence evidence
                in contentNode.Files)
            {
                if (
                    !evidenceByPhysicalPath.TryAdd(
                        evidence.Participant.FullPath,
                        evidence))
                {
                    throw new InvalidOperationException(
                        $"Windows-logical leaf '{leaf.WindowsLogicalPath}' " +
                        $"contains duplicate stable evidence for physical " +
                        $"path '{evidence.Participant.FullPath}'."
                    );
                }
            }

            var representations =
                new List<
                    DataRelativePathAggregateNamespaceManifestFileRepresentation
                >(
                    leaf.PhysicalRepresentations.Count
                );

            foreach (
                DataRelativePathRepairSourceSnapshot snapshot
                in leaf.PhysicalRepresentations.OrderBy(
                    snapshot =>
                        snapshot.PhysicalPath,
                    StringComparer.Ordinal
                ))
            {
                if (
                    !evidenceByPhysicalPath.TryGetValue(
                        snapshot.PhysicalPath,
                        out WindowsNamespacePhysicalFileContentEvidence?
                            evidence))
                {
                    throw new InvalidOperationException(
                        $"Projected snapshot '{snapshot.PhysicalPath}' " +
                        $"cannot be joined back to stable content evidence " +
                        $"for logical leaf '{leaf.WindowsLogicalPath}'."
                    );
                }

                WindowsNamespaceFileIncarnationObservation expected =
                    evidence.ExpectedIncarnationObservation!;

                if (expected.InodeGeneration is not uint inodeGeneration)
                {
                    throw new InvalidOperationException(
                        $"Stable evidence for '{snapshot.PhysicalPath}' " +
                        "does not retain complete pass-1 inode-generation " +
                        "evidence."
                    );
                }

                representations.Add(
                    new
                        DataRelativePathAggregateNamespaceManifestFileRepresentation(
                            RelativePath:
                                evidence.Participant.RelativePath,
                            Snapshot:
                                snapshot,
                            InodeGeneration:
                                inodeGeneration
                        )
                );
            }

            manifestLeaves.Add(
                new DataRelativePathAggregateNamespaceManifestLogicalLeaf(
                    WindowsLogicalPath:
                        leaf.WindowsLogicalPath,
                    State:
                        leaf.State,
                    PhysicalRepresentations:
                        representations.ToArray()
                )
            );
        }

        var manifest =
            new DataRelativePathAggregateNamespaceManifestRecord(
                SchemaVersion:
                    DataRelativePathAggregateNamespaceManifestRecord
                        .CurrentSchemaVersion,
                CreatedUtc:
                    createdUtc,
                DataRoot:
                    namespaceAnalysis.DataRootPath,
                RootWindowsLogicalPath:
                    namespaceAnalysis.RootLogicalPath.Value,
                DataRootChildNames:
                    namespaceAnalysis.DataRootChildNames!.ToArray(),
                DirectoryLookupObservations:
                    namespaceAnalysis.DirectoryLookupObservations.ToArray(),
                DirectoryIncarnationObservations:
                    namespaceAnalysis
                        .DirectoryIncarnationObservations
                        .ToArray(),
                LogicalLeaves:
                    manifestLeaves.ToArray()
            );

        string? validationError =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        if (validationError is not null)
        {
            throw new InvalidOperationException(
                "Projected aggregate namespace manifest is invalid: " +
                validationError
            );
        }

        return manifest;
    }

    private static void ValidateNamespaceShape(
        WindowsNamespaceAnalysis analysis)
    {
        if (analysis.Errors is null)
        {
            throw new InvalidOperationException(
                "Windows namespace analysis has a null error collection."
            );
        }

        if (analysis.Nodes is null)
        {
            throw new InvalidOperationException(
                "Windows namespace analysis has a null node collection."
            );
        }

        if (analysis.DirectoryLookupObservations is null)
        {
            throw new InvalidOperationException(
                "Windows namespace analysis has a null directory-lookup " +
                "observation collection."
            );
        }

        if (analysis.DirectoryIncarnationObservations is null)
        {
            throw new InvalidOperationException(
                "Windows namespace analysis has a null directory-incarnation " +
                "observation collection."
            );
        }

        if (analysis.FileIncarnationObservations is null)
        {
            throw new InvalidOperationException(
                "Windows namespace analysis has a null file-incarnation " +
                "observation collection."
            );
        }

        if (analysis.DataRootChildNames is null)
        {
            throw new InvalidOperationException(
                "Windows namespace analysis lacks complete Data-root " +
                "child-name evidence."
            );
        }

        foreach (WindowsNamespaceNode? node in analysis.Nodes)
        {
            if (node is null)
            {
                throw new InvalidOperationException(
                    "Windows namespace analysis contains a null node."
                );
            }

            if (node.Participants is null)
            {
                throw new InvalidOperationException(
                    $"Namespace logical node '{node.LogicalPath.Value}' " +
                    "has a null participant collection."
                );
            }

            foreach (
                WindowsNamespacePhysicalParticipant? participant
                in node.Participants)
            {
                if (participant is null)
                {
                    throw new InvalidOperationException(
                        $"Namespace logical node '{node.LogicalPath.Value}' " +
                        "contains a null physical participant."
                    );
                }
            }
        }

        foreach (
            WindowsNamespaceFileIncarnationObservation? observation
            in analysis.FileIncarnationObservations)
        {
            if (observation is null)
            {
                throw new InvalidOperationException(
                    "Windows namespace analysis contains a null " +
                    "file-incarnation observation."
                );
            }
        }
    }

    private static void ValidateContentShape(
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

            if (node.Files is null)
            {
                throw new InvalidOperationException(
                    $"Regular-file logical leaf '{node.LogicalPath.Value}' " +
                    "has a null evidence collection."
                );
            }

            foreach (
                WindowsNamespacePhysicalFileContentEvidence? evidence
                in node.Files)
            {
                if (evidence is null)
                {
                    throw new InvalidOperationException(
                        $"Regular-file logical leaf '{node.LogicalPath.Value}' " +
                        "contains null physical-file evidence."
                    );
                }

                if (evidence.Participant is null)
                {
                    throw new InvalidOperationException(
                        $"Regular-file logical leaf '{node.LogicalPath.Value}' " +
                        "contains evidence with a null participant."
                    );
                }
            }
        }
    }

    private static Dictionary<string, ExpectedRegularFileNode>
        BuildExpectedRegularFileNodes(
            WindowsNamespaceAnalysis analysis)
    {
        var expected =
            new Dictionary<string, ExpectedRegularFileNode>(
                StringComparer.Ordinal
            );

        foreach (
            WindowsNamespaceNode node
            in analysis.Nodes.OrderBy(
                node =>
                    node.LogicalPath.Value,
                StringComparer.Ordinal
            ))
        {
            WindowsNamespaceNodeTopology topology =
                WindowsNamespaceNodeTopologyClassifier.Classify(
                    node
                );

            bool include =
                topology switch
                {
                    WindowsNamespaceNodeTopology.SinglePhysicalObject =>
                        node.Participants.Count == 1 &&
                        node.Participants[0].Kind ==
                            WindowsNamespacePhysicalObjectKind.File,

                    WindowsNamespaceNodeTopology.MultipleFiles =>
                        true,

                    WindowsNamespaceNodeTopology.MultipleDirectories =>
                        false,

                    _ =>
                        throw new InvalidOperationException(
                            $"Namespace logical node " +
                            $"'{node.LogicalPath.Value}' has topology " +
                            $"{topology}, which cannot participate in a " +
                            "complete regular-file aggregate manifest."
                        )
                };

            if (!include)
            {
                continue;
            }

            if (
                node.Participants.Any(
                    participant =>
                        participant.Kind !=
                            WindowsNamespacePhysicalObjectKind.File))
            {
                throw new InvalidOperationException(
                    $"Namespace regular-file logical node " +
                    $"'{node.LogicalPath.Value}' contains a non-file " +
                    "participant."
                );
            }

            if (
                !expected.TryAdd(
                    node.LogicalPath.Value,
                    new ExpectedRegularFileNode(
                        Node:
                            node,
                        Topology:
                            topology
                    )))
            {
                throw new InvalidOperationException(
                    $"Namespace logical path '{node.LogicalPath.Value}' " +
                    "occurs more than once."
                );
            }
        }

        return expected;
    }

    private static
        Dictionary<string, WindowsNamespaceFileIncarnationObservation>
        BuildNamespaceFileIncarnations(
            WindowsNamespaceAnalysis analysis)
    {
        var observations =
            new Dictionary<
                string,
                WindowsNamespaceFileIncarnationObservation
            >(
                StringComparer.Ordinal
            );

        foreach (
            WindowsNamespaceFileIncarnationObservation observation
            in analysis.FileIncarnationObservations)
        {
            if (
                string.IsNullOrWhiteSpace(
                    observation.RelativePath) ||
                string.IsNullOrWhiteSpace(
                    observation.FullPath) ||
                observation.Error is not null ||
                observation.InodeGeneration is null)
            {
                throw new InvalidOperationException(
                    "The supplied namespace analysis contains incomplete " +
                    "regular-file incarnation evidence."
                );
            }

            if (
                !observations.TryAdd(
                    observation.RelativePath,
                    observation))
            {
                throw new InvalidOperationException(
                    $"Namespace file-incarnation relative path " +
                    $"'{observation.RelativePath}' occurs more than once."
                );
            }
        }

        return observations;
    }

    private static Dictionary<
        string,
        WindowsNamespaceRegularFileContentNodeAnalysis
    > BuildContentNodes(
        WindowsNamespaceRegularFileContentAnalysis analysis)
    {
        var nodes =
            new Dictionary<
                string,
                WindowsNamespaceRegularFileContentNodeAnalysis
            >(
                StringComparer.Ordinal
            );

        foreach (
            WindowsNamespaceRegularFileContentNodeAnalysis node
            in analysis.Nodes)
        {
            if (
                !nodes.TryAdd(
                    node.LogicalPath.Value,
                    node))
            {
                throw new InvalidOperationException(
                    $"Regular-file content logical path " +
                    $"'{node.LogicalPath.Value}' occurs more than once."
                );
            }
        }

        return nodes;
    }

    private static void ValidateContentNodeBinding(
        string logicalPath,
        WindowsNamespaceNode namespaceNode,
        WindowsNamespaceRegularFileContentNodeAnalysis contentNode,
        IReadOnlyDictionary<
            string,
            WindowsNamespaceFileIncarnationObservation
        > namespaceFileIncarnations)
    {
        if (
            contentNode.Files.Count !=
                namespaceNode.Participants.Count)
        {
            throw new InvalidOperationException(
                $"Regular-file content logical leaf '{logicalPath}' does " +
                "not exactly cover its namespace physical participants."
            );
        }

        var evidenceByRelativePath =
            new Dictionary<
                string,
                WindowsNamespacePhysicalFileContentEvidence
            >(
                StringComparer.Ordinal
            );

        foreach (
            WindowsNamespacePhysicalFileContentEvidence evidence
            in contentNode.Files)
        {
            if (
                !evidenceByRelativePath.TryAdd(
                    evidence.Participant.RelativePath,
                    evidence))
            {
                throw new InvalidOperationException(
                    $"Regular-file content logical leaf '{logicalPath}' " +
                    $"contains duplicate participant relative path " +
                    $"'{evidence.Participant.RelativePath}'."
                );
            }
        }

        foreach (
            WindowsNamespacePhysicalParticipant namespaceParticipant
            in namespaceNode.Participants)
        {
            if (
                !evidenceByRelativePath.TryGetValue(
                    namespaceParticipant.RelativePath,
                    out WindowsNamespacePhysicalFileContentEvidence?
                        evidence))
            {
                throw new InvalidOperationException(
                    $"Regular-file content logical leaf '{logicalPath}' is " +
                    $"missing namespace participant " +
                    $"'{namespaceParticipant.RelativePath}'."
                );
            }

            if (
                !SameParticipant(
                    namespaceParticipant,
                    evidence.Participant))
            {
                throw new InvalidOperationException(
                    $"Stable content participant " +
                    $"'{evidence.Participant.RelativePath}' does not match " +
                    "the supplied namespace participant."
                );
            }

            if (!evidence.Success)
            {
                throw new InvalidOperationException(
                    $"Stable content evidence for " +
                    $"'{namespaceParticipant.RelativePath}' is incomplete."
                );
            }

            if (
                !namespaceFileIncarnations.TryGetValue(
                    namespaceParticipant.RelativePath,
                    out WindowsNamespaceFileIncarnationObservation?
                        namespaceIncarnation))
            {
                throw new InvalidOperationException(
                    $"Namespace participant " +
                    $"'{namespaceParticipant.RelativePath}' has no pass-1 " +
                    "file-incarnation observation."
                );
            }

            if (
                !string.Equals(
                    namespaceIncarnation.FullPath,
                    namespaceParticipant.FullPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Namespace pass-1 file-incarnation path for " +
                    $"'{namespaceParticipant.RelativePath}' does not match " +
                    "its physical participant."
                );
            }

            WindowsNamespaceFileIncarnationObservation?
                evidenceExpected =
                    evidence.ExpectedIncarnationObservation;

            if (
                evidenceExpected is null ||
                !SameFileIncarnationObservation(
                    namespaceIncarnation,
                    evidenceExpected))
            {
                throw new InvalidOperationException(
                    $"Stable content evidence for " +
                    $"'{namespaceParticipant.RelativePath}' is not bound to " +
                    "the supplied namespace pass-1 file incarnation."
                );
            }

            uint expectedGeneration =
                namespaceIncarnation.InodeGeneration!.Value;

            ValidateObservedIncarnation(
                logicalPath,
                namespaceParticipant,
                evidence.InitialIncarnation,
                expectedGeneration,
                "initial"
            );

            ValidateObservedIncarnation(
                logicalPath,
                namespaceParticipant,
                evidence.PostObservationIncarnation,
                expectedGeneration,
                "post-observation"
            );
        }
    }

    private static void ValidateObservedIncarnation(
        string logicalPath,
        WindowsNamespacePhysicalParticipant participant,
        LinuxOpenedFileIncarnationResult? incarnation,
        uint expectedGeneration,
        string phase)
    {
        if (
            incarnation is null ||
            !incarnation.Success ||
            incarnation.Identity is null ||
            incarnation.PhysicalIdentity is null)
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' lacks complete " +
                $"{phase} incarnation evidence for " +
                $"'{participant.RelativePath}'."
            );
        }

        LinuxFileIncarnationIdentity identity =
            incarnation.Identity;

        if (identity.InodeGeneration != expectedGeneration)
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' {phase} inode " +
                $"generation for '{participant.RelativePath}' does not " +
                "match the supplied namespace pass-1 generation."
            );
        }

        if (
            !SamePhysicalParticipantIdentity(
                participant,
                identity.PhysicalIdentity) ||
            !SamePhysicalParticipantIdentity(
                participant,
                incarnation.PhysicalIdentity))
        {
            throw new InvalidOperationException(
                $"Windows-logical leaf '{logicalPath}' {phase} physical " +
                $"identity for '{participant.RelativePath}' does not match " +
                "the supplied namespace participant."
            );
        }
    }

    private static bool SameParticipant(
        WindowsNamespacePhysicalParticipant left,
        WindowsNamespacePhysicalParticipant right)
    {
        return
            string.Equals(
                left.FullPath,
                right.FullPath,
                StringComparison.Ordinal) &&
            string.Equals(
                left.RelativePath,
                right.RelativePath,
                StringComparison.Ordinal) &&
            string.Equals(
                left.Name,
                right.Name,
                StringComparison.Ordinal) &&
            left.Kind == right.Kind &&
            left.DeviceMajor == right.DeviceMajor &&
            left.DeviceMinor == right.DeviceMinor &&
            left.Inode == right.Inode &&
            left.MountId == right.MountId &&
            string.Equals(
                left.IdentityError,
                right.IdentityError,
                StringComparison.Ordinal);
    }

    private static bool SameFileIncarnationObservation(
        WindowsNamespaceFileIncarnationObservation left,
        WindowsNamespaceFileIncarnationObservation right)
    {
        return
            string.Equals(
                left.FullPath,
                right.FullPath,
                StringComparison.Ordinal) &&
            string.Equals(
                left.RelativePath,
                right.RelativePath,
                StringComparison.Ordinal) &&
            left.InodeGeneration == right.InodeGeneration &&
            string.Equals(
                left.Error,
                right.Error,
                StringComparison.Ordinal);
    }

    private static bool SamePhysicalParticipantIdentity(
        WindowsNamespacePhysicalParticipant participant,
        LinuxOpenedFileIdentityResult identity)
    {
        return
            identity.Success &&
            participant.DeviceMajor ==
                identity.DeviceMajor &&
            participant.DeviceMinor ==
                identity.DeviceMinor &&
            participant.Inode ==
                identity.Inode &&
            participant.MountId ==
                identity.MountId;
    }
}
