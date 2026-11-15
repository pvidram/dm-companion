# DM Companion — Fase 6: ambiente sonoro

- **Estado:** aprobado (15/11/2026). `ambience:`, efecto de una vez por defecto y `_audio/_dm/` aprobados.
- **Orden de fases acordado el 15/11:** 6 (sonido) → 7 (TV) → 4 (niebla) → 5 (glosario) → 8 (diario).
- **Relacionado:** diseño global `2026-11-03-dm-companion-design.md` (§3 arquitectura, §4 `_audio/`, §6 fase 6).

## 1. Intención

El autor dirige en mesa con la tablet y quiere ambientar la partida: una música o ambiente de fondo que cambia con el lugar y, encima, efectos puntuales (un trueno, una puerta) o continuos (lluvia, fuego), varios a la vez. Todo lo prepara antes de la partida con ficheros que él mismo consigue; en mesa solo lanza, mezcla y cambia, sin salir del mapa durante el combate y sin que el sonido se corte si la pantalla se apaga.

## 2. Objetivos

- Biblioteca local en `_audio/` del vault, organizada por las subcarpetas que el autor cree, con buscador.
- Un **fondo** (un track o una sección como lista aleatoria) con fundido al cambiar.
- **Efectos** simultáneos, cada uno con su volumen, de un solo disparo o en bucle.
- **Escenas**: guardar la mezcla actual con un nombre y lanzarla con un toque.
- **Vincular notas** a una escena, sección o track y lanzarlo desde la nota.
- Controles accesibles desde cualquier pantalla (mini control en la franja superior y panel rápido de favoritos).
- Sigue sonando con la pantalla apagada o la app en segundo plano (Android).
- Funciona también en la versión de PC y en el DevHost.

## 3. No objetivos

- Buscar o descargar sonido desde la app (YouTube, Freesound, enlaces): el autor descarga los ficheros por su cuenta y los mete en `_audio/`. Todo lo que suena en partida es local; la app nunca necesita internet para el sonido.
- Editar audio (recortar, normalizar), ecualizador o efectos de sonido.
- Sacar el sonido por la TV (fase 7 decidirá si hace falta).
- Sincronizar la reproducción entre dispositivos.

## 4. Anti-objetivos

- Que algo suene solo al abrir una nota (al preparar la partida se abren notas para leerlas).
- Que el sonido se corte al apagarse la pantalla.
- Que lanzar un efecto en combate obligue a salir del mapa.
- Tocar los ficheros de audio del autor: la app nunca mueve, renombra ni borra nada de `_audio/`; solo escribe sus propios ficheros de escenas y favoritos (§6.6).

## 5. Enfoques considerados (decidido el 15/11: A)

- **A (elegido): lógica común, reproductor por plataforma.** Mezcla, escenas, listas, fundidos, volúmenes y vínculos viven en código compartido y se prueban sin dispositivo. Cada plataforma solo carga y reproduce ficheros con el volumen que se le indica, detrás de `IAudioPlayer`: en Android, reproductor nativo con servicio en primer plano y notificación; en PC y DevHost, el `<audio>`/Web Audio del navegador. Coste: dos reproductores finos que probar.
- **B (descartado): audio de la parte web también en Android.** Un solo código, pero Android pausa el navegador interno en segundo plano de forma variable según modelo y versión: se cortaría.
- **C (descartado): solo Android nativo.** El PC (y el amigo del autor) se quedaría sin sonido y no se podría probar en el DevHost.

## 6. Diseño

### 6.1 Biblioteca

- Raíz: `_audio/` del vault. Se listan los ficheros de audio de todas sus subcarpetas; cada subcarpeta es una **sección** plegable con su ruta (`Música/Tabernas`). Las carpetas que empiezan por `_` no se listan (son de la app, §6.6).
- Formatos: `.mp3`, `.ogg`, `.wav`, `.m4a`, `.flac` (por extensión, sin distinguir mayúsculas).
- Buscador por nombre de fichero y de sección, en la propia pantalla.
- Se reindexa con los cambios de la carpeta (como el resto del vault, incluidos los que llegan por Syncthing).
- Nombre visible: el del fichero sin extensión.
- **Acciones** (aprobadas el 15/11): tocar un track lo lanza como **efecto** (una vez); el botón «Fondo» de su fila lo pone como **fondo**; cada sección tiene «▶ Lista como fondo». Los tracks no tienen tipo fijo.

### 6.2 Mezcla

