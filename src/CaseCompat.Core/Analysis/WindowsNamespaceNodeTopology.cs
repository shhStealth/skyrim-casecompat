namespace CaseCompat.Core.Analysis;

/*
 * Structural classification only.
 *
 * This classification describes the physical participant shapes already
 * observed for one Windows-logical namespace node.
 *
 * It does not imply:
 *
 * - content equality;
 * - provider precedence;
 * - canonical spelling;
 * - reconciliation eligibility;
 * - repair safety.
 */
public enum WindowsNamespaceNodeTopology
{
    NoPhysicalParticipants,
    SinglePhysicalObject,
    MultipleDirectories,
    MultipleFiles,
    FileDirectoryCollision,
    UnsupportedObject
}

public static class WindowsNamespaceNodeTopologyClassifier
{
    public static WindowsNamespaceNodeTopology Classify(
        WindowsNamespaceNode node)
    {
        ArgumentNullException.ThrowIfNull(
            node
        );

        if (node.Participants.Count == 0)
        {
            return WindowsNamespaceNodeTopology
                .NoPhysicalParticipants;
        }

        if (
            node.Participants.Any(participant =>
                participant.Kind is
                    WindowsNamespacePhysicalObjectKind.SymbolicLink or
                    WindowsNamespacePhysicalObjectKind.Other))
        {
            return WindowsNamespaceNodeTopology
                .UnsupportedObject;
        }

        bool hasFiles =
            node.Participants.Any(participant =>
                participant.Kind ==
                    WindowsNamespacePhysicalObjectKind.File
            );

        bool hasDirectories =
            node.Participants.Any(participant =>
                participant.Kind ==
                    WindowsNamespacePhysicalObjectKind.Directory
            );

        if (
            hasFiles &&
            hasDirectories)
        {
            return WindowsNamespaceNodeTopology
                .FileDirectoryCollision;
        }

        if (node.Participants.Count == 1)
        {
            return WindowsNamespaceNodeTopology
                .SinglePhysicalObject;
        }

        if (hasDirectories)
        {
            return WindowsNamespaceNodeTopology
                .MultipleDirectories;
        }

        if (hasFiles)
        {
            return WindowsNamespaceNodeTopology
                .MultipleFiles;
        }

        /*
         * This is intentionally conservative. If the physical-object enum
         * gains another kind later and this classifier has not yet learned
         * how to classify it, do not silently treat the node as supported.
         */
        return WindowsNamespaceNodeTopology
            .UnsupportedObject;
    }
}
