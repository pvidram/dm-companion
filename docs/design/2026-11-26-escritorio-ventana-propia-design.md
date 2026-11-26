# DM Companion — Versión de escritorio con ventana propia

- **Fecha:** 2026-11-26
- **Estado:** diseño aprobado por secciones el 26/11; pendiente de revisión del texto.
- **Sustituye en parte a:** [`2026-11-13-version-pc-design.md`](2026-11-13-version-pc-design.md). Se mantienen el servidor local, la seguridad en loopback, los registros, la instancia única y el guardado al cerrar. Cambian la forma de abrir la app, la consola y la distribución.

## 1. Intención

Hoy la versión de PC es un `.exe` con una consola negra que abre la app en el navegador. Funciona, pero parece una herramienta de desarrollo.

Esta versión la convierte en una **aplicación de escritorio de verdad**:

- ventana propia con título e icono;
- sin consola;
- instalador en Windows y AppImage en Linux;
- publicación gratuita en GitHub Releases;
- aviso cuando hay una versión nueva.

Las pantallas (`DmCompanion.Screens`) son las mismas que usan la tablet y el DevHost.

## 2. Objetivos

1. **Ventana nativa** con WebView: WebView2 en Windows y WebKitGTK en Linux. Título «DM Companion», icono propio, tamaño y posición recordados.
2. **Sin consola** en Windows.
3. **Distribución:**
   - en Windows, instalador por usuario (Inno Setup, sin administrador) y zip portable;
   - en Linux, AppImage;
   - todo lo construye GitHub Actions al subir un tag y queda en una release en borrador.
4. **Aviso de versión nueva** al arrancar, desactivable, sin enviar datos.
5. **Sin regresiones:** se mantienen el servidor solo en loopback, `/_vault/` con rangos para pdf.js, la instancia única, los registros y el guardado del mapa al cerrar.

## 3. No objetivos

- Actualización automática (descargar e instalar sola).
- Firma del ejecutable o del instalador: no se compra certificado, y la primera vez sale el aviso de SmartScreen.
- macOS.
- Icono en la bandeja del sistema.
- Blazor dentro del proceso, sin servidor HTTP (`Photino.Blazor` o MAUI para Windows). Ver §10.
- Cambios en la app de Android o en el DevHost. `IUpdateCheck` es un servicio opcional, como `ILogFolder`: solo lo registra el host de escritorio.

## 4. Arquitectura y arranque

### 4.1 Proyecto

- Se mantiene `DmCompanion.Desktop`.
- Se añade el paquete `Photino.NET`, que aporta la ventana con WebView. Blazor sigue corriendo en el servidor (Interactive Server), como hoy.
- `OutputType`:
  - en Windows, `WinExe`, para que no aparezca la consola;
  - en Linux da igual, porque se lanza desde el `.desktop` del AppImage.

### 4.2 Secuencia de arranque (`Program.cs`)

1. Igual que hoy: comprobación de la instalación (`InstallationCheck`), registros en fichero, mutex de instancia única (`SingleInstance`), Kestrel solo en loopback (`PortSelector`, puerto preferido 5190) y registro de servicios.
2. El host arranca con `app.StartAsync()` en lugar de `app.Run()`.
3. Cuando Kestrel escucha y `LoopbackGuard` da el visto bueno, se escribe `instance.json` y se crea la ventana Photino en el hilo principal. La ventana tiene:
   - título «DM Companion» e icono propio;
   - posición y tamaño restaurados (§6.1);
   - la URL `http://127.0.0.1:<puerto>`.
4. `WaitForClose()` bloquea hasta que el usuario cierra la ventana.
5. Al cerrarla se llama a `app.StopAsync()`, que guarda el mapa pendiente (`MapFlushOnStop`) y borra `instance.json`. Después termina el proceso.

`Program.cs` queda fino. La lógica comprobable va en clases propias (`WindowPlacement`, `UpdateCheck`, `FocusRequest`), y la creación de la ventana se aísla en una clase (`DesktopWindow`) que los tests no instancian.

### 4.3 Segunda instancia

- Si el mutex ya está tomado, la nueva instancia lee la URL de `instance.json` (con la espera de hasta 10 s que ya existe) y hace un `GET` a `<url>/_desktop/focus`.
- La instancia viva responde 204 y trae su ventana al frente: la restaura si estaba minimizada y le da el foco.
- El endpoint `/_desktop/focus`:
  - solo existe en modo escritorio;
  - rechaza con 403 lo que no venga de una dirección loopback;
  - no acepta parámetros.
