# Interrogon

A native Windows desktop app for organizing and reviewing interview-prep questions — Theory Q&A, Work Experience, Code Review, Algorithms, and System Design — browsable by type, category, and tag, with search and favorites. Built for practicing technical interview answers against your own material rather than a generic flashcard deck.

It's a learning/portfolio project, not a finished product.

---

## Features

| Feature | Description |
|---|---|
| Drill-down browsing | Home screen shows the five question types as tiles; clicking one shows the categories present within it, clicking a category shows the matching questions — each level's tile counts reflect what's actually reachable |
| Tag facets | Sidebar lists every tag in the current scope with usage counts, sorted by frequency; selecting multiple tags narrows with AND semantics (a question must carry all selected tags), and the tag list itself re-scopes to only tags still co-occurring with the current selection |
| Search | A dedicated search dialog (opened from the sidebar) searches title, answer, and notes across the whole database; supports an exact-match toggle for whole-word matching, and inline autocomplete that completes the word you're typing if any question title contains it |
| Question detail view | Full markdown rendering with syntax-highlighted, copyable code blocks; adjustable text size (two zoom steps up from default); tags shown under the title; a Notes side panel for private annotations separate from the answer |
| Favorites & Recently Viewed | Star any question to pin it in Favorites; every opened question is tracked in Recently Viewed automatically |
| Add / Edit question | Two-column form — title, type, answer (markdown), and notes on one side, scrollable category/tag checklists on the other; new categories/tags are added directly into the checklist (checked by default, but can be unchecked) rather than force-included; at least one category is required, since categories are how questions stay reachable when browsing |
| Settings | Rename or delete categories and tags outside the edit flow; deleting a category is blocked if it would leave any question with zero categories (rather than silently creating an unreachable question) |
| Bulk import via AI agent | No in-app bulk-import UI by design — `IMPORT_INSTRUCTIONS.md` at the repo root documents a process for handing source material (docs, notes, transcripts) to an AI coding agent, which extracts individual questions, assigns Type/Category/Tags, checks for duplicates, and writes directly to the LiteDB file |
| Diagnostics | Unhandled errors are caught, logged, and shown as a dialog with an Open Log Folder action instead of silently crashing; a rolling log file (30-day retention) also lives at `%LOCALAPPDATA%\Interrogon\Logs`, reachable from the Settings screen |

---

## Tech Stack

| Layer | Technology |
|---|---|
| UI Framework | WinUI 3 with Windows App SDK |
| Language | C# on .NET 9 |
| MVVM | CommunityToolkit.Mvvm (source generators) |
| Syntax highlighting | ColorCode.WinUI |
| Local storage | LiteDB |
| Logging | Serilog (file sink), exceptions only — no per-action logging |
| Design | Fluent UI, Mica background, Windows 11 design language |

---

## Architecture

Three projects in one solution:

- **Interrogon.App** — the WinUI 3 desktop app. Views are XAML, logic lives in ViewModels.
- **Interrogon.Core** — class library with zero UI dependency: models (`Question`, `Category`, `Tag`, `QuestionType`), repository interfaces, and shared markdown-content parsing.
- **Interrogon.Services** — LiteDB-backed persistence implementing the interfaces defined in `Interrogon.Core`.
- **Interrogon.Installer** — a WiX Toolset project that packages a self-contained build of the app into one MSI installer. Lives outside the `.sln` (a different toolchain, not something you build/debug day to day) — see "Building the installer" below.

App data (`interrogon.db`) lives at `%LOCALAPPDATA%\Interrogon\` — the app is unpackaged, so no MSIX/`ApplicationData` persistence is used. Logs live alongside it in `%LOCALAPPDATA%\Interrogon\Logs\`, one rolling file per day, kept for 30 days.

---

## Getting Started

### Prerequisites

- Windows 10 version 1809 or later (Windows 11 recommended)
- Visual Studio 2022 (or later) with the **Windows application development** workload
- .NET 9 SDK
- Windows App SDK 1.6

### Build and run

```
git clone https://github.com/AndreasGkesos/Interrogon.git
cd Interrogon
# Open Interrogon.sln in Visual Studio
# Set Interrogon.App as the startup project
# Build and run (F5)
```

### Importing questions in bulk

The app has no bulk-import screen — for loading many questions at once from notes, transcripts, or documents, see [`IMPORT_INSTRUCTIONS.md`](IMPORT_INSTRUCTIONS.md). Drop source material into `ImportData/`, hand an AI coding agent the instructions in that file, and it extracts questions, assigns Type/Category/Tags, checks for duplicates against what's already in the database, and writes a report of what it did.

### Building the installer

For a standalone install with no .NET SDK or Visual Studio required on the target machine, run:

```
build-installer.bat
```

from the repo root. This publishes `Interrogon.App` as a self-contained win-x64 executable, then packages it into `Interrogon.Installer\bin\Release\Interrogon.Installer.msi`. The resulting MSI is fully self-contained (embeds its payload) and can be copied/run anywhere.

The installer is unsigned — Windows SmartScreen may warn on first run of the MSI; choose "More info" → "Run anyway" if you trust the source. Installing a newer version over an older one on the same machine upgrades in place (same install folder, question data untouched); running an older MSI over a newer install is blocked with a clear message instead of silently downgrading.

Run it again after changing app code to cut a new installer build. Bump `<Version>` in `Interrogon.App.csproj` before a real release — the installer reads it automatically, no need to touch `Product.wxs`.

---

## License

MIT
