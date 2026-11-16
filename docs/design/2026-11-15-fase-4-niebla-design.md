# DM Companion — Fase 4: niebla de guerra y vista de jugadores

- **Estado:** borrador para revisión.
- **Orden de fases acordado el 15/11:** 6 (sonido) → 7 (TV) → 4 (niebla) → 5 (glosario) → 8 (diario).
- **Relacionado:** diseño global `2026-11-03-dm-companion-design.md` (§5 vistas DM/jugador, §6 fase 4); fase 2 `2026-11-12-fase-2-mapa-design.md` (modos del mapa, lápiz y dedo, `.state.json`); fase 7 `2026-11-15-fase-7-segunda-pantalla-design.md` (vista de jugador, TV, tokens ocultos, encuadre).
- **Lo que ya existe de la fase 4 global:** el dibujo con lápiz (adelantado a la fase 2), los tokens ocultos y el rectángulo del encuadre de la TV (adelantados a la fase 7). Esta fase añade la niebla y la previsualización.

## 1. Intención

El autor prepara los mapas antes de la partida y quiere que los jugadores no vean en la TV las zonas que aún no han descubierto. La forma más sencilla para él (decidido, 15/11): **dibuja con el lápiz una forma cerrada y todo lo de dentro es niebla**; en la partida, **la destapa de un toque**. Además quiere poder poner la tablet en **«Vista de jugadores»** y dejarla así, para comprobar exactamente lo que ve la TV, solo para mirar.

## 2. Objetivos

- **Zonas de niebla** dibujadas tocando sus esquinas con el lápiz (polígono). Lo que queda dentro de una zona tapada no se ve en la TV; fuera de las zonas, el mapa se ve.
- **Destapar y volver a tapar** una zona con un toque.
- **Emboscadas automáticas:** un token bajo niebla tapada no existe para los jugadores, aunque no esté marcado como oculto.
- **Vista de jugadores en la tablet:** un botón que se deja puesto y muestra el mapa abierto tal como lo verían los jugadores. Solo para mirar.
- En Android, la versión de PC y el DevHost.

## 3. No objetivos

- Revelado automático por visión de los tokens, líneas de visión, luz u oscuridad.
- Pincel o goma de niebla a mano alzada, destapar parte de una zona.
- Formas de niebla que no sean polígonos de esquinas (círculos, rectángulos con arrastre, curvas).
- Editar las esquinas de una zona ya creada (se borra y se vuelve a dibujar).
- Interactuar en la vista de jugadores (mover, destapar, combate, zoom).
- Niebla en el estado Contenido de la TV (fichas y notas).

## 4. Anti-objetivos

- Que la TV enseñe, aunque sea un instante, lo que hay bajo una zona tapada: imagen, trazos o tokens.
- Que un token bajo niebla aparezca en la TV en el mapa o en la tira de iniciativa, o que su turno se resalte.
- Que el toque que cierra una zona al dibujarla la destape, o que un toque de la palma o del dedo cree, destape o borre niebla.
- Que la niebla se pierda: un cierre de la app, un cambio de mapa o una sincronización no pueden borrar zonas ni su estado tapado/destapado.
- Que la vista de jugadores de la tablet y la TV muestren cosas distintas.

## 5. Enfoques considerados (decididos el 15/11)

**Cómo llega la niebla a la TV — A (elegido): la TV recibe las formas y las dibuja encima.** Las zonas tapadas viajan a la TV como polígonos dentro de la vista de jugador, igual que los tokens y trazos; la TV las pinta opacas sobre todo lo demás. Destapar es instantáneo y no carga la wifi. Coste: la imagen entera del mapa llega al navegador de la TV y solo la tapa una capa; alguien que trasteara con el navegador de la TV podría verla (riesgo asumido en el salón de el autor).
- *B (descartado): la tablet manda la imagen ya tapada.* Lo oculto nunca saldría de la tablet, pero cada destape exige recomponer y reenviar una imagen grande (lento en la TV) y los trazos de mapas dibujados habría que componerlos también.

**Cómo se hace la vista de jugadores — A (elegido): el mismo dibujante de la TV dentro de la tablet.** La tablet pinta a pantalla completa la misma vista de jugador que se manda a la TV, con el mismo código de dibujo de la TV. Es fiel por construcción y ya es de solo lectura.
- *B (descartado): un modo «jugadores» en el mapa normal de la tablet.* Repite en `map-canvas.js` cada filtro (niebla opaca, ocultos, alias, vida) y la tablet y la TV acabarían divergiendo.

## 6. Diseño

### 6.1 Modelo y guardado

