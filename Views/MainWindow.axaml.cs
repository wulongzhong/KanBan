using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KanBan.ViewModels;

namespace KanBan.Views;

public partial class MainWindow : Window
{
    private static readonly DataFormat<string> CardDragFormat = DataFormat.CreateInProcessFormat<string>("KanBan.Card");
    private static readonly DataFormat<string> LaneDragFormat = DataFormat.CreateInProcessFormat<string>("KanBan.Lane");
    private Point _dragStart;
    private string? _pressedCardId;
    private string? _pressedLaneId;
    private PointerPressedEventArgs? _pressedEventArgs;

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

        _pressedCardId = card.Id;
        _pressedLaneId = null;
        _pressedEventArgs = e;
        _dragStart = e.GetPosition(control);
    }

    private async void Card_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedCardId is null || sender is not Control control)
        {
            return;
        }

        var point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed || !HasDragThreshold(e.GetPosition(control)))
        {
            return;
        }

        var cardId = _pressedCardId;
        _pressedCardId = null;

        if (_pressedEventArgs is null)
        {
            return;
        }

        var data = CreateDragData(CardDragFormat, cardId);
        await DragDrop.DoDragDropAsync(_pressedEventArgs, data, DragDropEffects.Move);
        _pressedEventArgs = null;
    }

    private void Card_DragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(CardDragFormat))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void Card_Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not Control { DataContext: CardViewModel targetCard } ||
            e.DataTransfer.TryGetValue(CardDragFormat) is not string cardId)
        {
            return;
        }

        viewModel.MoveCardBefore(cardId, targetCard.Id);
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

        _pressedLaneId = lane.Id;
        _pressedCardId = null;
        _pressedEventArgs = e;
        _dragStart = e.GetPosition(control);
    }

    private async void LaneHeader_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedLaneId is null || sender is not Control control)
        {
            return;
        }

        var point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed || !HasDragThreshold(e.GetPosition(control)))
        {
            return;
        }

        var laneId = _pressedLaneId;
        _pressedLaneId = null;

        if (_pressedEventArgs is null)
        {
            return;
        }

        var data = CreateDragData(LaneDragFormat, laneId);
        await DragDrop.DoDragDropAsync(_pressedEventArgs, data, DragDropEffects.Move);
        _pressedEventArgs = null;
    }

    private void Lane_DragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(CardDragFormat) || e.DataTransfer.Contains(LaneDragFormat))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void Lane_Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not Control { DataContext: LaneViewModel targetLane })
        {
            return;
        }

        if (e.DataTransfer.TryGetValue(CardDragFormat) is string cardId)
        {
            viewModel.MoveCardToLane(cardId, targetLane.Id);
            e.Handled = true;
            return;
        }

        if (e.DataTransfer.TryGetValue(LaneDragFormat) is string laneId)
        {
            viewModel.MoveLaneBefore(laneId, targetLane.Id);
            e.Handled = true;
        }
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

    private static DataTransfer CreateDragData(DataFormat<string> format, string value)
    {
        var item = new DataTransferItem();
        item.Set(format, value);

        var transfer = new DataTransfer();
        transfer.Add(item);
        return transfer;
    }
}