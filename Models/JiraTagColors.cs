using System;
using System.Collections.Generic;

namespace KanBan.Models;

public static class JiraTagColors
{
    private static readonly IReadOnlyList<string> Palette =
    [
        "#D9CDB5",
        "#C4D4BC",
        "#B5CCD9",
        "#C9BFD4",
        "#D9B8B8",
        "#B5C4D9",
        "#D4D9B5",
        "#B0C9C4",
    ];

    public static string GetBackground(string tag)
    {
        var index = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(tag)) % Palette.Count;
        return Palette[index];
    }

    public static string GetForeground(string background) => "#3D4A5C";
}
