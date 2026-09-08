namespace WhiteSpaceFix;

using System.Text;

internal sealed class TextLine
{
	public static TextLine WithNewline(TextLine line, byte[] newline)
	{
		return line.Newline.Length == 0
			? line
			: new TextLine(line.Content, newline);
	}

	private TextLine(byte[] content, byte[] newline)
	{
		Content = content;
		Newline = newline;
	}

	public byte[] Content { get; }

	public byte[] Newline { get; }

	public string NormalizedContent => Encoding.UTF8.GetString(Content);

	public byte[] ToBytes()
	{
		if (Newline.Length == 0)
		{
			return Content;
		}

		var bytes = new byte[Content.Length + Newline.Length];
		Buffer.BlockCopy(Content, 0, bytes, 0, Content.Length);
		Buffer.BlockCopy(Newline, 0, bytes, Content.Length, Newline.Length);
		return bytes;
	}

	public static IReadOnlyList<TextLine> Parse(byte[] data)
	{
		var lines = new List<TextLine>();
		var start = 0;

		for (var index = 0; index < data.Length; index++)
		{
			if (data[index] != (byte)'\n')
			{
				continue;
			}

			var lineEnd = index;
			var contentLength = lineEnd - start;
			byte[] newline;

			if (contentLength > 0 && data[lineEnd - 1] == (byte)'\r')
			{
				contentLength--;
				newline = new[] { (byte)'\r', (byte)'\n' };
			}
			else
			{
				newline = new[] { (byte)'\n' };
			}

			var content = new byte[contentLength];
			Buffer.BlockCopy(data, start, content, 0, contentLength);
			lines.Add(new TextLine(content, newline));
			start = index + 1;
		}

		if (start < data.Length)
		{
			var content = new byte[data.Length - start];
			Buffer.BlockCopy(data, start, content, 0, content.Length);
			lines.Add(new TextLine(content, Array.Empty<byte>()));
		}

		return lines;
	}

	public static byte[] Serialize(IReadOnlyList<TextLine> lines)
	{
		if (lines.Count == 0)
		{
			return Array.Empty<byte>();
		}

		var totalLength = lines.Sum(line => line.Content.Length + line.Newline.Length);
		var result = new byte[totalLength];
		var offset = 0;

		foreach (var line in lines)
		{
			Buffer.BlockCopy(line.Content, 0, result, offset, line.Content.Length);
			offset += line.Content.Length;
			Buffer.BlockCopy(line.Newline, 0, result, offset, line.Newline.Length);
			offset += line.Newline.Length;
		}

		return result;
	}

	public static byte[] GetDominantNewline(IReadOnlyList<TextLine> lines)
	{
		var crlfCount = lines.Count(line => line.Newline.Length == 2);
		var lfCount = lines.Count(line => line.Newline.Length == 1);

		return crlfCount >= lfCount
			? new[] { (byte)'\r', (byte)'\n' }
			: new[] { (byte)'\n' };
	}
}