- Nueva lista opcional `fog` en el `.state.json` del mapa: zonas con identificador, lista de puntos (coordenadas del mapa, como los tokens y trazos) y estado tapada/destapada (`revealed`, omitido si es falso). Se omite si está vacía: los ficheros existentes se leen igual. *(Formato exacto y nombres: quien construya.)*
- **La versión del fichero sube (2 → 3).** Una app anterior abre en solo lectura (`FutureVersion`, mecanismo ya existente) un mapa guardado con niebla, en vez de guardarlo y borrar la niebla sin avisar. *(Decisión tomada: con Syncthing entre tablet y PC puede convivir una versión vieja.)*
- Una zona nueva nace **tapada**.
- Crear, destapar, tapar y borrar zonas son ediciones del mapa: entran en el historial de deshacer/rehacer con etiquetas genéricas «Deshacer»/«Rehacer» (como ocultar/revelar en la fase 7), se guardan con el mismo guardado diferido y pasan por la misma gestión de conflictos de Syncthing que tokens y trazos: hoy es por fichero entero y, con cambios locales pendientes, gana lo local. No se añade fusión por zona. *(Decisión tomada: el mismo riesgo ya aceptado para tokens.)*
- Zona válida: al menos 3 esquinas y área no nula. *(Decisión tomada.)*

### 6.2 Modo Niebla en la tablet

- Nuevo modo **Niebla** en los controles del mapa, junto a Navegar, Dibujar y Medir, con tres herramientas: **Destapar**, **Trazar** y **Goma** (decidido el 15/11: herramientas separadas, para que un toque nunca haga dos cosas según dónde caiga). Al entrar en el modo se usa la última herramienta elegida en esa sesión; la primera vez, Destapar. *(Decisión tomada: en la partida lo habitual es destapar.)* Deshabilitado en sesiones de solo lectura, como Dibujar.
- Como en Dibujar: **el dedo mueve y hace zoom; solo el lápiz actúa**. En el PC, el ratón hace de lápiz (igual que hoy en Dibujar). Los tokens no se pueden tocar en este modo.
- **Trazar:** cada toque del lápiz añade una esquina, también dentro de otra zona (zonas anidadas o solapadas); se ve el polígono en curso (líneas y puntos) y una línea de cierre provisional. **Se cierra tocando cerca de la primera esquina** (radio en píxeles de pantalla, no del mapa, para que funcione con cualquier zoom). Mientras se traza: un botón **«Deshacer esquina»** quita la última y **«Cancelar»** descarta el trazado; cambiar de modo, de herramienta o de mapa también lo descarta. Cerrar la zona no la destapa.
- **Destapar:** un toque del lápiz **dentro de una zona** alterna tapada ↔ destapada; fuera de toda zona no hace nada. Si varias zonas se solapan en ese punto, cambia la **última creada**. *(Decisión tomada.)*
- **Goma:** un toque del lápiz dentro de una zona la borra (la última creada si se solapan).
- **Fuera del modo Niebla**, la niebla se ve pero no se puede tocar: los toques pasan a los tokens y al mapa como hoy.

### 6.3 Cómo se ve la niebla

- **Tablet (vista del DM):** las zonas tapadas, en gris semitransparente por encima de imagen, trazos y tokens, para que el DM vea qué está tapado sin perder lo de debajo. Los tokens bajo niebla se ven a través del velo y se pueden seleccionar y mover fuera del modo Niebla. Las zonas destapadas no se ven, salvo en el modo Niebla, donde se marcan con un contorno discontinuo para poder volver a taparlas. *(Opacidad y colores, del design system: decisión tomada.)*
- **TV:** las zonas tapadas son **opacas**, por encima de imagen, cuadrícula, trazos y tokens, con un color de niebla del design system. Las destapadas no existen para la TV. *(Decisión tomada.)* La TV dibuja la niebla en el mismo paso que el resto del mapa: nunca se muestra un mapa nuevo sin su niebla.
- El rectángulo del encuadre de la TV no cambia.

### 6.4 Tokens bajo niebla

- Un token está **bajo niebla** si su centro cae dentro de una zona tapada **o justo en su borde**. *(Decisión tomada: el centro es fácil de entender en la mesa; un token grande a medio cubrir se ve si su centro está fuera; en el borde, gana la prudencia.)*
- Para los jugadores, un token bajo niebla se trata exactamente como un token oculto (fase 7 §6.4): no está en el mapa ni en la tira de iniciativa de la TV; si es su turno, la TV no resalta a nadie (la ronda sí se ve). Al destapar la zona o mover el token fuera, aparece.
- Su marca de oculto no cambia: un token oculto sigue oculto aunque se destape la niebla.
- Lo decide la proyección a vista de jugador (`PlayerViewProjector`), el único sitio que decide qué ven los jugadores.

### 6.5 Vista de jugadores en la tablet

