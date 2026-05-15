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

    public SwimlaneViewModel(
        KanbanSwimlane swimlane,
        Action<SwimlaneViewModel>? onChanged = null,
        Action<SwimlaneViewModel>? onDelete = null,
        Action<SwimlaneViewModel, int>? onMove = null)
    {
        Id = swimlane.Id;
        _title = swimlane.Title;
        _onChanged = onChanged;
        _onDelete = onDelete;
        _onMove = onMove;
        Lanes = [];

        BeginEditCommand = new RelayCommand(BeginEdit);
        EndEditCommand = new RelayCommand(EndEdit);
        DeleteSwimlaneCommand = new RelayCommand(() => _onDelete?.Invoke(this));
        MoveUpCommand = new RelayCommand(() => _onMove?.Invoke(this, -1));
        MoveDownCommand = new RelayCommand(() => _onMove?.Invoke(this, 1));
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

    public RelayCommand BeginEditCommand { get; }

    public RelayCommand EndEditCommand { get; }

    public RelayCommand DeleteSwimlaneCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public KanbanSwimlane ToModel()
    {
        return new KanbanSwimlane
        {
            Id = Id,
            Title = string.IsNullOrWhiteSpace(Title) ? "Untitled swimlane" : Title.Trim(),
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
}
