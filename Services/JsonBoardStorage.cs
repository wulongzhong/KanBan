using System;
using System.IO;
using System.Text.Json;
using KanBan.Models;
using KanBan.Serialization;

namespace KanBan.Services;

public sealed class JsonBoardStorage
{
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
            var data = JsonSerializer.Deserialize(json, KanBanJsonContext.Default.KanBanData);
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
        var json = JsonSerializer.Serialize(data, KanBanJsonContext.Default.KanBanData);
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

    public static string GetBoardPath(string workspaceFolder)
    {
        if (string.IsNullOrWhiteSpace(workspaceFolder))
        {
            throw new ArgumentException("Workspace folder is required.", nameof(workspaceFolder));
        }

        return Path.Combine(workspaceFolder.Trim(), "default-board.json");
    }
}
