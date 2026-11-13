# DM Companion — Versión de PC (Windows) para pruebas con amigos

- **Fecha:** 2026-11-13
- **Estado:** aprobado (diseño hablado el 13/11; pendiente de revisión del texto)
- **Depende de:** fase 2 cerrada en `feature/fase-2-mapa-v2` (spec [`2026-11-12-fase-2-mapa-design.md`](2026-11-12-fase-2-mapa-design.md)) y la arquitectura de la [spec global](2026-11-03-dm-companion-design.md) §3.

## 1. Intención

Un amigo del autor va a probar la app y darle feedback, pero la usará en un **PC con Windows**, no en una tablet. Hoy la app solo se compila para Android, y en PC solo existe el DevHost, que es una herramienta de desarrollo.

Esta versión lo resuelve con un **host de escritorio**: un ejecutable de Windows que arranca la app en local y la abre en el navegador. Reutiliza las mismas pantallas (`DmCompanion.Screens`) que la tablet y el DevHost.

## 2. Objetivos

1. **Entrega sencilla:** un `.zip` con un `.exe` autocontenido (sin instalar .NET), el vault de ejemplo y una guía corta en español.
2. **Arranque con doble clic:** abre el navegador en la app. Una sola instancia.
3. **Primera vez guiada:** el amigo elige su vault o prueba con el vault de ejemplo.
4. **Sin pérdida de datos:** al cerrar, se guarda el mapa pendiente.
5. **Feedback con contexto:** registros en disco y un botón para abrir su carpeta y mandarlos.
6. **Seguro en local:** solo accesible desde el propio PC y protegido contra *DNS rebinding*.

## 3. No objetivos

- Icono en la bandeja del sistema u ocultar la consola.
- Instalador (MSI/MSIX), firma del ejecutable y actualización automática.
- Versión MAUI nativa para Windows (`net10.0-windows`, la «opción A»). Queda para cuando el uso lo justifique.
- Adaptar la interfaz a escritorio más allá de lo que ya funciona con ratón en el DevHost.
- Telemetría o envío automático de errores.

## 4. Restricciones

- **El DevHost no cambia.** Sigue siendo solo para desarrollo, con su copia del vault de ejemplo.
- **Arquitectura:**
  - Desktop → Screens, UI, Core e Infrastructure. Screens no referencia Desktop ni Infrastructure.
  - Lo específico de Windows va en Desktop, detrás de las interfaces de `Screens/Platform`.
- **El vault del usuario es sagrado.** Desktop nunca crea, copia ni borra nada en un vault salvo con las operaciones explícitas de la app. La copia del ejemplo solo escribe en `Documentos\DM Companion\Ejemplo`, y solo si no existe.
- **Se compila desde WSL:** `dotnet publish` con `-r win-x64` no necesita Windows.
- **Registros sin contenido del vault** ni mensajes de excepción con rutas: solo el tipo de excepción, siguiendo la convención del repo.

## 5. Diseño

### 5.1 Proyecto

- **`src/DmCompanion.Desktop`:** Blazor Web App (Interactive Server) con el mismo montaje de servicios que el DevHost (`Program.cs`), pero:
  - sin `DevVaultSeeder` ni `/styleguide`;
  - con su propia plataforma: `DesktopStorageAccess`, `JsonFileAppPreferences` (se puede reutilizar moviéndola a un sitio compartido si hace falta, o duplicar la mínima) y un abridor de carpetas de Windows.
- **Tests:** `tests/DmCompanion.Desktop.Tests`, con el patrón de `DmCompanion.DevHost.Tests` (`WebApplicationFactory` / `TestServer`).
- **Nombre del ejecutable:** `DMCompanion.exe` (`AssemblyName`).

### 5.2 Arranque, puerto y una sola instancia

- **Puerto preferido: 5190.** Si está ocupado, se elige uno libre de loopback.
- **Una sola instancia:**
  - Al arrancar, Desktop toma un mutex con nombre (`Local\DmCompanion.Desktop`) y escribe la URL efectiva en `%LOCALAPPDATA%\DmCompanion\instance.json`.
  - Si el mutex ya está tomado, lee ese fichero, abre el navegador en esa URL y termina con código 0.
  - Si el fichero falta o la URL no responde, avisa en la consola y termina.
- **Navegador:** cuando Kestrel ya escucha (`ApplicationStarted`), se abre la URL con el navegador predeterminado (`Process.Start` con `UseShellExecute = true`).
- **Consola:** título «DM Companion — no cierres esta ventana mientras juegas». Al arrancar muestra la URL y la carpeta de registros.

