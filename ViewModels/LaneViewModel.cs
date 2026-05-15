using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using KanBan.Models;

namespace KanBan.ViewModels;

public sealed class LaneViewModel : ViewModelBase
{
    private readonly Action<LaneViewModel>? _onChanged;
    private readonly Action<LaneViewModel, string, string>? _onAddCard;
    private readonly Action<LaneViewModel>? _onDelete;
    private readonly Action<LaneViewModel, int>? _onMove;
    private readonly Action<LaneViewModel>? _onSort;
    private string _title;
    private string _maxItemsText;
    private LaneSort _sort;
    private bool _shouldMarkItemsComplete;
    private string _activeQuery = string.Empty;
    private bool _isEditing;
    private bool _isAddingCard;
    private string _newCardTitle = string.Empty;
    private string _newCardDetails = string.Empty;
    private int? _aggregateCardCount;

    public LaneViewModel(
        KanbanLane lane,
        Func<KanbanCard, CardViewModel> cardFactory,
        Action<LaneViewModel>? onChanged = null,
        Action<LaneViewModel, string, string>? onAddCard = null,
        Action<LaneViewModel>? onDelete = null,
        Action<LaneViewModel, int>? onMove = null,
        Action<LaneViewModel>? onSort = null,
        string? swimlaneId = null,
        bool isColumnHeader = false)
    {
        Id = lane.Id;
        SwimlaneId = swimlaneId;
        IsColumnHeader = isColumnHeader;
        _title = lane.Title;
        _maxItemsText = lane.MaxItems?.ToString() ?? string.Empty;
        _sort = lane.Sort;
        _shouldMarkItemsComplete = lane.ShouldMarkItemsComplete;
        _onChanged = onChanged;
        _onAddCard = onAddCard;
        _onDelete = onDelete;
        _onMove = onMove;
        _onSort = onSort;

        var laneCards = isColumnHeader
            ? []
            : lane.Cards.Where(card => card.SwimlaneId == swimlaneId);

        Cards = new ObservableCollection<CardViewModel>(laneCards.Select(cardFactory));
        FilteredCards = new ObservableCollection<CardViewModel>(Cards);

        AddCardCommand = new RelayCommand(BeginAddCard);
        CommitAddCardCommand = new RelayCommand(CommitAddCard);
        CancelAddCardCommand = new RelayCommand(CancelAddCard);
        BeginEditCommand = new RelayCommand(BeginEdit);
        EndEditCommand = new RelayCommand(EndEdit);
        DeleteLaneCommand = new RelayCommand(() => _onDelete?.Invoke(this));
        MoveLeftCommand = new RelayCommand(() => _onMove?.Invoke(this, -1));
        MoveRightCommand = new RelayCommand(() => _onMove?.Invoke(this, 1));
        SortCommand = new RelayCommand(() => _onSort?.Invoke(this));
    }

    public string Id { get; }

    public string? SwimlaneId { get; }

