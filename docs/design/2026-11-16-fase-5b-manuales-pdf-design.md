# DM Companion — Fase 5b: visor de manuales en PDF

- **Estado:** borrador para revisión.
- **Orden de fases:** 6 → 7 → 4 → 5 → **5b** → 8.
- **Relacionado:**
  - Fase 5 (`2026-11-16-fase-5-glosario-design.md`): el panel 📖 y la decisión de no distribuir manuales comerciales.
  - Fase 7 (`2026-11-15-fase-7-segunda-pantalla-design.md`): el contenido en la TV.
  - Diseño global §9 (sistemas de juego).
- **Decisiones (16/11):**
  - Los manuales viven en `manuales/<sistema>/` dentro del vault, con una subcarpeta por sistema de juego y varios PDF por sistema (básico, expansiones…).
  - Solo se ven los del sistema activo.
  - Índice del PDF con filtro e ir a página.
  - Además: buscar texto, recordar la última página, marcadores propios y mostrar en la TV la página entera o un recorte.

## 1. Intención

Cada persona pone en su vault los manuales en PDF que tenga, de D&D, Cosmere o cualquier otro sistema. La app los abre sin conexión, sin distribuir nada. En la mesa, el autor quiere:
- saltar a una sección por el índice del PDF o a una página concreta;
- buscar una palabra;
- volver a donde lo dejó;
- guardar sus páginas de referencia;
- enseñar a los jugadores una página o una tabla en la TV.

## 2. Objetivos

- Listar los PDF de `manuales/<id del sistema activo>/`, subcarpetas incluidas.
- Abrir un manual en un visor dentro del panel 📖, en la pestaña «Manuales».
- **Índice:**
  - Las secciones y subsecciones salen de los marcadores del PDF (outline), con su página.
  - Un filtro de texto conserva los padres de lo que coincide.
- **Ir a página:**
  - «Página [N] de M», anterior y siguiente;
  - pellizcar para el zoom;
  - deslizar para pasar de página.
- **Buscar en el manual:** lista de páginas con un fragmento de texto; tocar un resultado salta a esa página.
- **Última página leída** por manual y por dispositivo.
- **Marcadores propios** con nombre, en un fichero junto al PDF que viaja con el vault.
- **TV:** «Mostrar página en la TV» y «Recortar para la TV».
- Debe funcionar en Android, en la versión de PC y en el DevHost.

## 3. No objetivos

- Anotar, subrayar o editar el PDF.
- PDF fuera de `manuales/`, ni manuales de otros sistemas en un vault.
- Descargar o distribuir manuales.
- Extraer el contenido del PDF al glosario. El importador de Cosmere de la fase 5 sigue aparcado.
- PDF con contraseña: se muestra el error y nada más.

## 4. Descubrimiento de manuales

- **Ruta:** `manuales/<id>/**/*.pdf`. El `<id>` es el del sistema activo de la campaña (`dnd5e`, `cosmere`…, ver §9 del diseño global). Se compara sin distinguir mayúsculas.
- **Servicio `ManualLibrary`** (en Screens):
  - lista los manuales con el listado de ficheros del vault que ya existe (`IVaultStorage.ListFilesAsync`);
  - se actualiza con los cambios de ficheros del vault (`VaultService.FileChanged`) y al cambiar de vault;
  - cada manual tiene título (el nombre del fichero sin extensión), ruta relativa y subcarpeta. Los de subcarpetas se agrupan bajo el nombre de su subcarpeta.
- Sin carpeta o sin manuales, el panel lo explica: «Pon tus manuales en PDF en `manuales/dnd5e/` dentro del vault».

## 5. Visor

### 5.1 pdf.js

- **pdf.js** (`pdfjs-dist`, Apache-2.0) va embebido en Screens con una versión fijada. La última estable hoy es la 6.5.438 (01/11/2026); el plan fija la exacta. Funciona sin conexión.
- Se cargan el módulo y su worker.
- Un único servicio de interop, `ManualViewerInterop` (Screens), y un JS propio `manual-viewer.js`, que no es JS de la TV y por eso puede ser un módulo ES. Expone:
  - abrir un documento por URL `/_vault/…`;
  - pintar una página en un canvas a una escala;
  - leer el outline y resolver el destino de cada entrada a número de página;
  - extraer el texto de una página, para buscar;
  - generar un PNG de una página o de un recorte a un tamaño dado, para la TV;
  - cerrar el documento y liberar memoria al salir del visor o al cambiar de manual.