### 5.3 Seguridad

- **Solo loopback:**
  - `ListenLocalhost`, más la misma comprobación que el DevHost: si alguna dirección no es loopback, se detiene.
  - Se reutiliza la lógica de `DevHostBinding.IsLoopbackOnly`, movida a un sitio compartido o duplicada con sus tests.
- **Filtro de Host:**
  - Solo se aceptan peticiones cuyo `Host` sea `localhost` o `127.0.0.1`, con el puerto efectivo.
  - El resto recibe 400 sin cuerpo.
  - Se configura con `AllowedHosts` (o un middleware equivalente) y tiene test.
- **Antiforgery activo** (como el DevHost).
- **`/_vault/`:** se sirve con el mismo endpoint y el mismo confinamiento que el DevHost (Task 5 de la fase 2).

### 5.4 Primera vez y elección del vault

- **Sin vault guardado en las preferencias**, Desktop muestra una **pantalla de bienvenida** con dos acciones:
  - **«Abrir mi vault»:** va al explorador de carpetas (`VaultPicker`).
  - **«Probar con el vault de ejemplo»:**
    - Si `Documentos\DM Companion\Ejemplo` no existe, copia ahí el vault de ejemplo que va junto al `.exe` (`ejemplo/` en el zip).
    - Después lo abre. Si la carpeta ya existe, la abre tal cual, sin sobrescribir nada.
