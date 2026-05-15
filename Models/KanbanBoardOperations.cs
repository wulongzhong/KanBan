using System;
using System.Linq;

namespace KanBan.Models;

public static class KanbanBoardOperations
{
    public static bool MoveCard(
        KanbanBoard board,
        string cardId,
        string targetLaneId,
        string? beforeCardId = null,
        string? targetSwimlaneId = null)
    {
        KanbanBoardMigration.EnsureSwimlanes(board);

        var sourceLane = board.Lanes.FirstOrDefault(lane => lane.Cards.Any(card => card.Id == cardId));
        var targetLane = board.Lanes.FirstOrDefault(lane => lane.Id == targetLaneId);

        if (sourceLane is null || targetLane is null)
        {
            return false;
        }

        var card = sourceLane.Cards.First(card => card.Id == cardId);
        sourceLane.Cards.Remove(card);

        var targetIndex = beforeCardId is null
            ? targetLane.Cards.Count
            : targetLane.Cards.FindIndex(existingCard => existingCard.Id == beforeCardId);

        if (targetIndex < 0)
        {
            targetIndex = targetLane.Cards.Count;
        }

        if (targetSwimlaneId is not null)
        {
            card.SwimlaneId = targetSwimlaneId;
        }

        if (targetLane.ShouldMarkItemsComplete)
        {
            card.IsComplete = true;
        }

        card.UpdatedAt = DateTimeOffset.UtcNow;
        targetLane.Cards.Insert(targetIndex, card);
        board.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public static bool MoveLane(KanbanBoard board, string laneId, string beforeLaneId)
    {
        if (laneId == beforeLaneId)
        {
            return false;
        }

        var lane = board.Lanes.FirstOrDefault(existingLane => existingLane.Id == laneId);
        var targetIndex = board.Lanes.FindIndex(existingLane => existingLane.Id == beforeLaneId);

        if (lane is null || targetIndex < 0)
        {
            return false;
        }

        var currentIndex = board.Lanes.IndexOf(lane);
        board.Lanes.RemoveAt(currentIndex);

        if (currentIndex < targetIndex)
        {
            targetIndex--;
        }

        board.Lanes.Insert(targetIndex, lane);
        board.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public static bool ArchiveCard(KanbanBoard board, string cardId)
    {
        var sourceLane = board.Lanes.FirstOrDefault(lane => lane.Cards.Any(card => card.Id == cardId));
        if (sourceLane is null)
        {
            return false;
        }

        var card = sourceLane.Cards.First(card => card.Id == cardId);
        sourceLane.Cards.Remove(card);
        card.ArchivedAt = DateTimeOffset.UtcNow;
        card.UpdatedAt = DateTimeOffset.UtcNow;
        board.Archive.Add(card);
        TrimArchive(board);
        board.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public static bool RestoreCard(KanbanBoard board, string cardId, string targetLaneId)
    {
        var targetLane = board.Lanes.FirstOrDefault(lane => lane.Id == targetLaneId);
        var card = board.Archive.FirstOrDefault(existingCard => existingCard.Id == cardId);

        if (targetLane is null || card is null)
        {
            return false;
        }

        board.Archive.Remove(card);
        card.ArchivedAt = null;
        card.UpdatedAt = DateTimeOffset.UtcNow;
        targetLane.Cards.Add(card);
        board.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public static void SortLane(KanbanLane lane)
    {
        Comparison<KanbanCard> comparison = lane.Sort switch
        {
            LaneSort.TitleAsc => (left, right) => string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase),
            LaneSort.TitleDesc => (left, right) => string.Compare(right.Title, left.Title, StringComparison.OrdinalIgnoreCase),
            LaneSort.DateAsc => CompareDatesAscending,
            LaneSort.DateDesc => (left, right) => CompareDatesAscending(right, left),
            LaneSort.TagsAsc => (left, right) => string.Compare(CardTagHelper.GetSortKey(left.Description), CardTagHelper.GetSortKey(right.Description), StringComparison.OrdinalIgnoreCase),
            LaneSort.TagsDesc => (left, right) => string.Compare(CardTagHelper.GetSortKey(right.Description), CardTagHelper.GetSortKey(left.Description), StringComparison.OrdinalIgnoreCase),
            _ => (_, _) => 0,
        };

        if (lane.Sort == LaneSort.Manual)
        {
            return;
        }

        lane.Cards.Sort(comparison);
    }

    public static void TrimArchive(KanbanBoard board)
    {
        var maxArchiveSize = board.Settings.MaxArchiveSize;
        if (maxArchiveSize < 0 || board.Archive.Count <= maxArchiveSize)
        {
            return;
        }

        board.Archive = board.Archive
            .OrderByDescending(card => card.ArchivedAt ?? card.UpdatedAt)
            .Take(maxArchiveSize)
            .OrderBy(card => card.ArchivedAt ?? card.UpdatedAt)
            .ToList();
    }

    private static int CompareDatesAscending(KanbanCard left, KanbanCard right)
    {
        return Nullable.Compare(left.DueDate, right.DueDate);
    }
}
