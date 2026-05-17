using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using KanBan.Models;
using KanBan.Services;

namespace KanBan.ViewModels;

public sealed class LaneViewModel : ViewModelBase
{
    private readonly Action<LaneViewModel>? _onChanged;
    private readonly Action<LaneViewModel, NewCardCommit>? _onAddCard;
    private readonly Action<LaneViewModel, IReadOnlyList<string>>? _onDiscardNewCardDraft;
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
    private string _newCardDetails = string.Empty;
    private string? _newCardDraftId;
    private readonly List<string> _newCardDraftImagePaths = [];
    private int? _aggregateCardCount;
    private bool _showLeadingSeparator;
    private bool _isCollapsed;

    public LaneViewModel(
        KanbanLane lane,
        Func<KanbanCard, CardViewModel> cardFactory,
        Action<LaneViewModel>? onChanged = null,
        Action<LaneViewModel, NewCardCommit>? onAddCard = null,
        Action<LaneViewModel, IReadOnlyList<string>>? onDiscardNewCardDraft = null,
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
        _isCollapsed = lane.IsCollapsed;
        _onChanged = onChanged;
        _onAddCard = onAddCard;
        _onDiscardNewCardDraft = onDiscardNewCardDraft;
        _onDelete = onDelete;
        _onMove = onMove;
        _onSort = onSort;

        var laneCards = isColumnHeader
            ? []
            : lane.Cards.Where(card => card.SwimlaneId == swimlaneId);

        Cards = new ObservableCollection<CardViewModel>(laneCards.Select(cardFactory));
        FilteredCards = new ObservableCollection<CardViewModel>(Cards);
        NewCardPreviewImages = [];

        AddCardCommand = new RelayCommand(BeginAddCard);
        CommitAddCardCommand = new RelayCommand(CommitAddCard);
        CancelAddCardCommand = new RelayCommand(CancelAddCard);
        BeginEditCommand = new RelayCommand(BeginEdit);
        EndEditCommand = new RelayCommand(EndEdit);
        DeleteLaneCommand = new RelayCommand(() => _onDelete?.Invoke(this));
        MoveLeftCommand = new RelayCommand(() => _onMove?.Invoke(this, -1));
        MoveRightCommand = new RelayCommand(() => _onMove?.Invoke(this, 1));
        SortCommand = new RelayCommand(() => _onSort?.Invoke(this));
        ToggleCollapseCommand = new RelayCommand(ToggleCollapse);
    }

    public const double ExpandedColumnWidth = 272;
    public const double CollapsedColumnWidth = 96;
    public const double ExpandedColumnGridWidth = 284;
    public const double CollapsedColumnGridWidth = 108;

    public string Id { get; }

    public string? SwimlaneId { get; }

    public bool IsColumnHeader { get; }

