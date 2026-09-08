using System.Text;
using System.Text.RegularExpressions;

namespace Interrogon.Core;

public enum MarkdownSegmentKind
{
    Prose,
    Code
}

public sealed record MarkdownSegment(MarkdownSegmentKind Kind, string Text, string? Language);

public static class MarkdownContent
{
    private static readonly Regex FencedCodeBlock = new(
        @"```(?<lang>[a-zA-Z0-9_+-]*)\r?\n(?<code>.*?)```",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Splits markdown text into alternating prose/code segments for rendering.</summary>
    public static IReadOnlyList<MarkdownSegment> ParseSegments(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return [];
        }

        var segments = new List<MarkdownSegment>();
        var lastIndex = 0;

        foreach (Match match in FencedCodeBlock.Matches(markdown))
        {
            if (match.Index > lastIndex)
            {
                var prose = markdown[lastIndex..match.Index];
                if (!string.IsNullOrWhiteSpace(prose))
                {
                    segments.Add(new MarkdownSegment(MarkdownSegmentKind.Prose, prose.Trim(), null));
                }
            }

            var language = match.Groups["lang"].Value;
            var code = match.Groups["code"].Value.TrimEnd('\r', '\n');
            segments.Add(new MarkdownSegment(MarkdownSegmentKind.Code, code, string.IsNullOrEmpty(language) ? null : language));

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < markdown.Length)
        {
            var prose = markdown[lastIndex..];
            if (!string.IsNullOrWhiteSpace(prose))
            {
                segments.Add(new MarkdownSegment(MarkdownSegmentKind.Prose, prose.Trim(), null));
            }
        }

        return segments;
    }

    /// <summary>Strips markdown formatting for plain-text clipboard copy (fences become bare code).</summary>
    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var segment in ParseSegments(markdown))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(segment.Text);
        }

        var result = builder.Length > 0 ? builder.ToString() : markdown;

        // Strip remaining inline markdown syntax (bold/italic/inline code/headers).
        result = Regex.Replace(result, @"^#{1,6}\s*", string.Empty, RegexOptions.Multiline);
        result = Regex.Replace(result, @"(\*\*|__)(.*?)\1", "$2");
        result = Regex.Replace(result, @"(\*|_)(.*?)\1", "$2");
        result = Regex.Replace(result, @"`([^`]*)`", "$1");

        return result.Trim();
    }
}
