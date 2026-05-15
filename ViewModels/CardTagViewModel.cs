namespace KanBan.ViewModels;

public sealed class CardTagViewModel(string label, string background, string foreground)
{
    public string Label { get; } = label;

    public string Background { get; } = background;

    public string Foreground { get; } = foreground;
}
