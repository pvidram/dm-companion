# Niebla: dos tipos y trazado a mano alzada — diseño

Fecha: 22/11/2026. Estado: aprobado.

## 1. Intención

El DM quiere esconder a los jugadores cosas distintas según la escena: a veces una zona entera (una sala sin explorar), a veces solo quién hay en ella (una emboscada en un claro que los jugadores sí ven). Y quiere trazar las zonas más rápido, a mano alzada, además de punto a punto.

Éxito: en partida, sobre el mapa del bosque, el DM dibuja con el lápiz una niebla de PNJ alrededor del claro donde esperan los bandidos; en la TV los jugadores ven el claro pero ni rastro de los bandidos (ni en el mapa ni en la tira de iniciativa); al saltar la emboscada, el DM la destapa y aparecen.

## 2. Objetivos, no objetivos y antiobjetivos

**Objetivos**
- **Dos tipos de niebla** por zona:
  - **Total** (la de hoy): en la TV la zona es opaca y oculta los tokens que tiene dentro.
  - **De PNJ**: en la TV el mapa y los PJ se ven normal; los tokens de **PNJ** (bando aliado o enemigo, es decir, no PJ) que toquen la zona **desaparecen del todo**, del mapa y de la tira de iniciativa. La zona en sí **no se dibuja** en la TV.
- **Regla de «dentro»**: un token cuenta como dentro de una zona tapada si **cualquier parte** de su círculo (diámetro = tamaño en casillas × tamaño de casilla del mapa) toca la zona. Excepción: **los PJ frente a la niebla total** siguen contando solo por el centro (para que un PJ que roza el borde no desaparezca de la TV). Los PJ nunca se ocultan por una niebla de PNJ.
- **Trazado a mano alzada**: modo **«Mano alzada»** junto a «Trazar» (punto a punto). Arrastrar el lápiz y, al levantarlo, la zona se cierra uniendo el final con el principio, también si el trazo sale del lienzo. Se simplifica a un polígono con menos puntos antes de guardarlo.
- **En la tablet**: las dos clases de zona se ven con un velo semitransparente de color distinto; un selector **«Total / PNJ»** junto a «Trazar»/«Mano alzada» decide el tipo de las zonas nuevas; se puede **cambiar el tipo** de una zona ya trazada; «Destapar» y «Goma» funcionan igual con ambos tipos.
- Las zonas existentes en los mapas actuales se leen como **total** (compatibilidad).
- Deshacer/rehacer cubren crear, cambiar de tipo, destapar y borrar, como hoy.

**No objetivos**
- Niebla «de todo menos el mapa» u otros tipos; ocultar PJ con la niebla de PNJ.
- Línea de visión, luz o revelado automático por movimiento.
- Editar los vértices de una zona ya trazada.

**Antiobjetivos**
- Que la TV delate una niebla de PNJ (dibujando la zona, un hueco en la tira con un nombre, etc.).
- Que un PJ desaparezca de la TV por rozar el borde de una niebla total.
- Romper los mapas existentes o su sincronización (`.state.json` debe seguir leyéndose en versiones anteriores de la app sin perder datos de forma silenciosa, o subir de versión como ya se hace).
- Que el gesto de dibujar mueva el mapa o coja tokens (mismo aislamiento que hoy en modo Niebla, solo lápiz).

## 3. Restricciones

- Arquitectura: modelo y reglas en `DmCompanion.Core` (`MapFogZone`, `FogGeometry`, `MapEditor.Fog`, `PlayerViewProjector`); herramientas en Screens/JS (`FogControls`, `fog-mode.js`, `fog-geometry.js`, `map-gestures.js`); la geometría que existe en C# y en JS debe seguir coherente.
- `MapStateSerializer` valida y versiona el estado (`CurrentVersion = 3`): el tipo es un campo opcional nuevo (ausente = total) y, si hace falta, sube la versión como se hizo con la niebla.
- La TV recibe hoy solo los polígonos tapados; tras el cambio solo debe recibir los de niebla **total** (los de PNJ se usan para filtrar tokens en el servidor y no viajan).

## 4. Enfoques considerados

- **Ampliar las zonas existentes con un tipo** — *elegido*. Reutiliza destapar, goma, deshacer, guardado y sincronización; la mano alzada es otro gesto que acaba creando una zona normal.
- Capa aparte para la niebla de PNJ — descartado: duplicaría deshacer/borrar/destapar y complicaría mezclar tipos.
- Guardar el trazo a mano alzada tal cual — descartado: cientos de puntos en el `.state.json` y en la TV sin ganancia de precisión visible.

## 5. Decisiones de diseño

- Colores del velo en la tablet: total = el actual (`--fog-dm`); PNJ = un tono distinto del design system (p. ej. violáceo), con el mismo contorno discontinuo al estar destapada.
- Cambiar el tipo de una zona: con la herramienta activa «Cambiar tipo» (o tocando la zona con el selector puesto en el otro tipo — lo concreta el plan); queda en deshacer.
- Simplificación del trazo: Douglas-Peucker con tolerancia de unos pocos píxeles de pantalla; si tras simplificar no hay al menos 3 vértices con área, no se crea zona (sin aviso, como un trazo accidental).
- Tamaño de casilla para el círculo del token: el `CellSize` de la cuadrícula del mapa (`GridSettings.Default` ya trae 70 aunque la cuadrícula esté apagada).
- **Turno de un PNJ oculto por niebla**: igual que hoy con un token oculto o bajo niebla total: no aparece en la tira y no se resalta a nadie mientras dura su turno (sin pistas extra). Si hoy el comportamiento delata algo, se corrige igual para los dos tipos.
- **Versión del estado**: sube a 4 cuando hay alguna zona de PNJ (como se hizo al introducir la niebla); una app anterior abre el mapa en solo lectura, pero dibuja la zona de PNJ como si fuera total (su silueta se vería en la TV). Por eso hay que actualizar las dos apps a la vez. Hay que actualizar la tablet y el PC a la vez.
- **Cambiar el tipo de una zona destapada**: se permite; la zona elegida es la que ya da `ZoneAt` (la última que contiene el punto, tapada o no), como hoy en «Destapar» y «Goma».
- **Trazo que se cruza a sí mismo** (un ocho): se acepta si pasa `FogGeometry.IsValid`; no se repara ni se avisa. Queda anotado en la checklist como comportamiento conocido (lo que cubre alguna vuelta cuenta como zona, regla de vueltas).

## 6. Lo que queda al constructor

- Nombre del campo y de la versión del estado; forma exacta del selector y de la herramienta de cambiar tipo.
- Intersección círculo–polígono (bordes incluidos) en Core, con tests; versión JS solo si la tablet la necesita (la vista previa de jugadores usa el proyector del servidor).
- Comportamiento del gesto al salir del lienzo (seguir capturando el lápiz y cerrar con lo recogido).
- Tests: Core (serialización y compatibilidad, regla de dentro por tipo y bando, excepción de PJ, deshacer de cambiar tipo), JS (simplificación y cierre del trazo), Screens (selector, herramientas), proyección TV (zonas de PNJ no viajan, tira sin esos PNJ).
