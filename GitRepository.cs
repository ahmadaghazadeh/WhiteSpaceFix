namespace WhiteSpaceFix;

using System.Diagnostics;
using System.Text;

internal sealed class GitRepository
{
	private readonly string _root;

	public GitRepository(string root)
	{
		_root = root;
	}

	public string Root => _root;

	public static GitRepository Find(string? startDirectory)
	{
		var directory = string.IsNullOrWhiteSpace(startDirectory)
			? Directory.GetCurrentDirectory()
			: Path.GetFullPath(startDirectory);

		while (true)
		{
			if (Directory.Exists(Path.Combine(directory, ".git")))
			{
				return new GitRepository(directory);
			}

			var parent = Directory.GetParent(directory);
			if (parent == null)
			{
				throw new InvalidOperationException("Could not locate a git repository root.");
			}

			directory = parent.FullName;
		}
	}

	public RepositoryInfo GetRepositoryInfo(string baseRef)
	{
		var currentBranch = RunGit("branch --show-current").Trim();
		var statusSummary = RunGit("status -sb")
			.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.FirstOrDefault()
			?.Trim() ?? string.Empty;

		var unstagedFiles = GetChangedFiles($"diff --name-only {baseRef}");
		var stagedFiles = GetChangedFiles($"diff --cached --name-only {baseRef}");
		var allChangedFiles = unstagedFiles
			.Union(stagedFiles, StringComparer.OrdinalIgnoreCase)
			.OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
			.ToList();

		return new RepositoryInfo(
			_root,
			currentBranch,
			statusSummary,
			baseRef,
			unstagedFiles,
			stagedFiles,
			allChangedFiles);
	}

	public BranchComparisonInfo GetBranchComparisonInfo(string upstreamRef, string headRef = "HEAD")
	{
		var currentBranch = RunGit("branch --show-current").Trim();
		var statusSummary = RunGit("status -sb")
			.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.FirstOrDefault()
			?.Trim() ?? string.Empty;
		var mergeBase = ResolveMergeBase(upstreamRef, headRef);
		var changedFiles = GetChangedFilesBetween(mergeBase, headRef);

		return new BranchComparisonInfo(
			_root,
			currentBranch,
			statusSummary,
			upstreamRef,
			mergeBase,
			headRef,
			changedFiles);
	}

	public string ResolveUpstreamRef()
	{
		foreach (var candidate in GetUpstreamCandidates())
		{
			try
			{
				var resolved = RunGit($"rev-parse --verify {candidate}").Trim();
				if (!string.IsNullOrWhiteSpace(resolved))
				{
					return candidate;
				}
			}
			catch (InvalidOperationException)
			{
			}
		}

		throw new InvalidOperationException(
			"Could not resolve a branch target ref. Use --upstream <ref> (for example origin/master).");
	}

	private IEnumerable<string> GetUpstreamCandidates()
	{
		yield return "origin/HEAD";
		yield return "origin/main";
		yield return "origin/master";
		yield return "main";
		yield return "master";

		var currentBranch = RunGit("branch --show-current").Trim();
		if (string.IsNullOrWhiteSpace(currentBranch))
		{
			yield break;
		}

		string trackedUpstream;
		try
		{
			trackedUpstream = RunGit("rev-parse --abbrev-ref @{upstream}").Trim();
		}
		catch (InvalidOperationException)
		{
			yield break;
		}

		if (!IsSameBranch(currentBranch, trackedUpstream))
		{
			yield return trackedUpstream;
		}
	}

