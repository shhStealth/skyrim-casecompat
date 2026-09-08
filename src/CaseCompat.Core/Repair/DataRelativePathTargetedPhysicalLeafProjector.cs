using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Outcome of adapting one already-completed targeted current-leaf observation
// into the physical/content evidence used by C4D/C4E.
//
// Projected and NoPhysicalRepresentation are both complete observations.
// The latter is important for consumer-first discovery: an authoritative
// consumer path can legitimately have no loose physical provider.
//
// This type grants no consumer authority, source preference, repair-plan,
// persistence, authorization, or mutation authority.
public enum DataRelativePathTargetedPhysicalLeafProjectionState
{
    Projected,
    NoPhysicalRepresentation,

    CurrentLeafAnalysisFailed,
    InvalidEvidence
}

// Manifest-independent physical representation of one targeted logical leaf.
//
// Snapshot preserves the existing repair-source compatibility shape.
// InodeGeneration remains explicitly paired with it rather than being lost
// merely because DataRelativePathRepairSourceSnapshot predates generation-aware
// filesystem evidence.
public sealed record DataRelativePathTargetedPhysicalFileRepresentation(
    string RelativePath,
    DataRelativePathRepairSourceSnapshot Snapshot,
    uint InodeGeneration
);

// Pure adaptation result for one targeted Windows-logical file leaf.
//
// CurrentLeafAnalysis is retained by reference for diagnostic provenance.
// LogicalLeaf is present only when one or more physical representations exist.
public sealed record DataRelativePathTargetedPhysicalLeafProjection(
    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
        CurrentLeafAnalysis,
    DataRelativePathTargetedPhysicalLeafProjectionState State,
    IReadOnlyList<DataRelativePathTargetedPhysicalFileRepresentation>
        PhysicalRepresentations,
    DataRelativePathAggregateLogicalLeaf? LogicalLeaf,
    string? Error
)
{
    public bool EvidenceComplete =>
        State is
            DataRelativePathTargetedPhysicalLeafProjectionState.Projected or
            DataRelativePathTargetedPhysicalLeafProjectionState
                .NoPhysicalRepresentation;

    public DataRelativePathAggregateLogicalLeafState? PhysicalState =>
        LogicalLeaf?.State;
}

// C4E-4A compatibility seam.
//
// The expensive filesystem work has already been performed by the targeted
// current-leaf analyzer. This projector performs no filesystem enumeration,
// opening, hashing, content observation, or source reacquisition.
//
// It validates manually constructible current-leaf evidence, converts each
// stable representation into SourceSnapshot + InodeGeneration, and delegates
// physical topology classification to the existing aggregate logical-leaf
// classifier.
//
// The aggregate namespace manifest is deliberately absent from this API.
public static class DataRelativePathTargetedPhysicalLeafProjector
{
    public static DataRelativePathTargetedPhysicalLeafProjection Project(
        string dataRoot,
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
            currentLeafAnalysis)
    {
        if (string.IsNullOrWhiteSpace(
                dataRoot))
        {
            throw new ArgumentException(
                "A Data-root path is required.",
                nameof(dataRoot)
            );
        }

        ArgumentNullException.ThrowIfNull(
            currentLeafAnalysis
        );

        string canonicalDataRoot =
            CanonicalizeDataRoot(
                dataRoot
            );

        if (!currentLeafAnalysis.Success)
        {
            return Result(
                currentLeafAnalysis,
                DataRelativePathTargetedPhysicalLeafProjectionState
                    .CurrentLeafAnalysisFailed,
                [],
                logicalLeaf:
                    null,
                error:
                    currentLeafAnalysis.Error ??
                    currentLeafAnalysis.State.ToString()
            );
        }

        try
        {
            ValidateSuccessfulAnalysisShape(
                currentLeafAnalysis
            );

            IReadOnlyList<
                DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
            > currentRepresentations =
                currentLeafAnalysis.Representations;

            if (currentRepresentations.Count == 0)
            {
                return Result(
                    currentLeafAnalysis,
                    DataRelativePathTargetedPhysicalLeafProjectionState
                        .NoPhysicalRepresentation,
                    [],
                    logicalLeaf:
                        null,
                    error:
                        null
                );
            }

            DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation[]
                ordered =
                    currentRepresentations
                        .OrderBy(
                            representation =>
                                representation.RelativePath,
                            StringComparer.Ordinal
                        )
                        .ToArray();

            var exactPaths =
                new HashSet<string>(
                    StringComparer.Ordinal
                );

            var projected =
                new List<
                    DataRelativePathTargetedPhysicalFileRepresentation
                >(
                    ordered.Length
                );

            foreach (
                DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
                    representation
                in ordered)
            {
                DataRelativePathTargetedPhysicalFileRepresentation converted =
                    ConvertRepresentation(
                        canonicalDataRoot,
                        currentLeafAnalysis.LogicalPath,
                        representation
                    );

                if (!exactPaths.Add(
                        converted.RelativePath))
                {
                    throw new InvalidOperationException(
                        $"Targeted current-leaf evidence contains duplicate " +
                        $"physical path '{converted.RelativePath}'."
                    );
                }

                projected.Add(
                    converted
                );
            }

            DataRelativePathAggregateLogicalLeaf logicalLeaf =
                DataRelativePathAggregateLogicalLeafClassifier.Classify(
                    currentLeafAnalysis.LogicalPath.Value,
                    projected
                        .Select(
                            representation =>
                                representation.Snapshot
                        )
                        .ToArray()
                );

            return Result(
                currentLeafAnalysis,
                DataRelativePathTargetedPhysicalLeafProjectionState.Projected,
                projected.ToArray(),
                logicalLeaf,
                error:
                    null
            );
        }
        catch (ArgumentException ex)
        {
            return Invalid(
                currentLeafAnalysis,
                ex
            );
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(
                currentLeafAnalysis,
                ex
            );
        }
    }

