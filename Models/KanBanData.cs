using System;
using System.Collections.Generic;

namespace KanBan.Models;

public sealed class KanBanData
{
    public int Version { get; set; } = 1;

    public KanbanBoard Board { get; set; } = KanbanBoard.CreateDefault();
}

public sealed class KanbanBoard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "KanBan";

    public BoardViewMode ViewMode { get; set; } = BoardViewMode.Board;

    public BoardSettings Settings { get; set; } = new();

    public List<KanbanLane> Lanes { get; set; } = [];

    public List<KanbanCard> Archive { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public static KanbanBoard CreateDefault()
    {
        var now = DateTimeOffset.UtcNow;

        return new KanbanBoard
        {
            Title = "My Kanban Board",
            UpdatedAt = now,
            Lanes =
            [
                new KanbanLane
                {
                    Title = "To Do",
                    Cards =
                    [
                        new KanbanCard
                        {
                            Title = "Capture ideas",
                            Description = "Drop tasks here before they are ready to start.",
                            Tags = ["inbox"],
                            CreatedAt = now,
                            UpdatedAt = now,
                        },
                    ],
                },
                new KanbanLane
                {
                    Title = "In Progress",
                    MaxItems = 3,
                    Cards =
                    [
                        new KanbanCard
                        {
                            Title = "Build the board",
                            Description = "Columns, cards, search, archive, settings, and local JSON storage.",
                            Tags = ["kanban", "desktop"],
                            DueDate = now.AddDays(2),
                            CreatedAt = now,
                            UpdatedAt = now,
                        },
                    ],
                },
                new KanbanLane
                {
                    Title = "Done",
                    Cards =
                    [
                        new KanbanCard
                        {
                            Title = "Create project shell",
                            IsComplete = true,
                            Tags = ["setup"],
                            CreatedAt = now,
                            UpdatedAt = now,
                        },
                    ],
                },
            ],
        };
    }
}

public sealed class KanbanLane
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "New Lane";

    public int? MaxItems { get; set; }

    public LaneSort Sort { get; set; } = LaneSort.Manual;

    public bool ShouldMarkItemsComplete { get; set; }

    public List<KanbanCard> Cards { get; set; } = [];
}

public sealed class KanbanCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "New Card";

    public string Description { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public string CheckChar { get; set; } = " ";

    public List<string> Tags { get; set; } = [];

    public DateTimeOffset? DueDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ArchivedAt { get; set; }
}

public sealed class BoardSettings
{
    public bool ShowCardCheckbox { get; set; } = true;

    public bool HideDatesInTitles { get; set; }

    public bool ShowRelativeDates { get; set; } = true;

    public bool PrependNewCards { get; set; }

    public int MaxArchiveSize { get; set; } = 200;

    public string DateFormat { get; set; } = "yyyy-MM-dd";

    public List<TagColorSetting> TagColors { get; set; } = [];
}

public sealed class TagColorSetting
{
    public string Tag { get; set; } = string.Empty;

    public string Foreground { get; set; } = "#d7e6ff";

    public string Background { get; set; } = "#273449";
}

public enum BoardViewMode
{
    Board,
    List,
    Table,
}

public enum LaneSort
{
    Manual,
    TitleAsc,
    TitleDesc,
    DateAsc,
    DateDesc,
    TagsAsc,
    TagsDesc,
}