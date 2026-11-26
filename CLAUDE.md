# CLAUDE.md: DM Companion

## What this project is

An Android tablet app that helps run D&D 5e (2024) sessions: campaign notes from an Obsidian vault, a map with tokens, combat and initiative, a glossary (SRD 5.2), ambient sound, a players' view on a TV, continuous reading in the PDF manual viewer and a players' journal. There is also a Windows desktop host. Maps live in `Core/Maps/` and game systems in `Core/GameSystems/`.

Design docs (in Spanish): `docs/design/`, starting with `docs/design/2026-11-03-dm-companion-design.md`.

## Stack

- .NET 10 (LTS), .NET MAUI + Blazor Hybrid (`net10.0-android`).
- `DmCompanion.Screens` (shared Razor Class Library) and `DmCompanion.DevHost` (Blazor Web, Interactive Server, development only, in the browser).
- `DmCompanion.UI`: design system (CSS tokens, Razor components, EB Garamond and Source Sans 3 fonts, Lucide icons) and a `/styleguide` page; tested with bUnit.
- `DmCompanion.Desktop`: Windows host (Blazor Server that opens the browser), published as a self-contained `.exe`.
- Konva.js for the map canvas, pdf.js for manuals.
- YamlDotNet (frontmatter), Markdig (Markdown).
- xUnit + AwesomeAssertions.
- No database: the source of truth is the vault folder (Markdown + `.state.json`), synchronised externally with Syncthing.

## Commands

- Tests: `dotnet test tests/DmCompanion.Core.Tests`, `...Infrastructure.Tests`, `...Screens.Tests`, `...DevHost.Tests`, `...UI.Tests`, `...Desktop.Tests`, `...SrdImport.Tests`.
- JS tests (map measurement, gestures, audio, TV layout): `node --test tests/js/*.test.js` (passing the folder alone fails on Node 24).
- DevHost (http://localhost:5180): `dotnet watch --project src/DmCompanion.DevHost`.
- Desktop version (Windows): `scripts/publish-desktop.sh` publishes `src/DmCompanion.Desktop` (win-x64, single `.exe`) and builds `dist/dm_companion_pc.zip`. `dist/` is not committed.
- Regenerate the glossary packs (compendio-srd pinned in `NOTICE.md`): `dotnet run --project tools/DmCompanion.SrdImport -- <compendio-srd data folder> src/DmCompanion.Core/Glossary/Packs`.
- App, debug build: `dotnet build src/DmCompanion.App -f net10.0-android`.
- App, release APK: `dotnet publish src/DmCompanion.App -f net10.0-android -c Release -p:AndroidPackageFormat=apk`, then `adb install -r` the signed APK from the publish folder. Release builds are signed with a local key if one exists in `~/.android-keys`; keys are never committed.

## Architecture

- `DmCompanion.Core`: pure domain logic (vault, maps, game systems, combat, audio scenes, glossary, TV state). No MAUI, Android or Blazor.
- `DmCompanion.Infrastructure`: file-system access to the vault and the watcher.
- `DmCompanion.UI`: presentation only (see `.claude/rules/code-style-dotnet.md`).
- `DmCompanion.Screens`: screens and app services shared by every host; references only Core and UI.
- `DmCompanion.App`: MAUI Android shell plus the platform bridge.
- `DmCompanion.DevHost` and `DmCompanion.Desktop`: browser-based hosts.

## Rules for the agent

- Before writing code in non-trivial tasks, present a plan and wait for approval.
- Do not make changes outside the requested scope. A fix is a fix, not a chance to refactor.
- Follow the rules in `.claude/rules/`, split by topic. For .NET file names, `code-style-dotnet.md` wins.
- If something fails, diagnose the root cause before changing approach.
- Commits are conventional (`feat:`, `fix:`, `refactor:`, `docs:`, `chore:`) and written in English.
- The user's vault is sacred: never delete or overwrite files outside the app's explicit operations; in tests, always work on copies in temporary folders.

Private working notes (handoffs, device checklists) live outside this repository.