- La conversión del outline en árbol (título, página e hijos) y el filtro del árbol son funciones puras. Viven en Core (`Manuals/ManualOutline.cs`) para probarlas sin navegador. El JS solo entrega la lista cruda (título, página, nivel).

### 5.2 Carga por rangos

Un manual puede pesar cientos de MB. pdf.js pide el fichero por rangos (`Range: bytes=…`) si el servidor responde `206 Partial Content`, y entonces solo descarga lo que necesita.

| Host | Estado de `/_vault/` hoy | Qué hay que hacer |
|---|---|---|
| DevHost | `VaultFileEndpoint`, con `enableRangeProcessing: true` | Ya lo hace |
| PC (Desktop) | Usa el mismo `MapVaultFiles` | Ya lo hace |
| Android | `VaultFileRequestHandler` devuelve el fichero entero con 200 | Hay que añadirlo |

**Android:**
- `VaultFileRequestHandler` lee la cabecera `Range` de la petición del WebView. Si es válida, responde `206` con `Content-Range` y `Accept-Ranges: bytes` y un flujo limitado a ese tramo.
- Un rango inválido da `416`.
- Sin `Range`, responde como hoy.
- La lógica de rangos (interpretar la cabecera y calcular el tramo) va en Screens (`VaultFileRequest`) para probarla sin Android.
- **Plan B** si el evento de MAUI no deja leer las cabeceras de la petición: pdf.js con `PDFDataRangeTransport`, pidiendo los trozos a C# por interop (`ReadRangeAsync(path, offset, length)` sobre `IVaultStorage.OpenReadAsync`). El plan lo comprueba primero.

### 5.3 Estado por manual

- **Última página:** `IAppPreferences`, con la clave `manual.page.<ruta relativa>`. Es por dispositivo y no escribe en el vault.
- **Marcadores:** `<manual>.marcadores.json` junto al PDF; por ejemplo, `manuales/dnd5e/Manual del jugador.pdf` lleva `manuales/dnd5e/Manual del jugador.marcadores.json`.
  - Contenido: `{ "version": 1, "bookmarks": [ { "id", "name", "page" } ] }`.
  - Se escribe con `WritePrecondition.UnchangedSince(fecha leída)`, igual que el `.state.json`.
  - Si el fichero cambió fuera de la app, se vuelve a leer, se fusiona añadiendo los marcadores nuevos y se reintenta una vez.
  - Un fichero corrupto **no se sobrescribe**: se ignora, se avisa en Diagnóstico y el botón de añadir queda desactivado para ese manual con el aviso «El fichero de marcadores está dañado».
  - Los ficheros `*.sync-conflict-*` se tratan como el resto de conflictos de Syncthing: se listan en Diagnóstico y no se fusionan solos.
  - Su lógica (modelo, lectura, fusión) va en Core (`Manuals/ManualBookmarks.cs`) y la escritura, en el servicio de Screens que usa `VaultService`.

## 6. Interfaz

- **Panel 📖, con pestañas «Glosario» y «Manuales».** La pestaña elegida se recuerda en la sesión (`ShellState`).
- **Pestaña Manuales:** la lista de manuales, agrupada por subcarpeta. Tocar uno abre el visor.
- **Visor:**
  - **Barra:**
    - «Atrás», que vuelve a la lista;
    - título;
    - «Anterior», «Página [N] de M» con su campo, «Siguiente»;
    - «Índice», «Buscar», «Marcadores», que alternan el lateral;
    - «Mostrar en TV» y «Recortar para la TV».
  - **Lienzo:** la página ajustada al ancho del panel, con zoom de pellizco y desplazamiento.
  - **Lateral plegable**, con tres vistas:
    1. **Índice:** árbol con un filtro «Filtrar secciones». Sin outline, el mensaje es «Este PDF no tiene índice».
    2. **Buscar:** campo «Buscar en el manual»; los resultados dicen «p. 123 · …fragmento…». La búsqueda avanza por páginas en segundo plano, se puede cancelar y enseña los resultados según aparecen. Sin texto, el mensaje es «Este manual no tiene texto que buscar».
    3. **Marcadores:** lista con «p. N · nombre», el botón «Añadir marcador aquí» (pide un nombre; por defecto, el título de la sección que contiene la página) y la opción de borrar cada uno.
  - **«Atrás» de Android:** primero cierra el lateral, luego vuelve a la lista y luego cierra el panel.
