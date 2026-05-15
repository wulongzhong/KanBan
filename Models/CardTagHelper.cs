using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KanBan.Models;

public static partial class CardTagHelper
{
    [GeneratedRegex(@"#[\w/-]+", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    public static IReadOnlyList<string> ParseTags(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return TagPattern()
            .Matches(text)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string GetSortKey(string? text) => string.Join(' ', ParseTags(text));

    public static string StripTags(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : TagPattern().Replace(text, string.Empty).Trim();
}