    public bool IsColumnHeader { get; }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                NotifyChanged();
            }
        }
    }

    public string MaxItemsText
    {
        get => _maxItemsText;
        set
        {
            if (SetProperty(ref _maxItemsText, value))
            {
                OnPropertyChanged(nameof(MaxItems));
                OnPropertyChanged(nameof(WipText));
                OnPropertyChanged(nameof(IsOverLimit));
                NotifyChanged();
            }
        }
    }

    public int? MaxItems => int.TryParse(MaxItemsText, out var parsed) && parsed > 0 ? parsed : null;

    public LaneSort Sort
    {
        get => _sort;
        set
        {
            if (SetProperty(ref _sort, value))
            {
                _onSort?.Invoke(this);
                NotifyChanged();
            }
        }
    }

    public bool ShouldMarkItemsComplete
    {
        get => _shouldMarkItemsComplete;
        set
        {
            if (SetProperty(ref _shouldMarkItemsComplete, value))
            {
                NotifyChanged();
            }
        }
    }

    public ObservableCollection<CardViewModel> Cards { get; }

    public ObservableCollection<CardViewModel> FilteredCards { get; }

    public Array SortModes { get; } = Enum.GetValues(typeof(LaneSort));

    public int DisplayCardCount => IsColumnHeader && _aggregateCardCount is int aggregateCount
        ? aggregateCount
        : Cards.Count;

    public string CountText => $"{DisplayCardCount} cards";

    public string WipText => MaxItems is { } maxItems ? $"{DisplayCardCount}/{maxItems}" : DisplayCardCount.ToString();

    public bool IsOverLimit => MaxItems is { } maxItems && DisplayCardCount > maxItems;

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(IsDisplayMode));
            }
        }
    }

    public bool IsDisplayMode => !IsEditing;

    public bool IsAddingCard
    {
        get => _isAddingCard;
        set
        {
            if (SetProperty(ref _isAddingCard, value))
            {
                OnPropertyChanged(nameof(IsAddCardButtonVisible));
            }
        }
    }

    public bool IsAddCardButtonVisible => !IsAddingCard;

    public string NewCardTitle
    {
        get => _newCardTitle;
        set => SetProperty(ref _newCardTitle, value);
    }

    public string NewCardDetails
    {
        get => _newCardDetails;
        set => SetProperty(ref _newCardDetails, value);
    }

    public RelayCommand AddCardCommand { get; }

    public RelayCommand CommitAddCardCommand { get; }

    public RelayCommand CancelAddCardCommand { get; }

    public RelayCommand BeginEditCommand { get; }

    public RelayCommand EndEditCommand { get; }

    public RelayCommand DeleteLaneCommand { get; }

    public RelayCommand MoveLeftCommand { get; }

    public RelayCommand MoveRightCommand { get; }

    public RelayCommand SortCommand { get; }

    public KanbanLane ToModel()
    {
        return new KanbanLane
        {
            Id = Id,
            Title = string.IsNullOrWhiteSpace(Title) ? "Untitled lane" : Title.Trim(),
            MaxItems = MaxItems,
            Sort = Sort,
            ShouldMarkItemsComplete = ShouldMarkItemsComplete,
            Cards = Cards.Select(card =>
            {
                var model = card.ToModel();
                model.SwimlaneId = SwimlaneId;
                return model;
            }).ToList(),
        };
    }

    public KanbanLane ToMetadataModel()
    {
        return new KanbanLane
        {
            Id = Id,
            Title = string.IsNullOrWhiteSpace(Title) ? "Untitled lane" : Title.Trim(),
            MaxItems = MaxItems,
            Sort = Sort,
            ShouldMarkItemsComplete = ShouldMarkItemsComplete,
        };
    }

    public void RefreshFilter(string query)
    {
        _activeQuery = query;
        var matching = Cards.Where(card => card.Matches(query)).ToList();

        if (FilteredCards.Count == matching.Count)
        {
            var unchanged = true;
            for (var i = 0; i < matching.Count; i++)
            {
                if (!ReferenceEquals(FilteredCards[i], matching[i]))
                {
                    unchanged = false;
                    break;
                }
            }

            if (unchanged)
            {
                NotifyCounts();
                return;
            }
        }

        FilteredCards.Clear();

        foreach (var card in matching)
        {
            FilteredCards.Add(card);
        }

        NotifyCounts();
    }

    public void AddCard(CardViewModel card, int? index = null)
    {
        if (index is { } cardIndex && cardIndex >= 0 && cardIndex <= Cards.Count)
        {
            Cards.Insert(cardIndex, card);
        }
        else
        {
            Cards.Add(card);
        }

        RefreshFilter(_activeQuery);
    }

    public bool RemoveCard(CardViewModel card)
    {
        var removed = Cards.Remove(card);
        if (removed)
        {
            RefreshFilter(_activeQuery);
        }

        return removed;
    }

    public int IndexOf(CardViewModel card)
    {
        return Cards.IndexOf(card);
    }

    public void SetAggregateCardCount(int count)
    {
        if (!IsColumnHeader)
        {
            return;
        }

        _aggregateCardCount = count;
        NotifyCounts();
    }

    public void NotifyCounts()
    {
        OnPropertyChanged(nameof(DisplayCardCount));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(WipText));
        OnPropertyChanged(nameof(IsOverLimit));
    }

    private void NotifyChanged()
    {
        NotifyCounts();
        _onChanged?.Invoke(this);
    }

    private void BeginAddCard()
    {
        NewCardTitle = string.Empty;
        NewCardDetails = string.Empty;
        IsAddingCard = true;
    }

    private void CommitAddCard()
    {
        if (string.IsNullOrWhiteSpace(NewCardTitle) && string.IsNullOrWhiteSpace(NewCardDetails))
        {
            IsAddingCard = false;
            return;
        }

        _onAddCard?.Invoke(this, NewCardTitle, NewCardDetails);
        NewCardTitle = string.Empty;
        NewCardDetails = string.Empty;
        IsAddingCard = false;
    }

    private void CancelAddCard()
    {
        NewCardTitle = string.Empty;
        NewCardDetails = string.Empty;
        IsAddingCard = false;
    }

    public void BeginEdit()
    {
        IsEditing = true;
    }

    public void EndEdit()
    {
        IsEditing = false;
        NotifyChanged();
    }
}
