namespace WhiteSpaceFix;

internal static class Program
{
	private static int Main(string[] args)
	{
		if (args.Length == 0)
		{
			return ConsoleMenu.RunInteractive();
		}

		if (IsCheckWhiteSpaceCommand(args[0]))
		{
			return CheckWhiteSpaceCommand.Run(args.Skip(1).ToArray());
		}

		var options = ParseOptions(args);
		if (options.ShowHelp)
		{
			FixRunner.PrintHelp();
			return 0;
		}

		return FixRunner.Run(options);
	}

	private static FixOptions ParseOptions(string[] args)
	{
		var options = new FixOptions();

		for (var index = 0; index < args.Length; index++)
		{
			var argument = args[index];
			if (string.IsNullOrWhiteSpace(argument))
			{
				continue;
			}

			switch (argument)
			{
				case "--dry-run":
					options.DryRun = true;
					break;
				case "--branch":
					options.BranchMode = true;
					break;
				case "--upstream":
					options.UpstreamRef = RequireValue(args, ref index, "--upstream");
					break;
				case "--stash":
					options.StashBeforeApply = true;
					break;
				case "--repo":
					options.RepoPath = RequireValue(args, ref index, "--repo");
					break;
				case "--base":
					options.BaseRef = RequireValue(args, ref index, "--base");
					break;
				case "--help":
				case "-h":
				case "/?":
					options.ShowHelp = true;
					break;
				default:
					if (argument.StartsWith("-", StringComparison.Ordinal))
					{
						throw new InvalidOperationException($"Unknown argument: {argument}");
					}

					if (!string.IsNullOrWhiteSpace(options.RepoPath))
					{
						throw new InvalidOperationException($"Multiple repository paths were provided: '{options.RepoPath}' and '{argument}'.");
					}

					options.RepoPath = argument;
					break;
			}
		}

		return options;
	}

	private static string RequireValue(string[] args, ref int index, string argumentName)
	{
		if (index + 1 >= args.Length)
		{
			throw new InvalidOperationException($"Missing value for {argumentName}.");
		}

		index++;
		return args[index];
	}

	private static bool IsCheckWhiteSpaceCommand(string argument)
	{
		return argument.Equals("CheckWhiteSpace", StringComparison.OrdinalIgnoreCase);
	}
}