- **La pantalla de bienvenida** vive en Screens y solo se muestra si la plataforma ofrece un vault de ejemplo. La capacidad la declara una interfaz de plataforma nueva, por ejemplo `ISampleVault { bool IsAvailable; Task<string> PrepareAsync(); }`. En Android y el DevHost no se registra (o devuelve `IsAvailable = false`), así que su flujo no cambia.
- **Cambios compartidos en `VaultPicker`**, activados por la plataforma con nuevas propiedades de `IStorageAccess` (por ejemplo `bool CanTypePath`, `IReadOnlyList<string> Roots` y `string? InitialPath`, la carpeta donde empieza el explorador dentro de la raíz). Android y el DevHost devuelven `false`, una lista vacía y `null`, así que no cambian.
  - **Campo «Pegar una ruta»:** abre esa carpeta si existe y es legible; si no, muestra un aviso.
  - **Lista de unidades** (`C:\`, `D:\`…) para navegar fuera de la carpeta del usuario.
- **En PC:**
  - La raíz del explorador es la carpeta del usuario.
  - Se empieza en `Documentos`.
  - Las unidades vienen de `DriveInfo.GetDrives()`: solo las listas, de tipo `Fixed` o `Removable`.

### 5.5 Preferencias y registros

- **Preferencias:** `%APPDATA%\DmCompanion\prefs.json`, en el mismo formato que el DevHost.
- **Registros:**
  - En `%LOCALAPPDATA%\DmCompanion\logs\dm-companion-AAAAMMDD.log`: uno al día, se guardan los 7 más recientes, con nivel `Information` y superiores.
  - Se escriben con un proveedor de `ILogger` mínimo propio, sin dependencias nuevas salvo que el plan justifique una.
  - Las excepciones se registran solo con su tipo.
- **«Abrir carpeta de registros»:**
  - Botón en `VaultDiagnostics`, visible solo si la plataforma lo ofrece. Es una interfaz nueva de plataforma, por ejemplo `ILogFolder { bool IsAvailable; void Open(); }`.
  - En PC abre el Explorador de Windows en esa carpeta.
- **Versión de la app:** se muestra en `VaultDiagnostics` (versión del ensamblado).

### 5.6 Cierre sin pérdida

- Al pararse la aplicación (`IHostApplicationLifetime.ApplicationStopping`: Ctrl+C, cerrar la consola o apagado), se llama a `MapSession.FlushAsync()` y se espera con un límite de 5 s.
- **Límite de Windows al cerrar la consola** con la X: Windows da unos 5 s al proceso. El debounce de 750 ms y el flush caben de sobra. Se documenta en la guía: «cierra la ventana; no hace falta nada más».

### 5.7 Nombres de fichero en Windows

- **Nombres reservados:** al crear o renombrar notas y mapas se rechazan `CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9` y `LPT1`–`LPT9`, con o sin extensión y sin distinguir mayúsculas.
  - La validación vive en Core (`NoteFileName` y la validación de `MapLibrary`), así que aplica en todas las plataformas.
  - Se trata como «Nombre no válido», igual que los caracteres prohibidos.
- **Longitud:** se rechaza un nombre de fichero de más de 120 caracteres, con un mensaje claro en lugar de un error de IO genérico.
- **Revisión de Windows** (comprobaciones del plan, con tests donde se pueda desde Linux):
  - separadores `\` en rutas que llegan del explorador o pegadas;
  - comparación de rutas sin distinguir mayúsculas en el confinamiento y en `IsSafe`;
  - eventos del vigilante de ficheros al renombrar (Windows emite `Renamed`);
  - finales de línea CRLF en las notas al editar el frontmatter.

### 5.8 Empaquetado y guía

- **`scripts/publish-desktop.sh`:**
  - Ejecuta `dotnet publish src/DmCompanion.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
  - Copia `samples/vault-demo` a `ejemplo/` junto al `.exe` y añade la guía.
  - Genera `dist/dm_companion_pc.zip`. `dist/` no se commitea.
- **Guía** (`docs/guia-version-pc.md`, que se copia al zip como `LEEME.md`, en español y en una página):
  - cómo descomprimir y abrir, y el aviso de SmartScreen («Más información → Ejecutar de todas formas»);
  - qué hacer la primera vez;
  - qué funciona (notas, fichas, mapa, tokens, dibujo, medición, HP) y qué no (combate, glosario, sonido, vista TV);
  - cómo cerrar;
  - cómo mandar los registros.
- **Actualizar:** se sustituye la carpeta por la nueva. Las preferencias y el ejemplo copiado se conservan porque viven fuera.
- **Versión:** `Version` del proyecto Desktop, empezando en `0.1.0`. Se sube en cada entrega.

## 6. Errores

- **Vault ilegible o ruta pegada inexistente:** aviso en la pantalla, sin excepción. Igual que hoy en `VaultPicker`.
- **Copia del ejemplo fallida** (permisos, disco): aviso «No se pudo preparar el vault de ejemplo». No se abre nada y se registra solo el tipo.
- **No se puede abrir el navegador:** la consola muestra la URL para abrirla a mano.
- **Puerto 5190 ocupado:** se usa otro libre, sin avisar.
- **Fallo al escribir registros:** no detiene la app. Se ignora tras el primer aviso en consola.

## 7. Verificación y testing

- **`DmCompanion.Desktop.Tests`:**
  - solo loopback (direcciones no loopback → se detiene);
  - filtro de Host (`evil.example` → 400; `localhost:<puerto>` → 200);
  - `/_vault/` con el mismo confinamiento (traversal codificado → 404);
  - preferencias en la carpeta indicada;
  - el registro rota y no guarda mensajes de excepción;
  - el flush al parar.
- **Screens:**
  - la bienvenida aparece sin vault y con `ISampleVault` disponible;
  - no aparece sin `ISampleVault`;
  - «Probar con el ejemplo» llama a `PrepareAsync` y abre la ruta;
  - `VaultPicker` con `CanTypePath` muestra el campo y abre la ruta, y con una ruta inexistente avisa;
  - las unidades solo aparecen si la plataforma las da.
- **Core:** nombres reservados y longitud en `NoteFileName` y en la validación de mapas.
- **Infrastructure:**
  - la copia del ejemplo no sobrescribe una carpeta existente (`TempVault`/carpetas temporales);
  - unidades filtradas por tipo (con una abstracción mínima si hace falta para testearlo en Linux).
- **Prueba de humo desde WSL:** lanzar el `.exe` publicado con la interoperabilidad de WSL, comprobar que `http://localhost:5190/` responde 200 y que una segunda ejecución sale con código 0. Después, cerrarlo.
- **Prueba manual en Windows** (checklist manual corta en la guía):
  - SmartScreen;
  - primera vez con el ejemplo;
  - abrir su vault con la ruta pegada;
  - mapa, tokens y HP;
  - cerrar con la X y volver a abrir sin pérdida;
  - segunda instancia;
  - «Abrir carpeta de registros».

## 8. Qué decide quien construya

- Si `JsonFileAppPreferences` y la comprobación de loopback se mueven a un sitio compartido o se duplican, siempre con tests.
- Cómo se obtiene un puerto libre de reserva.
- El formato exacto de las líneas del registro.
- La disposición de la pantalla de bienvenida, con los componentes de `DmCompanion.UI`.
