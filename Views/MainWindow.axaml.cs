using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
    private Point _dragGrabOffset;
    private string? _pressedCardId;
    private string? _pressedLaneId;
    private string? _pressedTitle;
    private string? _draggingId;
    private DragKind _dragKind;
    private CardViewModel? _pickerCard;
    private Control? _pickerAnchor;
    private bool _pickerAlignRight;
    private bool _suppressDateSelectionChanged;

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
        _dragGrabOffset = e.GetPosition(control);
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
        _dragGrabOffset = e.GetPosition(control);
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

        MoveDragPreview(e);
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
        MoveDragPreview(e);

        e.Pointer.Capture(this);
    }

    private void MoveDragPreview(PointerEventArgs e)
    {
        if (DragPreview.Parent is not Canvas canvas)
        {
            return;
        }

        var position = e.GetPosition(canvas);
        Canvas.SetLeft(DragPreview, position.X - _dragGrabOffset.X);
        Canvas.SetTop(DragPreview, position.Y - _dragGrabOffset.Y);
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

    private void CardMenu_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pickerAnchor = sender as Control;
    }

    private void CardMenu_Date_Click(object? sender, RoutedEventArgs e)
    {
        if (GetCardFromSender(sender) is not { } card)
        {
            return;
        }

        var anchor = _pickerAnchor;
        e.Handled = true;
        SchedulePickerOpen(() => OpenDatePicker(card, anchor, fromCardMenu: true));
    }

    private void CardMenu_Time_Click(object? sender, RoutedEventArgs e)
    {
        if (GetCardFromSender(sender) is not { } card)
        {
            return;
        }

        var anchor = _pickerAnchor;
        e.Handled = true;
        SchedulePickerOpen(() => OpenTimePicker(card, anchor, fromCardMenu: true));
    }

    private void CardDateBadge_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: CardViewModel card } anchor)
        {
            e.Handled = true;
            SchedulePickerOpen(() => OpenDatePicker(card, anchor, fromCardMenu: false));
        }
    }

    private void CardTimeBadge_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: CardViewModel card } anchor)
        {
            e.Handled = true;
            SchedulePickerOpen(() => OpenTimePicker(card, anchor, fromCardMenu: false));
        }
    }

    private static void SchedulePickerOpen(Action open)
    {
        Dispatcher.UIThread.Post(open, DispatcherPriority.Loaded);
    }

    private void DatePickerCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressDateSelectionChanged || _pickerCard is null)
        {
            return;
        }

        if (DatePickerCalendar.SelectedDate is DateTime selectedDate)
        {
            CommitDueDate(selectedDate);
        }
    }

    private void DatePickerPopup_Closed(object? sender, EventArgs e)
    {
        ResetDatePickerState();
    }

    private void CommitDueDate(DateTime selectedDate)
    {
        if (_pickerCard is null)
        {
            return;
        }

        _pickerCard.SetDueDate(selectedDate);
        CloseDatePickerPopup();
    }

    private void CloseDatePickerPopup()
    {
        DatePickerPopup.IsOpen = false;
    }

    private void ResetDatePickerState()
    {
        _pickerCard = null;
        _pickerAnchor = null;

        _suppressDateSelectionChanged = true;
        try
        {
            DatePickerCalendar.SelectedDate = null;
        }
        finally
        {
            _suppressDateSelectionChanged = false;
        }
    }

    private void BoardTimePicker_SelectedTimeChanged(object? sender, TimePickerSelectedValueChangedEventArgs e)
    {
        // Time is committed when the popup closes so the picker can be adjusted freely.
    }

    private void OpenDatePicker(CardViewModel card, Control? placementTarget, bool fromCardMenu)
    {
        if (DatePickerPopup.IsOpen)
        {
            DatePickerPopup.IsOpen = false;
        }

        _pickerCard = card;
        _pickerAlignRight = fromCardMenu;
        _pickerAnchor = placementTarget ?? FindPickerAnchor(card, fromCardMenu);

        _suppressDateSelectionChanged = true;
        try
        {
            DatePickerCalendar.SelectedDate = null;
            DatePickerCalendar.DisplayDate = card.DueDate ?? DateTime.Today;
        }
        finally
        {
            _suppressDateSelectionChanged = false;
        }

        if (!TryConfigurePickerPopup(DatePickerPopup))
        {
            ResetDatePickerState();
            return;
        }

        DatePickerPopup.IsOpen = true;
    }

    private void OpenTimePicker(CardViewModel card, Control? placementTarget, bool fromCardMenu)
    {
        _pickerCard = card;
        _pickerAlignRight = fromCardMenu;
        _pickerAnchor = placementTarget ?? FindPickerAnchor(card, fromCardMenu);
        BoardTimePicker.SelectedTime = card.DueTime ?? DateTime.Now.TimeOfDay;

        if (!TryConfigurePickerPopup(TimePickerPopup))
        {
            return;
        }

        TimePickerPopup.Closed -= TimePickerPopup_Closed;
        TimePickerPopup.Closed += TimePickerPopup_Closed;
        TimePickerPopup.IsOpen = true;
    }

    private bool TryConfigurePickerPopup(Popup popup)
    {
        var anchor = _pickerAnchor;
        if (anchor is null && _pickerCard is not null)
        {
            anchor = FindPickerAnchor(_pickerCard, _pickerAlignRight);
        }

        if (anchor is null || !anchor.IsAttachedToVisualTree())
        {
            return false;
        }

        var host = TopLevel.GetTopLevel(anchor) as Visual ?? this;
        var anchorWidth = Math.Max(anchor.Bounds.Width, 1);
        var anchorHeight = Math.Max(anchor.Bounds.Height, 1);
        var topLeft = anchor.TranslatePoint(new Point(0, 0), host);
        var bottomRight = anchor.TranslatePoint(new Point(anchorWidth, anchorHeight), host);
        if (topLeft is null || bottomRight is null)
        {
            return false;
        }

        var anchorRect = new Rect(topLeft.Value, bottomRight.Value);
        popup.PlacementTarget = host as Control ?? this;
        popup.PlacementRect = anchorRect;
        popup.Placement = _pickerAlignRight
            ? PlacementMode.BottomEdgeAlignedRight
            : PlacementMode.BottomEdgeAlignedLeft;
        popup.HorizontalOffset = 0;
        popup.VerticalOffset = 4;
        return true;
    }

    private Control? FindPickerAnchor(CardViewModel card, bool preferMenu)
    {
        if (preferMenu)
        {
            var menu = FindCardControl<Button>(card, button => button.Classes.Contains("cardMenu"));
            if (menu is not null)
            {
                return menu;
            }
        }
        else
        {
            var dateBadge = FindCardControl<Button>(card, button => button.Classes.Contains("dateBadge"));
            if (dateBadge is not null)
            {
                return dateBadge;
            }

            var timeBadge = FindCardControl<Button>(card, button => button.Classes.Contains("timeBadge"));
            if (timeBadge is not null)
            {
                return timeBadge;
            }
        }

        return FindCardControl<Border>(card, border => border.Classes.Contains("card"));
    }

    private TControl? FindCardControl<TControl>(CardViewModel card, Func<TControl, bool> predicate)
        where TControl : Control
    {
        return this.GetVisualDescendants()
            .OfType<TControl>()
            .FirstOrDefault(control => ReferenceEquals(control.DataContext, card) && predicate(control));
    }

    private void TimePickerPopup_Closed(object? sender, EventArgs e)
    {
        TimePickerPopup.Closed -= TimePickerPopup_Closed;

        if (_pickerCard is not null && BoardTimePicker.SelectedTime is TimeSpan time)
        {
            _pickerCard.SetDueTime(time);
        }

        _pickerCard = null;
    }

    private static CardViewModel? GetCardFromSender(object? sender)
    {
        if (sender is StyledElement { DataContext: CardViewModel directCard })
        {
            return directCard;
        }

        if (sender is not Visual visual)
        {
            return null;
        }

        foreach (var ancestor in visual.GetSelfAndVisualAncestors())
        {
            if (ancestor is StyledElement { DataContext: CardViewModel card })
            {
                return card;
            }
        }

        return null;
    }
}