- **Fondo** (uno a la vez): un track en bucle o una **sección como lista**: sus tracks suenan uno tras otro en orden aleatorio, sin repetir hasta haberlos tocado todos (al acabar la vuelta se baraja de nuevo, evitando que el primero de la nueva vuelta sea el último de la anterior). Cambiar el fondo hace un **fundido cruzado** (por defecto 2 s).
- **Efectos** (varios a la vez): cada uno con su volumen y su **modo de repetición**. Por defecto un efecto suena **una vez** (decisión tomada, aprobada: lo más seguro es que no se quede sonando sin querer). Modos, que se cambian en la mezcla:
  - **Una vez.**
  - **Bucle continuo**: vuelve a empezar en cuanto termina (lluvia, fuego).
  - **Repetir con pausa** (pedido el 15/11): tras terminar, espera un tiempo y vuelve a sonar. La pausa es **fija** (p. ej. cada 15 s) o **aleatoria en un rango** (p. ej. entre 5 y 20 s, sorteada de nuevo en cada repetición). Se mide desde que termina una reproducción hasta que empieza la siguiente. Valores en segundos, mínimo 1 s; con mínimo = máximo es fija. Pensado para cuervos, goteos, campanas lejanas.
- Se puede lanzar el mismo efecto varias veces solapado (dos truenos seguidos).
- **Volumen general**, **pausar todo** / reanudar y **parar todo** (con fundido corto).
- Volúmenes de 0 a 100 %. El volumen de cada pista se aplica sobre el general.

### 6.3 Escenas

- «Guardar como escena» toma lo que suena: el fondo (track o sección), los efectos en bucle continuo o con pausa con su volumen y su pausa, y el volumen general. Los efectos de un solo disparo no se guardan.
- **Lanzar una escena sobre una mezcla que suena** (aprobado el 15/11):
  - el fondo cambia con fundido cruzado;
  - los efectos en bucle (continuo o con pausa) que no son de la escena se apagan con fundido;
  - los efectos de la escena que ya sonaban siguen sin cortarse y solo ajustan su volumen y su modo;
  - los efectos de una vez que estén sonando terminan de forma natural;
  - el volumen general pasa al de la escena con fundido.
- Los efectos sueltos que el autor lance después se suman encima sin parar la escena.
- Lanzar un **track o una sección** (desde la biblioteca o desde una nota) solo cambia el fondo; los efectos siguen.
- Escenas: renombrar, sobrescribir con la mezcla actual y borrar (con confirmación).
- Una escena que referencia un fichero o sección que ya no existe se lanza sin esa parte y lo indica («falta: …»).

### 6.4 Vínculo desde las notas

- Campo de frontmatter **`ambience:`** (en inglés, como `system:`, `type:` o `portrait:` del resto del vault; decisión tomada, en la conversación se habló de `ambiente:`). Acepta:
  - el nombre de una escena: `ambience: Taberna`;
  - una sección: `ambience: Música/Tabernas`;
  - un track: `ambience: Música/Tabernas/el_pony_pisador.mp3`;
  - un enlace de Obsidian a un fichero de audio: `ambience: "[[el_pony_pisador.mp3]]"` (se resuelve como cualquier enlace del vault).
- Resolución, en este orden: escena con ese nombre → sección → track. Las rutas son relativas a `_audio/`.
- Al abrir la nota sale un botón «▶ Ambiente: {nombre}»; **no suena solo**. Un toque lo lanza como fondo (escena: como escena). Si ya está sonando, el botón lo indica.
- Si no se encuentra, el botón sale tachado con «no encontrado» (resultado, no excepción).

### 6.5 Dónde viven los controles

- **Pantalla «Sonido»** en la barra lateral (junto a Notas y Mapas): biblioteca con secciones y buscador, escenas, y la mezcla actual (qué suena, volúmenes, bucle, parar).
- **Mini control en la franja superior** (junto a la tira de combate retraída y al aviso de conflictos): qué suena (nombre del fondo o escena), pausar/reanudar todo y volumen general. Al tocarlo abre un **panel rápido** con los efectos favoritos y las escenas, para dispararlos sin salir del mapa.
- **Favoritos:** estrella en cada track de la biblioteca; el panel rápido los muestra en el orden en que se marcaron y los lanza como **efectos** que suenan **una vez** (un favorito es un track, no guarda modo de repetición; para repetir, se lanza desde la biblioteca o se guarda en una escena); las escenas también salen en el panel rápido.
- Solo lectura del vault no afecta al sonido (reproducir no escribe); guardar escenas y favoritos sí queda deshabilitado.

### 6.6 Persistencia

