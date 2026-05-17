using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KanBan.Services;
using KanBan.ViewModels;

namespace KanBan.Views;

public partial class MainWindow : Window
{
    private const string CardDetailsPasteTunnelTag = "__KanBanCardDetailsPasteTunnel__";
    private const string NewCardComposerPasteTunnelTag = "__KanBanNewCardComposerPasteTunnel__";

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

    private bool _isBoardScrollSyncing;

    private const double LightboxMinZoom = 0.25;
    private const double LightboxMaxZoom = 8.0;
    private const double LightboxWheelFactor = 1.1;
    private double _lightboxZoom = 1;
    private Vector _lightboxPan;
    private bool _lightboxPanning;
    private Point _lightboxPanStart;
    private Vector _lightboxPanAtStart;
    private readonly ScaleTransform _lightboxScale = new(1, 1);
    private readonly TranslateTransform _lightboxTranslate = new();

    public MainWindow()
    {
        InitializeComponent();
        ImageLightboxImageHost.RenderTransform = new TransformGroup
        {
            Children = { _lightboxScale, _lightboxTranslate },
        };
        ImageLightboxImageHost.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        Opened += MainWindow_Opened;
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SetOwnerWindow(this);

        if (!await viewModel.EnsureWorkspaceConfiguredAsync())
        {
            Close();
        }
    }

    private const string BoardTitleKeyTunnelTag = "__KanBanBoardTitleKeyTunnel__";
    private const string LaneTitleKeyTunnelTag = "__KanBanLaneTitleKeyTunnel__";
    private const string SwimlaneTitleKeyTunnelTag = "__KanBanSwimlaneTitleKeyTunnel__";

    private void BoardTitleLabel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(BoardTitleLabel).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.BeginBoardTitleEdit();
        e.Handled = true;
        Dispatcher.UIThread.Post(() =>
        {
            BoardTitleTextBox.Focus();
            BoardTitleTextBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void BoardTitleTextBox_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox editor || Equals(editor.Tag, BoardTitleKeyTunnelTag))
        {
            return;
        }

        editor.Tag = BoardTitleKeyTunnelTag;
        editor.AddHandler(KeyDownEvent, BoardTitleEdit_KeyDown, RoutingStrategies.Tunnel);
    }

