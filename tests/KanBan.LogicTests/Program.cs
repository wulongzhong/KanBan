using System;
using System.IO;
using KanBan.Models;
using KanBan.Services;

var tests = new (string Name, Action Run)[]
{
    ("MoveCard moves cards across lanes and honors completion lanes", MoveCardAcrossLanes),
    ("ArchiveCard trims archive to configured size", ArchiveCardTrimsArchive),
    ("JsonBoardStorage round-trips board data", JsonStorageRoundTrips),
    ("EnsureSwimlanes migrates legacy boards", EnsureSwimlanesMigratesLegacyBoards),
    ("MoveCard updates swimlane when provided", MoveCardUpdatesSwimlane),
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

static void MoveCardAcrossLanes()
{
    var board = new KanbanBoard
    {
        Lanes =
        [
            new KanbanLane
            {
                Id = "todo",
                Title = "Todo",
                Cards = [new KanbanCard { Id = "a", Title = "A" }],
            },
            new KanbanLane
            {
                Id = "done",
                Title = "Done",
                ShouldMarkItemsComplete = true,
                Cards = [new KanbanCard { Id = "b", Title = "B" }],
            },
        ],
    };

    var moved = KanbanBoardOperations.MoveCard(board, "a", "done", "b");

    Assert(moved, "Expected move to succeed.");
    Assert(board.Lanes[0].Cards.Count == 0, "Expected source lane to be empty.");
    Assert(board.Lanes[1].Cards[0].Id == "a", "Expected moved card before target card.");
    Assert(board.Lanes[1].Cards[0].IsComplete, "Expected completion lane to mark card complete.");
}

static void ArchiveCardTrimsArchive()
{
    var board = new KanbanBoard
    {
        Settings = new BoardSettings { MaxArchiveSize = 1 },
        Lanes =
        [
            new KanbanLane
            {
                Id = "todo",
                Cards =
                [
                    new KanbanCard { Id = "a", Title = "A" },
                    new KanbanCard { Id = "b", Title = "B" },
                ],
            },
        ],
    };

    Assert(KanbanBoardOperations.ArchiveCard(board, "a"), "Expected first archive to succeed.");
    Assert(KanbanBoardOperations.ArchiveCard(board, "b"), "Expected second archive to succeed.");
    Assert(board.Archive.Count == 1, "Expected archive to respect max size.");
    Assert(board.Archive[0].Id == "b", "Expected newest archived card to remain.");
}

static void EnsureSwimlanesMigratesLegacyBoards()
{
    var board = new KanbanBoard
    {
        Lanes =
        [
            new KanbanLane
            {
                Cards = [new KanbanCard { Id = "a", Title = "A" }],
            },
        ],
    };

    KanbanBoardMigration.EnsureSwimlanes(board);

    Assert(board.Swimlanes.Count == 1, "Expected a default swimlane.");
    Assert(board.Lanes[0].Cards[0].SwimlaneId == board.Swimlanes[0].Id, "Expected cards to receive swimlane ids.");
}

static void MoveCardUpdatesSwimlane()
{
    var board = new KanbanBoard
    {
        Swimlanes =
        [
            new KanbanSwimlane { Id = "s1", Title = "Team A" },
            new KanbanSwimlane { Id = "s2", Title = "Team B" },
        ],
        Lanes =
        [
            new KanbanLane
            {
                Id = "todo",
                Cards = [new KanbanCard { Id = "a", Title = "A", SwimlaneId = "s1" }],
            },
            new KanbanLane
            {
                Id = "done",
                Cards = [],
            },
        ],
    };

    var moved = KanbanBoardOperations.MoveCard(board, "a", "done", targetSwimlaneId: "s2");

    Assert(moved, "Expected move to succeed.");
    Assert(board.Lanes[1].Cards[0].SwimlaneId == "s2", "Expected swimlane to update.");
}

static void JsonStorageRoundTrips()
{
    var testDirectory = Path.Combine(Path.GetTempPath(), "KanBanTests", Guid.NewGuid().ToString("N"));
    var boardPath = Path.Combine(testDirectory, "default-board.json");

    try
    {
        var storage = new JsonBoardStorage(boardPath);
        var data = new KanBanData
        {
            Board = new KanbanBoard
            {
                Title = "Round trip",
                Swimlanes = [new KanbanSwimlane { Id = "s1", Title = "Default" }],
                Lanes =
                [
                    new KanbanLane
                    {
                        Title = "Lane",
                        Cards = [new KanbanCard { Title = "Card", Description = "#test", SwimlaneId = "s1" }],
                    },
                ],
            },
        };

        storage.Save(data);
        var loaded = storage.LoadOrCreate();

        Assert(loaded.Board.Title == "Round trip", "Expected board title to round-trip.");
        Assert(loaded.Board.Lanes.Count == 1, "Expected lane to round-trip.");
        Assert(loaded.Board.Lanes[0].Cards[0].Description.Contains("#test", StringComparison.Ordinal), "Expected card description to round-trip.");
        Assert(loaded.Board.Swimlanes.Count == 1, "Expected swimlane to round-trip.");
    }
    finally
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
