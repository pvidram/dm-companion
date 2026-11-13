# DM Companion — Fase 2: Mapa con tokens

- **Fecha:** 2026-11-12
- **Estado:** aprobado
- **Depende de:** [spec global](2026-11-03-dm-companion-design.md) (§3 arquitectura, §4 modelo del vault, §5 interfaz, §6 fases) y fase 1 (Vault) implementada.
- **Integrado de la fase 2 anterior (13/11):** sistemas de juego (spec global §9: nota de campaña, `IGameSystem`, D&D 5e y Cosmere provisional) y los componentes de UI de `feature/fase-2-ui`. `IGameSystem.Grid` (`GridRules`: unidad por casilla y regla de diagonales) existe pero el mapa aún no lo usa; se conecta en la Task 8 (medición).

## 1. Intención

Que el fondo de la app, que hoy es un marcador de posición («El mapa llegará en la fase 2»), sea un mapa real. Es la capa base permanente sobre la que se superponen los paneles.

El objetivo es poder dirigir una escena en mesa con la tablet. Para eso hay que:

- Abrir un mapa de batalla, de ciudad o de región.
- Poner en él a los PJ, a los NPC y a enemigos improvisados.
- Moverlos y medir distancias.
- Consultar los datos de cada uno de un toque.

El mapa se puede **importar** como imagen o **dibujar a mano alzada** en la tablet con el lápiz. Se quiere ya en esta fase. Por eso la herramienta de lápiz, que la spec global situaba en la fase 4, se adelanta a esta. La niebla se queda en la fase 4.

## 2. Objetivos

1. **Mapas importados:** cualquier imagen PNG, JPG o WebP del vault, sin límite de tamaño de fichero.
2. **Mapas dibujados:** un lienzo en blanco donde se dibuja a mano alzada con el lápiz. También se puede dibujar encima de un mapa importado.
3. **Cuadrícula:** se activa o desactiva en cada mapa, con tamaño de casilla ajustable. Los mapas no suelen traerla dibujada.
4. **Tokens:**
   - Pueden salir de una nota de PJ o NPC, varias veces de la misma nota («Goblin 1», «Goblin 2»…), o crearse como **genéricos** sobre la marcha, sin nota.
   - Muestran el retrato o la inicial, con un anillo de color por bando, y ocupan 1×1, 2×2 o 3×3 casillas.
   - Se mueven arrastrándolos.
5. **Tarjeta rápida:** al tocar un token aparecen sus datos: AC, HP máximos, velocidad y bando. Tiene «Ver más», que abre la ficha completa si el token viene de una nota. La tarjeta permite **editar el token**: bando y tamaño en todos, y nombre, AC y HP en los genéricos. No tiene botones de daño ni de curación.
6. **Medición** de distancias en pies sobre la cuadrícula.
7. **Varios mapas:** un panel 🗺 para elegirlos, crearlos, renombrarlos y borrarlos, y wikilinks desde las notas que abren el mapa enlazado. Se recuerda el último mapa abierto de cada vault.
8. **Importación** desde el PC (dejando la imagen en `Mapas/` del vault) y desde la tablet (eligiendo la imagen de la galería o de Descargas).

## 3. No objetivos (fuera de esta fase)

- Niebla de guerra, tokens ocultos, previsualización TV y vista de jugador (fases 4 y 7).
- Botones de daño y curación, HP temporales, estados e iniciativa (fase 3). Los HP actuales sí se editan desde la tarjeta escribiendo en su número (§7.9, añadido el 13/11).
- Monstruos del glosario como tokens (fase 5).
- Herramientas de dibujo más allá del trazo libre: formas, líneas rectas, relleno, texto y capas.
- Exportar el mapa dibujado a imagen.
- Lienzo infinito. Los lienzos en blanco tienen un tamaño fijo.
- Rotar el mapa y usar cuadrículas hexagonales.

## 4. Anti-objetivos (sería un fracaso aunque funcionase)

