# KanBan

A cross-platform desktop kanban board for **personal** task and project tracking. Built with **.NET 9** and **Avalonia 12**, it keeps everything in a local workspace folder—no accounts, no server, no real-time collaboration.

The layout is inspired by Jira-style boards (columns, swimlanes, tags), but the app is meant for **one person on one machine** (or your own folders you control).

Runs on **Windows** (MSI installer available), **macOS**, and **Linux**.

---

## What it is (and isn’t)

| | |
|---|---|
| **For** | Solo planning, side projects, personal todos, visualizing your own work |
| **Not for** | Shared team boards, multi-user editing, permissions, or online sync |

Swimlanes exist so you can split work **for yourself** (e.g. “Work” vs “Personal”), not to coordinate a team.

---

## Features

| Feature | Description |
|---------|-------------|
| Board & swimlanes | Column + swimlane grid; collapse columns, reorder lists |
| Cards | Description text, `#tag` highlighting, due date/time, image attachments (drag-drop or Ctrl+V) |
| Search | Filter cards by text and dates |
| Archive | Sidebar for archived cards; restore or delete; configurable max size |
| Column options | WIP limits, sorting (title, date, tags, etc.) |
| Local-first storage | Board JSON and attachments live in a folder you choose |
| UI language | English and Chinese; follows system language until you pick one in Settings |

---

## Quick start

### Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download)

### Run from source

```powershell
git clone <your-repo-url>
cd KanBan

dotnet build KanBan.sln
dotnet run --project KanBan.csproj
```

### First launch

1. Choose a **workspace folder**. The app stores `board.json` and card images there.
2. The main board stays disabled until a workspace is set. You can change it anytime under **Settings → Change workspace folder**.
3. If there is no board file yet, a sample board with example columns and cards is created automatically.

---

## Using the app

### Board

- **Toolbar**: Archive, Settings, add swimlane, create list; search and card count in the center.
- **Column headers**: Double-click to rename; use `⋮` to collapse, move left/right, sort, or delete the list.
- **Swimlanes**: One row per swimlane—collapse, rename, reorder, or delete (at least one swimlane must remain).
- **Cards**: Double-click to edit; `⋮` for move, archive, delete, date/time; drop images or paste screenshots while editing.
- **New card**: **+ Add card** at the bottom of a column.

### Archive

Open **Archive** in the toolbar. **Restore** puts a card back on the board; **Delete** removes it permanently.

### Settings

| Option | Description |
|--------|-------------|
| Language | English or 中文; uses system language until you choose one |
| Show relative dates | Show labels like “Today” or “3 days later” on cards |
| Prepend new cards | Add new cards to the top of a column instead of the bottom |
| Maximum archive size | How many archived cards to keep |
| Date format | Display format (e.g. `yyyy-MM-dd`) |
| Reset sample board | Replace the current board with the built-in sample (destructive) |

Preferences are stored in `%AppData%\KanBan\preferences.json`. To follow the system language again, remove the `uiLanguage` field and restart.

---

## Where your data lives

```
<workspace>/
├── board.json       # Board layout, cards, settings, archive
└── attachments/     # Card images (per card id)
```

Data is plain JSON (`System.Text.Json` source generation). Edits are saved to disk as you work. Back up the workspace folder like any other personal files—copy it, zip it, or put it in your own cloud drive if you want copies on another device (avoid editing the same folder from two machines at once).

---

## Tech stack

| | |
|---|---|
| .NET | 9.0 |
| Avalonia UI | 12.x, Fluent theme |
| CommunityToolkit.Mvvm | MVVM, commands |
| Serialization | `KanBanJsonContext` (source-generated) |

**MVVM layout**: `Views` (AXAML) ↔ `ViewModels` ↔ `Models` / `Services`.

---

## Project layout

```
KanBan/
├── Assets/Localization/     # UI strings (en, zh-CN)
├── Models/                  # Board, lanes, cards, swimlanes
├── ViewModels/
├── Views/
├── Services/                # Storage, attachments, preferences, i18n
├── Serialization/
├── Themes/                  # Jira-inspired styles
├── Markup/                  # {loc:Loc} markup extension
├── tests/KanBan.LogicTests/
├── installer/               # WiX MSI (Windows)
└── scripts/build-installer.ps1
```

---

## Development

### Logic tests

```powershell
dotnet run --project tests/KanBan.LogicTests/KanBan.LogicTests.csproj
```

### Add UI strings

1. Add the same key to `Assets/Localization/en.json` and `zh-CN.json`.
2. In XAML: `{loc:Loc Key=Your.Key}`; in C#: `LocalizationService.Get("Your.Key")`.

---

## Release (Windows MSI)

Requires **.NET 9 SDK** and **WiX Toolset 6** (the script can install WiX for you).

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

```powershell
.\scripts\build-installer.ps1 -Version 1.0.1 -Runtime win-x64
.\scripts\build-installer.ps1 -Runtime win-arm64
```

Output: `dist/installer/`.

Other platforms:

```powershell
dotnet publish KanBan.csproj -c Release -r osx-arm64 --self-contained
dotnet publish KanBan.csproj -c Release -r linux-x64 --self-contained
```

---

## Notes

- **Personal, local-first**: No built-in sync. Your workspace is yours; optional cloud folders are a backup strategy, not collaboration.
- **Board content vs UI language**: Column names and card text are your data—they do not change when you switch English/Chinese in Settings.
- **Release builds** use `PublishTrimmed` to reduce size.

---

## License

No open-source license is declared in this repository. Contact the maintainer before redistributing or modifying for distribution.