- Si no hay respuesta tras dos reintentos breves, sale el diálogo «DM Companion ya está abierto, pero no responde».

### 4.4 Lo que desaparece o cambia

- Abrir la app en el navegador. `BrowserLauncher` se queda solo para enlaces externos (§6.3) y para la descarga de una versión nueva (§7).
- El título de la consola y la espera a Intro tras un fallo de arranque. `StartupFailure` deja de escribir en la consola y pasa a mostrar un diálogo nativo (§8).
- El modo `Desktop:SingleInstance=false` de los tests se mantiene: sin mutex, sin ventana y sin consulta de versiones.

## 5. Plataformas y WebView

| Plataforma | Runtime | WebView | Requisito en la máquina |
|---|---|---|---|
| Windows 10/11 x64 | `win-x64`, autocontenido | WebView2 | WebView2 Runtime (viene de serie en Windows 11 y en Windows 10 actualizado) |
| Linux x64 | `linux-x64`, autocontenido | WebKitGTK | `libwebkit2gtk` en la versión que pida Photino, instalada por el usuario |

El AppImage no incluye WebKitGTK. El README indica el paquete para Debian/Ubuntu y Fedora.

## 6. Ventana

### 6.1 Tamaño y posición

- Al cerrar se guardan en `window.json` (en `DesktopPaths.RoamingRoot`, junto a `prefs.json`): `x`, `y`, `ancho`, `alto` y si estaba maximizada.
- Al abrir:
  - si el rectángulo guardado cabe al menos en parte (un mínimo de 100 × 100 px visibles) en alguna pantalla, se restaura;
  - si no cabe, o si falta el fichero o está corrupto, la ventana se abre maximizada.
- El tamaño mínimo de la ventana es de 1024 × 640.
- `WindowPlacement` decide la restauración a partir de una lista de pantallas que recibe; no consulta el sistema, y por eso se puede probar.

### 6.2 Cerrar

Cerrar la ventana sale de la app, con el guardado de §4.2. No hay icono en la bandeja ni la app se queda en segundo plano.

### 6.3 Enlaces externos

- Todo enlace `http` o `https` con un origen distinto del de la app se abre en el navegador del sistema y nunca dentro de la ventana. Los demás esquemas (`mailto:`, `javascript:`, `file:`) no se abren.
- **Mecanismo:** Photino.NET 4.0.16 no ofrece ningún gancho de navegación ni de ventana nueva, así que `wwwroot/desktop-links.js`, que solo carga el host Desktop, intercepta en fase de captura los clics en `<a>` y los `window.open`. Clasifica la URL con una función pura (`isExternalLink`) y llama a `POST /_desktop/open-external`, que solo acepta loopback y valida que la URL sea `http` o `https` antes de pasarla a `IBrowserLauncher`.

### 6.4 WebView en Release

- Herramientas de desarrollo desactivadas; en Debug, activadas.
- Menú contextual del WebView desactivado, para que no salgan «Atrás» ni «Recargar». Los menús contextuales propios de la app (mapa, notas) no se ven afectados.

## 7. Aviso de versión nueva

### 7.1 Contrato

- `IUpdateCheck` vive en `DmCompanion.Screens/Platform` y es **opcional**: Screens lo pide con `GetService`, igual que `ILogFolder`. Solo lo registra el host de escritorio (`GitHubUpdateCheck`); la app de Android y el DevHost no cambian.
  - `Task<AvailableUpdate?> GetLatestAsync(CancellationToken ct)`: la última release publicada, o `null` si no se pudo saber;
  - `void OpenReleasePage(AvailableUpdate update)`: abre su página en el navegador del sistema;
  - `AvailableUpdate` es un `record` con la versión y la URL de la página de la release.
- `UpdateNotice` (servicio de Screens) aplica las preferencias (§7.3), compara versiones y guarda el aviso pendiente que pinta la banda.

### 7.2 Consulta (`GitHubUpdateCheck`)

- Una vez por arranque, en segundo plano, sin bloquear la interfaz.
- `GET https://api.github.com/repos/pvidram/dm-companion/releases/latest`, que ya excluye los borradores y las prerreleases.
- **Petición:**
  - cabeceras `User-Agent: DMCompanion/<versión>` y `Accept: application/vnd.github+json`, nada más;
  - sin cookies ni identificadores;
  - tiempo máximo de 5 s.
- **Respuesta:**
  - se leen `tag_name` (sin la `v` inicial) y `html_url`;
  - se compara con la versión del ensamblado según SemVer;
  - solo se avisa si la publicada es mayor y no coincide con la versión marcada como «no avisar».