- **Perder trabajo.** Un token movido o un trazo dibujado no puede desaparecer por un cierre de la app, un cambio de mapa o una sincronización.
- **Trazos fantasma al dibujar.** Apoyar la palma o mover el mapa con el dedo nunca debe dejar trazos.
- **Mover tokens sin querer** mientras se dibuja.
- **Tocar las notas de los personajes.** El estado del mapa vive en su `.state.json`; las notas de PJ y NPC nunca se modifican desde el mapa.
- **Lentitud con mapas grandes.** Una imagen de varios MB no puede bloquear la UI ni pasar entera por el puente C# ↔ JS.

## 5. Restricciones

- Hay que respetar la arquitectura de la spec global:
  - La lógica vive en `Core` y se testea sin emulador.
  - La App es solo UI más puente.
  - El interop C# ↔ JS se limita a «estado → JS» y «eventos JS → C#».
  - El mapa se dibuja con Konva.js.
- **Sin conexión:** Konva.js va empaquetado dentro de la app (`wwwroot`), nunca desde una CDN.
- **El DevHost tiene que poder mostrar el mapa** para probarlo en el navegador. Solo escucha en loopback.
- **Tablet de referencia:** Android 15, 12", con lápiz activo.
- **Formato del vault compatible con Obsidian:** las notas `type: map` son Markdown con frontmatter normal y el `.state.json` lo genera la app.
- **La sincronización es externa** (Syncthing u Obsidian Sync). El `.state.json` puede cambiar en disco mientras la app lo tiene abierto.

## 6. Enfoques considerados

### 6.1 Cómo llega la imagen del mapa al WebView

| Enfoque | Ventajas | Inconvenientes |
|---|---|---|
| **A. URL virtual interceptada** (**elegido**) | Sin límite de tamaño. El WebView carga y cachea la imagen como cualquier otra. En la App, `BlazorWebView.WebResourceRequested` (disponible en MAUI 10, verificado en el paquete 10.0.110) responde con un stream del fichero. En el DevHost basta un endpoint. | Hay dos implementaciones, una por host, y hay que validar las rutas para no salir del vault. |
| B. Data URL en base64, como los retratos actuales | Ya existe. | Copia el fichero entero en memoria tres veces (bytes, base64 y DOM). En el DevHost pasa por el circuito SignalR. Es el motivo del límite de 512 KB. |
| C. Copiar la imagen a la caché de la app y servirla desde ahí | — | Duplica ficheros y obliga a invalidar la caché. No aporta nada sobre A. |

**Decisión:** A. Los retratos migran también a A, con lo que desaparece el límite de 512 KB (§7.7).

### 6.2 Cómo se guarda lo dibujado

| Enfoque | Ventajas | Inconvenientes |
|---|---|---|
| **A. Trazos vectoriales en el `.state.json`** (**elegido**) | Se pueden retocar y borrar trazo a trazo. Se ven nítidos con cualquier zoom. Ocupan poco. Comparten fichero con los tokens. | Con miles de trazos el JSON crece, aunque para bocetos sigue siendo aceptable. |
| B. Rasterizar a un PNG junto al mapa | Obsidian lo ve como imagen. | No se puede retocar. Pierde calidad al hacer zoom. Hay escrituras binarias grandes en cada cambio. |

**Decisión:** A. La exportación a PNG queda fuera de esta fase.

### 6.3 Quién manda en el estado del mapa

| Enfoque | Ventajas | Inconvenientes |
|---|---|---|
| **A. C# es la fuente de verdad y JS solo pinta** (**elegido**) | Encaja con la spec global. Las operaciones (mover, añadir, borrar, deshacer) se testean en `Core`. | Cada movimiento cruza el puente, pero solo al soltar el dedo, no en cada fotograma. |
| B. El estado vive en JS y C# solo guarda el JSON | Menos interop. | La lógica queda sin tests en `Core` y va contra la arquitectura. |

**Decisión:** A. Durante un arrastre o un trazo, JS pinta en local y envía a C# **un evento al terminar** («token X a (x, y)», «trazo nuevo con puntos…»).

## 7. Diseño

### 7.1 Modelo en el vault

**Nota de mapa:** `Mapas/<Nombre>.md`