- Panel ancho por defecto, con la opción de ampliar el visor a casi toda la pantalla («Ampliar» / «Reducir»).

## 7. TV

- **«Mostrar en TV»:** el JS pinta la página actual en un PNG ajustado a 1920×1080 manteniendo la proporción. La imagen se registra en la lista blanca de ficheros de la TV y se manda como contenido de la fase 7, con el título «<Manual> · p. N» y la imagen.
- **«Recortar para la TV»:** entra en un modo en el que se arrastra un rectángulo sobre la página; «Enviar» o «Cancelar». Se pinta solo esa zona a la escala que llene la TV (con un límite de 1920×1080) y se manda igual.
- **Comportamiento de la TV:**
  - Lo enviado se queda en la TV aunque cambie el vault, como pasa con el glosario. Al cambiar de vault, la TV vuelve al mapa.
  - «Volver al mapa», como hoy.
  - Los botones solo se ven con la TV activa.
- La imagen de la TV se crea en memoria, nunca se escribe en el vault y se libera al cambiar de contenido.

## 8. Errores

- PDF dañado o con contraseña: «No se pudo abrir este manual». El log lleva solo el tipo de error de pdf.js y la ruta relativa.
- Si el fichero desaparece mientras está abierto, se vuelve a la lista con un aviso.
- Si no hay memoria al pintar una página muy grande, la escala baja y se reintenta una vez; después se muestra un aviso.
- Marcadores: ver §5.3.
- Ningún error de contenido es una excepción no capturada.

## 9. Tests

- **Core:**
  - `ManualOutline`: de la lista plana al árbol y el filtro, que conserva los padres y no distingue tildes ni mayúsculas.
  - `ManualBookmarks`: leer, serializar, fusionar y fichero corrupto.
  - Rutas `manuales/<sistema>`.
- **Screens:**
  - `VaultFileRequest`: interpretar `Range` (`bytes=0-99`, `bytes=100-`, `bytes=-500`, inválido o fuera de tamaño).
  - `ManualLibrary`: el sistema activo, las subcarpetas, Cosmere y la actualización con `FileChanged`.
  - Servicio de marcadores sobre un `TempVault`: escribir, el cambio externo con fusión y no sobrescribir un fichero corrupto.
  - Última página en preferencias.
- **DevHost:** un test de integración que pide `/_vault/…pdf` con `Range` y recibe `206`.
- **Android:** el handler con rangos se prueba en la tablet (checklist), apoyado en los tests de `VaultFileRequest`.
- **bUnit** (con el interop simulado):
  - pestañas, lista y visor;
  - ir a página, índice y filtro;
  - resultados de búsqueda;
  - añadir y borrar marcadores;
  - los botones de TV visibles solo con la TV activa;
  - el modo recorte;
  - el «Atrás».
- **Node** (`tests/js`): la parte pura de `manual-viewer.js`, que es el cálculo del rectángulo de recorte en coordenadas del PDF y la escala para la TV.

## 10. Checklist de la tablet

- Un manual de más de 100 MB abre en pocos segundos y pasar de página es fluido. Comprobar que no lo carga entero, por la memoria y el log.
- El índice lleva a la sección y el filtro funciona.
- «Ir a página 250».
- La búsqueda de una palabra encuentra resultados y salta a ellos.
- Al cerrar y volver, el manual se abre en la misma página.
- Un marcador añadido en la tablet aparece en el PC tras sincronizar con Syncthing.
- Página entera y recorte de una tabla en la TV.
- Vault Cosmere: solo se ven sus manuales.
- Un PDF escaneado sin texto: la búsqueda lo dice.
- Funciona en modo avión.
