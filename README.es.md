# DM Companion

[English](README.md) · Español

App Android para tablet que ayuda a dirigir partidas de D&D 5e (2024). Funcionalidades:

- Notas de campaña tomadas de un vault de Obsidian.
- Mapa con tokens.
- Combate e iniciativa.
- Glosario (SRD 5.2.1 en español, sin conexión).
- Ambiente sonoro.
- Vista de jugadores en una TV.

Es un proyecto personal: funciona sin conexión, sin cuentas y sin nube. La sincronización del vault entre el PC y la tablet la hace Syncthing, por fuera de la app.

**Estado:** están hechas las fases 1 a 7 (incluida la 5b) y la versión de PC: vault de Obsidian (fase 1), mapa con tokens y sistemas de juego (fase 2), combate e iniciativa (fase 3), niebla y vista de jugadores (fase 4), glosario del SRD 5.2.1 en español (fase 5), visor de manuales en PDF (fase 5b), ambiente sonoro (fase 6) y segunda pantalla en una TV (fase 7). Pendientes: lectura continua en el visor de manuales (fase 5c) y diario de jugadores (fase 8), de las que solo hay spec en [docs/design/](docs/design/). Las pruebas manuales en la tablet de algunas fases siguen pendientes.

> Se desarrolló en un repositorio privado y se publica con el historial condensado en un commit por fase.

## Stack

- .NET 10, .NET MAUI y Blazor Hybrid (`net10.0-android`, Android 11 o superior).
- Konva.js para el mapa; YamlDotNet y Markdig para el contenido del vault.
- xUnit, AwesomeAssertions y bUnit para los tests.

## Estructura

| Proyecto | Qué es |
|---|---|
| `src/DmCompanion.Core` | Lógica de dominio en .NET puro: vault, mapas (`Maps/`), sistemas de juego (`GameSystems/`) y combate |
| `src/DmCompanion.Infrastructure` | Acceso al sistema de ficheros del vault y watcher |
| `src/DmCompanion.UI` | Design system: tokens CSS y componentes Razor (página `/styleguide`) |
| `src/DmCompanion.Screens` | Pantallas Razor compartidas entre la app y el DevHost |
| `src/DmCompanion.App` | Aplicación MAUI para Android, que solo contiene la UI y el puente con la plataforma |
| `src/DmCompanion.DevHost` | Host Blazor para probar las pantallas en el navegador, solo para desarrollo ([README](src/DmCompanion.DevHost/README.md)) |
| `src/DmCompanion.Desktop` | Versión de PC para Windows: host Blazor Server que abre el navegador, se publica como `.exe` autocontenido (guía en [docs/guia-version-pc.md](docs/guia-version-pc.md)) |
| `samples/vault-demo` | Vault de ejemplo para los tests y las pruebas manuales (notas, dos mapas, una imagen suelta y la nota de campaña) |

## Entorno

Hace falta el SDK de .NET indicado en `global.json` (10.0.100 o posterior de la misma banda). Para compilar la app de Android además se necesita el workload `maui-android`, un JDK y el Android SDK. El DevHost, la versión de PC, los tests y la CI no los necesitan.

```bash
# SDK de .NET 10 (WSL o Linux, sin sudo; ajusta la carpeta de instalación)
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"

# Solo para la app de Android
dotnet workload install maui-android
export ANDROID_HOME="$HOME/Android/Sdk" JAVA_HOME=/ruta/a/tu/jdk
dotnet build src/DmCompanion.App -t:InstallAndroidDependencies -f net10.0-android \
  -p:AndroidSdkDirectory="$ANDROID_HOME" -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AcceptAndroidSDKLicenses=True
```

## Desarrollo

```bash
# Tests
dotnet test tests/DmCompanion.Core.Tests
dotnet test tests/DmCompanion.Infrastructure.Tests
dotnet test tests/DmCompanion.Screens.Tests
dotnet test tests/DmCompanion.DevHost.Tests
dotnet test tests/DmCompanion.UI.Tests
dotnet test tests/DmCompanion.Desktop.Tests
dotnet test tests/DmCompanion.SrdImport.Tests
node --test tests/js/*.test.js   # tests de JS (medición del mapa)

# Pantallas en el navegador: http://localhost:5180
dotnet watch --project src/DmCompanion.DevHost
```

## Glosario

El glosario (botón «Glosario» del menú) trae el SRD 5.2.1 en español dentro de la app, de solo lectura y sin conexión: reglas, estados, criaturas, conjuros, objetos mágicos, equipo y opciones de personaje. Permite buscar en español o en inglés, filtrar, copiar una entrada al vault para editarla, añadir una criatura al mapa como token y mostrar una entrada (menos las criaturas) en la TV. Las notas `rule` y `monster` del vault conviven con el SRD y mandan sobre él.

