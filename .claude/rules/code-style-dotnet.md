# Code standards: .NET (MAUI + Blazor Hybrid)

Applies to this repo (.NET 10, MAUI Blazor Hybrid for Android). Complements `code-style-general.md` and **takes precedence over it** for file names.

## Architecture

- **`DmCompanion.Core`** does not reference MAUI, Android or Blazor. All logic (vault, combat, maps) lives here and is tested without an emulator.
- **`DmCompanion.Infrastructure`** implements the Core interfaces against the file system.
- **`DmCompanion.UI`** (Razor Class Library) is presentation only: tokens, base components and `/styleguide`. It receives data through parameters and emits `EventCallback`; no business logic and no reference to Core, Infrastructure or MAUI. CSS classes use the `ds-` prefix. API: `src/DmCompanion.UI/README.md`; design: `docs/design/2026-11-03-design-system-design.md`.
- **`DmCompanion.Screens`** (Razor Class Library) holds the screens and the app services shared by App and DevHost. It references only Core (and `DmCompanion.UI`); nothing from MAUI, Android or Infrastructure. The platform sits behind interfaces in `Screens/Platform`; each host registers the concrete implementations.
- **`DmCompanion.DevHost`** is for development only: a Blazor Web App (Interactive Server) to try Screens in the browser. It listens on loopback only, has no authentication, is never published, and never uses the real vault unless `DevHost:VaultPath` is set explicitly.
- **`DmCompanion.App`** is UI plus the bridge to the platform. Razor components contain no business logic: they call Core services.
- Android-specific code lives only in `Platforms/Android/`, behind an interface (`ISecondScreen`, `IAudioPlayer`...).
- **Dependency injection** through `IServiceCollection`: `MauiProgram` (App) and `Program.cs` (DevHost). No `new` for services.
- **Async all the way.** No `.Result`, `.Wait()` or `async void` (except UI event handlers that require it).

## File names

- `.cs` and `.razor` files in **PascalCase**, one type per file (`VaultIndex.cs`, `NotesPanel.razor`). Razor derives the component name from the file name, and it is the .NET convention.
- JS and CSS in `wwwroot/` in kebab-case (`map-canvas.js`).

## Naming

- Interfaces start with `I`: `IVaultStorage`. Implementations carry no type suffix.
- Async methods end in `Async`.
- Immutable models as `record` whenever possible.

## Blazor / JS interop

- JS to C# interop goes only through one service per feature (e.g. `MapCanvasInterop`); components do not call `IJSRuntime` directly.
- Vault content rendered as HTML is always sanitised (Markdown can carry HTML).

## Errors and logging

- Errors in user content (invalid YAML, broken link) are **results**, not exceptions.
- `ILogger<T>` through DI, structured logging.

## Forbidden in new code

- `async void` (outside UI handlers) · `.Result` / `.Wait()` · silent `catch (Exception)` · mutable statics.

## Testing

- xUnit + AwesomeAssertions.
- Infrastructure is tested against real temporary folders (a copy of `samples/vault-demo/`), not against file-system mocks.
