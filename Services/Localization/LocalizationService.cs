using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using KanBan.Serialization;

namespace KanBan.Services.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    public const string English = "en";
    public const string Chinese = "zh-CN";

    public static LocalizationService Instance { get; } = new();

    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    private string _cultureName = English;
    private int _revision;

    private LocalizationService()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CultureName => _cultureName;

    public int Revision => _revision;

    public bool IsChinese => string.Equals(_cultureName, Chinese, StringComparison.Ordinal);

    public string this[string key]
    {
        get
        {
            if (_strings.TryGetValue(key, out var value))
            {
                return value;
            }

            return key;
        }
    }

    public static string Get(string key) => Instance[key];

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Instance[key], args);

    public void ApplyCulture(string? cultureName)
    {
        var normalized = ResolveCulture(cultureName);
        if (string.Equals(_cultureName, normalized, StringComparison.Ordinal) && _strings.Count > 0)
        {
            ApplyThreadCulture(normalized);
            return;
        }

        _cultureName = normalized;
        LoadStrings(normalized);
        ApplyThreadCulture(normalized);
        _revision++;
        OnPropertyChanged(nameof(Revision));
        OnPropertyChanged(nameof(CultureName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public static string ResolveCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return DetectSystemCulture();
        }

        return NormalizeCulture(cultureName);
    }

    public static string NormalizeCulture(string? cultureName) =>
        cultureName?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true
            ? Chinese
            : English;

    public static string DetectSystemCulture()
    {
        try
        {
            var ui = CultureInfo.CurrentUICulture;
            if (ui.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
            {
                return Chinese;
            }
        }
        catch (CultureNotFoundException)
        {
        }

        return English;
    }

    private void LoadStrings(string cultureName)
    {
        _strings.Clear();

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"KanBan.Assets.Localization.{cultureName}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null && !string.Equals(cultureName, English, StringComparison.Ordinal))
        {
            using var fallback = assembly.GetManifestResourceStream("KanBan.Assets.Localization.en.json");
            if (fallback is not null)
            {
                MergeJson(fallback);
            }

            return;
        }

        if (stream is null)
        {
            return;
        }

        MergeJson(stream);
    }

    private void MergeJson(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var parsed = JsonSerializer.Deserialize(json, KanBanJsonContext.Default.DictionaryStringString);
        if (parsed is null)
        {
            return;
        }

        foreach (var pair in parsed)
        {
            _strings[pair.Key] = pair.Value;
        }
    }

    private static void ApplyThreadCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
