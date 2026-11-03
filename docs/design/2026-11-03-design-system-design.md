# DM Companion — Design system

- **Fecha:** 2026-11-03
- **Estado:** aprobada
- **Alcance:** tokens visuales, componentes base de presentación, layout adaptable, entrada (táctil, ratón y teclado), vista TV y verificación. Complementa la §5 (Interfaz) de `2026-11-03-dm-companion-design.md`.

## 1. Concepto

La interfaz es **una mesa de taberna de noche**. La madera solo se ve en el **marco**: el menú izquierdo, la tira de iniciativa y un margen fino alrededor del mapa. El **mapa ocupa casi toda la pantalla**, como un pergamino grande clavado a la mesa, y se mantiene como capa base permanente, tal como dice la spec general. Las fichas, las notas, el glosario y el resto de paneles son **pergaminos más pequeños que se colocan encima**.

El tono es aventurero y cuidado, nunca sucio ni recargado. El carácter lo dan la paleta, la tipografía y la sombra, no la decoración.

Decisiones de partida:

| Decisión | Elección | Motivo |
|---|---|---|
| Relación mesa / mapa | Madera solo en el marco; mapa casi a pantalla completa | En mesa lo que más importa es un mapa grande y legible. Mover el mapa como un pergamino más se descartó porque resta espacio y cambia la spec general |
| Tema | Uno solo, «taberna de noche» | Se juega de noche con poca luz. Un pergamino claro y grande deslumbra. Un tema de día se podrá añadir redefiniendo los tokens semánticos |
| TV | La misma mesa, simplificada | Refuerza la inmersión. El mapa a sangre se descartó porque se pierde la identidad |
| Ornamentación | Mínima y sobria; **decisión aplazada** | Sin texturas, sellos ni bordes elaborados por ahora. Los tokens tienen ganchos para añadirla después sin tocar componentes (§3.7) |
| Tipografía | EB Garamond (cabeceras) + Source Sans 3 (texto, UI y números) | Da el tono de pergamino sin perder legibilidad. Cormorant se descartó por sus trazos demasiado finos con poca luz, y una display de fantasía (tipo Cinzel), por cliché |
| Dónde vive | Razor Class Library nueva `DmCompanion.UI` | Separa la presentación base de las pantallas de App. MudBlazor y FluentUI se descartaron porque pelean con la estética y pesan bastante |

## 2. Arquitectura

```
src/
├─ DmCompanion.Core/            # sin cambios
├─ DmCompanion.Infrastructure/  # sin cambios
├─ DmCompanion.UI/              # NUEVO: Razor Class Library, solo presentación
│   ├─ Components/              # TableFrame.razor, Parchment.razor… (PascalCase)
│   ├─ Interop/                 # FocusInterop.cs
│   ├─ Pages/                   # Styleguide.razor
│   └─ wwwroot/
│       ├─ tokens.css           # primitivas + semánticas
│       ├─ components.css
│       ├─ focus-trap.js        # kebab-case
│       ├─ icons/               # SVG de Lucide (ISC), solo los usados
│       └─ fonts/               # EB Garamond, Source Sans 3 (OFL) + licencias (OFL y LICENSE de Lucide)
└─ DmCompanion.App/             # referencia DmCompanion.UI
tests/
└─ DmCompanion.UI.Tests/        # xUnit + bUnit + AwesomeAssertions
```

Reglas:

