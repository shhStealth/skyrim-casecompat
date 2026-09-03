using CaseCompat.Core.Analysis;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure interpretation only.
 *
 * No filesystem access, hashing, namespace reacquisition, provider/archive
 * precedence, canonical-spelling selection, or repair decision occurs here.
 */
public static class SkyrimArmorAddonSnapshotLoosePathInterpreter
{
    public static SkyrimArmorAddonSnapshotLoosePathInterpretation Interpret(
        SkyrimWinningArmorAddonSnapshotPathEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(
            evidence
        );

        if (!TryValidateCompositionShape(
                evidence,
                out string? validationError))
        {
            return new SkyrimArmorAddonSnapshotLoosePathInterpretation(
                Evidence:
                    evidence,
                State:
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .IndeterminateEvidence,
                EvidenceStructureValid:
                    false,
                InterpretationError:
                    validationError
            );
        }

        SkyrimArmorAddonSnapshotLoosePathInterpretationState state =
            evidence.State switch
            {
                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .InvalidRequestedPath =>
                        SkyrimArmorAddonSnapshotLoosePathInterpretationState
                            .IndeterminateEvidence,

                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .NoMatchingNamespaceAnalysis =>
                        SkyrimArmorAddonSnapshotLoosePathInterpretationState
                            .IndeterminateEvidence,

                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .AmbiguousMatchingNamespaceAnalysis =>
                        SkyrimArmorAddonSnapshotLoosePathInterpretationState
                            .IndeterminateEvidence,

                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .LookupProduced =>
                        InterpretLookup(
                            evidence.Lookup!
                        ),

                _ =>
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .IndeterminateEvidence
            };

        return new SkyrimArmorAddonSnapshotLoosePathInterpretation(
            Evidence:
                evidence,
            State:
                state,
            EvidenceStructureValid:
                true,
            InterpretationError:
                null
        );
    }

    private static SkyrimArmorAddonSnapshotLoosePathInterpretationState
        InterpretLookup(
            WindowsNamespaceSnapshotFileLookup lookup)
    {
        return lookup.State switch
        {
            WindowsNamespaceSnapshotFileLookupState.Resolved =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseResolved,

            WindowsNamespaceSnapshotFileLookupState.Missing =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,

            WindowsNamespaceSnapshotFileLookupState.NotDirectory =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,

            WindowsNamespaceSnapshotFileLookupState.NotFile =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,

            WindowsNamespaceSnapshotFileLookupState.CasefoldUnknown =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .IndeterminateEvidence,

            WindowsNamespaceSnapshotFileLookupState
                .CasefoldEquivalenceUnknown =>
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .IndeterminateEvidence,

            WindowsNamespaceSnapshotFileLookupState.AmbiguousEquivalent =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .IndeterminateEvidence,

            WindowsNamespaceSnapshotFileLookupState.UnsupportedObject =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .IndeterminateEvidence,

            WindowsNamespaceSnapshotFileLookupState.IncompleteAnalysis =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .IndeterminateEvidence,

            WindowsNamespaceSnapshotFileLookupState.InvalidRequestedPath =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .IndeterminateEvidence,

            WindowsNamespaceSnapshotFileLookupState
                .RequestOutsideAnalyzedNamespace =>
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .IndeterminateEvidence,

            WindowsNamespaceSnapshotFileLookupState.InvalidSnapshotEvidence =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .IndeterminateEvidence,

            _ =>
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .IndeterminateEvidence
        };
    }

