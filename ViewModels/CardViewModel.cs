using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using KanBan.Models;
using KanBan.Services;

namespace KanBan.ViewModels;

public sealed class CardViewModel : ViewModelBase
{
    private readonly List<string> _imagePaths = [];
    private readonly Action<CardViewModel>? _onChanged;
    private readonly Action<CardViewModel>? _onArchive;
    private readonly Action<CardViewModel>? _onDelete;
    private readonly Action<CardViewModel>? _onRestore;
    private readonly Action<CardViewModel, int>? _onMove;
    private string _description;
    private DateTime? _dueDate;
    private TimeSpan? _dueTime;
    private DateTimeOffset _updatedAt;
    private DateTimeOffset? _archivedAt;
    private string? _swimlaneId;
    private bool _isEditing;

    public CardViewModel(
        KanbanCard card,
        Action<CardViewModel>? onChanged = null,
        Action<CardViewModel>? onArchive = null,
        Action<CardViewModel>? onDelete = null,
        Action<CardViewModel>? onRestore = null,
        Action<CardViewModel, int>? onMove = null)
    {
        Id = card.Id;
        _swimlaneId = card.SwimlaneId;
        CreatedAt = card.CreatedAt;
        _updatedAt = card.UpdatedAt;
        _archivedAt = card.ArchivedAt;
        _description = NormalizeDescription(card);
        _dueDate = card.DueDate?.LocalDateTime.Date;
        _dueTime = card.DueTime;
        _imagePaths.AddRange(card.Images);
        PreviewImages = [];
        _onChanged = onChanged;
        _onArchive = onArchive;
        _onDelete = onDelete;
        _onRestore = onRestore;
        _onMove = onMove;

        SaveCommand = new RelayCommand(Touch);
        BeginEditCommand = new RelayCommand(BeginEdit);
        EndEditCommand = new RelayCommand(EndEdit);
        ArchiveCommand = new RelayCommand(() => _onArchive?.Invoke(this));
        DeleteCommand = new RelayCommand(() => _onDelete?.Invoke(this));
        RestoreCommand = new RelayCommand(() => _onRestore?.Invoke(this));
        MoveUpCommand = new RelayCommand(() => _onMove?.Invoke(this, -1));
        MoveDownCommand = new RelayCommand(() => _onMove?.Invoke(this, 1));
        ClearDueDateCommand = new RelayCommand(ClearDueDate);
        ClearDueTimeCommand = new RelayCommand(ClearDueTime);
    }

    public string Id { get; }

    public string? SwimlaneId
    {
        get => _swimlaneId;
        set => SetProperty(ref _swimlaneId, value);
    }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt
    {
        get => _updatedAt;
        private set => SetProperty(ref _updatedAt, value);
    }

    public DateTimeOffset? ArchivedAt
    {
        get => _archivedAt;
        set
        {
            if (SetProperty(ref _archivedAt, value))
            {
                OnPropertyChanged(nameof(IsArchived));
            }
        }
    }

