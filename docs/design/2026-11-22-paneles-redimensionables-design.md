# Paneles redimensionables y ventanas flotantes — diseño

Fecha: 22/11/2026. Estado: aprobado.

## 1. Intención

En partida, el DM necesita ver a la vez el mapa y la información que consulta (notas, glosario, varias criaturas en combate). Hoy los paneles tienen anchos fijos y solo cabe una tarjeta de criatura, que se cierra al abrir otra. Quiere ajustar cada panel a lo que está mirando y dejar a la vista, «clavadas», las ventanas que le interesan sin que se cierren solas.

Éxito: en una partida real con la tablet, abre dos o tres tarjetas de criatura ancladas en una esquina, estira el glosario para leer un monstruo, y al día siguiente todo sale con el tamaño y el sitio que dejó.

## 2. Objetivos, no objetivos y antiobjetivos

**Objetivos**
- Paneles del raíl (Notas, Jugadores, Glosario, Mapas, Sonido, TV, Vault): redimensionables a lo ancho y alto, **fijos a su sitio de nacimiento** (no se mueven).
- Ventanas emergentes (tarjeta de criatura, historial de combate de la tira retraída, sonido rápido): redimensionables, **movibles** y con **chincheta**.
  - Con chincheta: no se cierran al tocar fuera y vuelven a salir donde se dejaron.
  - Sin chincheta: se comportan como ahora.
- Tarjetas de criatura: **una por criatura, varias abiertas a la vez**. Al abrir la de otra criatura, **sustituye a la tarjeta sin chincheta** (si la hay); las ancladas se quedan.
- Recordar tamaño (y posición de las ancladas) entre partidas, por dispositivo.
- Funciona con el dedo en la tablet (WebView de Android) y con el ratón en la versión de PC.

**No objetivos**
- Mover los paneles del raíl, acoplarlos o abrir varios a la vez (sigue habiendo uno activo).
- Redimensionar diálogos modales (`Dialog`) ni los `Parchment` que no son del raíl (bienvenida, selector de vault, acceso a archivos, diagnóstico, Home).
- Cambiar la vista de la TV: allí los paneles siguen a pantalla completa y sin tiradores.

**Antiobjetivos** (fallaría aunque funcionase)
- Que un gesto sobre un tirador o una cabecera llegue al lienzo del mapa (mover el mapa, dibujar o coger un token sin querer).
- Que una ventana quede fuera de la pantalla o más pequeña que su contenido útil sin forma de recuperarla.
- Que las tarjetas se acumulen sin querer.
- Que el tamaño elegido se pierda porque la pantalla se encogió un momento (rotación, tira de combate expandida).

## 3. Restricciones

- Arquitectura del repo (`.claude/rules/code-style-dotnet.md`): `DmCompanion.UI` solo presentación (parámetros + `EventCallback`, prefijo `ds-`, sin Core/Screens); lógica de app en `DmCompanion.Screens`; interop JS solo a través de un servicio por funcionalidad; nada de `IJSRuntime` en componentes.
- El `resize` de CSS no sirve: no funciona con el dedo en el WebView. Hace falta Pointer Events propios, como ya hace `drag-panel.js` (captura del puntero, `touch-action: none`, `stopPropagation`).
- Preferencias por dispositivo vía `IAppPreferences` (ya existe; claves como `combat.strip.expanded`). **No** se guarda nada en el vault (la tablet y el PC comparten vault por Syncthing y tienen pantallas distintas).
- Sin regresiones en la vista de TV (`tv.css` fuerza `inset` en `.ds-parchment`) ni en los tests existentes de `TokenCard`, `CombatHistoryPopover` y `SoundQuickPanel`.

## 4. Enfoques considerados

- **A. Gestor de ventanas común** — *elegido*. Un servicio en Screens lleva la lista de ventanas flotantes y sus reglas (sustituir la no anclada, orden, chincheta); un único módulo JS mueve y estira con tirador táctil y acota al área; el tirador y la chincheta son piezas del design system; un almacén de geometría persiste en `IAppPreferences`. Más trabajo inicial, pero un solo comportamiento para todo y fácil de añadir ventanas nuevas.
- **B. Lo mínimo por partes** — descartado. `resize` de CSS en cada panel y ajustes sueltos en cada popover: no va con el dedo en el WebView y duplicaría la lógica de chincheta en tres sitios.

## 5. Cómo funciona (lo decidido)

### 5.1 Paneles del raíl
- Siguen anclados arriba a la izquierda del área del mapa, como hoy.
- Tiradores: borde derecho (ancho), borde inferior (alto) y esquina inferior derecha (ambos). Área táctil de al menos 44 px aunque el trazo visible sea fino.
- Sin tamaño guardado se ven exactamente como hoy (los `clamp` de `Side`/`Wide`). Al estirar, el tamaño elegido sustituye al de CSS.
- `Full` y la vista TV no llevan tiradores.

### 5.2 Ventanas flotantes
- Se arrastran desde la cabecera (como la tarjeta hoy) y se estiran desde un tirador en la esquina inferior derecha.
- Botón de chincheta en la cabecera, junto al de cerrar (iconos Lucide `pin` / `pin-off`), con `aria-pressed`.
- Sin chincheta: como hoy. El historial y el sonido rápido se cierran al tocar fuera (fondo actual); la tarjeta se cierra con la X, al eliminar el token o al ir a «Ver más».
- Con chincheta: sin fondo que la cierre; tocar fuera no la afecta. Sigue cerrándose con la X (que también quita la chincheta).
- Tocar una ventana la trae al frente.

