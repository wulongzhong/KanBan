using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using KanBan.Models;

namespace KanBan.ViewModels;

public sealed class SwimlaneViewModel : ViewModelBase
{
    private readonly Action<SwimlaneViewModel>? _onChanged;
    private readonly Action<SwimlaneViewModel>? _onDelete;
    private readonly Action<SwimlaneViewModel, int>? _onMove;
    private string _title;
    private bool _isEditing;
    private bool _isCollapsed;

    public SwimlaneViewModel(
        KanbanSwimlane swimlane,
        Action<SwimlaneViewModel>? onChanged = null,
        Action<SwimlaneViewModel>? onDelete = null,
        Action<SwimlaneViewModel, int>? onMove = null)
    {
        Id = swimlane.Id;
        _title = swimlane.Title;
        _isCollapsed = swimlane.IsCollapsed;
        _onChanged = onChanged;
        _onDelete = onDelete;
        _onMove = onMove;
        Lanes = [];

        BeginEditCommand = new RelayCommand(BeginEdit);
        EndEditCommand = new RelayCommand(EndEdit);
        DeleteSwimlaneCommand = new RelayCommand(() => _onDelete?.Invoke(this));
        MoveUpCommand = new RelayCommand(() => _onMove?.Invoke(this, -1));
        MoveDownCommand = new RelayCommand(() => _onMove?.Invoke(this, 1));
        ToggleCollapseCommand = new RelayCommand(ToggleCollapse);
    }

    public string Id { get; }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                _onChanged?.Invoke(this);
            }
        }
    }

    public ObservableCollection<LaneViewModel> Lanes { get; }

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

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (SetProperty(ref _isCollapsed, value))
            {
                OnPropertyChanged(nameof(IsExpanded));
                OnPropertyChanged(nameof(CollapseButtonContent));
                OnPropertyChanged(nameof(CollapseToolTip));
                _onChanged?.Invoke(this);
            }
        }
    }

    public bool IsExpanded => !IsCollapsed;

    public string CollapseButtonContent => IsCollapsed ? "˅" : "˄";

    public string CollapseToolTip => IsCollapsed ? "展开泳道" : "折叠泳道";

    public RelayCommand BeginEditCommand { get; }

    public RelayCommand EndEditCommand { get; }

    public RelayCommand DeleteSwimlaneCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand ToggleCollapseCommand { get; }

    public KanbanSwimlane ToModel()
    {
        return new KanbanSwimlane
        {
            Id = Id,
            Title = string.IsNullOrWhiteSpace(Title) ? "Untitled swimlane" : Title.Trim(),
            IsCollapsed = IsCollapsed,
        };
    }

    public void BeginEdit()
    {
        IsEditing = true;
    }

    public void EndEdit()
    {
        IsEditing = false;
        _onChanged?.Invoke(this);
    }

    private void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }
}
