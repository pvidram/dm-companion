# DmCompanion.UI

Librería de clases Razor (RCL, `net10.0`) con el sistema de diseño de DM Companion: tokens, estilos, componentes y una página de catálogo. No referencia Core, Infrastructure, MAUI ni Android ni contiene lógica de negocio: las pantallas de App la consumen y le pasan datos y callbacks.

## Cómo alojarla

1. Enlazar la hoja de estilos (un solo fichero, importa el resto): `<link rel="stylesheet" href="_content/DmCompanion.UI/ui.css" />`.
2. Registrar los servicios: `builder.Services.AddDmCompanionUI();` (registra `IFocusInterop`, scoped).
3. Para la página `/styleguide`, añadir el ensamblado al router: `AdditionalAssemblies="new[] { typeof(DmCompanion.UI.Pages.Styleguide).Assembly }"`.
4. Todo debe renderizarse dentro de `TableFrame` (o de un elemento con `.ds-root`): ahí se definen tipografía, foco y la escala de los tokens. Fuera de `.ds-root` los estilos no aplican.
5. `TableFrame` (`.ds-frame`) usa `height: 100%`: el host debe dar altura a toda la cadena de contenedores, p. ej. `html, body, #app { height: 100%; margin: 0; }` (o el elemento raíz equivalente del host). Sin ello el marco colapsa en vertical (visto en DevHost). Nota: en Android con el teclado en pantalla puede convenir usar `100dvh` en la raíz; pendiente de validar en la tablet.

Ningún componente inyecta `IJSRuntime`: el único interop es `FocusInterop` (`wwwroot/focus-trap.js`).

## TableFrame y sus ranuras

| Ranura | Contenido |
|---|---|
| `Rail` | Raíl lateral de navegación (`RailButton`). Sin él, el marco usa el diseño sin raíl. |
| `Strip` | Franja superior (p. ej. cadena de iniciativa). |
| `Map` | Área del mapa. `.ds-frame__layer` (`position:absolute; inset:0`) es la capa pensada para el lienzo de Konva o un marcador de posición (`.ds-frame__layer--placeholder`). |
| `Overlays` | `Parchment` y `QuickCard`, dentro del área del mapa. |
| `Dialogs` | `Dialog` y `ConfirmDialog`, fuera del área del mapa (último hijo del marco). |

El área del mapa es contenedor de consulta (`map-area`): los anchos de pergaminos y tarjetas se calculan sobre ella.

## Modelo de superposiciones

- Siempre montadas y controladas con `IsOpen`: cerradas siguen en el DOM con `hidden`, para no perder el estado de su contenido.
- `Parchment` y `QuickCard` nacen abiertos; `Dialog` y `ConfirmDialog` nacen **cerrados**.
- Al abrirse mueven el foco dentro; al cerrarse (o desmontarse) lo devuelven. Solo los diálogos atrapan Tab.
- Esc cierra la superposición más interna (invoca `OnClose`/`OnCancel`) y detiene la propagación del keydown, para que no cierre también a su padre.
- Los diálogos van **solo** en la ranura `Dialogs`. Dentro de un pergamino quedarían en su contexto de apilamiento y las tarjetas se pintarían sobre el fondo del modal.
- `QuickCard.X` / `Y` son px relativos al área del mapa y no se limitan: el llamador los ajusta a los bordes.
- Capas (`z-index`): mapa 0, pergamino 10, tarjeta 20, diálogo 30.

## Atributos adicionales

`Button`, `IconButton`, `RailButton`, `ListRow`, `TableFrame`, `Parchment`, `QuickCard` y `Dialog` aceptan atributos HTML no declarados (`data-*`, `id`…) en su elemento raíz (en `Dialog`, el `div role="dialog"`); `TextField` y `SearchField`, en su `input`; `TextArea`, en su `textarea`; `Select<TValue>`, en su `select`. Una `class` pasada se **añade** a las propias. Los atributos que fija el componente (`aria-*`, `disabled`, `hidden`, `style` de `ListRow`/`QuickCard`) prevalecen.

## Componentes

Convención de booleanos: `Is*` para estado, nombres HTML para atributos HTML (`Disabled`) y nombre simple para modificadores de estilo (`Combat`, `Destructive`).

