namespace WhiteSpaceFix;

internal static class FixRunner
{
	public static int Run(FixOptions options)
	{
		var result = Execute(options);
		return result.ExitCode;
	}

	public static FixRunResult Execute(FixOptions options)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(options.RepoPath))
			{
				throw new InvalidOperationException("Repository path is required.");
			}

			var repository = GitRepository.Find(options.RepoPath);
			if (options.BranchMode)
			{
				return ExecuteBranchMode(repository, options);
			}

			var repositoryInfo = repository.GetRepositoryInfo(options.BaseRef);

			PrintRepositoryInfo(repositoryInfo);

			if (repositoryInfo.AllChangedFiles.Count == 0)
			{
				if (TryAutoEnableBranchMode(repository, options))
				{
					Console.WriteLine(
						$"No working-tree changes against {options.BaseRef}. Checking branch diff instead...");
					Console.WriteLine();
					return ExecuteBranchMode(repository, options);
				}

				Console.WriteLine($"No changes found against {options.BaseRef}.");
				return new FixRunResult { ExitCode = 0 };
			}

			if (!options.DryRun && options.StashBeforeApply)
			{
				var stashEntry = repository.CreateBackupStash("WhiteSpaceFix: backup before whitespace fix");
				if (stashEntry == null)
				{
					Console.WriteLine("Stash before apply: skipped (working tree is clean).");
				}
				else
				{
					Console.WriteLine($"Stash before apply: created {stashEntry}");
				}

				Console.WriteLine();
			}

			return ProcessFiles(
				repository,
				options,
				repositoryInfo.AllChangedFiles,
				options.BaseRef,
				relativePath => File.ReadAllBytes(repository.GetFullPath(relativePath)));
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception.Message);
			return new FixRunResult { ExitCode = 1 };
		}
	}

	private static FixRunResult ExecuteBranchMode(GitRepository repository, FixOptions options)
	{
		var upstreamRef = string.IsNullOrWhiteSpace(options.UpstreamRef)
			? repository.ResolveUpstreamRef()
			: options.UpstreamRef!;
		var branchInfo = repository.GetBranchComparisonInfo(upstreamRef, options.HeadRef);

		PrintBranchInfo(branchInfo);

		if (branchInfo.ChangedFiles.Count == 0)
		{
			Console.WriteLine($"No branch changes found between {branchInfo.MergeBaseRef} and {branchInfo.HeadRef}.");
			return new FixRunResult { ExitCode = 0 };
		}

		if (!options.DryRun && options.StashBeforeApply)
		{
			var stashEntry = repository.CreateBackupStash("WhiteSpaceFix: backup before whitespace fix");
			if (stashEntry == null)
			{
				Console.WriteLine("Stash before apply: skipped (working tree is clean).");
			}
			else
			{
				Console.WriteLine($"Stash before apply: created {stashEntry}");
			}

			Console.WriteLine();
		}

		Func<string, byte[]> readWorkingBytes = options.DryRun
			? relativePath => repository.ReadBlob(options.HeadRef, relativePath)
			: relativePath =>
			{
				if (repository.IsSameAsRef(relativePath, options.HeadRef))
				{
					return repository.ReadBlob(options.HeadRef, relativePath);
				}

				var fullPath = repository.GetFullPath(relativePath);
				return File.Exists(fullPath)
					? File.ReadAllBytes(fullPath)
					: repository.ReadBlob(options.HeadRef, relativePath);
			};

		return ProcessFiles(
			repository,
			options,
			branchInfo.ChangedFiles,
			branchInfo.MergeBaseRef,
			readWorkingBytes);
	}

	private static FixRunResult ProcessFiles(
		GitRepository repository,
		FixOptions options,
		IReadOnlyList<string> changedFiles,
		string baseRef,
		Func<string, byte[]> readWorkingBytes)
	{
		var fixer = new WhitespaceFixer();
		var fixedFileCount = 0;
		var unchangedFileCount = 0;
		var skippedFileCount = 0;
		var restoredLineCount = 0;

		var modeLabel = options.DryRun ? "Previewing" : "Checking";
		Console.WriteLine($"{modeLabel} {changedFiles.Count} changed file(s) against {baseRef}...");

		foreach (var relativePath in changedFiles)
		{
			byte[] baseBytes;
			try
			{
				baseBytes = repository.ReadBlob(baseRef, relativePath);
			}
			catch (InvalidOperationException)
			{
				Console.WriteLine($"skip  {relativePath} (not present in {baseRef})");
				skippedFileCount++;
				continue;
			}

			byte[] workingBytes;
			try
			{
				workingBytes = readWorkingBytes(relativePath);
			}
			catch (InvalidOperationException)
			{
				Console.WriteLine($"skip  {relativePath} (deleted in {options.HeadRef})");
				skippedFileCount++;
				continue;
			}

			var result = fixer.FixFile(baseBytes, workingBytes);
			restoredLineCount += result.RestoredLineCount;

			switch (result.Status)
			{
				case "fixed":
					if (!options.DryRun && result.FixedBytes != null)
					{
						var fullPath = repository.GetFullPath(relativePath);
						Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
						File.WriteAllBytes(fullPath, result.FixedBytes);
					}

					fixedFileCount++;
					Console.WriteLine(
						$"{(options.DryRun ? "would fix" : "fixed")}  {relativePath} ({result.RestoredLineCount} whitespace-only line(s) restored from {baseRef})");
					break;
				case "unchanged":
					unchangedFileCount++;
					Console.WriteLine($"ok    {relativePath}");
					break;
				default:
					skippedFileCount++;
					Console.WriteLine($"skip  {relativePath} ({result.Reason})");
					break;
			}
		}

		Console.WriteLine();
		Console.WriteLine(
			$"Summary: {fixedFileCount} fixed, {unchangedFileCount} unchanged, {skippedFileCount} skipped, {restoredLineCount} line(s) restored.");

		if (options.DryRun && fixedFileCount > 0)
		{
			Console.WriteLine();
			Console.WriteLine("Dry-run only. Re-run and choose 'Apply fixes' to write changes.");
		}

		return new FixRunResult
		{
			ExitCode = 0,
			FixedFileCount = fixedFileCount,
			UnchangedFileCount = unchangedFileCount,
			SkippedFileCount = skippedFileCount,
			RestoredLineCount = restoredLineCount,
			ChangedFileCount = changedFiles.Count
		};
	}

	public static void PrintHelp()
	{
		Console.WriteLine("WhiteSpaceFix");
		Console.WriteLine();
		Console.WriteLine("Restores whitespace and line endings from a git base ref for changed files.");
		Console.WriteLine();
		Console.WriteLine("Interactive menu:");
		Console.WriteLine("  dotnet run");
		Console.WriteLine();
		Console.WriteLine("Command line:");
		Console.WriteLine("  WhiteSpaceFix CheckWhiteSpace [repo-path] [options]");
		Console.WriteLine("  WhiteSpaceFix <repo-path> [options]");
		Console.WriteLine("  dotnet run -- CheckWhiteSpace [repo-path] [options]");
		Console.WriteLine("  dotnet run -- <repo-path> [options]");
		Console.WriteLine();
		Console.WriteLine("Arguments:");
		Console.WriteLine("  <repo-path>     Path to the git repository to inspect and fix.");
		Console.WriteLine();
		Console.WriteLine("Options:");
		Console.WriteLine("  --repo <path>   Git repository path. Same as positional <repo-path>.");
		Console.WriteLine("  --base <ref>    Git ref to preserve whitespace from. Defaults to HEAD.");
		Console.WriteLine("  --branch        Check the full branch diff (merge-base..HEAD), like a squashed commit.");
		Console.WriteLine("                  When the working tree is clean, branch mode is auto-enabled.");
		Console.WriteLine("  --upstream <ref> Upstream ref for --branch. Defaults to origin/HEAD, origin/main, or origin/master.");
		Console.WriteLine("  --dry-run       Show files that would be fixed without writing changes.");
		Console.WriteLine("  --stash         Create a git stash backup before applying fixes.");
		Console.WriteLine("  --help          Show help.");
	}

	private static void PrintRepositoryInfo(RepositoryInfo repositoryInfo)
	{
		Console.WriteLine("Repository:");
		Console.WriteLine($"  Path:   {repositoryInfo.Root}");
		Console.WriteLine($"  Branch: {GetBranchLabel(repositoryInfo)}");
		Console.WriteLine($"  Status: {repositoryInfo.StatusSummary}");
		Console.WriteLine($"  Base:   {repositoryInfo.BaseRef}");
		Console.WriteLine();

		Console.WriteLine($"Changes against {repositoryInfo.BaseRef}:");
		Console.WriteLine($"  Unstaged: {repositoryInfo.UnstagedFiles.Count}");
		Console.WriteLine($"  Staged:   {repositoryInfo.StagedFiles.Count}");
		Console.WriteLine($"  Total:    {repositoryInfo.AllChangedFiles.Count}");
		Console.WriteLine();

		if (repositoryInfo.UnstagedFiles.Count > 0)
		{
			Console.WriteLine("Unstaged files:");
			foreach (var file in repositoryInfo.UnstagedFiles)
			{
				Console.WriteLine($"  {file}");
			}

			Console.WriteLine();
		}

		if (repositoryInfo.StagedFiles.Count > 0)
		{
			Console.WriteLine("Staged files:");
			foreach (var file in repositoryInfo.StagedFiles)
			{
				Console.WriteLine($"  {file}");
			}

			Console.WriteLine();
		}
	}

	private static string GetBranchLabel(RepositoryInfo repositoryInfo)
	{
		return string.IsNullOrWhiteSpace(repositoryInfo.CurrentBranch)
			? "(detached HEAD)"
			: repositoryInfo.CurrentBranch;
	}

	private static void PrintBranchInfo(BranchComparisonInfo branchInfo)
	{
		Console.WriteLine("Repository:");
		Console.WriteLine($"  Path:     {branchInfo.Root}");
		Console.WriteLine($"  Branch:   {GetBranchLabel(branchInfo.CurrentBranch)}");
		Console.WriteLine($"  Status:   {branchInfo.StatusSummary}");
		Console.WriteLine($"  Upstream: {branchInfo.UpstreamRef}");
		Console.WriteLine($"  Merge-base: {branchInfo.MergeBaseRef}");
		Console.WriteLine($"  Head:     {branchInfo.HeadRef}");
		Console.WriteLine();
		Console.WriteLine($"Branch changes ({branchInfo.MergeBaseRef}..{branchInfo.HeadRef}):");
		Console.WriteLine($"  Files: {branchInfo.ChangedFiles.Count}");
		Console.WriteLine();

		if (branchInfo.ChangedFiles.Count > 0)
		{
			Console.WriteLine("Changed files:");
			foreach (var file in branchInfo.ChangedFiles)
			{
				Console.WriteLine($"  {file}");
			}

			Console.WriteLine();
		}
	}

	private static string GetBranchLabel(string currentBranch)
	{
		return string.IsNullOrWhiteSpace(currentBranch)
			? "(detached HEAD)"
			: currentBranch;
	}

	private static bool TryAutoEnableBranchMode(GitRepository repository, FixOptions options)
	{
		if (!string.Equals(options.BaseRef, "HEAD", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		try
		{
			var upstreamRef = string.IsNullOrWhiteSpace(options.UpstreamRef)
				? repository.ResolveUpstreamRef()
				: options.UpstreamRef!;
			var branchInfo = repository.GetBranchComparisonInfo(upstreamRef, options.HeadRef);
			if (branchInfo.ChangedFiles.Count == 0)
			{
				return false;
			}

			options.BranchMode = true;
			options.UpstreamRef = upstreamRef;
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}
}

internal sealed class FixOptions
{
	public string? RepoPath { get; set; }

	public string BaseRef { get; set; } = "HEAD";

	public string HeadRef { get; set; } = "HEAD";

	public string? UpstreamRef { get; set; }

	public bool BranchMode { get; set; }

	public bool DryRun { get; set; }

	public bool StashBeforeApply { get; set; }

	public bool DryRunOnly { get; set; }

	public bool ShowHelp { get; set; }
}
