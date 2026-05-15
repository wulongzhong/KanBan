namespace KanBan.ViewModels;

public sealed class CardImageViewModel
{
    public CardImageViewModel(string relativePath, string absolutePath)
    {
        RelativePath = relativePath;
        AbsolutePath = absolutePath;
    }

    public string RelativePath { get; }

    public string AbsolutePath { get; }
}