- **Fallos** (sin red, tiempo agotado, código distinto de 200, JSON inválido, versión ilegible): se devuelve `null` y se registra una línea en el log con el tipo del error. Nunca se muestra un error al usuario.
- Si la preferencia «Buscar versiones nuevas al arrancar» está apagada, no se hace ninguna petición.

### 7.3 Preferencias

En `IAppPreferences`:

- `update.check`: booleano, `true` por defecto;
- `update.skipVersion`: texto, vacío por defecto.

El interruptor «Buscar versiones nuevas al arrancar» va en la pantalla de diagnóstico (`VaultDiagnostics`), junto a «Abrir carpeta de registros», y solo se muestra si hay un `IUpdateCheck` registrado.

### 7.4 Interfaz

- Una banda discreta en lo alto del shell con el texto «Hay una versión nueva (x.y.z)» y tres botones:
  - **Descargar:** abre `html_url` con `IBrowserLauncher`;
  - **Ahora no:** oculta la banda hasta el próximo arranque;
  - **No avisar de esta versión:** guarda `update.skipVersion` y oculta la banda.
- El componente (`UpdateBanner`) está en Screens. Recibe el resultado por un servicio de estado y no llama a la red.

## 8. Errores

| Situación | Comportamiento |
|---|---|
| Falta WebView2 (Windows) | `MessageBox` nativo: «Falta Microsoft Edge WebView2», con la URL de descarga. Se registra en el log. |
| Falta WebKitGTK (Linux) | Mensaje en stderr y en el log. El README indica el paquete que hay que instalar. |
| Kestrel no arranca o `LoopbackGuard` lo para | No se abre la ventana. Diálogo nativo con el mensaje de `StartupFailure`. |
| El host se para con la ventana abierta, sin que el usuario la haya cerrado | La ventana se cierra con el aviso «DM Companion se ha detenido; revisa los registros» y la ruta de los registros. |
| Apagar Windows o cerrar sesión con la app abierta | En Linux, el manejador de `SIGTERM` guarda el mapa con `ShutdownFlush` (4 s). En Windows la app (WinExe) ya no recibe esa señal; el mapa se guarda unos 750 ms después de cada cambio (`MapSession.SaveDelay`), así que como mucho se pierde el último cambio y `window.json`. |
| Segunda instancia sin respuesta de la primera | Diálogo «DM Companion ya está abierto, pero no responde». |
| Faltan ficheros junto al ejecutable | Diálogo nativo con el mensaje actual de `InstallationCheck`, adaptado: «Reinstala DM Companion o vuelve a descomprimir el zip». |

**Diálogo nativo:**

- si la ventana ya existe, se usa `PhotinoWindow.ShowMessage`;
- si no, en Windows se usa `MessageBoxW` (P/Invoke) y en Linux, stderr.

## 9. Empaquetado y publicación

### 9.1 Script

`scripts/publish-desktop.sh <rid> <versión>`, con `win-x64` o `linux-x64`:

- hace `dotnet publish` autocontenido con `-p:Version=<versión>`, en carpeta y sin `PublishSingleFile` (las bibliotecas nativas de Photino y WebView2 van sueltas junto al ejecutable);
- copia el vault de ejemplo y deja la carpeta lista en `dist/<rid>/`;
- con `win-x64` además crea el zip, y con `linux-x64`, el AppImage.

El workflow llama a este script, así que la receta es una sola y se puede probar en local.

### 9.2 Workflow `.github/workflows/release.yml`

- **Disparadores:**
  - `push` de un tag `v*`;
  - `workflow_dispatch`, que no crea release y solo sube los ficheros como artefactos del run, para probar el empaquetado.
- **Jobs:**
  1. `windows` (`windows-latest`): script con `win-x64` y compilación del instalador con `iscc` (Inno Setup ya viene instalado en la imagen). Genera:
     - `dm-companion-<versión>-win-x64.zip`;
     - `dm-companion-<versión>-win-x64-setup.exe`.
  2. `linux` (`ubuntu-latest`): script con `linux-x64` y `appimagetool`. Genera `dm-companion-<versión>-x86_64.AppImage`.
  3. `release` (solo con tag): crea una release **en borrador** con los tres ficheros y las notas generadas desde los commits. El autor la revisa y la publica a mano en la web.
- **Permisos:** `contents: read` por defecto, y `contents: write` solo en el job `release`.

### 9.3 Instalador (Inno Setup, `installer/dm-companion.iss`)

