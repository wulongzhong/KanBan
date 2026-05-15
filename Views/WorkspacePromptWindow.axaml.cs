using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KanBan.Views;

public partial class WorkspacePromptWindow : Window
{
    public WorkspacePromptWindow()
    {
        InitializeComponent();
    }

    public WorkspacePromptResult Result { get; private set; } = WorkspacePromptResult.Exit;

    private void Select_Click(object? sender, RoutedEventArgs e) =>
        Complete(WorkspacePromptResult.SelectFolder);

    private void Exit_Click(object? sender, RoutedEventArgs e) =>
        Complete(WorkspacePromptResult.Exit);

    private void Complete(WorkspacePromptResult result)
    {
        Result = result;
        Close();
    }
}

public enum WorkspacePromptResult
{
    SelectFolder,
    Exit,
}