    public bool IsArchived => ArchivedAt is not null;

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                OnPropertyChanged(nameof(HasDescription));
                Touch();
            }
        }
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public string DragLabel
    {
        get
        {
            var text = Description.Trim();
            if (text.Length == 0)
            {
                return "Card";
            }

            var line = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return line.Length > 48 ? $"{line[..48]}…" : line;
        }
    }

    public ObservableCollection<CardImageViewModel> PreviewImages { get; }

    public bool HasPreviewImages => PreviewImages.Count > 0;

    public DateTime? DueDate => _dueDate;

    public TimeSpan? DueTime => _dueTime;

    public bool HasDueDate => _dueDate is not null;

    public bool HasDueTime => _dueTime is not null;

    public bool ShowRemoveDateMenu => HasDueDate;

    public bool ShowTimeMenuSection => HasDueDate;

    public bool ShowRemoveTimeMenu => HasDueTime;

    public bool ShowRelativeDueBadge => HasDueDate && !HasDueTime;

    public string DateMenuHeader => HasDueDate ? "编辑日期" : "添加日期";

    public string TimeMenuHeader => HasDueTime ? "编辑时间" : "添加时间";

    public string DateDisplayText =>
        HasDueDate ? _dueDate!.Value.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) : string.Empty;

    public string TimeDisplayText =>
        HasDueTime ? DateTime.Today.Add(_dueTime!.Value).ToString("HH:mm", CultureInfo.CurrentCulture) : string.Empty;

    public bool IsOverdue => HasDueDate && _dueDate!.Value < DateTime.Today;

    public string DueBadge
    {
        get
        {
            if (!HasDueDate)
            {
                return string.Empty;
            }

            if (HasDueTime)
            {
                return TimeDisplayText;
            }

            var today = DateTime.Today;
            var date = _dueDate!.Value;

            if (date == today)
            {
                return "今天";
            }

            var days = (date - today).Days;
            if (days == -1)
            {
                return "昨天";
            }

            if (days == 1)
            {
                return "明天";
            }

            return days > 0 ? $"{days}天后" : $"{Math.Abs(days)}天前";
        }
    }

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

    public RelayCommand SaveCommand { get; }

    public RelayCommand BeginEditCommand { get; }

    public RelayCommand EndEditCommand { get; }

    public RelayCommand ArchiveCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand RestoreCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand ClearDueDateCommand { get; }

    public RelayCommand ClearDueTimeCommand { get; }

    public void SetDueDate(DateTime date)
    {
        _dueDate = date.Date;
        NotifyDateTimeChanged();
    }

    public void SetDueTime(TimeSpan time)
    {
        if (!HasDueDate)
        {
            _dueDate = DateTime.Today;
        }

        _dueTime = time;
        NotifyDateTimeChanged();
    }

    public void ClearDueDate()
    {
        _dueDate = null;
        _dueTime = null;
        NotifyDateTimeChanged();
    }

    public void ClearDueTime()
    {
        _dueTime = null;
        NotifyDateTimeChanged();
    }

    public void LoadPreviewImages(CardAttachmentService attachments)
    {
        PreviewImages.Clear();

        foreach (var relativePath in _imagePaths)
        {
            var absolutePath = attachments.ResolveAbsolutePath(relativePath);
            if (!File.Exists(absolutePath))
            {
                continue;
            }

            var path = relativePath;
            PreviewImages.Add(new CardImageViewModel(
                path,
                absolutePath,
                new RelayCommand(() => RemoveImage(attachments, path))));
        }

        OnPropertyChanged(nameof(HasPreviewImages));
    }

    public void RemoveImage(CardAttachmentService attachments, string relativePath)
    {
        _imagePaths.RemoveAll(existing =>
            string.Equals(existing, relativePath, StringComparison.OrdinalIgnoreCase));
        attachments.DeleteImage(relativePath);
        LoadPreviewImages(attachments);
        Touch();
    }

    public void AddImageFromFile(CardAttachmentService attachments, string sourcePath)
    {
        if (!CardAttachmentService.IsImageFile(sourcePath))
        {
            return;
        }

        var relativePath = attachments.SaveImageFromFile(Id, sourcePath);
        AddImagePath(attachments, relativePath);
    }

    public void AddImageFromBitmap(CardAttachmentService attachments, Bitmap bitmap)
    {
        var relativePath = attachments.SaveImageFromBitmap(Id, bitmap);
        AddImagePath(attachments, relativePath);
    }

    public KanbanCard ToModel()
    {
        return new KanbanCard
        {
            Id = Id,
            SwimlaneId = SwimlaneId,
            Title = string.Empty,
            Description = Description.Trim(),
            Images = _imagePaths.ToList(),
            DueDate = _dueDate is null ? null : new DateTimeOffset(_dueDate.Value),
            DueTime = _dueTime,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            ArchivedAt = ArchivedAt,
        };
    }

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var haystack = $"{Description} {DateDisplayText} {TimeDisplayText}";
        return haystack.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDescription(KanbanCard card)
    {
        if (!string.IsNullOrWhiteSpace(card.Description))
        {
            return card.Description;
        }

        return card.Title?.Trim() ?? string.Empty;
    }

    public void MarkChanged()
    {
        Touch();
    }

    public void BeginEdit()
    {
        IsEditing = true;
    }

    public void EndEdit()
    {
        IsEditing = false;
        Touch();
    }

    private void NotifyDateTimeChanged()
    {
        OnPropertyChanged(nameof(DueDate));
        OnPropertyChanged(nameof(DueTime));
        OnPropertyChanged(nameof(HasDueDate));
        OnPropertyChanged(nameof(HasDueTime));
        OnPropertyChanged(nameof(ShowRemoveDateMenu));
        OnPropertyChanged(nameof(ShowTimeMenuSection));
        OnPropertyChanged(nameof(ShowRemoveTimeMenu));
        OnPropertyChanged(nameof(DateMenuHeader));
        OnPropertyChanged(nameof(TimeMenuHeader));
        OnPropertyChanged(nameof(DateDisplayText));
        OnPropertyChanged(nameof(TimeDisplayText));
        OnPropertyChanged(nameof(ShowRelativeDueBadge));
        OnPropertyChanged(nameof(DueBadge));
        OnPropertyChanged(nameof(IsOverdue));
        Touch();
    }

    private void AddImagePath(CardAttachmentService attachments, string relativePath)
    {
        if (_imagePaths.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _imagePaths.Add(relativePath);
        LoadPreviewImages(attachments);
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        _onChanged?.Invoke(this);
    }
}
