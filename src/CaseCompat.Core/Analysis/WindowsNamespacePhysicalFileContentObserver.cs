using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

/*
 * Stable, read-only content observation for one regular-file participant
 * already recorded by a complete WindowsNamespaceAnalysis.
 *
 * Evidence sequence:
 *
 * 1. reacquire the exact physical participant and its complete
 *    generation-aware hierarchy from pass-1 evidence;
 * 2. observe stable content through the same retained readable descriptor;
 * 3. reacquire the participant again after hashing, re-observing exact
 *    spelling and the complete generation-aware hierarchy.
 *
 * This is observational evidence only. It grants no repair, persistence,
 * source-selection, provider-precedence, or execution authority.
 */
public static class WindowsNamespacePhysicalFileContentObserver
{
    public static WindowsNamespacePhysicalFileContentEvidence Observe(
        WindowsNamespaceAnalysis analysis,
        WindowsNamespacePhysicalParticipant participant)
    {
        return ObserveCore(
            analysis,
            participant,
            afterStableContentObservation:
                null
        );
    }

    /*
     * Internal deterministic seam used by the existing MultipleFiles
     * analyzer's private race-regression seam.
     *
     * Production callers of Observe(...) cannot supply this callback.
     */
    internal static WindowsNamespacePhysicalFileContentEvidence ObserveCore(
        WindowsNamespaceAnalysis analysis,
        WindowsNamespacePhysicalParticipant participant,
        Action<WindowsNamespacePhysicalParticipant>?
            afterStableContentObservation)
    {
        using WindowsNamespacePhysicalFileReacquisition initial =
            WindowsNamespacePhysicalFileReacquirer.Reacquire(
                analysis,
                participant
            );

        if (!initial.Success)
        {
            return new WindowsNamespacePhysicalFileContentEvidence(
                Participant:
                    participant,
                State:
                    WindowsNamespacePhysicalFileContentEvidenceState
                        .InitialReacquisitionFailed,
                ExpectedIncarnationObservation:
                    initial.ExpectedIncarnationObservation,
                InitialReacquisitionState:
                    initial.State,
                InitialIncarnation:
                    initial.ActualIncarnation,
                ContentObservation:
                    null,
                PostObservationReacquisitionState:
                    null,
                PostObservationIncarnation:
                    null,
                FailedComponent:
                    initial.FailedComponent,
                Error:
                    initial.Error ??
                    initial.State.ToString()
            );
        }

        LinuxOpenedFileContentObservationResult content =
            LinuxOpenedFileContentObservation.Observe(
                initial.OpenedFile!,
                participant.FullPath
            );

        if (!content.Success)
        {
            return new WindowsNamespacePhysicalFileContentEvidence(
                Participant:
                    participant,
                State:
                    WindowsNamespacePhysicalFileContentEvidenceState
                        .ContentObservationFailed,
                ExpectedIncarnationObservation:
                    initial.ExpectedIncarnationObservation,
                InitialReacquisitionState:
                    initial.State,
                InitialIncarnation:
                    initial.ActualIncarnation,
                ContentObservation:
                    content,
                PostObservationReacquisitionState:
                    null,
                PostObservationIncarnation:
                    null,
                FailedComponent:
                    participant.Name,
                Error:
                    content.Error ??
                    content.State.ToString()
            );
        }

        /*
         * Null in normal production use.
         *
         * The existing deterministic regression test uses the MultipleFiles
         * analyzer's private seam to alter namespace spelling after stable
         * descriptor-backed content observation and before this mandatory
         * post-observation reacquisition.
         */
        afterStableContentObservation?.Invoke(
            participant
        );

        /*
         * Reacquire from the same complete pass-1 analysis after hashing.
         *
         * This re-observes exact spelling and rebinds Data + every directory
         * prefix + the final file to their pass-1 generation-aware
         * incarnations.
         */
        using WindowsNamespacePhysicalFileReacquisition post =
            WindowsNamespacePhysicalFileReacquirer.Reacquire(
                analysis,
                participant
            );

        if (!post.Success)
        {
            return new WindowsNamespacePhysicalFileContentEvidence(
                Participant:
                    participant,
                State:
                    WindowsNamespacePhysicalFileContentEvidenceState
                        .PostObservationReacquisitionFailed,
                ExpectedIncarnationObservation:
                    initial.ExpectedIncarnationObservation,
                InitialReacquisitionState:
                    initial.State,
                InitialIncarnation:
                    initial.ActualIncarnation,
                ContentObservation:
                    content,
                PostObservationReacquisitionState:
                    post.State,
                PostObservationIncarnation:
                    post.ActualIncarnation,
                FailedComponent:
                    post.FailedComponent,
                Error:
                    post.Error ??
                    post.State.ToString()
            );
        }

        return new WindowsNamespacePhysicalFileContentEvidence(
            Participant:
                participant,
            State:
                WindowsNamespacePhysicalFileContentEvidenceState
                    .StableContentEvidence,
            ExpectedIncarnationObservation:
                initial.ExpectedIncarnationObservation,
            InitialReacquisitionState:
                initial.State,
            InitialIncarnation:
                initial.ActualIncarnation,
            ContentObservation:
                content,
            PostObservationReacquisitionState:
                post.State,
            PostObservationIncarnation:
                post.ActualIncarnation,
            FailedComponent:
                null,
            Error:
                null
        );
    }
}