```markdown
---
type: map
name: Mazmorra del Kraken
image: Mapas/Mazmorra del Kraken.png   # opcional: sin image = lienzo en blanco
canvas: { width: 2800, height: 2100 }  # solo para lienzos en blanco (px)
---
Notas libres del DM sobre el mapa (se renderizan como cualquier nota).
```

- `image` es una ruta relativa a la raíz del vault. Puede apuntar a cualquier carpeta, aunque la convención es `Mapas/`.
- Sin `image`, el mapa es un lienzo en blanco de tamaño `canvas`. Por defecto mide 2800 × 2100 px, que son 40 × 30 casillas de 70 px.
- Si la imagen no existe o no se puede leer, el mapa se abre en blanco con un aviso. Tokens y trazos siguen disponibles.
- Si la nota pierde el `type: map`, deja de aparecer como mapa y su `.state.json` se queda huérfano. No se borra.

**Estado:** `Mapas/<Nombre>.state.json`, junto a la nota y con el mismo nombre base. Lo genera la app y no se edita a mano.

```json
{
  "version": 1,
  "grid": { "enabled": false, "cellSize": 70, "offsetX": 0, "offsetY": 0 },
  "tokens": [
    { "id": "t-8f3a", "name": "Goblin 2", "source": "Personajes/Goblin.md",
      "side": "enemy", "size": 1, "x": 455, "y": 280 },
    { "id": "t-91c0", "name": "Bandido", "source": null,
      "side": "enemy", "size": 1, "x": 525, "y": 280, "ac": 12, "hp": 11 }
  ],
  "strokes": [
    { "id": "s-02d1", "color": "#2b2b2b", "width": 6,
      "points": [10.5, 20.0, 0.5, 12.0, 24.5, 0.6] }
  ]
}
```

- **Coordenadas** en píxeles del mapa (de la imagen o del lienzo), independientes del zoom. `x`, `y` es el centro del token.
- **`source`** es la ruta de la nota de PJ o NPC, o `null` en los genéricos. `ac` y `hp` solo existen en los genéricos. En los tokens de nota, los datos se leen siempre de la nota al mostrarlos, así que un cambio en Obsidian se ve en el mapa.
- **`side`** vale `pc`, `ally` o `enemy`. Al crear desde una nota: `pc` → `pc` y `npc` → `enemy`. Se puede elegir otro al crearlo y cambiarlo después desde la tarjeta.
- **`size`** es el lado en casillas: 1, 2 o 3.
- **`points`** es una lista plana de tríos `x, y, presión` (presión entre 0 y 1; 0.5 si el dispositivo no da presión).
- **`version`**: un fichero con una versión mayor que la conocida se abre **en solo lectura** con un aviso. Uno con JSON inválido se abre vacío y en solo lectura, sin sobrescribirlo.
- Si no hay `.state.json`, el mapa está vacío. El fichero se crea en el primer cambio.

### 7.2 Componentes de Core (`DmCompanion.Core/Maps/`)

| Componente | Responsabilidad |
|---|---|
| `MapDefinition` | Se construye desde una `VaultEntity` `type: map` y da imagen o lienzo, tamaño y nombre. |
| `MapState`, `MapToken`, `MapStroke`, `GridSettings` | Modelo inmutable del §7.1. |
| `MapStateSerializer` | Pasa de JSON a modelo y al revés (System.Text.Json), con control de versión. Un JSON inválido da un resultado con error, no una excepción, igual que `FrontmatterParser`. |
| `MapEditor` | Aplica operaciones sobre `MapState`: añadir, mover, editar (bando, tamaño y, en genéricos, nombre, AC y HP) y quitar tokens, añadir y borrar trazos, cambiar la cuadrícula. Lleva una **pila de deshacer y rehacer solo para trazos**, en memoria y por mapa abierto. |
| `TokenNaming` | Da el siguiente nombre libre a partir de una nota: «Goblin», «Goblin 2», «Goblin 3»… |
| `GridMath` | Ajuste a la cuadrícula (centro de casilla según `size`) y distancia en pies. Regla 5e 2024: cada casilla, también en diagonal, vale 5 pies, así que distancia = máx(Δcasillas x, Δcasillas y) × 5. |
| `MapIndex` (o extensión de `VaultIndex`) | Lista los mapas del vault y detecta **imágenes huérfanas** en `Mapas/`, es decir, las que ninguna nota `type: map` referencia. |

