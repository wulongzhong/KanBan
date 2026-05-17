using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using KanBan.Services.Localization;

namespace KanBan.Markup;

public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding
        {
            Path = $"[{Key}]",
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
        };
    }
}
