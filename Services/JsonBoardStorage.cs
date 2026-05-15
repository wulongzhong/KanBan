using System;
using System.IO;
using System.Text.Json;
using KanBan.Models;

namespace KanBan.Services;

public sealed class JsonBoardStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public JsonBoardStorage()
        : this(GetBoardPath())
    {
    }

    public JsonBoardStorage(string boardPath)
    {
        BoardPath = boardPath;
    }

    public string BoardPath { get; }

    public KanBanData LoadOrCreate()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BoardPath)!);

        if (!File.Exists(BoardPath))
        {
            var initialData = new KanBanData();
            Save(initialData);
            return initialData;
        }

        try
        {
            var json = File.ReadAllText(BoardPath);
            var data = JsonSerializer.Deserialize<KanBanData>(json, SerializerOptions);
            if (data?.Board is null)
            {
                return new KanBanData();
            }

            KanbanBoardMigration.NormalizeLegacyFields(data.Board);
            return data;
        }
        catch (JsonException)
        {
            var brokenPath = $"{BoardPath}.broken-{DateTimeOffset.Now:yyyyMMddHHmmss}.json";
            File.Copy(BoardPath, brokenPath, overwrite: true);

            var replacement = new KanBanData();
            Save(replacement);
            return replacement;
        }
    }

    public void Save(KanBanData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BoardPath)!);
        data.Board.UpdatedAt = DateTimeOffset.UtcNow;

        var tempPath = $"{BoardPath}.tmp";
        var json = JsonSerializer.Serialize(data, SerializerOptions);
        File.WriteAllText(tempPath, json);

        if (File.Exists(BoardPath))
        {
            File.Move(tempPath, BoardPath, overwrite: true);
        }
        else
        {
            File.Move(tempPath, BoardPath);
        }
    }

    public static string GetBoardPath(string? workspaceFolder = null)
    {
        if (!string.IsNullOrWhiteSpace(workspaceFolder))
        {
            return Path.Combine(workspaceFolder.Trim(), "default-board.json");
        }

        return GetDefaultBoardPath();
    }

    public static string GetDefaultBoardPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "KanBan", "boards", "default-board.json");
    }
}
