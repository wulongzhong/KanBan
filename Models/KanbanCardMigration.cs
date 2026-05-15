using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KanBan.Models;

public sealed partial class KanbanCard
{
    [JsonPropertyName("tags")]
    public List<string>? LegacyTags { get; set; }

    public void NormalizeLegacyFields()
    {
        if (LegacyTags is not { Count: > 0 } legacyTags)
        {
            return;
        }

        var tagLine = string.Join(
            ' ',
            legacyTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.StartsWith('#') ? tag : $"#{tag}"));

        if (!string.IsNullOrWhiteSpace(tagLine) &&
            !Description.Contains('#', StringComparison.Ordinal))
        {
            Description = string.IsNullOrWhiteSpace(Description)
                ? tagLine
                : $"{Description.TrimEnd()}\n{tagLine}";
        }

        LegacyTags = null;
    }
}

public static class KanbanBoardMigration
{
    public const string DefaultSwimlaneId = "default";

    public static void NormalizeLegacyFields(KanbanBoard board)
    {
        EnsureSwimlanes(board);

        foreach (var card in board.Lanes.SelectMany(lane => lane.Cards))
        {
            card.NormalizeLegacyFields();
            card.SwimlaneId ??= board.Swimlanes[0].Id;
        }

        foreach (var card in board.Archive)
        {
            card.NormalizeLegacyFields();
        }
    }

    public static void EnsureSwimlanes(KanbanBoard board)
    {
        if (board.Swimlanes.Count == 0)
        {
            board.Swimlanes.Add(new KanbanSwimlane
            {
                Id = DefaultSwimlaneId,
                Title = "Default",
            });
        }

        var defaultSwimlaneId = board.Swimlanes[0].Id;
        foreach (var card in board.Lanes.SelectMany(lane => lane.Cards))
        {
            card.SwimlaneId ??= defaultSwimlaneId;
        }
    }
}
