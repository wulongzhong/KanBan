using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using KanBan.ViewModels;

namespace KanBan.Views;

public partial class MainWindow : Window
{
    private enum DragKind
    {
        None,
        Card,
        Lane,
    }

    private Point _dragStart;
    private string? _pressedCardId;
    private string? _pressedLaneId;
    private string? _pressedTitle;
    private string? _draggingId;
    private DragKind _dragKind;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Card_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not CardViewModel card)
        {
            return;
        }

        if (IsInteractiveSource(e.Source))
        {
            return;
        }

        _pressedCardId = card.Id;
        _pressedLaneId = null;
        _pressedTitle = card.Title;
        _dragStart = e.GetPosition(this);
    }

    private void Card_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedCardId is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || !HasDragThreshold(e.GetPosition(this)))
        {
            return;
        }

        BeginPointerDrag(DragKind.Card, _pressedCardId, _pressedTitle ?? "Card", e);
        e.Handled = true;
    }

    private void Card_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: CardViewModel card })
        {
            card.BeginEdit();
            e.Handled = true;
        }
    }

    private void CardEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is Control { DataContext: CardViewModel card } && e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            card.EndEdit();
            e.Handled = true;
        }
    }

    private void LaneHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not LaneViewModel lane)
        {
            return;
        }

        if (IsInteractiveSource(e.Source))
        {
            return;
        }

        _pressedLaneId = lane.Id;
        _pressedCardId = null;
        _pressedTitle = lane.Title;
        _dragStart = e.GetPosition(this);
    }

    private void LaneHeader_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedLaneId is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || !HasDragThreshold(e.GetPosition(this)))
        {
            return;
        }

        BeginPointerDrag(DragKind.Lane, _pressedLaneId, _pressedTitle ?? "List", e);
        e.Handled = true;
    }

    private void LaneHeader_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: LaneViewModel lane })
        {
            lane.BeginEdit();
            e.Handled = true;
        }
    }

    private void LaneEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is Control { DataContext: LaneViewModel lane } && e.Key == Key.Enter)
        {
            lane.EndEdit();
            e.Handled = true;
        }
    }

    private void LaneEdit_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: LaneViewModel lane })
        {
            lane.EndEdit();
        }
    }

    private bool HasDragThreshold(Point currentPosition)
    {
        return Math.Abs(currentPosition.X - _dragStart.X) > 6 ||
               Math.Abs(currentPosition.Y - _dragStart.Y) > 6;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragKind == DragKind.None)
        {
            return;
        }

        MoveDragPreview(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragKind == DragKind.None)
        {
            ClearPressedState();
            return;
        }

        CompletePointerDrag(e.GetPosition(this));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void BeginPointerDrag(DragKind kind, string id, string title, PointerEventArgs e)
    {
        _dragKind = kind;
        _draggingId = id;
        _pressedCardId = null;
        _pressedLaneId = null;
        _pressedTitle = null;

        DragPreview.Width = kind == DragKind.Lane ? 244 : 232;
        DragPreviewText.Text = kind == DragKind.Lane ? $"List: {title}" : title;
        DragPreview.IsVisible = true;
        MoveDragPreview(e.GetPosition(this));

        e.Pointer.Capture(this);
    }

    private void MoveDragPreview(Point point)
    {
        Canvas.SetLeft(DragPreview, point.X + 14);
        Canvas.SetTop(DragPreview, point.Y + 14);
    }

    private void CompletePointerDrag(Point point)
    {
        if (DataContext is not MainWindowViewModel viewModel || _draggingId is null)
        {
            ResetDragState();
            return;
        }

        var elements = this.GetInputElementsAt(point, enabledElementsOnly: false)
            .OfType<Control>()
            .ToList();

        if (_dragKind == DragKind.Card)
        {
            var targetCard = elements
                .Select(control => control.DataContext)
                .OfType<CardViewModel>()
                .FirstOrDefault(card => card.Id != _draggingId);

            if (targetCard is not null)
            {
                viewModel.MoveCardBefore(_draggingId, targetCard.Id);
                ResetDragState();
                return;
            }

            var targetLane = elements
                .Select(control => control.DataContext)
                .OfType<LaneViewModel>()
                .FirstOrDefault();

            if (targetLane is not null)
            {
                viewModel.MoveCardToLane(_draggingId, targetLane.Id);
            }
        }
        else if (_dragKind == DragKind.Lane)
        {
            var targetLane = elements
                .Select(control => control.DataContext)
                .OfType<LaneViewModel>()
                .FirstOrDefault(lane => lane.Id != _draggingId);

            if (targetLane is not null)
            {
                viewModel.MoveLaneBefore(_draggingId, targetLane.Id);
            }
        }

        ResetDragState();
    }

    private void ResetDragState()
    {
        DragPreview.IsVisible = false;
        _dragKind = DragKind.None;
        _draggingId = null;
        ClearPressedState();
    }

    private void ClearPressedState()
    {
        _pressedCardId = null;
        _pressedLaneId = null;
        _pressedTitle = null;
    }

    private static bool IsInteractiveSource(object? source)
    {
        return source is Visual visual &&
               visual.GetSelfAndVisualAncestors()
                   .OfType<Control>()
                   .Any(control => control is Button or TextBox or CheckBox or ComboBox);
    }
}