### 7.3 Infrastructure

Hay que ampliar `IVaultStorage`:

- `WriteBytesAsync(relativePath, stream, precondition)`: escritura atómica (`.tmp` + move) para importar imágenes.
- `ListFilesAsync(pattern)`, o equivalente, para listar imágenes además de `*.md`.
- `OpenReadAsync(relativePath)`: devuelve un stream para servir imágenes sin cargarlas enteras en memoria.
- `MoveAsync(from, to)`: renombra un fichero sin sobrescribir el destino. Sirve para renombrar mapas.

El watcher debe notificar también los cambios en `*.state.json` y en las imágenes de `Mapas/`, no solo en `*.md`.

La resolución de rutas (`VaultPaths`) debe rechazar cualquier ruta que salga de la raíz del vault (`..`, rutas absolutas, enlaces simbólicos hacia fuera). Es crítico, porque las URL virtuales del §7.7 reciben rutas desde el WebView.

### 7.4 Guardado y sincronización del estado

- Cada cambio actualiza el `MapState` en memoria al instante. El guardado en disco se agrupa con un **debounce de unos 750 ms** desde el último cambio, y además se **fuerza** al cambiar de mapa, al cambiar de vault y cuando la app pasa a segundo plano (`OnSleep`/`OnPause`).
- La escritura es atómica, como las notas, con precondición `UnchangedSince(mtime leído)`.
- **Cambio externo** (el watcher detecta que el `.state.json` cambió en disco):
  - Si no hay cambios locales sin guardar, se recarga y se repinta.
  - Si los hay, **gana la versión local**: se escribe con `WritePrecondition.Overwrite`, que ya existe, y se avisa con un `InlineNotice` de que había una versión más reciente en disco que se ha sustituido. Es una decisión consciente: el escenario normal es usar un solo dispositivo a la vez, y no se fusiona.
- Las copias `*.sync-conflict-*.state.json` se detectan como los conflictos de notas y salen en el mismo aviso del Strip. La resolución es elegir una de las dos copias, sin fusionar.

### 7.5 Interfaz

**Capa mapa (`<Map>` de `TableFrame`):** un componente `MapCanvas` (Razor más módulo JS `map.js` con Konva) sustituye al marcador de posición. Sin mapa abierto muestra un estado vacío con «Abrir o crear un mapa».

**Capas Konva, de abajo arriba:**
1. Imagen o lienzo.
2. Cuadrícula.
3. Trazos.
4. Tokens.
5. Medición.

**Gestos:**

| Modo | Dedo | Lápiz |
|---|---|---|
| **Navegar** (por defecto) | Arrastrar el fondo mueve el mapa. Pellizcar hace zoom. Arrastrar un token lo mueve, y al soltar se ajusta a la casilla si la cuadrícula está activa. Tocar un token abre la tarjeta. | Igual que el dedo. |
| **Dibujar** | Mueve el mapa y hace zoom; **nunca dibuja**. Los tokens no se pueden tocar. | Dibuja. Con la goma activa, borra el trazo entero que toca. |
| **Medir** | Arrastrar traza una línea de origen a destino con la distancia en pies (o en px si no hay cuadrícula). Se borra al soltar. | Igual. |

- La distinción entre dedo y lápiz usa `PointerEvent.pointerType` (`touch` o `pen`). **Riesgo:** hay que verificar en la tablet de referencia que el WebView informa de `pen` y de la presión. Si no lo hace, en modo Dibujar dibuja cualquier puntero y el dedo deja de mover el mapa; pasa a moverlo un gesto con dos dedos. El plan empieza por esta verificación (§9).
- La presión, si existe, modula el grosor del trazo entre un 50 % y un 150 % del grosor elegido.
- **Barra del mapa**, flotante en una esquina:
  - Selector de modo: Navegar, Dibujar o Medir.
  - En modo Dibujar: 6 colores fijos, 3 grosores, goma, deshacer y rehacer.
  - Cuadrícula sí/no y su ajuste: tamaño de casilla con un deslizador y desplazamiento para alinearla con la imagen.
  - «Añadir token».
  - «Ajustar a pantalla».

