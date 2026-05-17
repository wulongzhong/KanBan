using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using KanBan.Models;
using KanBan.Services;
using KanBan.Services.Localization;
using KanBan.Views;

namespace KanBan.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppPreferences _preferences;
    private JsonBoardStorage? _storage;
    private CardAttachmentService? _attachments;
    private Window? _ownerWindow;
    private string _boardTitle = string.Empty;
    private bool _isBoardTitleEditing;
    private string _searchQuery = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _showArchive;
    private bool _showSettings;
    private bool _showRelativeDates = true;
    private bool _prependNewCards;
    private string _maxArchiveSizeText = "200";
    private string _dateFormat = "yyyy-MM-dd";
    private LanguageOption? _selectedLanguage;

    public MainWindowViewModel()
        : this(AppPreferences.Load())
    {
    }

    public MainWindowViewModel(AppPreferences preferences)
    {
        _preferences = preferences;
        ColumnLanes = [];
        Swimlanes = [];
        ArchiveCards = [];

        AddLaneCommand = new RelayCommand(AddLane);
        AddSwimlaneCommand = new RelayCommand(AddSwimlane);
        ToggleArchiveCommand = new RelayCommand(() => ShowArchive = !ShowArchive);
        ToggleSettingsCommand = new RelayCommand(() => ShowSettings = !ShowSettings);
        SaveCommand = new RelayCommand(Save);
        NewBoardCommand = new RelayCommand(NewBoard);
        SelectWorkspaceFolderCommand = new AsyncRelayCommand(SelectWorkspaceFolderAsync);

        RefreshLanguageOptions();
        _selectedLanguage = AvailableLanguages.First(option =>
            option.CultureName == LocalizationService.Instance.CultureName);

        SubscribeLocalization(RefreshLocalizedProperties);

        if (TryInitializeStorage())
        {
            Load();
        }

        RefreshWorkspaceProperties();
    }

    public void SetOwnerWindow(Window window) => _ownerWindow = window;

    public ObservableCollection<LaneViewModel> ColumnLanes { get; }

    public ObservableCollection<SwimlaneViewModel> Swimlanes { get; }

    public ObservableCollection<CardViewModel> ArchiveCards { get; }

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

    public bool IsBoardTitleEditing
    {
        get => _isBoardTitleEditing;
        private set
        {
            if (SetProperty(ref _isBoardTitleEditing, value))
            {
                OnPropertyChanged(nameof(IsBoardTitleDisplayMode));
            }
        }
    }

    public bool IsBoardTitleDisplayMode => !IsBoardTitleEditing;

    public void BeginBoardTitleEdit() => IsBoardTitleEditing = true;

    public void EndBoardTitleEdit()
    {
        if (!IsBoardTitleEditing)
        {
            return;
        }

        CommitBoardTitle();
        IsBoardTitleEditing = false;
    }

    public void CommitBoardTitle()
    {
        var trimmed = string.IsNullOrWhiteSpace(BoardTitle) ? "KanBan" : BoardTitle.Trim();
        if (trimmed != BoardTitle)
        {
            BoardTitle = trimmed;
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

    public bool IsWorkspaceReady => _storage is not null;

    public string BoardFilePath => IsWorkspaceReady
        ? _storage!.BoardPath
        : LocalizationService.Get(UiKeys.WorkspaceNotConfigured);

    public string WorkspaceFolderDisplay =>
        IsWorkspaceReady
            ? _preferences.WorkspaceFolder!
            : LocalizationService.Get(UiKeys.WorkspaceNotSet);

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; private set; } = [];

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || !SetProperty(ref _selectedLanguage, value))
            {
                return;
            }

            var culture = LocalizationService.NormalizeCulture(value.CultureName);
            if (string.Equals(_preferences.UiLanguage, culture, StringComparison.Ordinal)
                && LocalizationService.Instance.CultureName == culture)
            {
                return;
            }

            _preferences.UiLanguage = culture;
            _preferences.Save();
            LocalizationService.Instance.ApplyCulture(culture);
        }
    }

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

    public int SwimlaneCount => Swimlanes.Count;

    public int TotalCards => Swimlanes
        .SelectMany(swimlane => swimlane.Lanes)
        .SelectMany(lane => lane.Cards)
        .DistinctBy(card => card.Id)
        .Count();

    public int ArchiveCount => ArchiveCards.Count;

    public string TotalCardsText => LocalizationService.Format(UiKeys.ToolbarCardCount, TotalCards);

    public string ArchiveCountText => LocalizationService.Format("Archive.Count", ArchiveCount);

    public RelayCommand AddLaneCommand { get; }

    public RelayCommand AddSwimlaneCommand { get; }

    public RelayCommand ToggleArchiveCommand { get; }

    public RelayCommand ToggleSettingsCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand NewBoardCommand { get; }

    public IAsyncRelayCommand SelectWorkspaceFolderCommand { get; }

    public async Task<bool> EnsureWorkspaceConfiguredAsync()
    {
        if (IsWorkspaceReady)
        {
            return true;
        }

        if (_ownerWindow is null)
        {
            return false;
        }

        while (!IsWorkspaceReady)
        {
            var prompt = new WorkspacePromptWindow();
            await prompt.ShowDialog(_ownerWindow);

            if (prompt.Result == WorkspacePromptResult.Exit)
            {
                return false;
            }

            await SelectWorkspaceFolderAsync();
        }

        return true;
    }

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

    public void MoveCardToLane(string cardId, string laneId, string? swimlaneId = null)
    {
        var card = FindCard(cardId);
        var targetLane = FindLane(laneId, swimlaneId);

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

        var lane = ColumnLanes.FirstOrDefault(existingLane => existingLane.Id == laneId);
        var targetLane = ColumnLanes.FirstOrDefault(existingLane => existingLane.Id == beforeLaneId);

        if (lane is null || targetLane is null)
        {
            return;
        }

        var oldIndex = ColumnLanes.IndexOf(lane);
        var newIndex = ColumnLanes.IndexOf(targetLane);
        ColumnLanes.RemoveAt(oldIndex);

        if (oldIndex < newIndex)
        {
            newIndex--;
        }

        ColumnLanes.Insert(newIndex, lane);
        ReorderSwimlaneLanes(laneId, beforeLaneId);
        RefreshColumnSeparators();
        SaveAndRefresh(UiKeys.StatusLaneMoved);
    }

    private bool TryInitializeStorage()
    {
        if (string.IsNullOrWhiteSpace(_preferences.WorkspaceFolder))
        {
            return false;
        }

        _storage = new JsonBoardStorage(JsonBoardStorage.GetBoardPath(_preferences.WorkspaceFolder));
        _attachments = new CardAttachmentService(_storage);
        return true;
    }

    private void Load()
    {
        if (!IsWorkspaceReady)
        {
            return;
        }

        var data = _storage!.LoadOrCreate();
        Hydrate(data.Board);
        StatusMessage = LocalizationService.Format(UiKeys.StatusLoaded, BoardFilePath);
    }

    private void Hydrate(KanbanBoard board)
    {
        ColumnLanes.Clear();
        Swimlanes.Clear();
        ArchiveCards.Clear();

        KanbanBoardMigration.EnsureSwimlanes(board);

        _boardTitle = board.Title;
        _showRelativeDates = board.Settings.ShowRelativeDates;
        _prependNewCards = board.Settings.PrependNewCards;
        _maxArchiveSizeText = board.Settings.MaxArchiveSize.ToString();
        _dateFormat = board.Settings.DateFormat;

        foreach (var lane in board.Lanes)
        {
            ColumnLanes.Add(CreateColumnHeader(lane));
        }

        foreach (var swimlane in board.Swimlanes)
        {
            Swimlanes.Add(CreateSwimlane(swimlane, board.Lanes));
        }

        RefreshColumnSeparators();

        foreach (var card in board.Archive)
        {
            ArchiveCards.Add(CreateCard(card, isArchived: true));
        }

        RefreshFilters();
        NotifyBoardProperties();
    }

    private SwimlaneViewModel CreateSwimlane(KanbanSwimlane swimlane, IReadOnlyList<KanbanLane> boardLanes)
    {
        var swimlaneViewModel = new SwimlaneViewModel(
            swimlane,
            _ => SaveAndRefresh(UiKeys.StatusSwimlaneUpdated),
            DeleteSwimlane,
            MoveSwimlane);

        foreach (var lane in boardLanes)
        {
            swimlaneViewModel.Lanes.Add(CreateLane(lane, swimlane.Id));
        }

        return swimlaneViewModel;
    }

    private LaneViewModel CreateColumnHeader(KanbanLane lane)
    {
        return new LaneViewModel(
            lane,
            card => CreateCard(card, isArchived: false),
            OnColumnHeaderChanged,
            onAddCard: null,
            onDelete: DeleteLane,
            onMove: MoveLane,
            onSort: SortLane,
            swimlaneId: null,
            isColumnHeader: true);
    }

    private LaneViewModel CreateLane(KanbanLane lane, string swimlaneId)
    {
        var laneViewModel = new LaneViewModel(
            lane,
            card => CreateCard(card, isArchived: false),
            _ => SaveAndRefresh(UiKeys.StatusLaneUpdated),
            AddCard,
            DiscardNewCardDraft,
            DeleteLane,
            MoveLane,
            SortLane,
            swimlaneId: swimlaneId,
            isColumnHeader: false);

        var header = ColumnLanes.FirstOrDefault(column => column.Id == lane.Id);
        if (header is not null)
        {
            laneViewModel.IsCollapsed = header.IsCollapsed;
        }

        return laneViewModel;
    }

    private void OnColumnHeaderChanged(LaneViewModel headerLane)
    {
        foreach (var swimlane in Swimlanes)
        {
            var lane = swimlane.Lanes.FirstOrDefault(existingLane => existingLane.Id == headerLane.Id);
            if (lane is null)
            {
                continue;
            }

            lane.Title = headerLane.Title;
            lane.MaxItemsText = headerLane.MaxItemsText;
            lane.Sort = headerLane.Sort;
            lane.ShouldMarkItemsComplete = headerLane.ShouldMarkItemsComplete;
            lane.IsCollapsed = headerLane.IsCollapsed;
        }

        SaveAndRefresh(UiKeys.StatusLaneUpdated);
    }

    private CardViewModel CreateCard(KanbanCard card, bool isArchived)
    {
        if (isArchived && card.ArchivedAt is null)
        {
            card.ArchivedAt = DateTimeOffset.UtcNow;
        }

        var cardViewModel = new CardViewModel(
            card,
            OnCardContentChanged,
            ArchiveCard,
            DeleteCard,
            RestoreCard,
            MoveCard);

        cardViewModel.LoadPreviewImages(_attachments!);
        return cardViewModel;
    }

    public CardAttachmentService Attachments =>
        _attachments ?? throw new InvalidOperationException("Workspace is not configured.");

    private void AddLane()
    {
        var laneModel = new KanbanLane
        {
            Title = LocalizationService.Format(UiKeys.LaneDefaultTitle, ColumnLanes.Count + 1),
        };
        ColumnLanes.Add(CreateColumnHeader(laneModel));

        foreach (var swimlane in Swimlanes)
        {
            swimlane.Lanes.Add(CreateLane(laneModel, swimlane.Id));
        }

        RefreshColumnSeparators();
        SaveAndRefresh(UiKeys.StatusLaneAdded);
    }

    private void AddSwimlane()
    {
        var swimlane = new KanbanSwimlane
        {
            Title = LocalizationService.Format(UiKeys.SwimlaneDefaultTitle, Swimlanes.Count + 1),
        };
        var swimlaneViewModel = CreateSwimlane(swimlane, ColumnLanes.Select(column => column.ToMetadataModel()).ToList());
        Swimlanes.Add(swimlaneViewModel);
        SaveAndRefresh(UiKeys.StatusSwimlaneAdded);
    }

    private void DeleteSwimlane(SwimlaneViewModel swimlane)
    {
        if (Swimlanes.Count <= 1)
        {
            return;
        }

        var fallback = Swimlanes.First(sw => sw.Id != swimlane.Id);
        foreach (var lane in swimlane.Lanes)
        {
            foreach (var card in lane.Cards.ToList())
            {
                lane.RemoveCard(card);
                card.SwimlaneId = fallback.Id;
                var targetLane = fallback.Lanes.FirstOrDefault(existingLane => existingLane.Id == lane.Id)
                    ?? fallback.Lanes.First();
                targetLane.AddCard(card);
            }
        }

        Swimlanes.Remove(swimlane);
        SaveAndRefresh(UiKeys.StatusSwimlaneDeleted);
    }

    private void MoveSwimlane(SwimlaneViewModel swimlane, int offset)
    {
        var oldIndex = Swimlanes.IndexOf(swimlane);
        var newIndex = oldIndex + offset;

        if (oldIndex < 0 || newIndex < 0 || newIndex >= Swimlanes.Count)
        {
            return;
        }

        Swimlanes.Move(oldIndex, newIndex);
        SaveAndRefresh(UiKeys.StatusSwimlaneMoved);
    }

    private void DeleteLane(LaneViewModel lane)
    {
        foreach (var swimlane in Swimlanes)
        {
            var swimlaneLane = swimlane.Lanes.FirstOrDefault(existingLane => existingLane.Id == lane.Id);
            if (swimlaneLane is null)
            {
                continue;
            }

            if (swimlaneLane.Cards.Count > 0)
            {
                foreach (var card in swimlaneLane.Cards.ToList())
                {
                    swimlaneLane.RemoveCard(card);
                    card.ArchivedAt = DateTimeOffset.UtcNow;
                    ArchiveCards.Add(card);
                }
            }

            swimlane.Lanes.Remove(swimlaneLane);
        }

        var headerLane = ColumnLanes.FirstOrDefault(existingLane => existingLane.Id == lane.Id);
        if (headerLane is not null)
        {
            ColumnLanes.Remove(headerLane);
        }

        RefreshColumnSeparators();
        SaveAndRefresh(UiKeys.StatusLaneDeletedArchived);
    }

    private void MoveLane(LaneViewModel lane, int offset)
    {
        var headerLane = ColumnLanes.FirstOrDefault(existingLane => existingLane.Id == lane.Id);
        if (headerLane is null)
        {
            return;
        }

        var oldIndex = ColumnLanes.IndexOf(headerLane);
        var newIndex = oldIndex + offset;

        if (oldIndex < 0 || newIndex < 0 || newIndex >= ColumnLanes.Count)
        {
            return;
        }

        ColumnLanes.Move(oldIndex, newIndex);
        ReorderSwimlaneLanesByOffset(lane.Id, offset);
        RefreshColumnSeparators();
        SaveAndRefresh(UiKeys.StatusLaneMoved);
    }

    private void RefreshColumnSeparators()
    {
        for (var index = 0; index < ColumnLanes.Count; index++)
        {
            ColumnLanes[index].ShowLeadingSeparator = index > 0;
        }

        foreach (var swimlane in Swimlanes)
        {
            for (var index = 0; index < swimlane.Lanes.Count; index++)
            {
                swimlane.Lanes[index].ShowLeadingSeparator = index > 0;
            }
        }
    }

    private void ReorderSwimlaneLanes(string laneId, string beforeLaneId)
    {
        foreach (var swimlane in Swimlanes)
        {
            var lane = swimlane.Lanes.FirstOrDefault(existingLane => existingLane.Id == laneId);
            var targetLane = swimlane.Lanes.FirstOrDefault(existingLane => existingLane.Id == beforeLaneId);

            if (lane is null || targetLane is null)
            {
                continue;
            }

            var oldIndex = swimlane.Lanes.IndexOf(lane);
            var newIndex = swimlane.Lanes.IndexOf(targetLane);
            swimlane.Lanes.RemoveAt(oldIndex);

            if (oldIndex < newIndex)
            {
                newIndex--;
            }

            swimlane.Lanes.Insert(newIndex, lane);
        }
    }

    private void ReorderSwimlaneLanesByOffset(string laneId, int offset)
    {
        foreach (var swimlane in Swimlanes)
        {
            var lane = swimlane.Lanes.FirstOrDefault(existingLane => existingLane.Id == laneId);
            if (lane is null)
            {
                continue;
            }

            var oldIndex = swimlane.Lanes.IndexOf(lane);
            var newIndex = oldIndex + offset;

            if (oldIndex < 0 || newIndex < 0 || newIndex >= swimlane.Lanes.Count)
            {
                continue;
            }

            swimlane.Lanes.Move(oldIndex, newIndex);
        }
    }

    private void AddCard(LaneViewModel lane, NewCardCommit commit)
    {
        var card = CreateCard(
            new KanbanCard
            {
                Id = commit.CardId,
                SwimlaneId = lane.SwimlaneId,
                Description = commit.Description.Trim(),
                Images = commit.ImagePaths.ToList(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            isArchived: false);

        lane.AddCard(card, PrependNewCards ? 0 : null);
        SaveAndRefresh(UiKeys.StatusCardAdded);
    }

    private void DiscardNewCardDraft(LaneViewModel lane, IReadOnlyList<string> imagePaths)
    {
        foreach (var relativePath in imagePaths)
        {
            Attachments.DeleteImage(relativePath);
        }
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
        SaveAndRefresh(UiKeys.StatusCardArchived);
    }

    private void RestoreCard(CardViewModel card)
    {
        var targetSwimlane = Swimlanes.FirstOrDefault();
        var targetLane = targetSwimlane?.Lanes.FirstOrDefault();
        if (targetLane is null)
        {
            AddLane();
            targetSwimlane = Swimlanes.FirstOrDefault();
            targetLane = targetSwimlane?.Lanes.FirstOrDefault();
        }

        if (targetLane is null)
        {
            return;
        }

        if (ArchiveCards.Remove(card))
        {
            card.ArchivedAt = null;
            card.SwimlaneId = targetLane.SwimlaneId;
            targetLane.AddCard(card);
            SaveAndRefresh(UiKeys.StatusCardRestored);
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

        SaveAndRefresh(UiKeys.StatusCardDeleted);
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
        SaveAndRefresh(UiKeys.StatusCardMoved);
    }

    private void MoveCardToLane(CardViewModel card, LaneViewModel targetLane, int index)
    {
        var sourceLane = FindLaneContaining(card);
        if (sourceLane is null)
        {
            return;
        }

        sourceLane.RemoveCard(card);

        card.SwimlaneId = targetLane.SwimlaneId;
        var targetIndex = Math.Clamp(index, 0, targetLane.Cards.Count);
        targetLane.AddCard(card, targetIndex);
        SaveAndRefresh(UiKeys.StatusCardMoved);
    }

    private void SortLane(LaneViewModel lane)
    {
        if (lane.IsColumnHeader)
        {
            foreach (var swimlane in Swimlanes)
            {
                var swimlaneLane = swimlane.Lanes.FirstOrDefault(existingLane => existingLane.Id == lane.Id);
                if (swimlaneLane is not null)
                {
                    SortLane(swimlaneLane);
                }
            }

            return;
        }

        if (lane.Sort == LaneSort.Manual)
        {
            SaveAndRefresh(UiKeys.StatusManualOrderEnabled);
            return;
        }

        var sortedCards = lane.Sort switch
        {
            LaneSort.TitleAsc => lane.Cards.OrderBy(card => card.Description, StringComparer.OrdinalIgnoreCase),
            LaneSort.TitleDesc => lane.Cards.OrderByDescending(card => card.Description, StringComparer.OrdinalIgnoreCase),
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
        SaveAndRefresh(UiKeys.StatusLaneSorted);
    }

    private void OnCardContentChanged(CardViewModel card)
    {
        if (card.IsEditing)
        {
            Save(UiKeys.StatusSaved);
            return;
        }

        SaveAndRefresh(UiKeys.StatusCardUpdated);
    }

    private void SaveAndRefresh(string messageKey)
    {
        RefreshFilters();
        Save(messageKey);
    }

    private void Save()
    {
        Save(UiKeys.StatusSaved);
    }

    private void Save(string messageKey)
    {
        if (!IsWorkspaceReady)
        {
            return;
        }

        try
        {
            _storage!.Save(ToData());
            StatusMessage = $"{LocalizationService.Get(messageKey)} {DateTimeOffset.Now:t}";
            NotifyBoardProperties();
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationService.Format(UiKeys.StatusSaveFailed, ex.Message);
        }
    }

    private void NewBoard()
    {
        Hydrate(KanbanBoard.CreateDefault());
        SaveAndRefresh(UiKeys.StatusNewBoardCreated);
    }

    private async Task SelectWorkspaceFolderAsync()
    {
        if (_ownerWindow is null)
        {
            return;
        }

        var folders = await _ownerWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = LocalizationService.Get(UiKeys.WorkspacePickerTitle),
            AllowMultiple = false,
        });

        if (folders.Count == 0)
        {
            return;
        }

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = LocalizationService.Get(UiKeys.WorkspaceCannotReadPath);
            return;
        }

        ApplyWorkspaceFolder(path);
    }

    private void ApplyWorkspaceFolder(string workspaceFolder)
    {
        try
        {
            if (IsWorkspaceReady)
            {
                Save();
            }

            var trimmed = workspaceFolder.Trim();
            Directory.CreateDirectory(trimmed);

            _preferences.WorkspaceFolder = trimmed;
            _preferences.Save();

            _storage = new JsonBoardStorage(JsonBoardStorage.GetBoardPath(trimmed));
            _attachments = new CardAttachmentService(_storage);
            Load();
            RefreshWorkspaceProperties();

            StatusMessage = LocalizationService.Format(UiKeys.WorkspaceSet, trimmed);
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationService.Format(UiKeys.WorkspaceSetFailed, ex.Message);
        }
    }

    private void RefreshWorkspaceProperties()
    {
        OnPropertyChanged(nameof(IsWorkspaceReady));
        OnPropertyChanged(nameof(WorkspaceFolderDisplay));
        OnPropertyChanged(nameof(BoardFilePath));
    }

    private KanBanData ToData()
    {
        var maxArchiveSize = int.TryParse(MaxArchiveSizeText, out var parsedMaxArchiveSize)
            ? parsedMaxArchiveSize
            : 200;

        var lanes = ColumnLanes
            .Select(header =>
            {
                var lane = header.ToMetadataModel();
                lane.Cards = Swimlanes
                    .SelectMany(swimlane => swimlane.Lanes)
                    .Where(swimlaneLane => swimlaneLane.Id == header.Id)
                    .SelectMany(swimlaneLane => swimlaneLane.Cards)
                    .Select(card => card.ToModel())
                    .ToList();
                return lane;
            })
            .ToList();

        var board = new KanbanBoard
        {
            Title = string.IsNullOrWhiteSpace(BoardTitle) ? "KanBan" : BoardTitle.Trim(),
            ViewMode = BoardViewMode.Board,
            Settings = new BoardSettings
            {
                ShowRelativeDates = ShowRelativeDates,
                PrependNewCards = PrependNewCards,
                MaxArchiveSize = maxArchiveSize,
                DateFormat = string.IsNullOrWhiteSpace(DateFormat) ? "yyyy-MM-dd" : DateFormat.Trim(),
            },
            Lanes = lanes,
            Swimlanes = Swimlanes.Select(swimlane => swimlane.ToModel()).ToList(),
            Archive = ArchiveCards.Select(card => card.ToModel()).ToList(),
        };

        KanbanBoardOperations.TrimArchive(board);
        return new KanBanData { Board = board };
    }

    private void RefreshFilters()
    {
        foreach (var swimlane in Swimlanes)
        {
            foreach (var lane in swimlane.Lanes)
            {
                lane.RefreshFilter(SearchQuery);
            }
        }

        foreach (var header in ColumnLanes)
        {
            var aggregateCount = Swimlanes
                .SelectMany(swimlane => swimlane.Lanes)
                .Where(lane => lane.Id == header.Id)
                .Sum(lane => lane.Cards.Count);
            header.SetAggregateCardCount(aggregateCount);
        }

        NotifyBoardProperties();
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
        return Swimlanes
            .SelectMany(swimlane => swimlane.Lanes)
            .SelectMany(lane => lane.Cards)
            .FirstOrDefault(card => card.Id == cardId);
    }

    private LaneViewModel? FindLaneContaining(CardViewModel card)
    {
        return Swimlanes
            .SelectMany(swimlane => swimlane.Lanes)
            .FirstOrDefault(lane => lane.Cards.Contains(card));
    }

    private LaneViewModel? FindLane(string laneId, string? swimlaneId)
    {
        if (swimlaneId is not null)
        {
            var swimlane = Swimlanes.FirstOrDefault(existingSwimlane => existingSwimlane.Id == swimlaneId);
            return swimlane?.Lanes.FirstOrDefault(lane => lane.Id == laneId);
        }

        return Swimlanes
            .SelectMany(swimlane => swimlane.Lanes)
            .FirstOrDefault(lane => lane.Id == laneId);
    }

    private void NotifyBoardProperties()
    {
        OnPropertyChanged(nameof(BoardTitle));
        OnPropertyChanged(nameof(ShowRelativeDates));
        OnPropertyChanged(nameof(PrependNewCards));
        OnPropertyChanged(nameof(MaxArchiveSizeText));
        OnPropertyChanged(nameof(DateFormat));
        OnPropertyChanged(nameof(TotalCards));
        OnPropertyChanged(nameof(TotalCardsText));
        OnPropertyChanged(nameof(ArchiveCount));
        OnPropertyChanged(nameof(ArchiveCountText));
        OnPropertyChanged(nameof(SwimlaneCount));
    }

    private void RefreshLanguageOptions()
    {
        AvailableLanguages =
        [
            new LanguageOption(LocalizationService.English, LocalizationService.Get("Settings.Language.English")),
            new LanguageOption(LocalizationService.Chinese, LocalizationService.Get("Settings.Language.Chinese")),
        ];
        OnPropertyChanged(nameof(AvailableLanguages));
    }

    private void RefreshLocalizedProperties()
    {
        RefreshLanguageOptions();
        _selectedLanguage = AvailableLanguages.First(option =>
            option.CultureName == LocalizationService.Instance.CultureName);
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(BoardFilePath));
        OnPropertyChanged(nameof(WorkspaceFolderDisplay));
        OnPropertyChanged(nameof(TotalCardsText));
        OnPropertyChanged(nameof(ArchiveCountText));

        foreach (var swimlane in Swimlanes)
        {
            foreach (var lane in swimlane.Lanes)
            {
                lane.NotifyCounts();
            }
        }

        foreach (var header in ColumnLanes)
        {
            header.NotifyCounts();
        }
    }
}
