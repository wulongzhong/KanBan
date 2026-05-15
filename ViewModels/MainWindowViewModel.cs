using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using KanBan.Models;
using KanBan.Services;

namespace KanBan.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly JsonBoardStorage _storage;
    private string _boardTitle = string.Empty;
    private string _searchQuery = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _showArchive;
    private bool _showSettings;
    private BoardViewMode _currentViewMode = BoardViewMode.Board;
    private bool _showCardCheckbox = true;
    private bool _showRelativeDates = true;
    private bool _prependNewCards;
    private string _maxArchiveSizeText = "200";
    private string _dateFormat = "yyyy-MM-dd";

    public MainWindowViewModel()
        : this(new JsonBoardStorage())
    {
    }

    public MainWindowViewModel(JsonBoardStorage storage)
    {
        _storage = storage;
        Lanes = [];
        ArchiveCards = [];
        TableCards = [];

        AddLaneCommand = new RelayCommand(AddLane);
        ToggleArchiveCommand = new RelayCommand(() => ShowArchive = !ShowArchive);
        ToggleSettingsCommand = new RelayCommand(() => ShowSettings = !ShowSettings);
        SetBoardViewCommand = new RelayCommand(() => CurrentViewMode = BoardViewMode.Board);
        SetListViewCommand = new RelayCommand(() => CurrentViewMode = BoardViewMode.List);
        SetTableViewCommand = new RelayCommand(() => CurrentViewMode = BoardViewMode.Table);
        SaveCommand = new RelayCommand(Save);
        NewBoardCommand = new RelayCommand(NewBoard);

        Load();
    }

    public ObservableCollection<LaneViewModel> Lanes { get; }

    public ObservableCollection<CardViewModel> ArchiveCards { get; }

    public ObservableCollection<CardViewModel> TableCards { get; }

    public string BoardTitle
    {
        get => _boardTitle;
        set
        {
            if (SetProperty(ref _boardTitle, value))
            {
                Save();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshFilters();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string BoardFilePath => _storage.BoardPath;

    public bool ShowArchive
    {
        get => _showArchive;
        set => SetProperty(ref _showArchive, value);
    }

    public bool ShowSettings
    {
        get => _showSettings;
        set => SetProperty(ref _showSettings, value);
    }

    public BoardViewMode CurrentViewMode
    {
        get => _currentViewMode;
        set
        {
            if (SetProperty(ref _currentViewMode, value))
            {
                OnPropertyChanged(nameof(IsBoardView));
                OnPropertyChanged(nameof(IsListView));
                OnPropertyChanged(nameof(IsTableView));
                Save();
            }
        }
    }

    public bool IsBoardView => CurrentViewMode == BoardViewMode.Board;

    public bool IsListView => CurrentViewMode == BoardViewMode.List;

    public bool IsTableView => CurrentViewMode == BoardViewMode.Table;

    public bool ShowCardCheckbox
    {
        get => _showCardCheckbox;
        set
        {
            if (SetProperty(ref _showCardCheckbox, value))
            {
                ApplyCardSettings();
                Save();
            }
        }
    }

    public bool ShowRelativeDates
    {
        get => _showRelativeDates;
        set
        {
            if (SetProperty(ref _showRelativeDates, value))
            {
                Save();
            }
        }
    }

    public bool PrependNewCards
    {
        get => _prependNewCards;
        set
        {
            if (SetProperty(ref _prependNewCards, value))
            {
                Save();
            }
        }
    }

    public string MaxArchiveSizeText
    {
        get => _maxArchiveSizeText;
        set
        {
            if (SetProperty(ref _maxArchiveSizeText, value))
            {
                Save();
            }
        }
    }

    public string DateFormat
    {
        get => _dateFormat;
        set
        {
            if (SetProperty(ref _dateFormat, value))
            {
                Save();
            }
        }
    }

    public int TotalCards => Lanes.Sum(lane => lane.Cards.Count);

    public int ArchiveCount => ArchiveCards.Count;

    public RelayCommand AddLaneCommand { get; }

    public RelayCommand ToggleArchiveCommand { get; }

    public RelayCommand ToggleSettingsCommand { get; }

    public RelayCommand SetBoardViewCommand { get; }

    public RelayCommand SetListViewCommand { get; }

    public RelayCommand SetTableViewCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand NewBoardCommand { get; }

    public void MoveCardBefore(string cardId, string targetCardId)
    {
        var card = FindCard(cardId);
        var targetCard = FindCard(targetCardId);

        if (card is null || targetCard is null || card == targetCard)
        {
            return;
        }

        var targetLane = FindLaneContaining(targetCard);
        if (targetLane is null)
        {
            return;
        }

        MoveCardToLane(card, targetLane, targetLane.IndexOf(targetCard));
    }

    public void MoveCardToLane(string cardId, string laneId)
    {
        var card = FindCard(cardId);
        var targetLane = Lanes.FirstOrDefault(lane => lane.Id == laneId);

        if (card is null || targetLane is null)
        {
            return;
        }

        MoveCardToLane(card, targetLane, targetLane.Cards.Count);
    }

    public void MoveLaneBefore(string laneId, string beforeLaneId)
    {
        if (laneId == beforeLaneId)
        {
            return;
        }

        var lane = Lanes.FirstOrDefault(existingLane => existingLane.Id == laneId);
        var targetLane = Lanes.FirstOrDefault(existingLane => existingLane.Id == beforeLaneId);

        if (lane is null || targetLane is null)
        {
            return;
        }

        var oldIndex = Lanes.IndexOf(lane);
        var newIndex = Lanes.IndexOf(targetLane);
        Lanes.RemoveAt(oldIndex);

        if (oldIndex < newIndex)
        {
            newIndex--;
        }

        Lanes.Insert(newIndex, lane);
        SaveAndRefresh("Lane moved.");
    }

    private void Load()
    {
        var data = _storage.LoadOrCreate();
        Hydrate(data.Board);
        StatusMessage = $"Loaded {BoardFilePath}";
    }

    private void Hydrate(KanbanBoard board)
    {
        Lanes.Clear();
        ArchiveCards.Clear();

        _boardTitle = board.Title;
        _currentViewMode = board.ViewMode;
        _showCardCheckbox = board.Settings.ShowCardCheckbox;
        _showRelativeDates = board.Settings.ShowRelativeDates;
        _prependNewCards = board.Settings.PrependNewCards;
        _maxArchiveSizeText = board.Settings.MaxArchiveSize.ToString();
        _dateFormat = board.Settings.DateFormat;

        foreach (var lane in board.Lanes)
        {
            Lanes.Add(CreateLane(lane));
        }

        foreach (var card in board.Archive)
        {
            ArchiveCards.Add(CreateCard(card, isArchived: true));
        }

        ApplyCardSettings();
        RefreshFilters();
        NotifyBoardProperties();
    }

    private LaneViewModel CreateLane(KanbanLane lane)
    {
        return new LaneViewModel(
            lane,
            card => CreateCard(card, isArchived: false),
            _ => SaveAndRefresh("Lane updated."),
            AddCard,
            DeleteLane,
            MoveLane,
            SortLane);
    }

    private CardViewModel CreateCard(KanbanCard card, bool isArchived)
    {
        if (isArchived && card.ArchivedAt is null)
        {
            card.ArchivedAt = DateTimeOffset.UtcNow;
        }

        var cardViewModel = new CardViewModel(
            card,
            _ => SaveAndRefresh("Card updated."),
            ArchiveCard,
            DeleteCard,
            RestoreCard,
            MoveCard);

        cardViewModel.ShowCheckbox = ShowCardCheckbox;
        return cardViewModel;
    }

    private void AddLane()
    {
        var lane = CreateLane(new KanbanLane { Title = $"Lane {Lanes.Count + 1}" });
        Lanes.Add(lane);
        SaveAndRefresh("Lane added.");
    }

    private void DeleteLane(LaneViewModel lane)
    {
        if (lane.Cards.Count > 0)
        {
            foreach (var card in lane.Cards.ToList())
            {
                lane.RemoveCard(card);
                card.ArchivedAt = DateTimeOffset.UtcNow;
                ArchiveCards.Add(card);
            }
        }

        Lanes.Remove(lane);
        SaveAndRefresh("Lane deleted. Cards moved to archive.");
    }

    private void MoveLane(LaneViewModel lane, int offset)
    {
        var oldIndex = Lanes.IndexOf(lane);
        var newIndex = oldIndex + offset;

        if (oldIndex < 0 || newIndex < 0 || newIndex >= Lanes.Count)
        {
            return;
        }

        Lanes.Move(oldIndex, newIndex);
        SaveAndRefresh("Lane moved.");
    }

    private void AddCard(LaneViewModel lane, string title, string description)
    {
        var card = CreateCard(
            new KanbanCard
            {
                Title = string.IsNullOrWhiteSpace(title) ? "New card" : title.Trim(),
                Description = description.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            isArchived: false);

        lane.AddCard(card, PrependNewCards ? 0 : null);
        SaveAndRefresh("Card added.");
    }

    private void ArchiveCard(CardViewModel card)
    {
        var lane = FindLaneContaining(card);
        if (lane is null)
        {
            return;
        }

        lane.RemoveCard(card);
        card.ArchivedAt = DateTimeOffset.UtcNow;
        ArchiveCards.Add(card);
        TrimArchive();
        SaveAndRefresh("Card archived.");
    }

    private void RestoreCard(CardViewModel card)
    {
        var targetLane = Lanes.FirstOrDefault();
        if (targetLane is null)
        {
            targetLane = CreateLane(new KanbanLane { Title = "Restored" });
            Lanes.Add(targetLane);
        }

        if (ArchiveCards.Remove(card))
        {
            card.ArchivedAt = null;
            targetLane.AddCard(card);
            SaveAndRefresh("Card restored.");
        }
    }

    private void DeleteCard(CardViewModel card)
    {
        var lane = FindLaneContaining(card);
        if (lane is not null)
        {
            lane.RemoveCard(card);
        }
        else
        {
            ArchiveCards.Remove(card);
        }

        SaveAndRefresh("Card deleted.");
    }

    private void MoveCard(CardViewModel card, int offset)
    {
        var lane = FindLaneContaining(card);
        if (lane is null)
        {
            return;
        }

        var oldIndex = lane.IndexOf(card);
        var newIndex = oldIndex + offset;

        if (oldIndex < 0 || newIndex < 0 || newIndex >= lane.Cards.Count)
        {
            return;
        }

        lane.Cards.Move(oldIndex, newIndex);
        lane.RefreshFilter(SearchQuery);
        SaveAndRefresh("Card moved.");
    }

    private void MoveCardToLane(CardViewModel card, LaneViewModel targetLane, int index)
    {
        var sourceLane = FindLaneContaining(card);
        if (sourceLane is null)
        {
            return;
        }

        sourceLane.RemoveCard(card);

        if (targetLane.ShouldMarkItemsComplete)
        {
            card.IsComplete = true;
        }

        var targetIndex = Math.Clamp(index, 0, targetLane.Cards.Count);
        targetLane.AddCard(card, targetIndex);
        SaveAndRefresh("Card moved.");
    }

    private void SortLane(LaneViewModel lane)
    {
        if (lane.Sort == LaneSort.Manual)
        {
            SaveAndRefresh("Manual order enabled.");
            return;
        }

        var sortedCards = lane.Sort switch
        {
            LaneSort.TitleAsc => lane.Cards.OrderBy(card => card.Title, StringComparer.OrdinalIgnoreCase),
            LaneSort.TitleDesc => lane.Cards.OrderByDescending(card => card.Title, StringComparer.OrdinalIgnoreCase),
            LaneSort.DateAsc => lane.Cards.OrderBy(card => card.DueDate),
            LaneSort.DateDesc => lane.Cards.OrderByDescending(card => card.DueDate),
            LaneSort.TagsAsc => lane.Cards.OrderBy(card => CardTagHelper.GetSortKey(card.Description), StringComparer.OrdinalIgnoreCase),
            LaneSort.TagsDesc => lane.Cards.OrderByDescending(card => CardTagHelper.GetSortKey(card.Description), StringComparer.OrdinalIgnoreCase),
            _ => lane.Cards.AsEnumerable(),
        };

        var ordered = sortedCards.ToList();
        lane.Cards.Clear();
        foreach (var card in ordered)
        {
            lane.Cards.Add(card);
        }

        lane.RefreshFilter(SearchQuery);
        SaveAndRefresh("Lane sorted.");
    }

    private void SaveAndRefresh(string message)
    {
        RefreshFilters();
        Save(message);
    }

    private void Save()
    {
        Save("Saved.");
    }

    private void Save(string message)
    {
        try
        {
            _storage.Save(ToData());
            StatusMessage = $"{message} {DateTimeOffset.Now:t}";
            NotifyBoardProperties();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private void NewBoard()
    {
        Hydrate(KanbanBoard.CreateDefault());
        SaveAndRefresh("New board created.");
    }

    private KanBanData ToData()
    {
        var maxArchiveSize = int.TryParse(MaxArchiveSizeText, out var parsedMaxArchiveSize)
            ? parsedMaxArchiveSize
            : 200;

        var board = new KanbanBoard
        {
            Title = string.IsNullOrWhiteSpace(BoardTitle) ? "KanBan" : BoardTitle.Trim(),
            ViewMode = CurrentViewMode,
            Settings = new BoardSettings
            {
                ShowCardCheckbox = ShowCardCheckbox,
                ShowRelativeDates = ShowRelativeDates,
                PrependNewCards = PrependNewCards,
                MaxArchiveSize = maxArchiveSize,
                DateFormat = string.IsNullOrWhiteSpace(DateFormat) ? "yyyy-MM-dd" : DateFormat.Trim(),
            },
            Lanes = Lanes.Select(lane => lane.ToModel()).ToList(),
            Archive = ArchiveCards.Select(card => card.ToModel()).ToList(),
        };

        KanbanBoardOperations.TrimArchive(board);
        return new KanBanData { Board = board };
    }

    private void RefreshFilters()
    {
        foreach (var lane in Lanes)
        {
            lane.RefreshFilter(SearchQuery);
        }

        TableCards.Clear();
        foreach (var card in Lanes.SelectMany(lane => lane.Cards).Where(card => card.Matches(SearchQuery)))
        {
            TableCards.Add(card);
        }

        NotifyBoardProperties();
    }

    private void ApplyCardSettings()
    {
        foreach (var card in Lanes.SelectMany(lane => lane.Cards).Concat(ArchiveCards))
        {
            card.ShowCheckbox = ShowCardCheckbox;
        }
    }

    private void TrimArchive()
    {
        var maxArchiveSize = int.TryParse(MaxArchiveSizeText, out var parsed) ? parsed : 200;
        if (maxArchiveSize < 0)
        {
            return;
        }

        while (ArchiveCards.Count > maxArchiveSize)
        {
            ArchiveCards.RemoveAt(0);
        }
    }

    private CardViewModel? FindCard(string cardId)
    {
        return Lanes.SelectMany(lane => lane.Cards).FirstOrDefault(card => card.Id == cardId);
    }

    private LaneViewModel? FindLaneContaining(CardViewModel card)
    {
        return Lanes.FirstOrDefault(lane => lane.Cards.Contains(card));
    }

    private void NotifyBoardProperties()
    {
        OnPropertyChanged(nameof(BoardTitle));
        OnPropertyChanged(nameof(CurrentViewMode));
        OnPropertyChanged(nameof(IsBoardView));
        OnPropertyChanged(nameof(IsListView));
        OnPropertyChanged(nameof(IsTableView));
        OnPropertyChanged(nameof(ShowCardCheckbox));
        OnPropertyChanged(nameof(ShowRelativeDates));
        OnPropertyChanged(nameof(PrependNewCards));
        OnPropertyChanged(nameof(MaxArchiveSizeText));
        OnPropertyChanged(nameof(DateFormat));
        OnPropertyChanged(nameof(TotalCards));
        OnPropertyChanged(nameof(ArchiveCount));
    }
}
