# DM Companion — Fase 5c: lectura continua en el visor de manuales

- **Estado:** borrador para revisión.
- **Parte de:** fase 5b (`2026-11-16-fase-5b-manuales-pdf-design.md`), integrada en `main` (merge `b735489`).
- **Decisiones (16/11):**
  - El visor pasa a ser **solo continuo**: todas las páginas una debajo de otra, sin selector de modo.
  - Se sigue pudiendo ir a una página concreta.
  - Se hace después de integrar la 5b en `main`.

## 1. Intención

Leer un manual como un documento: bajar y subir con la rueda del ratón en el PC o con el dedo en la tablet, sin pulsar «Siguiente» en cada página. La navegación de la 5b (ir a página, índice, búsqueda y marcadores) se conserva.

## 2. Objetivos

- Las páginas del manual se muestran en una columna con desplazamiento vertical.
- La rueda del ratón y el arrastre con un dedo desplazan la columna.
- «Página [N] de M» muestra la página que se está leyendo.
- «Ir», «Anterior», «Siguiente», el índice, la búsqueda y los marcadores desplazan la columna hasta la página pedida.
- La memoria y la descarga por rangos no crecen con el tamaño del manual: solo se pintan las páginas cercanas a la vista.

## 3. No objetivos

- Selector «Página a página / Continuo».
- Vista de dos páginas enfrentadas o desplazamiento horizontal.
- Miniaturas de las páginas.
- Cambios en la búsqueda, los marcadores, la carga por rangos de Android o el contenido de la TV.

## 4. Pintado (`manual-viewer.js`)

### 4.1 Columna de páginas

- Dentro del host del visor hay una columna con un hueco por página (`div.manual-page-slot`, con `data-page="N"`). El hueco tiene la altura de la página a la escala actual, con una separación fija entre páginas.
- **Alturas sin leer todo el PDF:**
  - Al abrir, todos los huecos toman el tamaño de la página 1.
  - Cuando una página se carga (`getPage`) y su tamaño es otro, su hueco se corrige.
  - Si la corrección afecta a un hueco que está por encima de la vista, el desplazamiento se compensa en la misma diferencia para que lo que se está leyendo no salte.
- **Ventana de pintado:**
  - Se pintan la página actual y una página por arriba y otra por abajo.
  - La visibilidad se detecta con `IntersectionObserver`, sobre el host y con un margen de una pantalla.
  - Cada página pintada tiene su propio `canvas` dentro de su hueco.
  - Cuando un hueco sale de la ventana, su `canvas` se vacía (ancho y alto 0) y se quita, se cancela su `renderTask` si la hay y se llama a `page.cleanup()`. El `manual-page-tracker.js` se adapta para llevar un conjunto de páginas visibles en lugar de una sola.
- **Página actual:** la página cuyo hueco ocupa más altura visible en el host. Si hay empate, gana la de número más bajo.
- **Aviso a C#:** cuando cambia la página actual, el JS llama a `onPageChanged(N)` en el objeto de callbacks. Lo hace con un retardo de 150 ms y solo si `N` es distinto del último que avisó.
- El cálculo de la ventana de pintado, de la página actual y de la compensación de altura son funciones puras en un JS clásico nuevo (`manual-scroll.js`, con el mismo patrón que `manual-geometry.js`) y se prueban con `node --test`.

### 4.2 Desplazamiento y zoom

- La rueda del ratón sin Ctrl y el arrastre con un dedo desplazan el host, que hace el navegador y, en la tablet, `touch-action: pan-y pan-x`.
- Desaparecen el deslizamiento para pasar de página (`onSwipe`) y su cálculo (`swipeDirection`), junto con sus tests.
- **Zoom:**
  - Pellizco en la tablet y Ctrl + rueda en el PC, como en la 5b.
  - Cambia el ancho de todas las páginas (ancho del host × zoom).
  - Conserva la página actual y la fracción de esa página que estaba en lo alto de la vista.
  - Durante el pellizco, la vista previa es una transformación CSS de la columna; al soltar, se recalculan los huecos y se repinta la ventana.
