using System;
using System.Collections.Generic;

namespace KanBan.Models;

public sealed class KanBanData
{
    public int Version { get; set; } = 2;

    public KanbanBoard Board { get; set; } = KanbanBoard.CreateDefault();
}

public sealed class KanbanBoard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "KanBan";

    public BoardViewMode ViewMode { get; set; } = BoardViewMode.Board;

    public BoardSettings Settings { get; set; } = new();

    public List<KanbanLane> Lanes { get; set; } = [];

    public List<KanbanSwimlane> Swimlanes { get; set; } = [];

    public List<KanbanCard> Archive { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public static KanbanBoard CreateDefault()
    {
        var now = DateTimeOffset.UtcNow;
        var defaultSwimlaneId = KanbanBoardMigration.DefaultSwimlaneId;

        return new KanbanBoard
        {
            Title = "My Kanban Board",
            UpdatedAt = now,
            Swimlanes =
            [
                new KanbanSwimlane
                {
                    Id = defaultSwimlaneId,
                    Title = "Default",
                },
            ],
            Lanes =
            [
                new KanbanLane
                {
                    Title = "To Do",
                    Cards =
                    [
                        new KanbanCard
                        {
                            SwimlaneId = defaultSwimlaneId,
                            Description = "Capture ideas\nDrop tasks here before they are ready to start. #inbox",
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
                            SwimlaneId = defaultSwimlaneId,
                            Description = "Build the board\nColumns, cards, search, archive, settings, and local JSON storage. #kanban #desktop",
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
                            SwimlaneId = defaultSwimlaneId,
                            Description = "Create project shell\n#setup",
                            CreatedAt = now,
                            UpdatedAt = now,
                        },
                    ],
                },
            ],
        };
    }
}

public sealed class KanbanSwimlane
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "New Swimlane";

    public bool IsCollapsed { get; set; }
}

public sealed class KanbanLane
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "New Lane";

    public int? MaxItems { get; set; }

    public LaneSort Sort { get; set; } = LaneSort.Manual;

    public bool ShouldMarkItemsComplete { get; set; }

    public bool IsCollapsed { get; set; }

    public List<KanbanCard> Cards { get; set; } = [];
}

public sealed partial class KanbanCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string? SwimlaneId { get; set; }

    public string Title { get; set; } = "New Card";

    public string Description { get; set; } = string.Empty;

    public List<string> Images { get; set; } = [];

    public DateTimeOffset? DueDate { get; set; }

    public TimeSpan? DueTime { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ArchivedAt { get; set; }
}

public sealed class BoardSettings
{
    public bool HideDatesInTitles { get; set; }

    public bool ShowRelativeDates { get; set; } = true;

    public bool PrependNewCards { get; set; }

    public int MaxArchiveSize { get; set; } = 200;

    public string DateFormat { get; set; } = "yyyy-MM-dd";
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