- **Origen:** texto de [compendio-srd](https://github.com/Ismaelbase/compendio-srd) (MIT), commit `2d61b5dcabe91ab69d5c4a5a41822eb83c2038cc`, carpeta `datos/`, idioma `es`. El contenido del SRD 5.2.1 es de Wizards of the Coast y va bajo licencia CC BY 4.0; la atribución está en [NOTICE.md](NOTICE.md) y en «Acerca de» del Diagnóstico.
- **Paquetes:** JSON embebidos en `src/DmCompanion.Core/Glossary/Packs/`. No llevan HTML: el texto va en spans y solo `GlossaryHtmlRenderer` genera HTML.
- **Regenerarlos:** clonar compendio-srd en ese commit y ejecutar `dotnet run --project tools/DmCompanion.SrdImport -- <carpeta datos> src/DmCompanion.Core/Glossary/Packs`. Los `datos/*.json` de ese commit ya llevan aplicadas las correcciones; el importador no las aplica.

## Manuales

La pestaña «Manuales» del glosario abre los PDF de tus manuales (de solo lectura, sin conexión).

- **Dónde ponerlos:** `manuales/<id del sistema>/` dentro del vault (por ejemplo `manuales/dnd5e/`), en cualquier subcarpeta. Cada vault solo muestra los manuales del sistema de juego activo. El id se compara sin distinguir mayúsculas.
- **Visor:** pdf.js carga el PDF por tramos, así que un manual grande abre rápido. Tiene navegación («Anterior», «Siguiente», ir a una página), zoom, índice del PDF con filtro, búsqueda de texto y marcadores. Recuerda la última página de cada manual.
- **Marcadores:** se guardan junto al PDF, en `<manual>.marcadores.json`; es lo único que la app escribe en el vault y viaja con Syncthing.
- **TV:** «Mostrar en TV» manda la página actual y «Recortar para la TV» manda la zona que arrastres (imagen en memoria, nunca se escribe en el vault). Se queda en la TV aunque cambie el vault; «Volver al mapa» la quita.
- **Spec:** [fase 5b](docs/design/2026-11-16-fase-5b-manuales-pdf-design.md).

## Tablet

### Vincular por Wi-Fi (una vez por equipo)

1. En la tablet, activa las opciones de desarrollador: *Ajustes → Acerca de la tablet* y pulsa 7 veces *Número de compilación*.
2. *Opciones de desarrollador → Depuración inalámbrica*: actívala y entra en *Vincular dispositivo con código de vinculación*. Apunta la IP, el puerto de vinculación y el código.
3. En el equipo, con la tablet en la misma red Wi-Fi:

```bash
adb pair <ip>:<puerto-de-vinculacion>    # pide el código de 6 dígitos
adb connect <ip>:<puerto-de-depuracion>  # el puerto de la pantalla principal de «Depuración inalámbrica», distinto del de vinculación
adb devices                              # debe aparecer la tablet como "device"
```

El puerto de depuración cambia cada vez que se activa la depuración inalámbrica o se reinicia la tablet. En ese caso basta con repetir `adb connect`; no hace falta volver a vincular.

### Compilar e instalar

```bash
# Build de depuración, instalado y arrancado en la tablet conectada
dotnet build src/DmCompanion.App -f net10.0-android -t:Run

# APK de release
dotnet publish src/DmCompanion.App -f net10.0-android -c Release -p:AndroidPackageFormat=apk
adb install -r src/DmCompanion.App/bin/Release/net10.0-android/publish/es.pauvidal.dmcompanion-Signed.apk
```

Cada equipo firma los APK con su propia clave de depuración. Si la app ya está instalada desde otro equipo, `adb install -r` falla con `INSTALL_FAILED_UPDATE_INCOMPATIBLE`. Para instalarla, desinstálala antes con `adb uninstall es.pauvidal.dmcompanion`. Las notas no se pierden, porque el vault está fuera de la app, pero sí las preferencias de la app.

### Versión de PC (Windows)

```bash
scripts/publish-desktop.sh   # genera dist/dm_companion_pc.zip (win-x64, autocontenido)
```

`dist/` no se commitea.

**Aviso:** la fase 3 sube el `.state.json` del mapa a la versión 2 y la fase 4 (niebla) a la 3. Una app anterior lo abre en solo lectura, así que conviene actualizar todas las instalaciones (tablet y PC) que compartan vault.

## Documentación

Los documentos de diseño (en español) están en [docs/design/](docs/design/):

- Diseño general: [dm-companion](docs/design/2026-11-03-dm-companion-design.md) y [design system](docs/design/2026-11-03-design-system-design.md)
- Fase 2 (mapa): [spec](docs/design/2026-11-12-fase-2-mapa-design.md)
- Fase 3 (combate): [spec](docs/design/2026-11-13-fase-3-combate-design.md)
- Versión de PC: [spec](docs/design/2026-11-13-version-pc-design.md)
- Fase 4 (niebla): [spec](docs/design/2026-11-15-fase-4-niebla-design.md)
- Fase 5 (glosario): [spec](docs/design/2026-11-16-fase-5-glosario-design.md)
- Fase 5b (manuales): [spec](docs/design/2026-11-16-fase-5b-manuales-pdf-design.md)
- Fase 6 (ambiente sonoro): [spec](docs/design/2026-11-15-fase-6-ambiente-sonoro-design.md)
- Fase 7 (segunda pantalla): [spec](docs/design/2026-11-15-fase-7-segunda-pantalla-design.md)
- Pendientes: [fase 5c](docs/design/2026-11-16-fase-5c-lectura-continua-design.md) y [fase 8](docs/design/2026-11-16-fase-8-diario-design.md)

## Licencias

El código es MIT ([LICENSE](LICENSE)). El contenido del SRD 5.2.1 incluido (`src/DmCompanion.Core/Glossary/Packs/*.json` y derivados) **no** es MIT: va bajo CC BY 4.0 y está modificado (traducción al español de terceros y reformateo a JSON). Los componentes de terceros y la atribución están en [NOTICE.md](NOTICE.md).
