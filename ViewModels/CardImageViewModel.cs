using System.Windows.Input;

namespace KanBan.ViewModels;

public sealed class CardImageViewModel
{
    public CardImageViewModel(string relativePath, string absolutePath, ICommand removeCommand)
    {
        RelativePath = relativePath;
        AbsolutePath = absolutePath;
        RemoveCommand = removeCommand;
    }

    public string RelativePath { get; }

    public string AbsolutePath { get; }

    public ICommand RemoveCommand { get; }
}