- Escenas: un fichero por escena en `_audio/_dm/scenes/{nombre}.json` (dentro del vault, para que viajen con Syncthing y existan también en el PC).
- Favoritos: `_audio/_dm/favorites.json`.
- La app solo escribe dentro de `_audio/_dm/`. Ficheros inválidos se tratan como el resto del vault: se ignoran o reparan con aviso, nunca se lanzan excepciones por contenido. Escritura atómica como el `.state.json` de los mapas.
- Lo que suena no se recuerda al cerrar la app (se empieza en silencio).

### 6.7 Plataformas

- `IAudioPlayer` (en `Screens/Platform`): operaciones mínimas por canal (cargar fichero, reproducir, pausar, parar, volumen, bucle, aviso de fin). Fundidos, listas y escenas no viven aquí.
- **Android:** reproductor nativo, varios a la vez; **servicio en primer plano** de tipo reproducción con notificación (qué suena, pausar/parar). Pide el permiso de notificaciones si hace falta. Gestión del foco de audio razonable (pausar ante una llamada).
- **PC y DevHost:** reproducción en el navegador vía interop JS (un servicio por funcionalidad, regla del proyecto); los ficheros se sirven desde el vault por el host local.
- Registros: solo rutas relativas y tipos de excepción.


### 6.8 Decisiones tomadas tras la revisión del diseño

1. **Volumen del fondo:** el fondo tiene su propio volumen, como cada efecto, y la escena lo guarda.
2. **Enlace `[[x.mp3]]` en `ambience:`:** se busca por nombre de fichero solo dentro de `_audio/` (sin distinguir mayúsculas ni tildes, como `VaultIndex`); si hay varios con ese nombre, gana la ruta más corta, como en Obsidian; un audio fuera de `_audio/` cuenta como «no encontrado». No toca el índice de notas: hay un resolvedor propio sobre el índice de audio.
3. **Control desde fuera (notificación, llamadas):** `IAudioPlayer` avisa a la lógica común de las pausas y paradas externas y la interfaz se actualiza. Tras una llamada, el sonido queda en pausa (no se reanuda solo). Cerrar la app o cambiar de vault para todo y cierra el servicio en primer plano.
4. **Solo lectura:** no hay un estado de solo lectura del vault; si escribir en `_audio/_dm/` falla, se avisa como cualquier fallo de escritura y la mezcla sigue sonando.
5. **Conflictos de Syncthing en `_audio/_dm/`:** los ficheros `*.sync-conflict-*` no aparecen como escenas ni se leen como favoritos; se registran (ruta relativa) y se dejan para que el autor los resuelva en el PC.
6. **Nombres de escena:** el nombre es el del fichero (saneado como `NoteTemplates`: sin `/`, `:` ni caracteres no válidos); se resuelve desde `ambience:` sin distinguir mayúsculas ni tildes; una escena gana a una sección con el mismo nombre (orden de §6.4). Renombrar una escena **no** cambia las notas que la citan (la app solo escribe en `_audio/_dm/`); el diálogo de renombrar avisa de cuántas notas la citan, que pasarán a mostrar «no encontrado».

## 7. Verificación

- **Core:** lista aleatoria sin repetición y nueva vuelta; resolución de `ambience:` (escena, sección, track, enlace, no encontrado); aplicar escena sobre una mezcla (qué para, qué sigue, qué entra); fundidos con `FakeTimeProvider` (volúmenes en el tiempo); repetir con pausa fija y aleatoria (el sorteo cae en el rango, con un aleatorio inyectado; parar durante la pausa no vuelve a sonar); parar/pausar todo; escenas y favoritos (lectura, escritura, ficheros inválidos).
- **Infrastructure:** escaneo de `_audio/` (formatos, carpetas `_` ocultas) en carpeta temporal.
- **Screens (bUnit):** pantalla Sonido, mini control, panel rápido, botón de ambiente en la nota, solo lectura.
- **JS:** lo puro del reproductor web con `node --test`.
- **Checklist tablet:** sonar con la pantalla apagada, notificación, varios efectos a la vez, fundidos, escena desde la nota, efecto desde el mapa en combate.

## 8. Qué decide quien construya

- La biblioteca concreta de Android (reproductor nativo de la plataforma, ExoPlayer/Media3 o una biblioteca MAUI de audio), siempre detrás de `IAudioPlayer` y cumpliendo §6.7.
- Duración exacta de los fundidos (2 s fondo, corto al parar) dentro de lo razonable.
- Iconos y aspecto dentro del design system (`ds-`), y si hacen falta componentes nuevos en `DmCompanion.UI`.
- Cómo sirve el host de PC los ficheros de audio.