### 5.3 Tarjetas de criatura
- Una por criatura (identificada por el id del token). Si se toca una criatura cuya tarjeta ya está abierta, esa tarjeta pasa al frente y no se duplica.
- Al tocar otra criatura: si hay una tarjeta sin chincheta, se sustituye por la nueva (sale en la posición junto al token, como hoy); las ancladas se quedan donde están.
- Varias ancladas a la vez sin límite.
- **Decidido:** una tarjeta sin chincheta que se está editando (nombre, alias, bando… sin guardar) no se sustituye: la de la otra criatura se abre a su lado. Al guardar o cancelar la edición, vuelve a ser sustituible (si para entonces hay dos sin chincheta, la próxima criatura sustituye a la más antigua de las no editadas). `TokenCard` avisa a `WindowManager` de que está en edición.

### 5.4 Límites y recolocación
- Tamaño mínimo por tipo (p. ej. tarjeta 240×160, panel 320×240) y máximo = el área del mapa menos los márgenes.
- Al girar la pantalla, expandir la tira o cambiar el tamaño de la ventana en el PC, todo se recoloca y encoge para caber, **sin sobrescribir el tamaño guardado**: se guarda lo que eligió el usuario y se muestra lo que cabe. Si vuelve a haber sitio, recupera su tamaño.

### 5.5 Memoria entre partidas (por dispositivo)
- Paneles del raíl: tamaño por panel.
- Historial y sonido rápido: tamaño, chincheta y posición (la posición solo si están anclados).
- Tarjetas de criatura: el tamaño es uno compartido por todas las tarjetas.
- Restablecer: doble toque en el tirador devuelve esa ventana al tamaño de fábrica (borra su entrada guardada).

## 6. Piezas (orientativo para el plan)

- **UI (`DmCompanion.UI`)**: tirador (`ResizeHandle`, o marcas `data-resize-handle="e|s|se"`), botón de chincheta y soporte en `Parchment` (parámetro opt-in `Resizable`, tamaño entrante) y `QuickCard` (o una `FloatingWindow` que la generalice) con `Width`/`Height`/`X`/`Y`, `IsPinned`, `OnPinnedChanged` y `OnGeometryChanged`. Página `/styleguide` y README actualizados. Tests bUnit.
- **JS (`Screens/wwwroot/js/window-frame.js`)**: sustituye a `drag-panel.js`. Arrastre por cabecera, estirado por tiradores, acotado con mínimos y máximos, `ResizeObserver` para recolocar, doble toque en el tirador. Notifica a C# la geometría **solo al soltar** (no en cada movimiento). Funciones puras exportadas y testeadas con `node --test` (como `dragPanelMath`).
- **Interop (`WindowFrameInterop`)**: sustituye a `DragPanelInterop`; usa `DotNetObjectReference` para el aviso al soltar.
- **Screens**: servicio `WindowManager` (scoped) con la lista de ventanas flotantes abiertas, orden z, chincheta y la regla de sustitución; sustituye a `ShellState.TokenCard`. `PanelLayoutStore` lee y escribe la geometría en `IAppPreferences` (JSON por clave, p. ej. `layout.panel.notes`, `layout.window.history`, `layout.window.token-card`). `TokenCard` pasa a renderizarse una vez por ventana abierta.

## 7. Decisiones de diseño

- Recordar las tarjetas ancladas **no** sobrevive a cerrar la app: los tokens son de cada mapa y al reabrir sería confuso. Sí se recuerda su tamaño. Al cambiar de mapa o de vault se cierran todas las tarjetas.
- Si el token de una tarjeta anclada se elimina del mapa, la tarjeta se cierra.
- Las ventanas ancladas quedan por encima de los fondos de cierre de los popovers, para poder seguir usándolas.
- El Glosario, que hoy cambia entre `Side` y `Wide` según lo que muestre, guarda un tamaño por variante (`layout.panel.glossary.side` / `.wide`).
- Restablecer con doble toque en el tirador (sin botón extra en la cabecera); se explica en la guía del usuario.
- La tarjeta nueva que sustituye a una sin chincheta sale junto a su token, como hoy (no hereda la posición de la sustituida); la posición de una ventana sin chincheta no se guarda.
- Cada tarjeta abierta conserva su tamaño; estirar una no cambia las demás. El tamaño guardado es el de partida para las tarjetas nuevas (el último que se eligió).
- La posición de una ventana anclada se guarda como distancia a la esquina del área del mapa más cercana (en px), para que una ventana «en la esquina inferior derecha» siga ahí tras girar la pantalla; después se acota al área.
- El historial y el sonido rápido no se reabren solos al arrancar. Si se cerraron anclados, la próxima vez que se pulsen salen ya anclados y en su sitio; si no, salen donde hoy.
- Orden z: las ventanas flotantes van por encima de los paneles del raíl (como hoy `--z-card` sobre `--z-panel`). Tocar fuera solo cierra el popover sin chincheta que esté abierto; los paneles del raíl se siguen cerrando como hoy.
- La vista de jugadores (TV) no renderiza ventanas flotantes ni tiradores: `WindowManager` es solo de la vista del DM.
- El contenedor de referencia es el área del mapa (`map-area`, la misma que usa hoy `drag-panel.js`), con un margen de 16 px.

## 8. Lo que queda al constructor

- Nombres exactos de componentes, parámetros y claves; mínimos concretos por ventana.
- Si `QuickCard` se amplía o nace `FloatingWindow`; cómo se adaptan visualmente el historial y el sonido rápido (hoy tienen estilo «madera», no pergamino) al nuevo marco sin cambiar su aspecto.
- Orden z concreto y tokens CSS nuevos.
- Estrategia de tests (bUnit para reglas y componentes, `node --test` para la matemática del JS), manteniendo verdes los tests existentes.
