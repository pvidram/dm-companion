# Crear vault desde la app — diseño

Fecha: 22/11/2026. Estado: aprobado.

## 1. Intención

El DM quiere empezar una campaña nueva desde la tablet (o el PC) sin preparar nada fuera: crear la carpeta, convertirla en vault y abrirla, todo desde la app. Hoy el explorador de carpetas solo navega carpetas existentes y, si la elegida no parece un vault, solo avisa («Abrir de todos modos»).

Éxito: en la tablet, desde «Cambiar vault», crea `Campañas/La Marca del Este`, pulsa «Crear vault aquí», pone nombre y sistema, y la app abre el vault nuevo listo para añadir jugadores y mapas; en el PC, Obsidian lo reconoce como vault.

## 2. Objetivos, no objetivos y antiobjetivos

**Objetivos**
- Botón **«Nueva carpeta»** en el explorador de carpetas de la app (`VaultBrowser`), en el selector inicial (`VaultPicker`) y en «Cambiar vault» (`VaultPanel`): pide un nombre y crea la carpeta dentro de la carpeta actual, y navega a ella.
- Al usar una carpeta que no parece un vault, el aviso ofrece **«Crear vault aquí»** además de «Abrir de todos modos» y «Cancelar».
- «Crear vault aquí» pide **nombre de la campaña** y **sistema** (D&D 5e o Cosmere; por defecto D&D 5e) y escribe:
  - `.obsidian/` (carpeta, para que Obsidian lo reconozca; con un `app.json` `{}` como el vault de ejemplo),
  - la nota de campaña en la raíz (`type: campaign`, `name`, `system`), con el mismo contenido que genera hoy `NoteTemplates` para una campaña.
- Después abre el vault (mismo camino que hoy: `Host.OpenAsync` en el selector inicial, `VaultBrowsing.SwitchAsync` en «Cambiar vault», con su confirmación de cambios sin guardar).

**No objetivos**
- Crear carpetas de contenido (`Personajes/`, `Mapas/`…): la app ya las crea al escribir.
- Plantillas de vault, notas de ejemplo o copiar el vault de ejemplo.
- Renombrar o borrar carpetas desde el explorador.

**Antiobjetivos**
- Sobrescribir o tocar ficheros existentes del usuario (el vault es sagrado): si ya hay una nota con el nombre de la campaña o un `.obsidian/`, no se pisa.
- Crear un vault en la raíz de una unidad o de la memoria interna (`/storage/emulated/0`).
- Que un nombre de carpeta inválido (barras, `..`, reservados de Windows) cree algo fuera de la carpeta actual.

## 3. Restricciones

- Arquitectura: la lógica de crear vault en `DmCompanion.Core` (testeable sin plataforma); el acceso al disco detrás de interfaces existentes (`IFolderBrowser` gana crear carpeta; la escritura de la nota y `.obsidian` con `FileSystemVaultStorage`/`IVaultStorage` o una interfaz equivalente de Core implementada en Infrastructure). Screens solo pinta y llama.
- Nombres de fichero válidos en Android y Windows: reutilizar `NoteFileName.FromTitle` para la nota y una validación equivalente para la carpeta.
- La decisión «¿parece un vault?» es hoy `IFolderBrowser.LooksLikeVault` (bool). Hay que distinguir **raíz de unidad** (sin oferta de crear) de **carpeta sin notas** (con oferta).
- Tests de Infrastructure contra carpetas temporales reales; nunca el vault real.

## 4. Enfoques considerados

- **Servicio de creación en Core + «Nueva carpeta» en el explorador** — *elegido*. Reutiliza la escritura y la apertura existentes.
- Copiar un vault plantilla (como el de ejemplo del PC) — descartado: en Android obliga a empaquetar ficheros, y el de ejemplo trae notas de prueba que no se quieren en una campaña nueva.

## 5. Comportamiento (lo decidido)