| Componente | Parámetros | Eventos |
|---|---|---|
| `TableFrame` | `IsTv`, `Rail`, `Strip`, `Map`, `Overlays`, `Dialogs` | |
| `Button` | `Variant` (`ButtonVariant`), `Combat`, `Disabled`, `Icon`, `ChildContent` | `OnClick` |
| `IconButton` | `Icon`, `Label` (obligatorio), `Combat`, `Disabled` | `OnClick` |
| `RailButton` | `Icon`, `Label`, `IsPressed` | `OnToggle` |
| `Parchment` | `Title`, `Size` (`ParchmentSize`), `IsOpen`, `ChildContent`, `Actions` | `OnClose` |
| `QuickCard` | `Title`, `Faction?`, `X`, `Y`, `IsOpen`, `ChildContent` | `OnClose` |
| `Dialog` | `Title`, `IsOpen` (cerrado por defecto), `ChildContent`, `Footer` | `OnClose` |
| `ConfirmDialog` | `Title`, `Message`, `IsOpen`, `ConfirmText`, `CancelText`, `Destructive` | `OnConfirm`, `OnCancel` |
| `TextField` | `Label`, `Value`, `Placeholder` | `ValueChanged` |
| `SearchField` | `Label`, `Value`, `Placeholder` | `ValueChanged` |
| `TextArea` | `Label`, `Value`, `Rows` (8), `Disabled`, `Placeholder` | `ValueChanged` |
| `Select<TValue>` | `Label`, `Value`, `Options`, `LabelOf`, `Disabled` | `ValueChanged` (el `option` usa el índice como `value`; si `Value` no está en `Options` se muestra vacío) |
| `TreeView<TItem>` | `Items`, `ChildrenOf`, `LabelOf`, `KeyOf`, `IsBranch`, `SelectedKey` | `OnSelect` |
| `ListRow` | `Icon`, `IsSelected`, `Depth`, `Expanded` (`bool?`), `ChildContent` | `OnSelect` |
| `InitiativeChip` | `Name`, `Faction`, `Initiative`, `PortraitUrl`, `IsCurrent` | `OnSelect` |
| `TokenAvatar` | `Name`, `Faction`, `PortraitUrl` | |
| `StatBadge` | `Label`, `Value` | |
| `HpControl` | `Current`, `Max` | `OnDamage(int)`, `OnHeal(int)` |
| `ConditionTag` | `Name` | `OnRemove` (muestra el botón de quitar solo si hay delegado) |
| `NoteContent` | `Html` (`MarkupString` ya sanitizado), `IsSecret` | |
| `InlineNotice` | `Kind` (`NoticeKind`), `ChildContent` | |
| `EmptyState` | `Icon`, `Title`, `ChildContent` | |
| `Icon` | `Name` (`IconName`), `Class` | |

Enums: `ButtonVariant { Primary, Secondary, Damage, Heal }`, `ParchmentSize { Side, Wide, Full }`, `NoticeKind { Info, Warning, Error }`, `Faction { Pc, Ally, Enemy }`, `IconName` (Lucide empaquetados en `wwwroot/icons`; añadir uno = añadir su SVG). `FactionVisuals` da icono, etiqueta y modificador CSS de cada bando: el bando nunca se comunica solo con color. El anillo lleva un contorno de 1px que contrasta con el fondo (WCAG 1.4.11): `--faction-outline-on-wood` sobre madera (raíl y franja) y `--faction-outline-on-sheet` sobre pergamino. En el mapa de Konva (fondo = imagen arbitraria) el anillo se dibuja con doble contorno, oscuro (`--faction-outline-on-sheet`) y claro (`--faction-outline-on-wood`), para que se lea sobre cualquier imagen. El contorno claro (`--faction-outline-on-wood`) se aplica automáticamente solo dentro de `.ds-frame__rail` y `.ds-frame__strip`; un `TokenAvatar` colocado sobre otra superficie de madera debe añadir esa regla (o una clase) por su cuenta.

`TreeView`: `KeyOf` debe devolver una clave **única** en todo el árbol (identifica expansión y selección). Las ramas solo se expanden; `OnSelect` se dispara solo en hojas.

## Clases CSS públicas

Todas llevan prefijo `ds-` salvo `.tv`, `.dm-only`, `.dm-secret` y las que emite `NoteHtmlRenderer` (`wikilink`, `broken`).

- `ds-stats`: rejilla de `StatBadge` y controles (`auto-fill`, mínimo 88px por celda); `ds-stats--split` pasa a dos columnas solo dentro de un pergamino de 600px o más de ancho (container query `parchment`).
- `ds-numeric`: cifras tabulares.
- `ds-visually-hidden`: solo para lectores de pantalla.
- `ds-frame__layer` (+ `--placeholder`): capa a toda el área del mapa.

## Vista TV

`IsTv="true"` añade `.tv` al marco: escala 1,75 (tipografía y espaciado se recalculan porque derivan de `--ui-scale` en `:root, .ds-root`), márgenes menores, sin anillo de foco, sin acciones ni botón de cerrar en los pergaminos, y oculta `.dm-only` y `.dm-secret`. Core ya filtra los secretos; el CSS es el segundo seguro. `tv.css` es siempre el último import de `ui.css`.

## Tokens

`tokens.css` tiene primitivas (`--wood-*`, `--parchment-*`, `--ink-*`, `--bone-*`, `--brass-*`, `--blue-*`, `--green-*`, `--red-*`, `--violet-*`) y semánticas. **Componentes, CSS y Konva usan solo semánticas** (un test lo vigila para los CSS de `wwwroot/css/`); un tema nuevo redefine solo ese bloque. Las que necesita Konva para dibujar el mapa: `--faction-pc`, `--faction-ally`, `--faction-enemy`, `--faction-outline-on-wood`, `--faction-outline-on-sheet`, `--accent-on-wood`, `--surface-frame`, `--surface-frame-raised`, `--surface-sheet`, `--surface-sheet-sunken`, `--surface-sheet-raised`. Leerlas con `getComputedStyle` sobre un elemento `.ds-root` para respetar el tema y la escala.

Los números en estilos inline se formatean con `CultureInfo.InvariantCulture` (la tablet está en es-ES).

## Tests

`dotnet test tests/DmCompanion.UI.Tests` (xUnit + bUnit + AwesomeAssertions). Incluye contraste WCAG de los pares de tokens (texto ≥ 4,5:1; foco, bordes e iconos ≥ 3:1).

Checklist manual para App (DevHost y tablet): [`docs/design-system-checklist.md`](../../docs/design-system-checklist.md).