- `DmCompanion.UI` **solo presenta**. Recibe datos por parámetros, emite `EventCallback` y no contiene lógica de negocio. Puede usar los tipos de Core como modelos de entrada, pero **no referencia Infrastructure**.
- La interop JS pasa siempre por un servicio (`FocusInterop`). Ningún componente usa `IJSRuntime` directamente.
- El HTML de las notas llega ya sanitizado desde Core (`NoteHtmlRenderer`). UI solo le aplica estilos.
- Konva (el mapa, fase futura) lee los colores de los tokens con `getComputedStyle`, así que hay **una sola fuente de colores**.
- **Iconos:** se usa un solo set, [Lucide](https://lucide.dev) (licencia ISC). Los SVG van empaquetados en `wwwroot/icons/` (la app funciona offline), solo los que se usan, y la `LICENSE` de Lucide va junto a las licencias OFL de las fuentes. Se usa para los bandos (§3.4) y para el resto de iconos de la UI (`x` para cerrar, `ellipsis` para «⋯», `tv` para «Mostrar en TV», los del menú izquierdo…).

Cambios de reglas, que se hacen en la implementación y **solo en este repo**:

- `.claude/rules/code-style-dotnet.md`: añadir `DmCompanion.UI` a la sección Arquitectura (solo presentación, sin lógica de negocio, sin referencia a Infrastructure).
- `CLAUDE.md`: añadir el proyecto al stack o la estructura.
- No se toca la plantilla global `~/.claude/plantillas/claude-project/` ni `~/.claude/CLAUDE.md`: es una decisión de este proyecto personal.

## 3. Tokens

### 3.1 Dos capas

- **Primitivas** (`--wood-800`, `--parchment-300`…): solo se usan dentro de `tokens.css`.
- **Semánticas** (`--surface-sheet`, `--text-ink`, `--accent-on-sheet`…): son las únicas que usan los componentes y Konva. Un tema futuro solo redefine esta capa.

Todos los valores de color son orientativos y se ajustan probando en la tablet de referencia, siempre sin bajar de los mínimos de contraste de §3.3.

### 3.2 Color

| Token semántico | Valor | Uso |
|---|---|---|
| `--surface-frame` | #1E140D | Fondo de la mesa |
| `--surface-frame-raised` | #2A1C12 | Menú izquierdo y tira de iniciativa |
| `--border-frame` | #4A3421 | Separadores sobre madera |
| `--text-on-wood` | #E8DCC2 | Texto sobre madera (12,1:1) |
| `--surface-sheet` | #D6C4A0 | Pergamino (crema apagado, no blanco) |
| `--surface-sheet-sunken` | #C9B48C | Campos, filas, `ListRow` |
| `--surface-sheet-raised` | #DFCFAE | Elementos destacados dentro del pergamino |
| `--text-ink` | #2B2118 | Tinta principal (9,2:1 sobre sheet) |
| `--text-ink-secondary` | #4A3B2D | Tinta secundaria |
| `--accent-on-wood` | #C9A24A | Latón: turno actual, foco y selección sobre madera |
| `--accent-on-sheet` | #6E5112 | Latón oscuro: foco y selección sobre pergamino |
| `--faction-pc` | #3E6A8E | Anillo / relleno PJ |
| `--faction-ally` | #4F7A4A | Anillo / relleno aliado |
| `--faction-enemy` | #9E2B25 | Anillo / relleno enemigo |
| `--fill-heal` / `--fill-damage` | #4F7A4A / #9E2B25 | Rellenos y anillos (ni texto ni fondo de texto) |
| `--button-primary-bg` / `--button-damage-bg` / `--button-heal-bg` | #2B2118 / #7A1E19 / #2B4D27 | Fondo de botones con texto `--button-text-on-fill` #DFCFAE (10,3 / 6,8 / 6,2); hover #4A3B2D / #8A231E / #36602F (7,0 / 5,8 / 4,8) |
| `--text-heal` | #2B4D27 | Texto de curación |
| `--text-damage` | #7A1E19 | Texto de daño |
| `--dm-only` | #6B4E8C | Violeta arcano: filete o borde del contenido que solo ve el DM (no texto) |
| `--text-dm-only` | #4F3570 | Violeta oscuro: texto de la etiqueta «Solo DM» |
| `--map-grid-line-dark` / `--map-grid-line-light` | #2B2118 / #E8DCC2 | Cuadrícula del mapa en Konva: trazo doble, oscuro debajo y claro encima |
| `--map-measure-line-dark` / `--map-measure-line-light` | #2B2118 / #E6C877 | Regla: trazo doble (el claro usa la primitiva nueva `--brass-200`) |
| `--map-calibration-line-dark` / `--map-calibration-line-light` | #2B2118 / #E8DCC2 | Rectángulo de calibración: trazo doble discontinuo |
| `--map-measure-label-bg` / `--map-measure-label-text` | #1E140D / #E8DCC2 | Etiqueta de distancia de la regla |
| `--map-hidden-badge-bg` / `--map-hidden-badge-icon` | #4F3570 / #E8DCC2 | Insignia del token oculto |

### 3.3 Contraste mínimo (WCAG 2.1)

Texto ≥ 4,5:1 (1.4.3). Foco, bordes y elementos gráficos ≥ 3:1 (1.4.11). Valores comprobados sobre sheet / sunken / raised:

| Pareja | Contraste |
|---|---|
| `--text-ink` | 9,2 / 7,8 / 10,3 |
| `--text-ink-secondary` | 6,3 / 5,3 / 7,0 |
| `--text-heal` | 5,6 / 4,7 / 6,2 |
| `--text-damage` | 6,1 / 5,1 / 6,8 |
| `--text-dm-only` | 5,9 / 5,0 / 6,6 |
| `--accent-on-sheet` (foco) | 4,3 / 3,65 / 4,8 |
| `--dm-only` (filete o borde) | 3,96 / 3,36 / 4,42 |
| `--accent-on-wood` sobre `--surface-frame-raised` | 6,9 |
| `--text-on-wood` sobre `--surface-frame-raised` | 12,1 |

**Trazo doble sobre imagen.** Si dos trazos superpuestos tienen un ratio R entre sí, el peor fondo posible queda a √R de los dos; con R ≥ 9 uno de ellos da siempre ≥ 3:1. Pares: cuadrícula y calibración 11,6; regla 9,7; contorno de anillo 11,6. La cuadrícula se pinta a opacidad 0,7, que rebaja esa garantía: se valida en la tablet.

| Pareja | Contraste |
|---|---|
| `--map-measure-label-text` sobre `--map-measure-label-bg` | 13,3 |
| `--map-hidden-badge-icon` sobre `--map-hidden-badge-bg` | 7,4 |

Combinaciones que **no se usan**: el latón claro sobre pergamino (1,4:1), los rellenos de curación y daño como texto (2,9 y 4,3) y `--dm-only` como texto (3,4 a 4,4).

### 3.4 Bandos: no solo color

Aliado y enemigo tienen casi la misma luminancia (1,5:1 entre ellos) y se confunden con daltonismo rojo-verde. Por eso cada bando tiene también una **forma de anillo**, igual en HTML y en Konva:

| Bando | Anillo | Icono (Lucide) |
|---|---|---|
| PJ | continuo | escudo (`shield`) |
| Aliado | discontinuo | mano (`hand`) |
| Enemigo | doble | espadas cruzadas (`swords`) |

El icono aparece en `InitiativeChip` y en `QuickCard`.

**Token oculto** (solo DM): retrato o inicial al 50 % de opacidad e insignia violeta con el icono `eye-off`; el anillo no cambia. Igual en `TokenAvatar` y en Konva (README, «Pintar en Konva»).

El anillo también debe contrastar con el **fondo** (≥ 3:1, WCAG 1.4.11), así que lleva un contorno cuyo color depende de la superficie. En HTML, un contorno de 1px en `--faction-outline-on-wood` (`--bone-100`) sobre madera (raíl y franja) o en `--faction-outline-on-sheet` (`--ink-900`) sobre pergamino. En el mapa de Konva, con una imagen arbitraria de fondo, el anillo se dibuja con **doble contorno**, oscuro (`--faction-outline-on-sheet`) y claro (`--faction-outline-on-wood`), para que se lea sobre cualquier imagen. Los colores de bando no cambian.

### 3.5 Tipografía

- `--font-heading`: EB Garamond. `--font-body`: Source Sans 3. Las dos van empaquetadas en `wwwroot/fonts/` (la app funciona offline), con su licencia OFL.
- Stats (HP, AC, iniciativa, velocidad): `font-variant-numeric: tabular-nums`.
- Escala (× `--ui-scale`): 14 / **16 (cuerpo)** / 20 / 24 / 32 px.

### 3.6 Espaciado, táctil, elevación, capas y movimiento

- Espaciado en una escala de 4 px: 4, 8, 12, 16, 24, 32 (× `--ui-scale`).
- Táctil: mínimo **48 dp**, y **56 dp** en las acciones de combate (daño, curación, siguiente turno). Se mantiene en PC.
- Elevación: solo sombra, sin bordes gruesos. `--elevation-1` para el panel y `--elevation-2` para `QuickCard` y `Dialog`.
- Capas: `--z-map` (0) < `--z-toolbar` (5) < `--z-panel` (10) < `--z-card` (20) < `--z-toast` (25) < `--z-dialog` (30). Qué se abre o se cierra lo decide App.
- Movimiento: el pergamino entra con 160 ms de desplazamiento corto + fundido. Con `prefers-reduced-motion`, sin desplazamiento.

### 3.7 Ganchos de ornamentación (aplazada)

Ya existen con valor neutro: `--sheet-texture: none`, `--frame-texture: none`, `--sheet-edge: none` y `--heading-ornament: none`. Los componentes ya los aplican, así que añadir ornamentación será **cambiar valores, no componentes**.

## 4. Componentes

Todos están en `DmCompanion.UI`. Los interactivos son `<button>` u otros controles nativos, con hover (§6) y `:focus-visible`.

| Grupo | Componente | Notas |
|---|---|---|
| Estructura | `TableFrame` | Grid: menú, tira de iniciativa y área de mapa con margen de madera |
| | `RailButton` | Alterna su panel; 56 dp; activo en latón con `aria-pressed` |
| Pergaminos | `Parchment` | Título, cerrar, cuerpo con scroll y hueco de acciones (App pone ahí «📺 Mostrar en TV»). Tamaños: lateral, ancho y completo |
| | `QuickCard` | Pergamino pequeño anclado a un token |
| | `Dialog` / `ConfirmDialog` | `--elevation-2`, foco atrapado y Esc cierra. Se usa, por ejemplo, para resolver conflictos de escritura del vault (ya detectados en Core) |
| Acciones | `Button` | Variantes primaria, secundaria, daño y curación |
| | `IconButton` | Con etiqueta accesible obligatoria, e `IsPressed` (`aria-pressed`) para herramientas que se alternan |
| Entrada | `TextField` / `SearchField` | Búsqueda del vault (`VaultIndex`) y del glosario |
| Listas | `ListRow` + `TreeView` | Árbol de carpetas del vault (`VaultTree`); filas sobre `--surface-sheet-sunken` |
| Combate y datos | `StatBadge` | AC, HP, velocidad; cifras tabulares; `IsManual` (lápiz + texto oculto) |
| | `HpControl` | −/+ de 56 dp y valor editable |
| | `ConditionTag` | Estado (envenenado, aturdido…) |
| | `InitiativeChip` | Retrato o inicial, anillo e icono de bando, iniciativa; resplandor de latón en el turno actual |
| | `TokenAvatar` | Mismo aspecto que el token de Konva, e `IsHidden` |
| Contenido | `NoteContent` | Estilos del HTML sanitizado: cabeceras, listas, tablas, citas, wikilinks y enlaces rotos. Los fragmentos secretos (`span.dm-secret`, uno por línea) llevan un filete `--dm-only` y un fondo violeta suave. La etiqueta «Solo DM» en `--text-dm-only` se pinta una vez, arriba, cuando la nota entera es secreta |
| | `StatGroup` | Encabezado h3 y rejilla de `StatBadge` |
| Estructura (mapa) | `MapToolbar` | Barra flotante de madera sobre el mapa, a la derecha, `role="group"` |
| Estados | `Toast` | Uno a la vez, `aria-live="polite"`, acción opcional; se pausa con foco o puntero |
| Entrada | `Checkbox`, `NumberStepper` | ± de 56 dp, `InvariantCulture` |
| Estados | `EmptyState`, `InlineNotice` | Los errores de contenido (YAML inválido, enlace roto) se muestran como resultados, no como excepciones |

**Fuera de alcance:** el canvas del mapa, la lógica de combate y las pantallas concretas (las hacen App y las fases correspondientes).

## 5. Layout adaptable

- `TableFrame` en CSS grid: menú izquierdo fijo de **72 px**, tira de iniciativa de **64 px** y el mapa ocupa el resto, con un margen de madera de **8 px**. Sin breakpoints de página: sirve igual en la tablet apaisada, en pantallas más anchas y en ventanas de PC de tamaño variable.
- El área del mapa se declara como contenedor (`container-type: inline-size`), y los pergaminos se miden en unidades `cqw` de ese contenedor, no en `vw`. Así la referencia es siempre el área del mapa, igual en la tablet, en la TV y en una ventana de PC:
  - lateral: `clamp(min(360px, 100%), 40cqw, 520px)`
  - ancho: `clamp(min(560px, 100%), 70cqw, 960px)`
  - completo: el área del mapa
  - `QuickCard`: `clamp(min(280px, 100%), 25cqw, 340px)`
  - altura máxima: el área del mapa, con scroll interno.

  El `min(…, 100%)` evita que una ventana pequeña de PC, con el menú de 72 px, tenga scroll horizontal.
- El `Parchment` también es contenedor (`container-type: inline-size`, con nombre propio para no confundirlo con el del mapa). Los stats pasan a dos columnas si el pergamino mide ≥ 600 px. El contenido se adapta al pergamino, no a la pantalla.

## 6. Entrada: táctil, ratón y teclado

- Hover solo dentro de `@media (hover: hover)`, para que no se quede «pegado» en la tablet. `:focus-visible` con el anillo de latón (`--accent-on-wood` o `--accent-on-sheet`, según el fondo) en todos los interactivos.
- Teclado:
  - Orden de tabulación lógico: menú → tira de iniciativa → panel abierto.
  - **Esc** cierra `Parchment`, `QuickCard` y `Dialog` y **devuelve el foco** a quien lo abrió.
  - **Enter/Espacio** activan los botones (son `<button>` nativos).
  - Foco atrapado en `Dialog`.
- El foco atrapado y la devolución del foco pasan por `FocusInterop` (C#) + `wwwroot/focus-trap.js`. Los componentes no usan `IJSRuntime`.
- Ninguna acción depende solo de un gesto: todas tienen un botón visible (por ejemplo, `HpControl` con −/+, o un menú «⋯» en lugar de pulsación larga).
- Requisito anotado para la fase del mapa (Konva/App): pinch-zoom tiene alternativa con la rueda del ratón, y long-press, con clic derecho o un botón.

## 7. Vista TV

- Clase `.tv` en la raíz:
  - `--ui-scale: 1.75` (se ve a 2-3 m);
  - sin estados de hover ni foco;
  - `Parchment` en tamaño completo y sin hueco de acciones;
  - marco de madera fino.
- **Doble seguro contra secretos:** Core ya filtra el contenido secreto, y además el CSS de `.tv` oculta `.dm-only` con `display: none`, por si alguno se cuela. También oculta la barra del mapa, los avisos y los tokens ocultos (`display: none`).
- **PC (fase futura, no fase 1):** en un portátil con Windows (MAUI WinUI), la vista TV sería una segunda ventana en un monitor externo, con una implementación de `ISecondScreen` para Windows. El design system ya lo cubre con el layout adaptable (§5) y la entrada por ratón y teclado (§6).

## 8. Verificación

- **`/styleguide`:** página de `DmCompanion.UI` con todos los componentes en todos sus estados y un conmutador DM/TV. La ruta se registra en App solo en Debug.
- **Test de contraste (xUnit):** parsea `tokens.css` y comprueba cada pareja de color declarada. Es un `[Theory]` con un caso por pareja y un assert lógico por test: texto ≥ 4,5 y foco o bordes ≥ 3. Incluye `--text-dm-only` como texto y `--dm-only` como borde, sobre los tres pergaminos. Si alguien cambia un color por debajo del mínimo, el test falla.
- **Tests de componentes (bUnit, MIT):**
  - `RailButton` refleja `aria-pressed`.
  - Esc en `Parchment` y `Dialog` emite el cierre.
  - `Dialog` llama a `FocusInterop` para atrapar el foco y, al cerrarse, para devolverlo.

  bUnit no tiene DOM real, así que lo que se verifica es la llamada al servicio.
- **Checklist manual** en la tablet de referencia con poca luz y en un PC con la ventana redimensionada:
  - legibilidad;
  - tamaños táctiles;
  - el foco se mueve y vuelve de verdad;
  - Esc, Tab, Enter y Espacio funcionan;
  - sin scroll horizontal en ventana estrecha;
  - en la vista TV no aparece nada «Solo DM».

## 9. Decisiones aplazadas

- **Ornamentación** (texturas, sellos, bordes, capitulares): se decidirá más adelante y se aplicará mediante los ganchos de §3.7.
- **Tema de día:** posible en el futuro redefiniendo los tokens semánticos.
- **Soporte de PC:** fase propia (MAUI WinUI + `ISecondScreen` para Windows).
