# DM Companion

[English](README.md) · [Español](README.es.md)

An offline Android tablet app (plus a Windows desktop host) that helps a Dungeon Master run **D&D 5e (2024)** sessions: campaign notes, a map with tokens, combat and initiative, an offline rules glossary, ambient sound and a players' view on a TV.

<!-- screenshots: TODO -->

## Why

I run my games from a tablet at the table and keep my campaign in an [Obsidian](https://obsidian.md) vault. I wanted one app that reads that vault directly and covers what I actually use during a session, without the weight of a VTT:

- **Offline first.** No accounts, no cloud, no telemetry. The app works on a folder of Markdown files.
- **The vault is the database.** Notes, characters and maps live as plain files (Markdown + YAML frontmatter, JSON sidecar state next to each map), so Obsidian and the app can both edit them.
- **Sync is not the app's job.** The vault is kept in sync between the PC and the tablet by [Syncthing](https://syncthing.net); the app only detects and surfaces sync conflicts.
- **Built for a tablet and a stylus**: big touch targets, pen gestures, low-light friendly theme.

## Features

- **Vault of notes:** Obsidian-style Markdown with YAML frontmatter, wiki links, character sheets, creature notes, broken-link and sync-conflict handling.
- **Map with tokens:** Konva-based canvas, grid, drawing and measuring, undo/redo, tokens for PCs, allies and enemies, current HP on tokens.
- **Game systems:** pluggable systems (D&D 5e, a generic one and a Cosmere preview) that define stats, HP rules and combat rules.
- **Combat and initiative:** turn strip, rounds, damage and healing, conditions with durations, death saves, combat log.
- **Fog of war and players' view:** fog zones (reveal, trace, erase), opaque on the TV and translucent for the DM.
- **Second screen on a TV:** a small local HTTP/SSE server with a user-chosen code; the TV shows a players-safe view (hidden tokens, aliases, enemy health by state, fixed framing), character sheets, images, glossary entries and manual pages.
- **Glossary (SRD 5.2.1 in Spanish):** rules, conditions, creatures, spells, magic items, equipment and character options, searchable in Spanish or English, copy an entry to the vault, add a creature to the map.
- **PDF manuals:** pdf.js viewer with range loading, zoom, outline, text search, bookmarks stored next to the PDF, "show on TV" for a page or a cropped area.
- **Ambient sound:** background tracks and sound effects with per-effect volume and repeat mode, fades, scenes, a field in note frontmatter to start a scene.
- **Desktop version:** the same screens as a self-contained Windows executable.

The UI language of the app and the bundled rules content is Spanish.

## Architecture

The solution (`DmCompanion.slnx`) separates rules and domain logic from presentation and from the platform, so almost everything runs and is tested without a device:

| Project | What it is |
|---|---|
| `src/DmCompanion.Core` | Pure .NET domain logic: vault model, maps, game systems, combat, audio scenes, glossary and manuals models, TV state. No UI or I/O dependencies. |
| `src/DmCompanion.Infrastructure` | File-system access to the vault and the file watcher. |
| `src/DmCompanion.UI` | Design system: CSS tokens and Razor components (with a `/styleguide` page). |
| `src/DmCompanion.Screens` | Razor screens and JS interop shared by every host. |
| `src/DmCompanion.App` | .NET MAUI Android app (Blazor Hybrid): only the shell and the platform bridge (audio, files, TV server). |
| `src/DmCompanion.DevHost` | Blazor host to run the screens in a browser during development. |
| `src/DmCompanion.Desktop` | Windows desktop host (Blazor Server that opens the browser), published as a self-contained `.exe`. |
| `tools/DmCompanion.SrdImport` | Tool that turns the compendio-srd data into the glossary JSON packs. |
| `tests/*` | xUnit test projects for each of the above, plus JS tests in `tests/js`. |
| `samples/vault-demo` | Demo vault used by tests and manual checks. |

Why this split: the MAUI project is the slowest to build and the hardest to test, so it holds nothing but platform glue. All screens live in a Razor class library that three hosts (Android, browser, desktop) reuse, which makes the browser DevHost a fast feedback loop and lets the desktop version come almost for free.

## Stack

- .NET 10, .NET MAUI and Blazor Hybrid (`net10.0-android`, Android 11 or later), Blazor Server for the DevHost and desktop hosts.
- Konva.js for the map, pdf.js for manuals, YamlDotNet and Markdig for vault content.
- xUnit, AwesomeAssertions and bUnit for tests; `node --test` for the JS modules.

## Testing

```bash
dotnet test tests/DmCompanion.Core.Tests
dotnet test tests/DmCompanion.Infrastructure.Tests
dotnet test tests/DmCompanion.UI.Tests
dotnet test tests/DmCompanion.Screens.Tests        # bUnit component and session tests
dotnet test tests/DmCompanion.DevHost.Tests
dotnet test tests/DmCompanion.Desktop.Tests
dotnet test tests/DmCompanion.SrdImport.Tests
node --test tests/js/*.test.js                     # JS geometry, gestures, audio, TV layout
```

The CI (`.github/workflows/ci.yml`) builds and tests everything except the MAUI app on Linux. The Android app itself is checked manually on a tablet.

## Build and run

Requirements: the .NET SDK from `global.json`. The Android build also needs the `maui-android` workload, a JDK and the Android SDK.

```bash
# Screens in the browser (DevHost): http://localhost:5180
dotnet watch --project src/DmCompanion.DevHost

# Desktop version (Windows host)
dotnet run --project src/DmCompanion.Desktop
scripts/publish-desktop.sh        # dist/dm_companion_pc.zip (win-x64, self-contained)

# Android (tablet connected through adb)
dotnet workload install maui-android
dotnet build src/DmCompanion.App -f net10.0-android -t:Run
```

The DevHost works on a copy of `samples/vault-demo`. See [README.es.md](README.es.md) for the full setup, wireless `adb` pairing and APK release steps (in Spanish).

## Roadmap

Done: vault and screens (1), map with tokens and game systems (2), combat (3), fog of war and players' view (4), SRD glossary (5), PDF manuals (5b), ambient sound (6), TV second screen (7), desktop version. Planned, with a design doc each in [`docs/design/`](docs/design/): continuous reading in the manual viewer (5c) and a players' journal (8).

## About this repository

DM Companion was developed in a private repository. This public version has its history condensed into one commit per phase, and process documents (agent instructions, handoffs, manual checklists) were left out. Design specs, in Spanish, are kept in [`docs/design/`](docs/design/).

## License

Code: [MIT](LICENSE). The SRD 5.2.1 content in `src/DmCompanion.Core/Glossary/Packs/` is **not** MIT: it is CC-BY-4.0, modified (third-party Spanish translation via compendio-srd, reformatted to JSON). Third-party components and attributions: [NOTICE.md](NOTICE.md).

Not affiliated with, endorsed or sponsored by Wizards of the Coast. Dungeons & Dragons and D&D are trademarks of Wizards of the Coast.