    private static void ValidateSuccessfulAnalysisShape(
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(
                analysis.LogicalPath.Value))
        {
            throw new InvalidOperationException(
                "Successful targeted current-leaf evidence has no " +
                "Windows-logical path."
            );
        }

        if (analysis.Representations is null)
        {
            throw new InvalidOperationException(
                "Successful targeted current-leaf evidence has a null " +
                "physical representation collection."
            );
        }

        if (analysis.Representations.Any(
                representation =>
                    representation is null))
        {
            throw new InvalidOperationException(
                "Successful targeted current-leaf evidence contains a null " +
                "physical representation."
            );
        }
    }

    private static
        DataRelativePathTargetedPhysicalFileRepresentation
        ConvertRepresentation(
            string canonicalDataRoot,
            WindowsLogicalPath expectedLogicalPath,
            DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
                representation)
    {
        ValidateRelativePath(
            representation.RelativePath,
            expectedLogicalPath
        );

        LinuxFileIncarnationIdentity? incarnation =
            representation.IncarnationIdentity;

        if (
            incarnation is null ||
            incarnation.PhysicalIdentity is null ||
            !incarnation.Success)
        {
            throw new InvalidOperationException(
                $"Physical representation '{representation.RelativePath}' " +
                "does not contain complete file-incarnation evidence."
            );
        }

        LinuxOpenedFileIdentityResult physicalIdentity =
            incarnation.PhysicalIdentity;

        if (
            physicalIdentity.Error is not null ||
            physicalIdentity.DeviceMajor is null ||
            physicalIdentity.DeviceMinor is null ||
            physicalIdentity.Inode is null ||
            physicalIdentity.MountId is null)
        {
            throw new InvalidOperationException(
                $"Physical representation '{representation.RelativePath}' " +
                "does not contain complete physical identity."
            );
        }

        if (representation.Size < 0)
        {
            throw new InvalidOperationException(
                $"Physical representation '{representation.RelativePath}' " +
                "has a negative size."
            );
        }

        if (!IsSha256(
                representation.Sha256))
        {
            throw new InvalidOperationException(
                $"Physical representation '{representation.RelativePath}' " +
                "has malformed SHA-256 evidence."
            );
        }

        string physicalPath =
            BuildPhysicalPath(
                canonicalDataRoot,
                representation.RelativePath
            );

        var compatibilityIdentity =
            new LinuxFileIdentityResult(
                FullPath:
                    physicalPath,
                DeviceMajor:
                    physicalIdentity.DeviceMajor,
                DeviceMinor:
                    physicalIdentity.DeviceMinor,
                Inode:
                    physicalIdentity.Inode,
                LinkCount:
                    physicalIdentity.LinkCount,
                MountId:
                    physicalIdentity.MountId,
                Error:
                    null
            );

        var snapshot =
            new DataRelativePathRepairSourceSnapshot(
                PhysicalPath:
                    physicalPath,
                Size:
                    representation.Size,
                Sha256:
                    representation.Sha256,
                Identity:
                    compatibilityIdentity
            );

        return new(
            RelativePath:
                representation.RelativePath,
            Snapshot:
                snapshot,
            InodeGeneration:
                incarnation.InodeGeneration
        );
    }

    private static void ValidateRelativePath(
        string relativePath,
        WindowsLogicalPath expectedLogicalPath)
    {
        if (
            string.IsNullOrWhiteSpace(
                relativePath) ||
            Path.IsPathRooted(
                relativePath) ||
            relativePath.Contains('\\') ||
            relativePath.Contains('\0'))
        {
            throw new InvalidOperationException(
                "A targeted physical representation has an invalid relative " +
                "path."
            );
        }

        string[] components =
            relativePath.Split(
                '/',
                StringSplitOptions.None
            );

        if (
            components.Length == 0 ||
            components.Any(
                component =>
                    string.IsNullOrEmpty(
                        component) ||
                    component is "." or ".."))
        {
            throw new InvalidOperationException(
                $"Targeted physical representation '{relativePath}' has an " +
                "invalid path component."
            );
        }

        WindowsLogicalPath logical;

        try
        {
            logical =
                WindowsLogicalPath.FromRelativePath(
                    relativePath
                );
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"Targeted physical representation '{relativePath}' cannot " +
                "be converted to a Windows-logical path.",
                ex
            );
        }

        if (!string.Equals(
                logical.Value,
                expectedLogicalPath.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Targeted physical representation '{relativePath}' does not " +
                $"belong to logical leaf '{expectedLogicalPath.Value}'."
            );
        }
    }

    private static string CanonicalizeDataRoot(
        string dataRoot)
    {
        string canonical;

        try
        {
            if (!Path.IsPathFullyQualified(
                    dataRoot))
            {
                throw new ArgumentException(
                    "The Data-root path must be absolute.",
                    nameof(dataRoot)
                );
            }

            canonical =
                Path.GetFullPath(
                    dataRoot
                );
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"The Data-root path is invalid: {ex.Message}",
                nameof(dataRoot),
                ex
            );
        }

        if (!string.Equals(
                canonical,
                dataRoot,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The Data-root path must already be in canonical absolute " +
                "form.",
                nameof(dataRoot)
            );
        }

        return canonical;
    }

    private static string BuildPhysicalPath(
        string canonicalDataRoot,
        string relativePath)
    {
        string physicalPath =
            Path.GetFullPath(
                Path.Combine(
                    canonicalDataRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar
                    )
                )
            );

        string projectedRelative =
            Path.GetRelativePath(
                canonicalDataRoot,
                physicalPath
            );

        if (
            Path.IsPathRooted(
                projectedRelative) ||
            projectedRelative == ".." ||
            projectedRelative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Targeted physical representation '{relativePath}' escapes " +
                "the trusted Data root."
            );
        }

        return physicalPath;
    }

    private static bool IsSha256(
        string? value)
    {
        return
            value is not null &&
            value.Length == 64 &&
            value.All(
                Uri.IsHexDigit
            );
    }

    private static DataRelativePathTargetedPhysicalLeafProjection Invalid(
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis analysis,
        Exception exception)
    {
        return Result(
            analysis,
            DataRelativePathTargetedPhysicalLeafProjectionState.InvalidEvidence,
            [],
            logicalLeaf:
                null,
            error:
                exception.Message
        );
    }

    private static DataRelativePathTargetedPhysicalLeafProjection Result(
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis analysis,
        DataRelativePathTargetedPhysicalLeafProjectionState state,
        IReadOnlyList<DataRelativePathTargetedPhysicalFileRepresentation>
            physicalRepresentations,
        DataRelativePathAggregateLogicalLeaf? logicalLeaf,
        string? error)
    {
        return new(
            CurrentLeafAnalysis:
                analysis,
            State:
                state,
            PhysicalRepresentations:
                physicalRepresentations,
            LogicalLeaf:
                logicalLeaf,
            Error:
                error
        );
    }
}
