# DM Companion

App Android para tablet que ayuda a dirigir partidas de D&D 5e (2024). Funcionalidades:

- Notas de campaña tomadas de un vault de Obsidian.
- Mapa con tokens.
- Combate e iniciativa.
- Glosario (SRD 5.2).
- Ambiente sonoro.
- Vista de jugadores en una TV.

Es un proyecto personal: funciona sin conexión, sin cuentas y sin nube. La sincronización del vault entre el PC y la tablet la hace Syncthing, por fuera de la app.

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

El proyecto usa un SDK de .NET propio en `~/.dotnet-dm`, separado del SDK del sistema y de los proyectos del trabajo, con la configuración en `~/.dotnet-dm/env-dm-companion.sh`. Todos los comandos se lanzan después de cargarlo:

```bash
source ~/.dotnet-dm/env-dm-companion.sh
```

Para montar el entorno en un equipo nuevo (WSL o Linux, sin `sudo`):

```bash
# 1. SDK de .NET 10 en ~/.dotnet-dm/sdk
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir ~/.dotnet-dm/sdk

# 2. Crear ~/.dotnet-dm/env-dm-companion.sh con el contenido de abajo y cargarlo.

# 3. Workload de Android
dotnet workload install maui-android

# 4. JDK y Android SDK (descarga varios GB)
dotnet build src/DmCompanion.App -t:InstallAndroidDependencies -f net10.0-android \
  -p:AndroidSdkDirectory="$ANDROID_HOME" -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AcceptAndroidSDKLicenses=True
```

Contenido de `~/.dotnet-dm/env-dm-companion.sh`:

```bash
export DOTNET_ROOT="$HOME/.dotnet-dm/sdk"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$HOME/Android/Sdk/platform-tools:$PATH"
export ANDROID_HOME="$HOME/Android/Sdk"
export JAVA_HOME="$HOME/.dotnet-dm/jdk"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
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
node --test tests/js/*.test.js   # tests de JS (medición del mapa)

# Pantallas en el navegador: http://localhost:5180
dotnet watch --project src/DmCompanion.DevHost
```

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

## Documentación

- Diseño: [docs/design/2026-11-03-dm-companion-design.md](docs/design/2026-11-03-dm-companion-design.md)
- Fase 2 (mapa): [spec](docs/design/2026-11-12-fase-2-mapa-design.md)
- Design system: [docs/design/2026-11-03-design-system-design.md](docs/design/2026-11-03-design-system-design.md)