    private static bool TryValidateCompositionShape(
        SkyrimWinningArmorAddonSnapshotPathEvidence evidence,
        out string? error)
    {
        error =
            null;

        if (!Enum.IsDefined(
                typeof(
                    SkyrimArmorAddonSnapshotLookupEvidenceState
                ),
                evidence.State))
        {
            error =
                "The checkpoint-10B-B composition state is not defined.";
            return false;
        }

        if (evidence.References is null ||
            evidence.References.Count == 0)
        {
            error =
                "A checkpoint-10C path group must contain at least " +
                "one winning reference context.";
            return false;
        }

        foreach (
            SkyrimWinningArmorAddonSnapshotReferenceContext? context
            in evidence.References)
        {
            if (context is null ||
                context.Reference is null)
            {
                error =
                    "A checkpoint-10C path group contains a null " +
                    "reference context.";
                return false;
            }

            if (!string.Equals(
                    context.Reference.DataRelativePath,
                    evidence.RequestedPath,
                    StringComparison.Ordinal))
            {
                error =
                    "A checkpoint-10C reference does not retain the " +
                    "group's exact requested Data-relative spelling.";
                return false;
            }
        }

        bool pathValid =
            WindowsDataRelativePathParser.TryParse(
                evidence.RequestedPath,
                out string[] components,
                out _
            );

        if (
            evidence.State ==
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .InvalidRequestedPath)
        {
            if (pathValid)
            {
                error =
                    "The wrapper reports InvalidRequestedPath for a " +
                    "path accepted by the shared parser.";
                return false;
            }

            if (evidence.RequestedRootLogicalPath is not null ||
                evidence.MatchingAnalysisCount != 0 ||
                evidence.SelectedAnalysis is not null ||
                evidence.Lookup is not null)
            {
                error =
                    "InvalidRequestedPath carries unexpected namespace " +
                    "selection or lookup evidence.";
                return false;
            }

            return true;
        }

        if (!pathValid)
        {
            error =
                "A non-invalid wrapper state carries a path rejected " +
                "by the shared parser.";
            return false;
        }

        WindowsLogicalPath expectedRoot =
            WindowsLogicalPath.FromRelativePath(
                components[0]
            );

        if (
            evidence.RequestedRootLogicalPath is null ||
            evidence.RequestedRootLogicalPath.Value !=
                expectedRoot)
        {
            error =
                "The recorded requested namespace root does not match " +
                "the requested Data-relative path.";
            return false;
        }

        switch (evidence.State)
        {
            case
                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .NoMatchingNamespaceAnalysis:

                if (evidence.MatchingAnalysisCount != 0 ||
                    evidence.SelectedAnalysis is not null ||
                    evidence.Lookup is not null)
                {
                    error =
                        "NoMatchingNamespaceAnalysis carries unexpected " +
                        "selected-analysis or lookup evidence.";
                    return false;
                }

                return true;

            case
                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .AmbiguousMatchingNamespaceAnalysis:

                if (evidence.MatchingAnalysisCount <= 1 ||
                    evidence.SelectedAnalysis is not null ||
                    evidence.Lookup is not null)
                {
                    error =
                        "AmbiguousMatchingNamespaceAnalysis does not " +
                        "carry a valid ambiguous-selection shape.";
                    return false;
                }

                return true;

            case
                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .LookupProduced:

                return TryValidateLookupProducedShape(
                    evidence,
                    expectedRoot,
                    out error
                );

            default:
                error =
                    "Unexpected checkpoint-10B-B composition state.";
                return false;
        }
    }

    private static bool TryValidateLookupProducedShape(
        SkyrimWinningArmorAddonSnapshotPathEvidence evidence,
        WindowsLogicalPath expectedRoot,
        out string? error)
    {
        error =
            null;

        if (evidence.MatchingAnalysisCount != 1 ||
            evidence.SelectedAnalysis is null ||
            evidence.Lookup is null)
        {
            error =
                "LookupProduced requires exactly one selected analysis " +
                "and one checkpoint-10A lookup.";
            return false;
        }

        if (!ReferenceEquals(
                evidence.SelectedAnalysis,
                evidence.Lookup.Analysis))
        {
            error =
                "The selected namespace analysis is not the analysis " +
                "retained by the checkpoint-10A lookup.";
            return false;
        }

        if (
            evidence.SelectedAnalysis.RootLogicalPath !=
            expectedRoot)
        {
            error =
                "The selected namespace analysis does not match the " +
                "requested namespace root.";
            return false;
        }

        if (!string.Equals(
                evidence.Lookup.RequestedRelativePath,
                evidence.RequestedPath,
                StringComparison.Ordinal))
        {
            error =
                "The checkpoint-10A lookup does not preserve the exact " +
                "checkpoint-10C requested spelling.";
            return false;
        }

        if (!Enum.IsDefined(
                typeof(
                    WindowsNamespaceSnapshotFileLookupState
                ),
                evidence.Lookup.State))
        {
            error =
                "The checkpoint-10A lookup state is not defined.";
            return false;
        }

        if (
            evidence.Lookup.State ==
            WindowsNamespaceSnapshotFileLookupState.Resolved)
        {
            if (!evidence.Lookup.Success)
            {
                error =
                    "A Resolved checkpoint-10A lookup does not retain " +
                    "a resolved physical participant.";
                return false;
            }
        }
        else if (evidence.Lookup.ResolvedParticipant is not null)
        {
            error =
                "A non-resolved checkpoint-10A lookup unexpectedly " +
                "retains a resolved physical participant.";
            return false;
        }

        return true;
    }
}