- «Nueva carpeta»: diálogo con un campo de nombre; «Crear» deshabilitado si está vacío; error en línea si el nombre no es válido o ya existe; al crear, el explorador entra en la carpeta nueva.
- Al pulsar «Usar esta carpeta»:
  - parece vault (`.obsidian/` o notas `.md`) → se abre como hoy;
  - raíz de unidad → el aviso de hoy, sin «Crear vault aquí»;
  - carpeta sin notas ni `.obsidian/` → aviso con «Crear vault aquí», «Abrir de todos modos» y «Cancelar».
- «Crear vault aquí» → diálogo con nombre de campaña (obligatorio; por defecto el nombre de la carpeta) y sistema (desplegable con los sistemas conocidos del registro). «Crear» escribe y abre.
- Errores de disco (permiso, IO) → aviso en línea en el diálogo; no se abre nada a medias. Si `.obsidian/` se creó pero la nota falló, se informa y no se deshace (no borramos nada del usuario).

## 6. Decisiones de diseño

- La nota de campaña se llama como la campaña (`<nombre>.md`) en la raíz, como `Campaña.md` en el ejemplo.
- Si la carpeta tiene notas `.md` pero no `.obsidian/`, no se ofrece crear nada: ya funciona como vault (y se evita escribir en carpetas del usuario que ya usa).
- «Nueva carpeta» también está en el selector inicial (primer arranque), no solo en «Cambiar vault».
- **Raíces donde no se ofrece crear:** la raíz del sistema de ficheros (`Path.GetPathRoot`: `/`, `C:\`, UNC) **y** las raíces que da la plataforma en `IStorageAccess` (`DefaultBrowseRoot` y `Roots`; en Android, `/storage/emulated/0`). En ellas sigue el aviso de hoy, sin «Crear vault aquí». «Nueva carpeta» sí funciona dentro de ellas.
- **Carpeta que no se puede examinar** (error de IO o permiso, o se alcanza el tope de 2000 entradas de la búsqueda): resultado «desconocida» → aviso de hoy, sin ofrecer crear (más vale no escribir donde no sabemos qué hay).
- **Escritura de `.obsidian/`:** la hace el servicio de creación con acceso directo al disco en Infrastructure (crear carpeta + `app.json` sin sobrescribir), no `IVaultStorage`, que trata los segmentos ocultos como no indexables.
- **Orden y comprobaciones:** antes de escribir nada se comprueba que no existen ni la nota de campaña ni `.obsidian/`; si alguna existe, error en línea y no se escribe nada. Después se escribe primero la nota y luego `.obsidian/`. Si falla lo segundo, se informa y no se borra la nota.
- **Nota de campaña:** `type: campaign`, `name` (con el escape YAML que ya usa `NoteTemplates`), `system` = id del sistema en `GameSystemRegistry` (`dnd5e`, `cosmere`), y el cuerpo actual de la plantilla de campaña. Nombre de fichero con `NoteFileName.FromTitle`; si el nombre queda vacío al sanearlo, error en línea.
- **Nombre de carpeta:** se rechaza (no se sanea) si contiene caracteres inválidos en Windows o Android (`/ \ : * ? " < > |`), es `.` o `..`, empieza por punto (quedaría oculta en el explorador), es un nombre reservado de Windows o termina en espacio o punto; los espacios iniciales y finales se recortan antes de validar. «Ya existe» se comprueba sin distinguir mayúsculas.
- Sin permisos nuevos ni SAF en Android: si el sistema rechaza crear la carpeta, error en línea.

## 7. Lo que queda al constructor

- Nombres de tipos y métodos; si `LooksLikeVault` pasa a devolver un enum (`Vault`, `DriveRoot`, `Empty`/`NotAVault`) o se añade un método aparte.
- Dónde viven los dos diálogos nuevos (patrón de `NewNoteDialog`/`ConfirmDialog` y estado en `ShellState` o local al componente).
- Tests: Core (contenido escrito, nombres inválidos, no sobrescribir), Infrastructure (crear carpeta y vault en temporal), Screens (botones y flujo con bUnit), y que los tests existentes que cuentan diálogos (`NotesPanelTests`) se ajusten.