- **Redimensionado del host** (por ejemplo, «Ampliar» o «Reducir»): recalcula los huecos y conserva la posición igual que el zoom.

### 4.3 Ir a una página

- `showPage(N)` desplaza el host para que el borde superior del hueco N quede arriba, sin animación, y devuelve:
  - `true` en cuanto la página N queda pintada;
  - `false` si no se pudo pintar ni con el reintento a media escala de la 5b.
- `open(host, url, page, callbacks)` monta la columna, desplaza hasta `page` y devuelve lo mismo que hoy (`pageCount`, `outline`, `hasText`).
- Si falla la primera página que se pinta al abrir, la apertura falla con «No se pudo abrir este manual», como en la 5b.

### 4.4 Errores de pintado

- **Reintento:** si una página falla por algo que no sea una cancelación, se reintenta una vez a la mitad de escala.
- **Si falla también el reintento:**
  - su hueco muestra el texto «No se pudo mostrar esta página»;
  - el resto del manual sigue funcionando;
  - se registra en el log el tipo de error, sin la ruta completa (§8 de la 5b).
- Un pintado cancelado al desplazarse no es un error.

## 5. C# (`ManualViewer`, `ManualViewerInterop`)

- `ManualViewerInterop` expone un evento o callback `PageChanged(int page)`, que llega desde `onPageChanged`.
- `ManualViewer` actualiza `Context.Page` y el campo «Página [N] de M» al recibir `PageChanged`, sin volver a llamar a `ShowPageAsync`, para que no haya bucle.
- **Botones:**
  - «Anterior» y «Siguiente» llaman a `ShowPageAsync(Page ± 1)`.
  - «Ir» llama a `ShowPageAsync(N)`.
  - El índice, la búsqueda y los marcadores siguen usando `Context.GoToPageAsync`, que llama a `ShowPageAsync`.
- **Última página leída** (`ManualPositions`): se guarda al recibir `PageChanged`, además de cuando se navega con los botones.
- El aviso «No se pudo mostrar esta página» de la 5b cuando `ShowPageAsync` devuelve `false` se mantiene.
- Desaparece el manejador `OnSwipe` del interop y del visor.

## 6. Recorte y TV

- «Mostrar en TV» envía la página actual, sin cambios respecto a la 5b.
- **«Recortar para la TV»:**
  - el velo se coloca sobre el hueco de la página actual y sigue a ese hueco si cambian el tamaño o el zoom;
  - mientras dura el recorte, el desplazamiento del host queda bloqueado (`overflow: hidden`), y también el zoom y los botones de navegación (`Context.Cropping`, como en la 5b);
  - al terminar o cancelar, se restaura el desplazamiento.

## 7. Tests

- **Node** (`tests/js/manual-scroll.test.js`):
  - ventana de pintado según la posición y la altura de la vista;
  - página actual, incluido el empate;
  - compensación de desplazamiento al corregir la altura de un hueco por encima de la vista;
  - posición conservada al cambiar el zoom.
- **Node:** el tracker de páginas con varias páginas visibles.
- **bUnit (interop simulado):**
  - `PageChanged` actualiza el contador sin llamar a `ShowPageAsync`;
  - «Ir», «Anterior», «Siguiente» y el salto desde el índice llaman a `ShowPageAsync` con la página correcta;
  - `PageChanged` guarda la última página;
  - durante un recorte, los botones de navegación están desactivados.
- Se quitan los tests del deslizamiento.

## 8. Checklist de la tablet (sección «Lectura continua»)

- Bajar por un manual de más de 100 MB es fluido, el contador de página sigue lo que se ve y la memoria no crece sin límite.
- La rueda del ratón en el PC baja y sube; Ctrl + rueda hace zoom sin perder la página.
- «Ir a página 250», el índice, la búsqueda y los marcadores llevan a la página correcta.
- Al cerrar y volver, el manual se abre en la página en la que se estaba leyendo.
- El recorte para la TV funciona sobre la página que se ve y no deja desplazarse mientras dura.
- Un PDF con páginas de tamaños distintos no salta al desplazarse.
