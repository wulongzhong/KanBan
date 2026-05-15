using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KanBan.Services;
using KanBan.ViewModels;
using KanBan.Views;

namespace KanBan;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var preferences = AppPreferences.Load();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(preferences),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}