    private void BoardTitleEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return or Key.Escape))
        {
            return;
        }

        if (sender is TextBox editor)
        {
            FinishBoardTitleEdit(editor);
        }

        e.Handled = true;
    }

    private void BoardTitleEdit_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox editor ||
            !ReferenceEquals(editor, BoardTitleTextBox) ||
            DataContext is not MainWindowViewModel { IsBoardTitleEditing: true })
        {
            return;
        }

        FinishBoardTitleEdit(editor);
    }

    private void FinishBoardTitleEdit(TextBox editor)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsBoardTitleEditing)
        {
            return;
        }

        SyncBoardTitleFromEditor(editor, viewModel);
        viewModel.EndBoardTitleEdit();
    }

    private static void SyncBoardTitleFromEditor(TextBox editor, MainWindowViewModel viewModel)
    {
        var text = editor.Text ?? string.Empty;
        if (!string.Equals(viewModel.BoardTitle, text, StringComparison.Ordinal))
        {
            viewModel.BoardTitle = text;
        }
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is TextBox or Button or TextBlock)
        {
            return;
        }

        if (e.Source is Visual visual &&
            (visual.FindAncestorOfType<Button>() is not null ||
             visual.FindAncestorOfType<TextBox>() is not null))
        {
            return;
        }

        BeginMoveDrag(e);
    }

    private void ColumnHeaderScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isBoardScrollSyncing || e.OffsetDelta.X == 0)
        {
            return;
        }

        _isBoardScrollSyncing = true;
        BoardBodyScrollViewer.Offset = new Vector(ColumnHeaderScrollViewer.Offset.X, BoardBodyScrollViewer.Offset.Y);
        _isBoardScrollSyncing = false;
    }

    private void BoardBodyScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isBoardScrollSyncing)
        {
            return;
        }

        if (ColumnHeaderScrollViewer.Offset.X == BoardBodyScrollViewer.Offset.X)
        {
            return;
        }

        _isBoardScrollSyncing = true;
        ColumnHeaderScrollViewer.Offset = new Vector(BoardBodyScrollViewer.Offset.X, 0);
        _isBoardScrollSyncing = false;
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
        _pressedTitle = card.DragLabel;
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
        if (e.Source is Visual source &&
            source.GetSelfAndVisualAncestors().OfType<Image>().Any(image => image.Classes.Contains("cardPreview")))
        {
            return;
        }

        if (sender is Control { DataContext: CardViewModel card })
        {
            card.BeginEdit();
            e.Handled = true;
        }
    }

    private void CardImage_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is StyledElement { DataContext: CardImageViewModel image })
        {
            ShowImageLightbox(image);
            e.Handled = true;
        }
    }

    private void ShowImageLightbox(CardImageViewModel image)
    {
        ReleaseLightboxBitmap();
        ImageLightboxImage.Source = new Bitmap(image.AbsolutePath);
        ImageLightboxTitle.Text = System.IO.Path.GetFileName(image.AbsolutePath);
        ResetLightboxTransform();
        ImageLightbox.IsVisible = true;
        ImageLightbox.Focus();
    }

    private void CloseImageLightbox()
    {
        ImageLightbox.IsVisible = false;
        ReleaseLightboxBitmap();
        ImageLightboxTitle.Text = string.Empty;
        EndLightboxPan();
        ResetLightboxTransform();
    }

    private void ReleaseLightboxBitmap()
    {
        if (ImageLightboxImage.Source is Bitmap bitmap)
        {
            ImageLightboxImage.Source = null;
            bitmap.Dispose();
        }
    }

    private void ResetLightboxTransform()
    {
        _lightboxZoom = 1;
        _lightboxPan = default;
        ApplyLightboxTransform();
    }

    private void ApplyLightboxTransform()
    {
        _lightboxScale.ScaleX = _lightboxZoom;
        _lightboxScale.ScaleY = _lightboxZoom;
        _lightboxTranslate.X = _lightboxPan.X;
        _lightboxTranslate.Y = _lightboxPan.Y;
    }

    private void EndLightboxPan()
    {
        _lightboxPanning = false;
        ImageLightboxImageHost.Cursor = Cursor.Default;
    }

    private void ImageLightbox_ContentArea_WheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!ImageLightbox.IsVisible)
        {
            return;
        }

        var delta = e.Delta.Y;
        if (delta == 0)
        {
            return;
        }

        var factor = delta > 0 ? LightboxWheelFactor : 1 / LightboxWheelFactor;
        var zoom = Math.Clamp(_lightboxZoom * factor, LightboxMinZoom, LightboxMaxZoom);
        if (Math.Abs(zoom - _lightboxZoom) < double.Epsilon)
        {
            e.Handled = true;
            return;
        }

        _lightboxZoom = zoom;
        ApplyLightboxTransform();
        e.Handled = true;
    }

    private void ImageLightbox_Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ImageLightboxBackdrop).Properties.IsLeftButtonPressed)
        {
            return;
        }

        CloseImageLightbox();
        e.Handled = true;
    }

    private void ImageLightbox_Host_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ImageLightboxImageHost).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _lightboxPanning = true;
        _lightboxPanStart = e.GetPosition(ImageLightboxContentArea);
        _lightboxPanAtStart = _lightboxPan;
        e.Pointer.Capture(ImageLightboxImageHost);
        ImageLightboxImageHost.Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Handled = true;
    }

    private void ImageLightbox_Host_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_lightboxPanning)
        {
            return;
        }

        var now = e.GetPosition(ImageLightboxContentArea);
        var delta = now - _lightboxPanStart;
        _lightboxPan = _lightboxPanAtStart + new Vector(delta.X, delta.Y);
        ApplyLightboxTransform();
        e.Handled = true;
    }

    private void ImageLightbox_Host_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_lightboxPanning)
        {
            return;
        }

        EndLightboxPan();
        e.Handled = true;
    }

    private void ImageLightbox_Host_CaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndLightboxPan();
    }

    private void ImageLightbox_Close_Click(object? sender, RoutedEventArgs e)
    {
        CloseImageLightbox();
        e.Handled = true;
    }

    private void ImageLightbox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseImageLightbox();
            e.Handled = true;
        }
    }

    private void ImageLightbox_Image_DoubleTapped(object? sender, TappedEventArgs e)
    {
        ResetLightboxTransform();
        e.Handled = true;
    }

    private void CardDetailsEditor_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox editor)
        {
            return;
        }

        if (Equals(editor.Tag, CardDetailsPasteTunnelTag))
        {
            return;
        }

        editor.Tag = CardDetailsPasteTunnelTag;
        editor.AddHandler(KeyDownEvent, CardEdit_KeyDown, RoutingStrategies.Tunnel);
    }

    private async void CardEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox editor || sender is not Control { DataContext: CardViewModel card })
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            card.EndEdit();
            e.Handled = true;
            return;
        }

        if (TryHandleCardDetailsEnterKey(editor, e, card.EndEdit))
        {
            return;
        }

        if (e.Key != Key.V || (e.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (await TryPasteCardImagesFromClipboardAsync(card, viewModel))
        {
            e.Handled = true;
        }
    }

    private void NewCardComposerEditor_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox editor)
        {
            return;
        }

        if (Equals(editor.Tag, NewCardComposerPasteTunnelTag))
        {
            return;
        }

        editor.Tag = NewCardComposerPasteTunnelTag;
        editor.AddHandler(KeyDownEvent, NewCardComposerEdit_KeyDown, RoutingStrategies.Tunnel);
    }

    private async void NewCardComposerEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox editor || sender is not Control { DataContext: LaneViewModel lane })
        {
            return;
        }

        if (TryHandleCardDetailsEnterKey(editor, e, () => lane.CommitAddCardCommand.Execute(null)))
        {
            return;
        }

        if (e.Key != Key.V || (e.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (await TryPasteNewCardImagesFromClipboardAsync(lane, viewModel))
        {
            e.Handled = true;
        }
    }

    private static bool TryHandleCardDetailsEnterKey(TextBox editor, KeyEventArgs e, Action confirm)
    {
        if (e.Key != Key.Enter)
        {
            return false;
        }

        if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            InsertNewlineAtCaret(editor);
            e.Handled = true;
            return true;
        }

        confirm();
        e.Handled = true;
        return true;
    }

    private static void InsertNewlineAtCaret(TextBox editor)
    {
        var caret = Math.Clamp(editor.CaretIndex, 0, editor.Text?.Length ?? 0);
        var text = editor.Text ?? string.Empty;
        const string newline = "\n";
        editor.Text = text.Insert(caret, newline);
        editor.CaretIndex = caret + newline.Length;
    }

    private async void NewCardComposer_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: LaneViewModel lane })
        {
            return;
        }

        if (e.Key != Key.V || (e.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (await TryPasteNewCardImagesFromClipboardAsync(lane, viewModel))
        {
            e.Handled = true;
        }
    }

    private void NewCardComposer_DragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Control { DataContext: LaneViewModel })
        {
            return;
        }

        if (CardImageDropHelper.CanAccept(e.DataTransfer))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void NewCardComposer_Drop(object? sender, DragEventArgs e)
    {
        if (sender is not Control { DataContext: LaneViewModel lane })
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (!CardImageDropHelper.CanAccept(e.DataTransfer))
        {
            return;
        }

        ImportNewCardImages(e.DataTransfer, lane, viewModel);
        e.Handled = true;
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

    private void LaneTitleEditor_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox editor || Equals(editor.Tag, LaneTitleKeyTunnelTag))
        {
            return;
        }

        editor.Tag = LaneTitleKeyTunnelTag;
        editor.AddHandler(KeyDownEvent, LaneTitleEdit_KeyDown, RoutingStrategies.Tunnel);
    }

    private void LaneTitleEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return or Key.Escape))
        {
            return;
        }

        if (sender is TextBox editor && editor.DataContext is LaneViewModel lane)
        {
            FinishLaneTitleEdit(editor, lane);
        }

        e.Handled = true;
    }

    private void LaneTitleEdit_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox editor ||
            editor.DataContext is not LaneViewModel { IsEditing: true } lane)
        {
            return;
        }

        FinishLaneTitleEdit(editor, lane);
    }

    private static void FinishLaneTitleEdit(TextBox editor, LaneViewModel lane)
    {
        if (!lane.IsEditing)
        {
            return;
        }

        SyncLaneTitleFromEditor(editor, lane);
        lane.EndEdit();
    }

    private static void SyncLaneTitleFromEditor(TextBox editor, LaneViewModel lane)
    {
        var text = editor.Text ?? string.Empty;
        if (!string.Equals(lane.Title, text, StringComparison.Ordinal))
        {
            lane.Title = text;
        }
    }

    private void SwimlaneLabel_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: SwimlaneViewModel swimlane })
        {
            swimlane.BeginEdit();
            e.Handled = true;
        }
    }

    private void SwimlaneTitleEditor_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox editor || Equals(editor.Tag, SwimlaneTitleKeyTunnelTag))
        {
            return;
        }

        editor.Tag = SwimlaneTitleKeyTunnelTag;
        editor.AddHandler(KeyDownEvent, SwimlaneTitleEdit_KeyDown, RoutingStrategies.Tunnel);
    }

    private void SwimlaneTitleEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return or Key.Escape))
        {
            return;
        }

        if (sender is TextBox editor && editor.DataContext is SwimlaneViewModel swimlane)
        {
            FinishSwimlaneTitleEdit(editor, swimlane);
        }

        e.Handled = true;
    }

    private void SwimlaneTitleEdit_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox editor ||
            editor.DataContext is not SwimlaneViewModel { IsEditing: true } swimlane)
        {
            return;
        }

        FinishSwimlaneTitleEdit(editor, swimlane);
    }

    private static void FinishSwimlaneTitleEdit(TextBox editor, SwimlaneViewModel swimlane)
    {
        if (!swimlane.IsEditing)
        {
            return;
        }

        SyncSwimlaneTitleFromEditor(editor, swimlane);
        swimlane.EndEdit();
    }

    private static void SyncSwimlaneTitleFromEditor(TextBox editor, SwimlaneViewModel swimlane)
    {
        var text = editor.Text ?? string.Empty;
        if (!string.Equals(swimlane.Title, text, StringComparison.Ordinal))
        {
            swimlane.Title = text;
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
                .FirstOrDefault(lane => !lane.IsColumnHeader);

            if (targetLane is not null)
            {
                viewModel.MoveCardToLane(_draggingId, targetLane.Id, targetLane.SwimlaneId);
            }
        }
        else if (_dragKind == DragKind.Lane)
        {
            var targetLane = elements
                .Select(control => control.DataContext)
                .OfType<LaneViewModel>()
                .FirstOrDefault(lane => lane.IsColumnHeader && lane.Id != _draggingId);

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

    private void TimePickerConfirm_Click(object? sender, RoutedEventArgs e)
    {
        CommitDueTime();
        e.Handled = true;
    }

    private void TimePickerCancel_Click(object? sender, RoutedEventArgs e)
    {
        CloseTimePickerPopup();
        e.Handled = true;
    }

    private void CommitDueTime()
    {
        if (_pickerCard is null)
        {
            return;
        }

        if (BoardTimePicker.SelectedTime is TimeSpan time)
        {
            _pickerCard.SetDueTime(time);
        }

        CloseTimePickerPopup();
    }

    private void CloseTimePickerPopup()
    {
        TimePickerPopup.IsOpen = false;
    }

    private void ResetTimePickerState()
    {
        _pickerCard = null;
        _pickerAnchor = null;
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
        if (TimePickerPopup.IsOpen)
        {
            TimePickerPopup.IsOpen = false;
        }

        _pickerCard = card;
        _pickerAlignRight = fromCardMenu;
        _pickerAnchor = placementTarget ?? FindPickerAnchor(card, fromCardMenu);
        BoardTimePicker.SelectedTime = card.DueTime ?? DateTime.Now.TimeOfDay;

        if (!TryConfigurePickerPopup(TimePickerPopup))
        {
            ResetTimePickerState();
            return;
        }

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
        ResetTimePickerState();
    }

    private void Card_DragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Border { DataContext: CardViewModel })
        {
            return;
        }

        if (CardImageDropHelper.CanAccept(e.DataTransfer))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Card_Drop(object? sender, DragEventArgs e)
    {
        if (sender is not Border { DataContext: CardViewModel card })
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (!CardImageDropHelper.CanAccept(e.DataTransfer))
        {
            return;
        }

        ImportImages(e.DataTransfer, card, viewModel);
        e.Handled = true;
    }

    private async void Card_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Border { DataContext: CardViewModel card })
        {
            return;
        }

        if (e.Key == Key.Escape && card.IsEditing)
        {
            card.EndEdit();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.V || (e.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (await TryPasteCardImagesFromClipboardAsync(card, viewModel))
        {
            e.Handled = true;
        }
    }

    private async Task<bool> TryPasteCardImagesFromClipboardAsync(CardViewModel card, MainWindowViewModel viewModel)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        var dataTransfer = await clipboard.TryGetDataAsync();
        if (dataTransfer is null || !CardImageDropHelper.CanAccept(dataTransfer))
        {
            return false;
        }

        await ImportImagesAsync(dataTransfer, card, viewModel);
        return true;
    }

    private async Task<bool> TryPasteNewCardImagesFromClipboardAsync(LaneViewModel lane, MainWindowViewModel viewModel)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        var dataTransfer = await clipboard.TryGetDataAsync();
        if (dataTransfer is null || !CardImageDropHelper.CanAccept(dataTransfer))
        {
            return false;
        }

        await ImportNewCardImagesAsync(dataTransfer, lane, viewModel);
        return true;
    }

    private static void ImportImages(
        IDataTransfer dataTransfer,
        CardViewModel card,
        MainWindowViewModel viewModel)
    {
        var attachments = viewModel.Attachments;
        var files = dataTransfer.TryGetFiles();
        if (files is not null)
        {
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                if (CardAttachmentService.IsImageFile(path))
                {
                    card.AddImageFromFile(attachments, path);
                }
            }

            return;
        }

        var bitmap = dataTransfer.TryGetBitmap();
        if (bitmap is not null)
        {
            card.AddImageFromBitmap(attachments, bitmap);
        }
    }

    private static async Task ImportImagesAsync(
        IAsyncDataTransfer dataTransfer,
        CardViewModel card,
        MainWindowViewModel viewModel)
    {
        var attachments = viewModel.Attachments;
        var files = await dataTransfer.TryGetFilesAsync();
        if (files is not null)
        {
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                if (CardAttachmentService.IsImageFile(path))
                {
                    card.AddImageFromFile(attachments, path);
                }
            }

            return;
        }

        var bitmap = await dataTransfer.TryGetBitmapAsync();
        if (bitmap is not null)
        {
            card.AddImageFromBitmap(attachments, bitmap);
        }
    }

    private static void ImportNewCardImages(
        IDataTransfer dataTransfer,
        LaneViewModel lane,
        MainWindowViewModel viewModel)
    {
        var attachments = viewModel.Attachments;
        var files = dataTransfer.TryGetFiles();
        if (files is not null)
        {
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                if (CardAttachmentService.IsImageFile(path))
                {
                    lane.AddDraftImageFromFile(attachments, path);
                }
            }

            return;
        }

        var bitmap = dataTransfer.TryGetBitmap();
        if (bitmap is not null)
        {
            lane.AddDraftImageFromBitmap(attachments, bitmap);
        }
    }

    private static async Task ImportNewCardImagesAsync(
        IAsyncDataTransfer dataTransfer,
        LaneViewModel lane,
        MainWindowViewModel viewModel)
    {
        var attachments = viewModel.Attachments;
        var files = await dataTransfer.TryGetFilesAsync();
        if (files is not null)
        {
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                if (CardAttachmentService.IsImageFile(path))
                {
                    lane.AddDraftImageFromFile(attachments, path);
                }
            }

            return;
        }

        var bitmap = await dataTransfer.TryGetBitmapAsync();
        if (bitmap is not null)
        {
            lane.AddDraftImageFromBitmap(attachments, bitmap);
        }
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