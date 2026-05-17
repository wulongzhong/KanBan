using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace KanBan.Views.Controls;

public partial class InlineRenameHost : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<InlineRenameHost, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string?> DisplayTextProperty =
        AvaloniaProperty.Register<InlineRenameHost, string?>(nameof(DisplayText));

    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<InlineRenameHost, bool>(nameof(IsEditing));

    public static readonly StyledProperty<double> MinHitWidthProperty =
        AvaloniaProperty.Register<InlineRenameHost, double>(nameof(MinHitWidth), 72);

    public static readonly StyledProperty<double> MaxHitWidthProperty =
        AvaloniaProperty.Register<InlineRenameHost, double>(nameof(MaxHitWidth), 0);

    public static readonly StyledProperty<bool> FillAvailableSpaceProperty =
        AvaloniaProperty.Register<InlineRenameHost, bool>(nameof(FillAvailableSpace));

    public static readonly StyledProperty<string> DisplayClassesProperty =
        AvaloniaProperty.Register<InlineRenameHost, string>(nameof(DisplayClasses), string.Empty);

    public static readonly StyledProperty<string> EditorClassesProperty =
        AvaloniaProperty.Register<InlineRenameHost, string>(nameof(EditorClasses), string.Empty);

    public static readonly StyledProperty<bool> EnableClickToEditProperty =
        AvaloniaProperty.Register<InlineRenameHost, bool>(nameof(EnableClickToEdit));

    private double? _lockedHostWidth;

    public InlineRenameHost()
    {
        InitializeComponent();
        UpdateDisplayLabel();
        ApplyHostLayout();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? DisplayText
    {
        get => GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    public double MinHitWidth
    {
        get => GetValue(MinHitWidthProperty);
        set => SetValue(MinHitWidthProperty, value);
    }

    /// <summary>0 = no maximum width cap.</summary>
    public double MaxHitWidth
    {
        get => GetValue(MaxHitWidthProperty);
        set => SetValue(MaxHitWidthProperty, value);
    }

    public bool FillAvailableSpace
    {
        get => GetValue(FillAvailableSpaceProperty);
        set => SetValue(FillAvailableSpaceProperty, value);
    }

    public string DisplayClasses
    {
        get => GetValue(DisplayClassesProperty);
        set => SetValue(DisplayClassesProperty, value);
    }

    public string EditorClasses
    {
        get => GetValue(EditorClassesProperty);
        set => SetValue(EditorClassesProperty, value);
    }

    public bool EnableClickToEdit
    {
        get => GetValue(EnableClickToEditProperty);
        set => SetValue(EnableClickToEditProperty, value);
    }

    public TextBox EditorControl => Editor;

    public event EventHandler? BeginEditRequested;
    public event EventHandler<RoutedEventArgs>? EditorLoaded;
    public event EventHandler<RoutedEventArgs>? EditorLostFocus;
    public event EventHandler<KeyEventArgs>? EditorKeyDown;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty || change.Property == DisplayTextProperty)
        {
            UpdateDisplayLabel();
        }

        if (change.Property == DisplayClassesProperty || change.Property == EditorClassesProperty)
        {
            ApplyClasses();
        }

        if (change.Property == MinHitWidthProperty ||
            change.Property == MaxHitWidthProperty ||
            change.Property == FillAvailableSpaceProperty)
        {
            ApplyHostLayout();
        }

        if (change.Property == IsEditingProperty)
        {
            if (IsEditing)
            {
                BeginEditing();
            }
            else
            {
                EndEditing();
            }
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ApplyClasses();
        ApplyHostLayout();
        EnsureEditorHandlers();
    }

    private void EnsureEditorHandlers()
    {
        if (Equals(Editor.Tag, EditorHandlersAttachedTag))
        {
            return;
        }

        Editor.Tag = EditorHandlersAttachedTag;
        Editor.AddHandler(KeyDownEvent, Editor_KeyDown, RoutingStrategies.Tunnel);
    }

    private const string EditorHandlersAttachedTag = "__KanBanInlineRenameEditorHandlers__";

    private void ApplyClasses()
    {
        DisplayLabel.Classes.Clear();
        foreach (var token in DisplayClasses.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            DisplayLabel.Classes.Add(token);
        }

        Editor.Classes.Clear();
        foreach (var token in EditorClasses.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Editor.Classes.Add(token);
        }
    }

    private void ApplyHostLayout()
    {
        if (FillAvailableSpace)
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            HitHost.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            HitHost.MinWidth = 0;
            HitHost.MaxWidth = double.PositiveInfinity;
            HitHost.Width = double.NaN;
            return;
        }

        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        HitHost.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        HitHost.MinWidth = MinHitWidth;
        HitHost.MaxWidth = MaxHitWidth > 0 ? MaxHitWidth : double.PositiveInfinity;
        if (_lockedHostWidth is null)
        {
            HitHost.Width = double.NaN;
        }
    }

    private void UpdateDisplayLabel()
    {
        DisplayLabel.Text = DisplayText ?? Text;
    }

    private void BeginEditing()
    {
        if (!FillAvailableSpace)
        {
            var measured = Bounds.Width;
            if (measured <= 0)
            {
                measured = HitHost.DesiredSize.Width;
            }

            _lockedHostWidth = ResolveHostWidth(measured);
            HitHost.Width = _lockedHostWidth.Value;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Editor.Focus();
            Editor.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void EndEditing()
    {
        _lockedHostWidth = null;
        if (!FillAvailableSpace)
        {
            HitHost.Width = double.NaN;
        }

        InvalidateMeasure();
    }

    private double ResolveHostWidth(double measuredWidth)
    {
        var width = Math.Max(measuredWidth, MinHitWidth);
        if (MaxHitWidth > 0)
        {
            width = Math.Min(width, MaxHitWidth);
        }

        return width;
    }

    private void HitHost_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!EnableClickToEdit || IsEditing)
        {
            return;
        }

        if (!e.GetCurrentPoint(HitHost).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginEditRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void Editor_Loaded(object? sender, RoutedEventArgs e)
    {
        EnsureEditorHandlers();
        EditorLoaded?.Invoke(Editor, e);
    }

    private void Editor_LostFocus(object? sender, RoutedEventArgs e) =>
        EditorLostFocus?.Invoke(Editor, e);

    private void Editor_KeyDown(object? sender, KeyEventArgs e) =>
        EditorKeyDown?.Invoke(Editor, e);
}
