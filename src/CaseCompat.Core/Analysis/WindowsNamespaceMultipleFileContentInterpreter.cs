namespace CaseCompat.Core.Analysis;

/*
 * Pure classifier over checkpoint-9A evidence.
 *
 * This type deliberately performs no filesystem access. It does not
 * reacquire, enumerate, open, hash, or mutate any filesystem object.
 *
 * Successful checkpoint-9A evidence is the authority boundary. In
 * particular, an internally computed hash that checkpoint 9A refused to
 * publish after failed post-observation namespace validation must remain
 * indeterminate here.
 */
public static class WindowsNamespaceMultipleFileContentInterpreter
{
    public static WindowsNamespaceMultipleFileContentInterpretation Interpret(
        WindowsNamespaceMultipleFileContentAnalysis contentAnalysis)
    {
        ArgumentNullException.ThrowIfNull(
            contentAnalysis
        );

        var nodes =
            new List<
                WindowsNamespaceMultipleFileContentNodeInterpretation
            >();

        var errors =
            new List<string>();

        if (contentAnalysis.Nodes is null)
        {
            errors.Add(
                "The checkpoint-9A node collection is null."
            );

            return new WindowsNamespaceMultipleFileContentInterpretation(
                ContentAnalysis:
                    contentAnalysis,
                Nodes:
                    nodes.ToArray(),
                Errors:
                    errors.ToArray()
            );
        }

        if (contentAnalysis.Errors is null)
        {
            errors.Add(
                "The checkpoint-9A error collection is null."
            );
        }

        for (
            int index = 0;
            index < contentAnalysis.Nodes.Count;
            index++)
        {
            WindowsNamespaceMultipleFileContentNodeAnalysis? node =
                contentAnalysis.Nodes[index];

            if (node is null)
            {
                errors.Add(
                    $"Checkpoint-9A node evidence at index {index} is null."
                );

                continue;
            }

            nodes.Add(
                InterpretNode(
                    node
                )
            );
        }

        return new WindowsNamespaceMultipleFileContentInterpretation(
            ContentAnalysis:
                contentAnalysis,
            Nodes:
                nodes.ToArray(),
            Errors:
                errors.ToArray()
        );
    }

    private static WindowsNamespaceMultipleFileContentNodeInterpretation
        InterpretNode(
            WindowsNamespaceMultipleFileContentNodeAnalysis node)
    {
        ArgumentNullException.ThrowIfNull(
            node
        );

        if (
            node.Files is null ||
            node.Files.Count < 2)
        {
            return Indeterminate(
                node,
                "A MultipleFiles content interpretation requires at least " +
                "two participant evidence records."
            );
        }

        if (
            node.Files.Any(
                file =>
                    file is null
            ))
        {
            return Indeterminate(
                node,
                "At least one physical participant evidence record is null."
            );
        }

        WindowsNamespacePhysicalFileContentEvidence? incomplete =
            node.Files.FirstOrDefault(
                file =>
                    !HasPublishedStableContentEvidence(
                        file
                    )
            );

        if (incomplete is not null)
        {
            return Indeterminate(
                node,
                "At least one physical participant does not expose " +
                "successful checkpoint-9A stable size and SHA-256 evidence: " +
                incomplete.Participant.RelativePath
            );
        }

        WindowsNamespacePhysicalFileContentEvidence first =
            node.Files[0];

        long expectedSize =
            first.Size!.Value;

        string expectedSha256 =
            first.Sha256!;

        bool divergent =
            node.Files
                .Skip(1)
                .Any(
                    file =>
                        file.Size!.Value !=
                            expectedSize ||
                        !string.Equals(
                            file.Sha256,
                            expectedSha256,
                            StringComparison.Ordinal
                        )
                );

        return new WindowsNamespaceMultipleFileContentNodeInterpretation(
            ContentEvidence:
                node,
            State:
                divergent
                    ? WindowsNamespaceMultipleFileContentInterpretationState
                        .DivergentContent
                    : WindowsNamespaceMultipleFileContentInterpretationState
                        .IdenticalContent,
            Error:
                null
        );
    }

    private static bool HasPublishedStableContentEvidence(
        WindowsNamespacePhysicalFileContentEvidence file)
    {
        if (
            !file.Success ||
            file.Size is null ||
            file.Size.Value < 0 ||
            string.IsNullOrWhiteSpace(
                file.Sha256
            ))
        {
            return false;
        }

        /*
         * Checkpoint 9A's descriptor-backed SHA-256 producer emits the
         * canonical 64-character hexadecimal form. Reject malformed
         * fabricated input rather than interpreting it as content evidence.
         */
        string sha256 =
            file.Sha256;

        if (sha256.Length != 64)
        {
            return false;
        }

        foreach (char value in sha256)
        {
            bool hexadecimal =
                value is >= '0' and <= '9' ||
                value is >= 'A' and <= 'F';

            if (!hexadecimal)
            {
                return false;
            }
        }

        return true;
    }

    private static WindowsNamespaceMultipleFileContentNodeInterpretation
        Indeterminate(
            WindowsNamespaceMultipleFileContentNodeAnalysis node,
            string error)
    {
        return new WindowsNamespaceMultipleFileContentNodeInterpretation(
            ContentEvidence:
                node,
            State:
                WindowsNamespaceMultipleFileContentInterpretationState
                    .IndeterminateEvidence,
            Error:
                error
        );
    }
}
