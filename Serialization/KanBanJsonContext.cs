using System.Collections.Generic;
using System.Text.Json.Serialization;
using KanBan.Models;
using KanBan.Services;

namespace KanBan.Serialization;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(AppPreferences))]
[JsonSerializable(typeof(KanBanData))]
[JsonSerializable(typeof(KanbanBoard))]
[JsonSerializable(typeof(KanbanSwimlane))]
[JsonSerializable(typeof(KanbanLane))]
[JsonSerializable(typeof(KanbanCard))]
[JsonSerializable(typeof(BoardSettings))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class KanBanJsonContext : JsonSerializerContext;