	private static bool IsSameBranch(string currentBranch, string upstreamRef)
	{
		if (string.Equals(currentBranch, upstreamRef, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		var upstreamBranch = upstreamRef;
		var slashIndex = upstreamRef.LastIndexOf('/');
		if (slashIndex >= 0 && slashIndex < upstreamRef.Length - 1)
		{
			upstreamBranch = upstreamRef[(slashIndex + 1)..];
		}

		return string.Equals(currentBranch, upstreamBranch, StringComparison.OrdinalIgnoreCase);
	}

	public string ResolveMergeBase(string upstreamRef, string headRef = "HEAD")
	{
		return RunGit($"merge-base {headRef} {upstreamRef}").Trim();
	}

	public bool IsSameAsRef(string relativePath, string gitRef = "HEAD")
	{
		try
		{
			RunGit($"diff --quiet {gitRef} -- {relativePath}");
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	public byte[] ReadBlob(string baseRef, string relativePath)
	{
		return RunGitBytes($"show {baseRef}:{relativePath}");
	}

	public string GetFullPath(string relativePath)
	{
		return Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
	}

	public string? CreateBackupStash(string message)
	{
		if (IsWorkingTreeClean())
		{
			return null;
		}

		var stashCommit = RunGit("stash create -u").Trim();
		if (string.IsNullOrWhiteSpace(stashCommit))
		{
			return null;
		}

		var escapedMessage = message.Replace("\"", "\\\"", StringComparison.Ordinal);
		RunGit($"stash store -m \"{escapedMessage}\" {stashCommit}");
		return RunGit("stash list")
			.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.FirstOrDefault()
			?.Trim();
	}

	public bool IsWorkingTreeClean()
	{
		return string.IsNullOrWhiteSpace(RunGit("status --porcelain").Trim());
	}

	public DiffComparison CompareDiffStats(string baseRef)
	{
		var normalSummary = GetDiffStat($"diff --stat {baseRef}");
		var whitespaceIgnoredSummary = GetDiffStat($"diff -w --stat {baseRef}");
		var hasSameScope = string.Equals(normalSummary, whitespaceIgnoredSummary, StringComparison.Ordinal);

		return new DiffComparison(normalSummary, whitespaceIgnoredSummary, hasSameScope);
	}

	public DiffComparison CompareBranchDiffStats(string baseRef, string headRef = "HEAD")
	{
		var normalSummary = GetDiffStat($"diff --stat {baseRef}..{headRef}");
		var whitespaceIgnoredSummary = GetDiffStat($"diff -w --stat {baseRef}..{headRef}");
		var hasSameScope = string.Equals(normalSummary, whitespaceIgnoredSummary, StringComparison.Ordinal);

		return new DiffComparison(
			normalSummary,
			whitespaceIgnoredSummary,
			hasSameScope,
			$"{baseRef}..{headRef}");
	}

	private IReadOnlyList<string> GetChangedFilesBetween(string fromRef, string toRef)
	{
		return GetChangedFiles($"diff --name-only {fromRef}..{toRef}");
	}

	private IReadOnlyList<string> GetChangedFiles(string arguments)
	{
		var files = new List<string>();

		foreach (var line in RunGit(arguments).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
		{
			files.Add(line.Replace('\\', '/'));
		}

		return files;
	}

	private string GetDiffStat(string arguments)
	{
		var lines = RunGit(arguments)
			.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(line => line.TrimEnd())
			.Where(line => !string.IsNullOrWhiteSpace(line))
			.ToList();

		if (lines.Count == 0)
		{
			return "(no changes)";
		}

		return lines[^1];
	}

	private string RunGit(string arguments)
	{
		return Encoding.UTF8.GetString(RunGitBytes(arguments));
	}

	private byte[] RunGitBytes(string arguments)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "git",
			Arguments = arguments,
			WorkingDirectory = _root,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Failed to start git.");

		var output = process.StandardOutput.ReadToEnd();
		var error = process.StandardError.ReadToEnd();
		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"git {arguments} failed: {error.Trim()}");
		}

		return Encoding.UTF8.GetBytes(output);
	}
}

internal sealed class RepositoryInfo
{
	public RepositoryInfo(
		string root,
		string currentBranch,
		string statusSummary,
		string baseRef,
		IReadOnlyList<string> unstagedFiles,
		IReadOnlyList<string> stagedFiles,
		IReadOnlyList<string> allChangedFiles)
	{
		Root = root;
		CurrentBranch = currentBranch;
		StatusSummary = statusSummary;
		BaseRef = baseRef;
		UnstagedFiles = unstagedFiles;
		StagedFiles = stagedFiles;
		AllChangedFiles = allChangedFiles;
	}

	public string Root { get; }

	public string CurrentBranch { get; }

	public string StatusSummary { get; }

	public string BaseRef { get; }

	public IReadOnlyList<string> UnstagedFiles { get; }

	public IReadOnlyList<string> StagedFiles { get; }

	public IReadOnlyList<string> AllChangedFiles { get; }
}

internal sealed class DiffComparison
{
	public DiffComparison(string normalSummary, string whitespaceIgnoredSummary, bool hasSameScope, string? rangeLabel = null)
	{
		NormalSummary = normalSummary;
		WhitespaceIgnoredSummary = whitespaceIgnoredSummary;
		HasSameScope = hasSameScope;
		RangeLabel = rangeLabel;
	}

	public string NormalSummary { get; }

	public string WhitespaceIgnoredSummary { get; }

	public bool HasSameScope { get; }

	public string? RangeLabel { get; }
}

internal sealed class BranchComparisonInfo
{
	public BranchComparisonInfo(
		string root,
		string currentBranch,
		string statusSummary,
		string upstreamRef,
		string mergeBaseRef,
		string headRef,
		IReadOnlyList<string> changedFiles)
	{
		Root = root;
		CurrentBranch = currentBranch;
		StatusSummary = statusSummary;
		UpstreamRef = upstreamRef;
		MergeBaseRef = mergeBaseRef;
		HeadRef = headRef;
		ChangedFiles = changedFiles;
	}

	public string Root { get; }

	public string CurrentBranch { get; }

	public string StatusSummary { get; }

	public string UpstreamRef { get; }

	public string MergeBaseRef { get; }

	public string HeadRef { get; }

	public IReadOnlyList<string> ChangedFiles { get; }
}
