// Small interactive prompt helpers shared by every guided entry point
// (apply wizard, rollback wizard), so both walk the user through
// auto-detected paths and yes/no confirmations the same way.
internal static class CaseCompatWizardPrompts
{
    public static string? ResolvePath(
        TextReader input,
        TextWriter output,
        string label,
        string? detected,
        Func<string, bool> validate,
        string invalidMessage)
    {
        if (
            detected is not null &&
            validate(
                detected))
        {
            output.Write(
                $"{label} [auto-detected]: {detected}\nUse this? (Y/n): "
            );

            if (IsYes(
                    input.ReadLine()))
            {
                return detected;
            }
        }

        while (true)
        {
            output.Write(
                $"Enter path for {label} (leave empty to cancel): "
            );

            string? entered =
                input.ReadLine();

            if (string.IsNullOrWhiteSpace(
                    entered))
            {
                output.WriteLine(
                    "Cancelled."
                );

                return null;
            }

            string trimmed =
                entered.Trim();

            if (!validate(
                    trimmed))
            {
                output.WriteLine(
                    invalidMessage
                );

                continue;
            }

            return trimmed;
        }
    }

    public static bool IsYes(
        string? response)
    {
        if (string.IsNullOrWhiteSpace(
                response))
        {
            return true;
        }

        char first =
            response.Trim()[0];

        return
            first != 'n' &&
            first != 'N';
    }
}
