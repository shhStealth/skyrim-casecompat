public static class CaseCompatRollbackWizardCommand
{
    public static int Run(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine(
                "Error: rollback does not take any arguments."
            );

            return 2;
        }

        return CaseCompatRollbackWizard.Run(
            Console.In,
            Console.Out
        );
    }
}
