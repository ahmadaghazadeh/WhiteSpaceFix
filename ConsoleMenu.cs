namespace WhiteSpaceFix;

internal static class ConsoleMenu
{
	public static int RunInteractive()
	{
		var session = new MenuSession();

		Console.WriteLine();
		Console.WriteLine("========================================");
		Console.WriteLine("             WhiteSpaceFix");
		Console.WriteLine("========================================");
		Console.WriteLine();

		while (true)
		{
			PrintSession(session);
			PrintMenu();

			var choice = ReadLine("Choice").Trim();
			Console.WriteLine();

			switch (choice)
			{
				case "1":
					SetRepoPath(session);
					break;

				case "2":
					if (!TryEnsureRepoPath(session))
					{
						break;
					}

					if (!ConfirmApply())
					{
						Console.WriteLine("Cancelled.");
						Console.WriteLine();
						break;
					}

					RunAndPause(new FixOptions
					{
						RepoPath = session.RepoPath!,
						BaseRef = session.BaseRef,
						DryRun = false,
						StashBeforeApply = session.StashBeforeApply
					});
					break;

				case "3":
					if (!TryEnsureRepoPath(session))
					{
						break;
					}

					RunAndPause(new FixOptions
					{
						RepoPath = session.RepoPath!,
						BaseRef = session.BaseRef,
						DryRun = true
					});
					break;

				case "4":
					SetBaseRef(session);
					break;

				case "5":
					ToggleStashBeforeApply(session);
					break;

				case "6":
					if (!TryEnsureRepoPath(session))
					{
						break;
					}

					RunCheckWhiteSpaceAndPause(session);
					break;

				case "7":
					if (!TryEnsureRepoPath(session))
					{
						break;
					}

					RunBranchCheckWhiteSpaceAndPause(session);
					break;

				case "8":
					SetUpstreamRef(session);
					break;

				case "9":
					FixRunner.PrintHelp();
					Console.WriteLine();
					break;

				case "0":
				case "q":
				case "Q":
					return 0;

				default:
					Console.WriteLine("Invalid choice. Enter 1-9, or 0 to exit.");
					Console.WriteLine();
					break;
			}
		}
	}

	private static void PrintSession(MenuSession session)
	{
		var repoLabel = string.IsNullOrWhiteSpace(session.RepoPath)
			? "(not set)"
			: session.RepoPath;

		var stashLabel = session.StashBeforeApply ? "On" : "Off";

		var upstreamLabel = string.IsNullOrWhiteSpace(session.UpstreamRef)
			? "(auto)"
			: session.UpstreamRef;

		Console.WriteLine($"  Repo:      {repoLabel}");
		Console.WriteLine($"  Base:      {session.BaseRef}");
		Console.WriteLine($"  Upstream:  {upstreamLabel}");
		Console.WriteLine($"  Stash:     {stashLabel} (before apply)");
		Console.WriteLine();
	}

	private static void PrintMenu()
	{
		Console.WriteLine("  1. Set repository path");
		Console.WriteLine("  2. Apply fixes");
		Console.WriteLine("  3. Preview changes (dry-run)");
		Console.WriteLine("  4. Set base ref");
		Console.WriteLine("  5. Toggle stash before apply");
		Console.WriteLine("  6. CheckWhiteSpace (preview + fix)");
		Console.WriteLine("  7. Check branch whitespace (preview + fix)");
		Console.WriteLine("  8. Set upstream ref");
		Console.WriteLine("  9. Help");
		Console.WriteLine("  0. Exit");
		Console.WriteLine();
	}

	private static bool TryEnsureRepoPath(MenuSession session)
	{
		if (!string.IsNullOrWhiteSpace(session.RepoPath))
		{
			return true;
		}

		Console.WriteLine("Repository path is not set. Choose option 1 first.");
		Console.WriteLine();
		return false;
	}

	private static void SetRepoPath(MenuSession session)
	{
		var input = ReadLine("Repository path", session.RepoPath);
		if (string.IsNullOrWhiteSpace(input))
		{
			Console.WriteLine("Repository path was not changed.");
			Console.WriteLine();
			return;
		}

		try
		{
			var repository = GitRepository.Find(input);
			session.RepoPath = repository.Root;
			Console.WriteLine($"Using repository: {session.RepoPath}");
		}
		catch (Exception exception)
		{
			Console.WriteLine($"Error: {exception.Message}");
		}

		Console.WriteLine();
	}