**Tokens:**
- Tienen forma de círculo. Muestran el retrato (por URL virtual) o la inicial y llevan un anillo de color por bando. El diámetro es `size × cellSize`; sin cuadrícula se toma `cellSize` = 70.
- **«Añadir token»** abre un diálogo con:
  - Un buscador de notas `pc` y `npc` que crea el token con nombre de `TokenNaming` y bando por defecto.
  - Una pestaña «Genérico», con nombre, bando, tamaño y opcionalmente AC y HP.
  - Un botón «Añadir todos los PJ».
- Los tokens nuevos aparecen en el centro de la vista actual, sin solaparse.

**Tarjeta rápida** (superpuesta junto al token):
- Muestra nombre, bando, AC, HP máximos y velocidad. Los datos vienen de la nota o, en los genéricos, del propio token.
- Muestra también los HP actuales frente a los máximos («HP 7 / 11»), editables con un toque en el número (§7.9).
- Acciones:
  - «Ver más», solo en tokens de nota: abre la ficha en el panel correspondiente.
  - «Editar»: cambia la tarjeta a un formulario breve con bando y tamaño en todos los tokens, y nombre, AC y HP en los genéricos. En los tokens de nota, el nombre, la AC y los HP vienen de la nota y se editan en la nota.
  - «Quitar del mapa».
- No tiene botones de daño ni de curación (fase 3): los HP actuales se editan escribiendo un valor, `-daño` o `+curación` (§7.9).

**Panel 🗺 Mapas** (botón nuevo en el rail; `OverlayPanel.Maps`):
- Lista de mapas del vault. Tocar uno lo abre y cierra el panel.
- Sección «Imágenes sin mapa»: las imágenes huérfanas de `Mapas/`, cada una con «Crear mapa», que crea la nota con `image`.
- **Renombrar** (en cada mapa de la lista): renombra la nota y su `.state.json` (`MoveAsync`) y actualiza el último mapa recordado. **No reescribe los wikilinks de otras notas** que apuntaban al nombre antiguo; el diálogo lo avisa. La imagen no se renombra. Si el nombre nuevo ya existe, avisa y no hace nada.
- **Borrar** (en cada mapa de la lista): pide confirmación con el nombre del mapa y borra la nota y su `.state.json`. **La imagen se conserva** por si se usa en otro sitio, y pasa a aparecer en «Imágenes sin mapa». Si el mapa borrado estaba abierto, la capa vuelve al estado vacío.
- «Nuevo mapa» pide un nombre y ofrece dos opciones:
  - **Lienzo en blanco:** crea la nota con `canvas`.
  - **Imagen de la tablet:** abre el selector de ficheros del sistema y copia la imagen a `Mapas/<nombre>.<ext>`. Si el nombre ya existe, avisa y no sobrescribe.

**Enlaces desde notas:** un wikilink a una nota `type: map` abre ese mapa como fondo y cierra el panel de notas. La vista de lectura de una nota `type: map` muestra además un botón «Abrir mapa».

**Persistencia de la vista:** el último mapa abierto se guarda por vault en `IAppPreferences`. El zoom y el encuadre no se guardan: al abrir un mapa se ajusta a la pantalla.

### 7.6 Interop C# ↔ JS

- **C# → JS:** `mapInterop.load(definition, state, imageUrl)`, `mapInterop.applyState(state)` (repintado completo, que es suficiente para el volumen esperado), `mapInterop.setMode(mode, tool)`, `mapInterop.fit()`.
- **JS → C#:** `TokenMoved(id, x, y)`, `TokenTapped(id, screenX, screenY)`, `StrokeAdded(color, width, points)`, `StrokeErased(id)`.
- **Validación en C#:** C# valida cada evento (el token existe, las coordenadas están dentro del mapa) antes de aplicarlo con `MapEditor`.

### 7.7 Servir ficheros del vault al WebView