- `PrivilegesRequired=lowest`; instala en `{localappdata}\Programs\DM Companion`.
- Acceso en el menú Inicio e icono opcional en el escritorio.
- Desinstalador registrado en «Aplicaciones instaladas».
- Ni el instalador ni el desinstalador tocan los datos del usuario (`%APPDATA%\DmCompanion`, `%LOCALAPPDATA%\DmCompanion`) ni el vault.
- Si la app está abierta durante una actualización, Inno Setup pide cerrarla (`CloseApplications`).

### 9.4 Icono

Se genera una vez desde `src/DmCompanion.App/Resources/AppIcon/appicon.svg` y `appiconfg.svg` y se guarda en `src/DmCompanion.Desktop/Assets/`:

- `.ico` con varios tamaños, para el ejecutable, la ventana y el instalador;
- `.png` de 256 px, para la ventana en Linux y el AppImage.

### 9.5 README

- Sección «Download» con el enlace a Releases.
- Los dos avisos de la primera ejecución: en SmartScreen, «Más información → Ejecutar de todas formas»; en Linux, `chmod +x` al AppImage.
- El requisito de WebKitGTK en Linux.
- La tabla de proyectos actualizada: el host de escritorio ya no «abre el navegador».

La guía `docs/guia-version-pc.md` y su `LEEME.txt` se actualizan igual.

## 10. Alternativas descartadas

- **Photino.Blazor dentro del proceso, sin servidor HTTP.** Es el mismo modelo que la app de Android, pero obliga a reescribir `/_vault/` con rangos como esquema propio y a validar otra vez el visor PDF y los mapas en ese WebView. Más riesgo para un resultado idéntico de cara al usuario.
- **MAUI para Windows.** Solo para Windows, y el empaquetado MSIX pide firma.
- **Electron.** Habría que incluir Chromium entero (más de 100 MB) y añadir Node al stack sin ninguna ventaja para esta app.
- **Actualización automática (Velopack).** Más complejidad y otro paso en cada versión. El aviso con enlace es suficiente.

## 11. Tests

- **`DmCompanion.Desktop.Tests`:**
  - `WindowPlacement`:
    - un rectángulo dentro de una pantalla se restaura;
    - uno fuera de todas abre maximizada;
    - uno parcialmente visible por encima del mínimo se restaura;
    - un `window.json` ausente o corrupto abre maximizada;
    - el estado maximizado se recuerda.
  - `GitHubUpdateCheck`, con un `HttpMessageHandler` falso:
    - versión mayor, igual o menor;
    - `v` inicial;
    - prerrelease con sufijo SemVer;
    - sin red, tiempo agotado, 404 o 403 (límite de peticiones) y JSON inválido;
    - versión marcada como «no avisar»;
    - con `update.check = false`, ninguna petición.
  - `/_desktop/focus` y `/_desktop/open-external`:
    - desde loopback, 204 y el efecto esperado (evento de foco o `IBrowserLauncher` llamado);
    - una URL que no es `http` ni `https`, 400;
    - en modo test sin escritorio, el endpoint no existe.
  - Los tests actuales del host (TestServer, sin mutex) siguen pasando.
- **`DmCompanion.Screens.Tests` (bUnit):**
  - `UpdateBanner` se muestra con una versión, «Descargar» llama al lanzador, «Ahora no» la oculta y «No avisar» guarda la preferencia;
  - no aparece sin `IUpdateCheck` registrado;
  - el interruptor de `VaultDiagnostics` solo aparece en escritorio.
- **Node:** `tests/js/desktop-links.test.js` con `isExternalLink` (mismo origen, otro origen, `mailto:`, `javascript:`, rutas relativas y `/_vault/`).
- **CI:** `ci.yml` no cambia; el proyecto Desktop se sigue compilando y probando en Linux. El empaquetado se prueba con `workflow_dispatch`.

## 12. Verificación manual

Checklist en las notas privadas del autor. En un PC con Windows:

1. Instalar con el instalador y pasar el aviso de SmartScreen.
2. Abrir la app, mover y redimensionar la ventana, y comprobar que al reabrir la recuerda (también maximizada).
3. Con la app abierta, hacer doble clic en el acceso y comprobar que la ventana viene al frente y no se abre otra.
4. Mover tokens y cerrar la ventana: al reabrir, el mapa está guardado.
5. Abrir un enlace externo de una nota: se abre en el navegador.
6. Probar el zip portable en otra carpeta.
7. Publicar una versión de prueba mayor (release en borrador publicada temporalmente) y comprobar la banda.
8. Desinstalar: los datos y el vault siguen ahí.

En Linux (WSLg sirve): el AppImage abre la ventana y el visor PDF funciona.
