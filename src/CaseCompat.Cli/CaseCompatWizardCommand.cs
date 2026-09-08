public static class CaseCompatWizardCommand
{
    public static int Run(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine(
                "Error: run does not take any arguments."
            );

            return 2;
        }

        return CaseCompatWizard.Run(
            Console.In,
            Console.Out
        );
    }
}