	private static void SetBaseRef(MenuSession session)
	{
		var input = ReadLine("Base ref", session.BaseRef);
		if (string.IsNullOrWhiteSpace(input))
		{
			Console.WriteLine("Base ref was not changed.");
			Console.WriteLine();
			return;
		}

		session.BaseRef = input.Trim();
		Console.WriteLine($"Base ref set to: {session.BaseRef}");
		Console.WriteLine();
	}

	private static void ToggleStashBeforeApply(MenuSession session)
	{
		session.StashBeforeApply = !session.StashBeforeApply;
		var state = session.StashBeforeApply ? "On" : "Off";
		Console.WriteLine($"Stash before apply is now: {state}");

		if (session.StashBeforeApply)
		{
			Console.WriteLine("A backup stash will be created before writing fixes (working tree is not cleared).");
		}

		Console.WriteLine();
	}

	private static bool ConfirmApply()
	{
		var input = ReadLine("Apply fixes to working tree? (y/N)", "N").Trim();
		return input.Equals("y", StringComparison.OrdinalIgnoreCase)
			|| input.Equals("yes", StringComparison.OrdinalIgnoreCase);
	}

	private static void RunAndPause(FixOptions options)
	{
		FixRunner.Run(options);
		Console.WriteLine();
		Console.WriteLine("Press Enter to return to menu...");
		Console.ReadLine();
		Console.WriteLine();
	}

	private static void SetUpstreamRef(MenuSession session)
	{
		var input = ReadLine("Upstream ref (blank = auto)", session.UpstreamRef);
		if (string.IsNullOrWhiteSpace(input))
		{
			session.UpstreamRef = null;
			Console.WriteLine("Upstream ref cleared. Branch checks will auto-detect origin/HEAD, origin/main, or origin/master.");
		}
		else
		{
			session.UpstreamRef = input.Trim();
			Console.WriteLine($"Upstream ref set to: {session.UpstreamRef}");
		}

		Console.WriteLine();
	}

	private static void RunBranchCheckWhiteSpaceAndPause(MenuSession session)
	{
		var args = new List<string>
		{
			session.RepoPath!,
			"--branch"
		};

		if (session.StashBeforeApply)
		{
			args.Add("--stash");
		}
		else
		{
			args.Add("--no-stash");
		}

		if (!string.IsNullOrWhiteSpace(session.UpstreamRef))
		{
			args.Add("--upstream");
			args.Add(session.UpstreamRef!);
		}

		CheckWhiteSpaceCommand.Run(args.ToArray());
		Console.WriteLine();
		Console.WriteLine("Press Enter to return to menu...");
		Console.ReadLine();
		Console.WriteLine();
	}

	private static void RunCheckWhiteSpaceAndPause(MenuSession session)
	{
		var args = new List<string> { session.RepoPath! };
		if (session.StashBeforeApply)
		{
			args.Add("--stash");
		}
		else
		{
			args.Add("--no-stash");
		}

		if (!string.Equals(session.BaseRef, "HEAD", StringComparison.Ordinal))
		{
			args.Add("--base");
			args.Add(session.BaseRef);
		}

		if (!string.IsNullOrWhiteSpace(session.UpstreamRef))
		{
			args.Add("--upstream");
			args.Add(session.UpstreamRef!);
		}

		CheckWhiteSpaceCommand.Run(args.ToArray());
		Console.WriteLine();
		Console.WriteLine("Press Enter to return to menu...");
		Console.ReadLine();
		Console.WriteLine();
	}

	private static string ReadLine(string prompt, string? defaultValue = null)
	{
		if (string.IsNullOrWhiteSpace(defaultValue))
		{
			Console.Write($"{prompt}: ");
		}
		else
		{
			Console.Write($"{prompt} [{defaultValue}]: ");
		}

		var input = Console.ReadLine();
		if (string.IsNullOrWhiteSpace(input))
		{
			return defaultValue ?? string.Empty;
		}

		return input.Trim().Trim('"');
	}

	private sealed class MenuSession
	{
		public string? RepoPath { get; set; }

		public string BaseRef { get; set; } = "HEAD";

		public string? UpstreamRef { get; set; }

		public bool StashBeforeApply { get; set; }
	}
}
