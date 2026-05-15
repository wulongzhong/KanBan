using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using KanBan.Models;

namespace KanBan.ViewModels;

public sealed class CardViewModel : ViewModelBase
{
    private readonly Action<CardViewModel>? _onChanged;
    private readonly Action<CardViewModel>? _onArchive;
    private readonly Action<CardViewModel>? _onDelete;
    private readonly Action<CardViewModel>? _onRestore;
    private readonly Action<CardViewModel, int>? _onMove;
    private string _title;
    private string _description;
    private bool _isComplete;
    private string _tagsText;
    private string _dueDateText;
    private bool _showCheckbox = true;
    private DateTimeOffset _updatedAt;
    private DateTimeOffset? _archivedAt;
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
        CreatedAt = card.CreatedAt;
        _updatedAt = card.UpdatedAt;
        _archivedAt = card.ArchivedAt;
        _title = card.Title;
        _description = card.Description;
        _isComplete = card.IsComplete;
        _tagsText = string.Join(' ', card.Tags.Select(tag => tag.StartsWith('#') ? tag : $"#{tag}"));
        _dueDateText = card.DueDate?.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) ?? string.Empty;
        Tags = new ObservableCollection<string>(ParseTags(_tagsText));
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
    }

    public string Id { get; }

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

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                Touch();
            }
        }
    }

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

    public bool IsComplete
    {
        get => _isComplete;
        set
        {
            if (SetProperty(ref _isComplete, value))
            {
                Touch();
            }
        }
    }

    public string TagsText
    {
        get => _tagsText;
        set
        {
            if (SetProperty(ref _tagsText, value))
            {
                ReplaceTags(ParseTags(value));
                Touch();
            }
        }
    }

    public ObservableCollection<string> Tags { get; }

    public bool HasTags => Tags.Count > 0;

    public string DueDateText
    {
        get => _dueDateText;
        set
        {
            if (SetProperty(ref _dueDateText, value))
            {
                OnPropertyChanged(nameof(DueDate));
                OnPropertyChanged(nameof(HasDueDate));
                OnPropertyChanged(nameof(DueBadge));
                OnPropertyChanged(nameof(IsOverdue));
                Touch();
            }
        }
    }

    public DateTimeOffset? DueDate
    {
        get
        {
            if (DateTimeOffset.TryParse(DueDateText, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                return parsed;
            }

            return null;
        }
    }

    public bool HasDueDate => DueDate is not null;

    public bool IsOverdue => DueDate?.Date < DateTimeOffset.Now.Date && !IsComplete;

    public string DueBadge
    {
        get
        {
            if (DueDate is not { } dueDate)
            {
                return string.Empty;
            }

            var today = DateTimeOffset.Now.Date;
            var date = dueDate.Date;

            if (date == today)
            {
                return "Today";
            }

            var days = (date - today).Days;
            return days > 0 ? $"{days}d left" : $"{Math.Abs(days)}d late";
        }
    }

    public bool ShowCheckbox
    {
        get => _showCheckbox;
        set => SetProperty(ref _showCheckbox, value);
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

    public KanbanCard ToModel()
    {
        return new KanbanCard
        {
            Id = Id,
            Title = string.IsNullOrWhiteSpace(Title) ? "Untitled card" : Title.Trim(),
            Description = Description.Trim(),
            IsComplete = IsComplete,
            CheckChar = IsComplete ? "x" : " ",
            Tags = Tags.Select(tag => tag.TrimStart('#')).Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            DueDate = DueDate,
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

        var haystack = $"{Title} {Description} {TagsText} {DueDateText}";
        return haystack.Contains(query, StringComparison.OrdinalIgnoreCase);
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

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        OnPropertyChanged(nameof(DueBadge));
        OnPropertyChanged(nameof(IsOverdue));
        _onChanged?.Invoke(this);
    }

    private void ReplaceTags(IEnumerable<string> tags)
    {
        Tags.Clear();
        foreach (var tag in tags)
        {
            Tags.Add(tag);
        }

        OnPropertyChanged(nameof(HasTags));
    }

    private static IEnumerable<string> ParseTags(string value)
    {
        return value
            .Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => tag.StartsWith('#') ? tag : $"#{tag}")
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
