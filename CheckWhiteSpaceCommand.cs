namespace WhiteSpaceFix;

internal static class CheckWhiteSpaceCommand
{
	public static int Run(string[] args)
	{
		try
		{
			var options = ParseOptions(args);
			if (options.ShowHelp)
			{
				PrintHelp();
				return 0;
			}

			if (string.IsNullOrWhiteSpace(options.RepoPath))
			{
				options.RepoPath = Directory.GetCurrentDirectory();
			}

			Console.WriteLine("CheckWhiteSpace");
			Console.WriteLine("===============");
			Console.WriteLine();

			var runOptions = new FixOptions
			{
				RepoPath = options.RepoPath,
				BaseRef = options.BaseRef,
				HeadRef = options.HeadRef,
				UpstreamRef = options.UpstreamRef,
				BranchMode = options.BranchMode,
				DryRun = true
			};

			var preview = FixRunner.Execute(runOptions);

			if (preview.ExitCode != 0)
			{
				return preview.ExitCode;
			}

			options.BranchMode = runOptions.BranchMode;
			options.UpstreamRef = runOptions.UpstreamRef;

			if (preview.FixedFileCount == 0)
			{
				Console.WriteLine();
				Console.WriteLine("No whitespace fixes needed.");
			}
			else if (options.DryRunOnly)
			{
				Console.WriteLine();
				Console.WriteLine("Dry-run only. Re-run without --dry-run-only to apply fixes.");
			}
			else
			{
				Console.WriteLine();
				Console.WriteLine($"Applying {preview.FixedFileCount} whitespace fix(es)...");
				Console.WriteLine();

				runOptions.DryRun = false;
				runOptions.StashBeforeApply = options.StashBeforeApply;

				var apply = FixRunner.Execute(runOptions);

				if (apply.ExitCode != 0)
				{
					return apply.ExitCode;
				}
			}

			var repository = GitRepository.Find(options.RepoPath);
			var fixesApplied = !options.DryRunOnly && preview.FixedFileCount > 0;

			DiffComparison diffComparison;
			if (options.BranchMode)
			{
				var upstreamRef = string.IsNullOrWhiteSpace(options.UpstreamRef)
					? repository.ResolveUpstreamRef()
					: options.UpstreamRef!;
				var mergeBase = repository.ResolveMergeBase(upstreamRef, options.HeadRef);
				diffComparison = fixesApplied
					? repository.CompareDiffStats(mergeBase)
					: repository.CompareBranchDiffStats(mergeBase, options.HeadRef);
			}
			else
			{
				diffComparison = repository.CompareDiffStats(options.BaseRef);
			}

			Console.WriteLine();
			Console.WriteLine("Diff check:");
			if (!string.IsNullOrWhiteSpace(diffComparison.RangeLabel))
			{
				Console.WriteLine($"  Range: {diffComparison.RangeLabel}");
			}

			Console.WriteLine($"  git diff:    {diffComparison.NormalSummary}");
			Console.WriteLine($"  git diff -w: {diffComparison.WhitespaceIgnoredSummary}");

			if (diffComparison.HasSameScope)
			{
				Console.WriteLine("  Result: diff scope matches (no whitespace-only noise).");
			}
			else
			{
				Console.WriteLine("  Result: diff scope differs. Review changes manually.");
			}

			return diffComparison.HasSameScope ? 0 : 2;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception.Message);
			return 1;
		}
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
				case "--dry-run-only":
					options.DryRunOnly = true;
					break;
				case "--branch":
					options.BranchMode = true;
					break;
				case "--upstream":
					options.UpstreamRef = RequireValue(args, ref index, "--upstream");
					break;
				case "--no-stash":
					options.StashBeforeApply = false;
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

		if (!options.DryRunOnly && !args.Any(argument => argument.Equals("--no-stash", StringComparison.Ordinal)))
		{
			options.StashBeforeApply = true;
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

	private static void PrintHelp()
	{
		Console.WriteLine("CheckWhiteSpace");
		Console.WriteLine();
		Console.WriteLine("Preview whitespace fixes, apply when needed, then verify diff scope.");
		Console.WriteLine();
		Console.WriteLine("Usage:");
		Console.WriteLine("  WhiteSpaceFix CheckWhiteSpace [repo-path] [options]");
		Console.WriteLine("  dotnet run -- CheckWhiteSpace [repo-path] [options]");
		Console.WriteLine();
		Console.WriteLine("Arguments:");
		Console.WriteLine("  [repo-path]       Git repository path. Defaults to current directory.");
		Console.WriteLine();
		Console.WriteLine("Options:");
		Console.WriteLine("  --repo <path>     Git repository path.");
		Console.WriteLine("  --base <ref>      Git ref to preserve whitespace from. Default: HEAD.");
		Console.WriteLine("  --branch          Check full branch diff (merge-base..HEAD), like a squashed commit.");
		Console.WriteLine("  --upstream <ref>  Upstream ref for --branch. Default: origin/HEAD, origin/main, or origin/master.");
		Console.WriteLine("                    When the working tree is clean, branch mode is auto-enabled.");
		Console.WriteLine("  --dry-run-only    Preview only. Do not apply fixes.");
		Console.WriteLine("  --stash           Create stash backup before apply (default).");
		Console.WriteLine("  --no-stash        Do not create stash backup before apply.");
		Console.WriteLine("  --help            Show help.");
	}
}