- Botón **«Vista de jugadores»** en los controles del mapa, que se queda activado hasta volver a pulsarlo. No necesita la TV activada ni conectada.
- Mientras está activa, el área del mapa de la tablet muestra **el mapa abierto en la tablet** (no el de la TV) dibujado con el mismo código que la TV y con la misma vista de jugador: niebla opaca, sin tokens ocultos ni bajo niebla, alias, vida de enemigos como «Ileso / Herido / Malherido» y tira de iniciativa si hay combate en ese mapa.
- Muestra **el mapa entero** ajustado a la pantalla, no el encuadre de la TV. *(Decisión tomada: el encuadre ya se ve con el rectángulo; así se revisan zonas que aún no se enseñan.)*
- **Solo para mirar** (decidido): sin zoom, sin mover, sin destapar, sin tocar tokens. El resto de controles del mapa quedan deshabilitados salvo el propio botón. Los paneles laterales (notas, jugadores, TV…) siguen funcionando.
- Un **aviso visible** («Vista de jugadores», con borde o banda de color) indica que está activa, para no confundirla con tokens desaparecidos.
- Se actualiza si el estado del mapa cambia (por ejemplo, por Syncthing o desde la tira de combate). Al cambiar de mapa sigue activa con el nuevo; al cerrar el mapa se desactiva. *(Decisión tomada.)*

### 6.6 Arquitectura

- **Core:** el modelo de zona de niebla en `MapState`, las ediciones en `MapEditor` (crear, alternar, borrar; con deshacer), la geometría (punto en polígono, validez de la zona) y la proyección: `PlayerMapView` incluye las zonas tapadas y excluye los tokens bajo ellas en mapa y tira. Todo probado sin dispositivo.
- **Screens / `map-canvas.js`:** el modo Niebla (trazado, cierre, toque para alternar, goma) y el dibujo de la niebla del DM. `map-canvas.js` ya ronda las 990 líneas: la niebla va en un módulo JS propio que el lienzo usa, no dentro de ese fichero. *(Decisión tomada.)*
- **TV (`TvPage/tv-map.js`):** dibujar la capa de niebla opaca. Sigue siendo JS sin módulos ni sintaxis reciente (navegadores de TV).
- **Vista de jugadores:** la tablet calcula la vista de jugador del mapa abierto con la proyección de Core (encuadre = mapa entero) y la pinta con el dibujante de la TV (`tv-map.js` y la tira) cargado dentro de la propia app, sin pasar por el servidor de la TV. La imagen del mapa y los retratos se cargan por el mecanismo que ya usa la tablet para los ficheros del vault (`VaultFileUrls`), no por las URLs del servidor de la TV; la vista funciona igual con la TV desactivada, y una imagen o retrato que falte se ve como en la TV (lienzo en blanco, inicial). *(Decisión tomada.)* Cómo se cargan los scripts del dibujante en la app (además de como recursos del servidor de la TV) y cómo se adapta el dibujante a un contenedor que no es la ventana entera: quien construya.

### 6.7 Decisiones tomadas

- Un token está bajo niebla si su centro lo está.
- En solapes, Destapar y Goma actúan sobre la última zona creada; el modo arranca en la última herramienta usada (Destapar la primera vez).
- Zona nueva tapada; cerrar no destapa; mínimo 3 esquinas.
- En la tablet, niebla gris semitransparente encima de los tokens; destapadas visibles solo en el modo Niebla.
- Vista de jugadores con el mapa entero, el mapa de la tablet y sin interacción; sigue activa al cambiar de mapa.
- Módulo JS aparte para la niebla en la tablet.
- Versión del fichero de estado a 3 para que una app vieja no borre la niebla.
- Conflictos de Syncthing como hoy (fichero entero, gana lo local).
- La vista de jugadores carga imágenes por el mecanismo de la tablet, sin servidor de la TV.

## 7. Verificación

- **Core:** punto en polígono (dentro, fuera, borde cuenta como dentro, polígonos cóncavos); zona inválida rechazada; crear, alternar y borrar con deshacer/rehacer; serialización con y sin `fog` y fichero antiguo sin el campo; proyección: zonas destapadas fuera, token con el centro bajo niebla ausente en mapa y tira y sin resaltado de turno, token oculto sigue oculto al destapar.
- **Screens/bUnit:** modo Niebla en los controles (y deshabilitado en solo lectura), «Deshacer esquina» y «Cancelar», botón «Vista de jugadores» que se queda pulsado, aviso visible, controles deshabilitados durante la vista.
- **JS:** tests de las piezas puras (cierre por distancia en píxeles de pantalla, elección de zona bajo el lápiz con solapes, dibujo de niebla en el dibujante de la TV).
- **TV:** la vista de jugador enviada lleva las zonas tapadas y no los tokens bajo niebla.
- **Checklist manual** en la tablet con la TV: trazar una zona sobre una sala con un enemigo dentro, comprobar que la TV la tapa y no muestra al enemigo, destaparla de un toque, volver a taparla, deshacer, palma y dedo sin efecto, vista de jugadores puesta mientras se juega desde la tira; y en el PC con ratón.

## 8. Qué decide quien construya

- Nombres de tipos, formato exacto en `.state.json` y en los mensajes a la TV.
- Radio de cierre y de toque, colores y opacidades dentro del design system.
- Iconos y disposición de los botones del modo Niebla y de «Vista de jugadores».
- Cómo se reutiliza el dibujante de la TV dentro de la app.
