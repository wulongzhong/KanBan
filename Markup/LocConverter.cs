using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KanBan.Services.Localization;

namespace KanBan.Markup;

public sealed class LocConverter : IValueConverter
{
    public static LocConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = parameter as string;
        return string.IsNullOrEmpty(key) ? string.Empty : LocalizationService.Get(key);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
