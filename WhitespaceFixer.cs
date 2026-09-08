namespace WhiteSpaceFix;

internal sealed class WhitespaceFixer
{
	public WhitespaceFixResult FixFile(byte[] baseBytes, byte[] workingBytes)
	{
		if (ContainsNullByte(baseBytes) || ContainsNullByte(workingBytes))
		{
			return WhitespaceFixResult.Skipped("binary file");
		}

		var baseLines = TextLine.Parse(baseBytes);
		var workingLines = TextLine.Parse(workingBytes);
		var dominantNewline = TextLine.GetDominantNewline(baseLines);
		var matches = BuildLongestCommonSubsequence(baseLines, workingLines);
		var fixedLines = new List<TextLine>(workingLines.Count);
		var restoredLineCount = 0;
		var baseIndex = 0;
		var workingIndex = 0;
		var matchIndex = 0;

		while (workingIndex < workingLines.Count)
		{
			if (matchIndex < matches.Count
				&& matches[matchIndex].BaseIndex == baseIndex
				&& matches[matchIndex].WorkingIndex == workingIndex)
			{
				var baseLine = baseLines[baseIndex];
				var workingLine = workingLines[workingIndex];

				if (LinesMatchIgnoringWhitespace(baseLine, workingLine))
				{
					if (!baseLine.ToBytes().SequenceEqual(workingLine.ToBytes()))
					{
						restoredLineCount++;
					}

					fixedLines.Add(baseLine);
				}
				else
				{
					fixedLines.Add(TextLine.WithNewline(workingLine, dominantNewline));
				}

				baseIndex++;
				workingIndex++;
				matchIndex++;
				continue;
			}

			if (matchIndex < matches.Count && matches[matchIndex].WorkingIndex == workingIndex)
			{
				baseIndex++;
				continue;
			}

			fixedLines.Add(TextLine.WithNewline(workingLines[workingIndex], dominantNewline));
			workingIndex++;
		}

		var fixedBytes = TextLine.Serialize(fixedLines);
		if (fixedBytes.SequenceEqual(workingBytes))
		{
			return WhitespaceFixResult.Unchanged(restoredLineCount);
		}

		return WhitespaceFixResult.Fixed(fixedBytes, restoredLineCount);
	}

	private static bool LinesMatchIgnoringWhitespace(TextLine left, TextLine right)
	{
		return left.NormalizedContent == right.NormalizedContent;
	}

	private static bool ContainsNullByte(byte[] data)
	{
		return data.Any(value => value == 0);
	}

	private static List<SequenceMatch> BuildLongestCommonSubsequence(
		IReadOnlyList<TextLine> baseLines,
		IReadOnlyList<TextLine> workingLines)
	{
		var lengths = new int[baseLines.Count + 1, workingLines.Count + 1];

		for (var baseIndex = baseLines.Count - 1; baseIndex >= 0; baseIndex--)
		{
			for (var workingIndex = workingLines.Count - 1; workingIndex >= 0; workingIndex--)
			{
				if (LinesMatchIgnoringWhitespace(baseLines[baseIndex], workingLines[workingIndex]))
				{
					lengths[baseIndex, workingIndex] = lengths[baseIndex + 1, workingIndex + 1] + 1;
				}
				else
				{
					lengths[baseIndex, workingIndex] = Math.Max(
						lengths[baseIndex + 1, workingIndex],
						lengths[baseIndex, workingIndex + 1]);
				}
			}
		}

		var matches = new List<SequenceMatch>();
		var currentBaseIndex = 0;
		var currentWorkingIndex = 0;

		while (currentBaseIndex < baseLines.Count && currentWorkingIndex < workingLines.Count)
		{
			if (LinesMatchIgnoringWhitespace(baseLines[currentBaseIndex], workingLines[currentWorkingIndex]))
			{
				matches.Add(new SequenceMatch(currentBaseIndex, currentWorkingIndex));
				currentBaseIndex++;
				currentWorkingIndex++;
				continue;
			}

			if (lengths[currentBaseIndex + 1, currentWorkingIndex] >= lengths[currentBaseIndex, currentWorkingIndex + 1])
			{
				currentBaseIndex++;
			}
			else
			{
				currentWorkingIndex++;
			}
		}

		return matches;
	}

	private sealed class SequenceMatch
	{
		public SequenceMatch(int baseIndex, int workingIndex)
		{
			BaseIndex = baseIndex;
			WorkingIndex = workingIndex;
		}

		public int BaseIndex { get; }

		public int WorkingIndex { get; }
	}
}

internal sealed class WhitespaceFixResult
{
	private WhitespaceFixResult(string status, byte[]? fixedBytes, int restoredLineCount, string? reason)
	{
		Status = status;
		FixedBytes = fixedBytes;
		RestoredLineCount = restoredLineCount;
		Reason = reason;
	}

	public string Status { get; }

	public byte[]? FixedBytes { get; }

	public int RestoredLineCount { get; }

	public string? Reason { get; }

	public static WhitespaceFixResult Fixed(byte[] fixedBytes, int restoredLineCount)
	{
		return new WhitespaceFixResult("fixed", fixedBytes, restoredLineCount, null);
	}

	public static WhitespaceFixResult Unchanged(int restoredLineCount)
	{
		return new WhitespaceFixResult("unchanged", null, restoredLineCount, null);
	}

	public static WhitespaceFixResult Skipped(string reason)
	{
		return new WhitespaceFixResult("skipped", null, 0, reason);
	}
}