- `IVaultFileUrls` (en Screens) da la URL de una ruta del vault: `/_vault/<ruta codificada>?v=<mtime>`. El `v` invalida la caché del WebView cuando el fichero cambia.
- **App:** un manejador de `BlazorWebView.WebResourceRequested` atiende las peticiones a `/_vault/`:
  - Valida la ruta (§7.3) y que la extensión esté en la lista blanca: `png`, `jpg`, `jpeg`, `webp`, `gif`.
  - Responde con `OpenReadAsync` y el `Content-Type` adecuado.
  - Si falla la validación, responde `404`.
- **DevHost:** un endpoint `GET /_vault/{**path}` con la misma validación y la misma lista blanca. Sigue solo en loopback.
- Los **retratos** (`PortraitUrlProvider`) pasan a usar esta vía y desaparece el límite de 512 KB. El `InlineNotice` por imagen grande sobra y se quita.

### 7.8 Importar imagen desde la tablet

- `IImagePicker` en Screens, con dos implementaciones:
  - **App:** `FilePicker` de MAUI con tipos de imagen.
  - **DevHost:** `InputFile` de Blazor con un máximo de 50 MB.
- La copia al vault usa `WriteBytesAsync` con `MustNotExist`.

### 7.9 HP actuales (adelanto de la fase 3)

Añadido el 13/11/2026 a petición del autor, con la fase ya en marcha. Adelanta de la fase 3 solo los HP actuales por token. Daño y curación con botones, HP temporales, estados, iniciativa y la barra de vida sobre el token siguen en la fase 3.

- **Dónde se guardan:** campo opcional `hpCurrent` (entero ≥ 0) de cada token en el `.state.json`, tanto en los tokens de nota como en los genéricos. Si no existe, el token está al máximo, así que los `.state.json` anteriores siguen siendo válidos sin migrar. **La nota nunca se escribe**: dos tokens de la misma nota llevan HP distintos (spec global §6, fase 3).
- **Máximo:**
  - En un genérico es su `hp`.
  - En un token de nota es la stat de vida del sistema activo, leída de la nota en cada pintado. Cada `IGameSystem` declara cuál es: D&D `hp` («HP»), Cosmere el máximo del recurso `health` («Salud»), y en el sistema desconocido `stats.hp` si existe («HP»).
  - Sin máximo, la fila muestra «—» y no se puede editar.
- **Valor mostrado:** `mín(hpCurrent ?? máximo, máximo)`. Si el máximo baja, porque se edita la nota o el genérico, el actual nunca aparece por encima. El dato guardado solo se recorta cuando se vuelve a editar.
- **Tarjeta:** la fila se ve como «HP 7 / 11». El número actual es un botón; al pulsarlo se convierte en un campo de texto con teclado numérico y el foco puesto.
  - `12` fija 12; `-5` resta 5 (daño); `+3` suma 3 (curación). Se admiten espacios alrededor.
  - El resultado se limita entre 0 y el máximo.
  - Enter o perder el foco aplica el cambio; Esc cancela. Un texto que no es un entero (con o sin signo) no cambia nada y muestra un aviso breve en la tarjeta.
  - Si el resultado es igual al máximo, se guarda `hpCurrent` igualmente: no se borra el campo.
- **Solo lectura:** el número no es un botón.
- **Cosmere:** la fila de Salud es la primera stat que muestra su tarjeta. No contradice §9.5 de la spec global, que solo excluye las derivadas.
- **Core:** `HpEntry.Apply(int current, int max, string text)` (función pura que devuelve el nuevo valor o un error de entrada) y `MapEditor.SetHp(id, value)`, que recorta a `[0, máximo]` con el máximo que le pasa quien llama, sin deshacer. El serializador lee y escribe `hpCurrent` y rechaza valores negativos al leer, como el resto de enteros del estado.

## 8. Errores

