using System;
using System.Collections.Generic;

namespace KanBan.Models;

public static class JiraTagColors
{
    private static readonly IReadOnlyList<string> Palette =
    [
        "#FFC400",
        "#FF8B00",
        "#00B8D9",
        "#6554C0",
        "#36B37E",
        "#FF5630",
        "#0065FF",
        "#00875A",
    ];

    public static string GetBackground(string tag)
    {
        var index = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(tag)) % Palette.Count;
        return Palette[index];
    }

    public static string GetForeground(string background) =>
        background is "#FFC400" or "#FFAB00" or "#FF8B00" ? "#172B4D" : "#FFFFFF";
}