    public bool ShowLeadingSeparator
    {
        get => _showLeadingSeparator;
        set => SetProperty(ref _showLeadingSeparator, value);
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (SetProperty(ref _isCollapsed, value))
            {
                OnPropertyChanged(nameof(IsExpanded));
                OnPropertyChanged(nameof(ColumnWidth));
                OnPropertyChanged(nameof(ColumnGridWidth));
                OnPropertyChanged(nameof(CollapseButtonContent));
                OnPropertyChanged(nameof(CollapseToolTip));
                NotifyChanged();
            }
        }
    }

    public bool IsExpanded => !IsCollapsed;

    public double ColumnWidth => IsCollapsed ? CollapsedColumnWidth : ExpandedColumnWidth;

    public double ColumnGridWidth => IsCollapsed ? CollapsedColumnGridWidth : ExpandedColumnGridWidth;

    public string CollapseButtonContent => IsCollapsed ? "›" : "‹";

    public string CollapseToolTip => IsCollapsed ? "展开列" : "折叠列";

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                OnPropertyChanged(nameof(HeaderTitle));
                NotifyChanged();
            }
        }
    }

    public string HeaderTitle => Title.ToUpperInvariant();

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

    public string NewCardDetails
    {
        get => _newCardDetails;
        set => SetProperty(ref _newCardDetails, value);
    }

    public ObservableCollection<CardImageViewModel> NewCardPreviewImages { get; }

    public bool HasNewCardPreviewImages => NewCardPreviewImages.Count > 0;

    public RelayCommand AddCardCommand { get; }

    public RelayCommand CommitAddCardCommand { get; }

    public RelayCommand CancelAddCardCommand { get; }

    public RelayCommand BeginEditCommand { get; }

    public RelayCommand EndEditCommand { get; }

    public RelayCommand DeleteLaneCommand { get; }

    public RelayCommand MoveLeftCommand { get; }

    public RelayCommand MoveRightCommand { get; }

    public RelayCommand SortCommand { get; }

    public RelayCommand ToggleCollapseCommand { get; }

    public KanbanLane ToModel()
    {
        return new KanbanLane
        {
            Id = Id,
            Title = string.IsNullOrWhiteSpace(Title) ? "Untitled lane" : Title.Trim(),
            MaxItems = MaxItems,
            Sort = Sort,
            ShouldMarkItemsComplete = ShouldMarkItemsComplete,
            IsCollapsed = IsCollapsed,
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
            IsCollapsed = IsCollapsed,
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
        DiscardNewCardDraft();
        NewCardDetails = string.Empty;
        _newCardDraftId = Guid.NewGuid().ToString("N");
        IsAddingCard = true;
    }

    private void CommitAddCard()
    {
        var description = NewCardDetails.Trim();
        if (string.IsNullOrWhiteSpace(description) && _newCardDraftImagePaths.Count == 0)
        {
            IsAddingCard = false;
            DiscardNewCardDraft();
            return;
        }

        var cardId = EnsureNewCardDraftId();
        _onAddCard?.Invoke(this, new NewCardCommit(cardId, description, _newCardDraftImagePaths.ToList()));
        NewCardDetails = string.Empty;
        ResetNewCardDraftState();
        IsAddingCard = false;
    }

    private void CancelAddCard()
    {
        NewCardDetails = string.Empty;
        IsAddingCard = false;
        DiscardNewCardDraft();
    }

    public void AddDraftImageFromFile(CardAttachmentService attachments, string sourcePath)
    {
        if (!CardAttachmentService.IsImageFile(sourcePath))
        {
            return;
        }

        var relativePath = attachments.SaveImageFromFile(EnsureNewCardDraftId(), sourcePath);
        AddDraftImagePath(attachments, relativePath);
    }

    public void AddDraftImageFromBitmap(CardAttachmentService attachments, Bitmap bitmap)
    {
        var relativePath = attachments.SaveImageFromBitmap(EnsureNewCardDraftId(), bitmap);
        AddDraftImagePath(attachments, relativePath);
    }

    public void RemoveDraftImage(CardAttachmentService attachments, string relativePath)
    {
        _newCardDraftImagePaths.RemoveAll(existing =>
            string.Equals(existing, relativePath, StringComparison.OrdinalIgnoreCase));
        attachments.DeleteImage(relativePath);
        LoadNewCardPreviewImages(attachments);
    }

    private void AddDraftImagePath(CardAttachmentService attachments, string relativePath)
    {
        if (_newCardDraftImagePaths.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _newCardDraftImagePaths.Add(relativePath);
        LoadNewCardPreviewImages(attachments);
    }

    private void LoadNewCardPreviewImages(CardAttachmentService attachments)
    {
        NewCardPreviewImages.Clear();

        foreach (var relativePath in _newCardDraftImagePaths)
        {
            var absolutePath = attachments.ResolveAbsolutePath(relativePath);
            if (!File.Exists(absolutePath))
            {
                continue;
            }

            var path = relativePath;
            NewCardPreviewImages.Add(new CardImageViewModel(
                path,
                absolutePath,
                new RelayCommand(() => RemoveDraftImage(attachments, path))));
        }

        OnPropertyChanged(nameof(HasNewCardPreviewImages));
    }

    private string EnsureNewCardDraftId() => _newCardDraftId ??= Guid.NewGuid().ToString("N");

    private void DiscardNewCardDraft()
    {
        if (_newCardDraftImagePaths.Count > 0)
        {
            _onDiscardNewCardDraft?.Invoke(this, _newCardDraftImagePaths.ToList());
        }

        ResetNewCardDraftState();
    }

    private void ResetNewCardDraftState()
    {
        _newCardDraftId = null;
        _newCardDraftImagePaths.Clear();
        NewCardPreviewImages.Clear();
        OnPropertyChanged(nameof(HasNewCardPreviewImages));
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

    private void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }
}

public sealed record NewCardCommit(string CardId, string Description, IReadOnlyList<string> ImagePaths);
