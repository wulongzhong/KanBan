using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using KanBan.Services.Localization;

namespace KanBan.Services;

/// <summary>
/// Windows system tray: minimize/close hides the main window; restore from tray icon or menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    public static TrayIconService Instance { get; } = new();

    private TrayIcon? _trayIcon;
    private NativeMenuItem? _showMenuItem;
    private NativeMenuItem? _exitMenuItem;
    private Window? _window;
    private bool _enabled;
    private bool _isExiting;

    public static bool IsSupported => OperatingSystem.IsWindows();

    private TrayIconService()
    {
    }

    public void Enable(Window window)
    {
        if (!IsSupported || _enabled)
        {
            return;
        }

        _enabled = true;
        _window = window;
        EnsureTrayIcon();
        RefreshLocalizedText();

        window.Closing += OnWindowClosing;
        window.PropertyChanged += OnWindowPropertyChanged;
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    public void HideToTray()
    {
        if (!_enabled || _window is null)
        {
            return;
        }

        _window.Hide();
        _window.ShowInTaskbar = false;
    }

    public void ShowMainWindow()
    {
        if (!_enabled || _window is null)
        {
            return;
        }

        _window.ShowInTaskbar = true;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void ExitApplication()
    {
        _isExiting = true;
        _trayIcon?.Dispose();
        _trayIcon = null;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public void Dispose()
    {
        if (_window is not null)
        {
            _window.Closing -= OnWindowClosing;
            _window.PropertyChanged -= OnWindowPropertyChanged;
        }

        LocalizationService.Instance.PropertyChanged -= OnLocalizationChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null || Application.Current is not { } app)
        {
            return;
        }

        using var iconStream = AssetLoader.Open(new Uri("avares://KanBan/Assets/avalonia-logo.ico"));
        var icon = new WindowIcon(iconStream);

        _showMenuItem = new NativeMenuItem
        {
            Command = new RelayCommand(ShowMainWindow),
        };
        _exitMenuItem = new NativeMenuItem
        {
            Command = new RelayCommand(ExitApplication),
        };

        var menu = new NativeMenu
        {
            Items =
            {
                _showMenuItem,
                new NativeMenuItemSeparator(),
                _exitMenuItem,
            },
        };

        _trayIcon = new TrayIcon
        {
            Icon = icon,
            Menu = menu,
            Command = new RelayCommand(ShowMainWindow),
            IsVisible = true,
        };

        var trayIcons = TrayIcon.GetIcons(app) ?? new TrayIcons();
        trayIcons.Add(_trayIcon);
        TrayIcon.SetIcons(app, trayIcons);
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty
            && e.NewValue is WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        RefreshLocalizedText();

    private void RefreshLocalizedText()
    {
        if (_trayIcon is null)
        {
            return;
        }

        var tooltip = LocalizationService.Get(UiKeys.AppTitle);
        _trayIcon.ToolTipText = tooltip;
        if (_showMenuItem is not null)
        {
            _showMenuItem.Header = LocalizationService.Get(UiKeys.TrayShow);
        }

        if (_exitMenuItem is not null)
        {
            _exitMenuItem.Header = LocalizationService.Get(UiKeys.TrayExit);
        }
    }
}
