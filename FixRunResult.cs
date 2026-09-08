namespace WhiteSpaceFix;

internal sealed class FixRunResult
{
	public int ExitCode { get; init; }

	public int FixedFileCount { get; init; }

	public int UnchangedFileCount { get; init; }

	public int SkippedFileCount { get; init; }

	public int RestoredLineCount { get; init; }

	public int ChangedFileCount { get; init; }
}