| Situación | Comportamiento |
|---|---|
| La imagen de un mapa no existe o es ilegible | Lienzo en blanco del tamaño por defecto y aviso «No se encuentra la imagen». Tokens y trazos visibles. |
| `.state.json` con JSON inválido o versión futura | Se abre vacío o tal cual, en **solo lectura**, con aviso. Nunca se sobrescribe. En solo lectura **se desactivan todas las acciones de edición**: arrastrar, el modo Dibujar, añadir y quitar tokens y cambiar la cuadrícula. Navegar y medir siguen disponibles. *(Decisión de diseño.)* |
| Token cuya nota `source` ya no existe | Se muestra con su nombre e inicial. La tarjeta indica «Nota no encontrada» y oculta «Ver más». |
| Imagen mayor que el límite del WebView (memoria o textura) | **Nunca se abre en blanco por tamaño.** Si el lado mayor supera el límite medido en la tablet (§9; 8192 px, medido en la tablet de referencia), JS crea en memoria una copia reducida al límite (`createImageBitmap` con `resizeWidth`/`resizeHeight`) y la pinta escalada al tamaño original. Las coordenadas siguen en píxeles de la imagen original, así que tokens y trazos no cambian. Se pierde nitidez con mucho zoom, pero no aparece ningún aviso. No se genera ningún fichero derivado. *(Decisión de diseño.)* |
| Ya existe una imagen con ese nombre al importar | Aviso y no se sobrescribe. |
| El guardado falla (disco o permisos) | Aviso persistente y reintento en el siguiente cambio. El estado sigue en memoria. |

## 9. Verificación y testing

- **Antes de todo, una prueba en la tablet** con una página Konva mínima en la App. Hay que comprobar:
  - Si llegan `pointerType` (`pen` frente a `touch`) y la presión.
  - Si se rechaza la palma.
  - Qué tamaño de imagen carga con fluidez: probar 2048, 4096 y 8192 px de lado.

  El resultado fija el comportamiento de los gestos (§7.5) y el límite de reducción de imágenes (§8). **El plan tiene aquí una puerta:** las tareas de `Core` e `Infrastructure` pueden avanzar en paralelo, pero `map.js` (gestos y dibujo) no se implementa hasta tener el resultado. Mientras, se asume que llegan `pen` y la presión y que el límite es 4096 px. El modo alternativo de gestos (§7.5) **solo se implementa si la prueba lo exige**. Si el rechazo de palma falla pero `pen` sí llega, no hace falta nada más, porque el dedo nunca dibuja. *(Decisión de diseño.)*
- **`Core.Tests`** (TDD):
  - Serializador: válido, inválido, versión futura y redondeo de coordenadas.
  - `MapEditor`: cada operación, y deshacer y rehacer de trazos.
  - `TokenNaming`.
  - `GridMath`: ajuste a casilla con tamaños 1 a 3 y distancias en diagonal.
  - `MapDefinition`: con imagen, en blanco y con frontmatter inválido.
  - Detección de imágenes huérfanas.
- **`Infrastructure.Tests`:**
  - `WriteBytesAsync` atómico.
- `MoveAsync` sin sobrescribir el destino.
  - `OpenReadAsync`.
  - Rechazo de rutas fuera del vault: `..`, absolutas y enlaces simbólicos.
  - Eventos del watcher para `.state.json` e imágenes.
- **`Screens.Tests`** (bUnit): panel de Mapas (lista, huérfanas, nuevo mapa, renombrar con nombre ocupado, borrar con confirmación que conserva la imagen), diálogo de añadir token, tarjeta rápida (nota, genérico, nota perdida, edición de bando, tamaño y campos de genérico), debounce y guardado forzado.
- **`DevHost.Tests`:** el endpoint `/_vault/` sirve una imagen válida y responde `404` ante rutas con traversal, extensiones fuera de la lista y ficheros inexistentes.
- **Manual:** checklist manual, en el DevHost y en la tablet de referencia. Debe cubrir gestos, lápiz frente a dedo, mapa grande, cambio externo del `.state.json` y salir y volver a la app sin perder cambios.
- **Vault de ejemplo:** se añade a `samples/vault-demo/Mapas/` un mapa con imagen pequeña, un lienzo en blanco con algunos trazos y una imagen huérfana.

## 10. Qué decide quien construya

- La estructura interna de `map.js` y si se divide en módulos.
- Los colores exactos de los bandos y de la paleta de dibujo, tomados de los tokens del design system.
- El algoritmo de colocación sin solapes de los tokens nuevos.
- El suavizado de trazos, si hace falta.
- Si `MapIndex` es una clase aparte o una extensión de `VaultIndex`.
