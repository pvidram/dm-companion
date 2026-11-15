# DM Companion — Fase 3, ampliación: muertos, historial con deshacer y barra plegable

- **Fecha:** 2026-11-13
- **Estado:** aprobado el 13/11
- **Depende de:** [fase 3](2026-11-13-fase-3-combate-design.md) (combate en el `.state.json` v2, `CombatController`, tira de turnos) y la Task 11 (barra del mapa abajo, tira retraída en la franja superior). Se construye en la rama `feature/fase-3-combate`, antes de integrarla.
- **Origen:** peticiones del autor tras probar la fase 3 en la tablet (13/11).

## 1. Intención

En mesa, cuando un PJ o un enemigo cae para siempre, el DM quiere dejarlo en el mapa (como cadáver) sin que la tira de turnos se pare en él. Hoy solo puede sacarlo del combate o quitarlo del mapa.

Durante un combate, el DM quiere **ver lo que ha pasado** (seguimiento) y **deshacer errores** (un daño al token equivocado, un estado que no tocaba, un «Siguiente» de más), incluido recuperar un combate terminado por error. No quiere releer el combate después de la sesión: el historial vive solo mientras dura el combate.

Además quiere poder ocultar la barra inferior de herramientas para ver el mapa entero.

## 2. Objetivos

1. **Muerto:** cualquier token (PJ, aliado o enemigo) puede marcarse como muerto y revivirse. Un muerto sigue en el mapa y en la tira, se ve claramente distinto y «Siguiente» lo salta.
2. **Historial:** cada acción de combate queda apuntada con su ronda; se ve en la tira expandida, lo más reciente arriba.
3. **Deshacer / Rehacer** de las acciones de combate, varias veces seguidas, también tras cerrar y abrir la app.
4. **Recuperar un combate terminado por error** con «Deshacer».
5. **Barra inferior plegable:** un botón la oculta y la muestra; la app recuerda en el dispositivo cómo la dejaste.

## 3. No objetivos

- Releer el combate después de la sesión, exportarlo o guardarlo como nota del vault.
- Deshacer movimientos de tokens, trazos o cambios de cuadrícula desde el historial de combate (tienen su propio deshacer o ninguno, como hoy).
- Deshacer más allá de las últimas 100 acciones.
- Reglas automáticas de muerte: la app nunca marca a nadie como muerto por su cuenta.
- Las otras mejoras pedidas el mismo día (ajustar la cuadrícula con los dedos, paneles movibles y redimensionables): van en otra rama, después. Mover tokens con el lápiz en modo Dibujar sí entra (§6.8, añadido el 13/11).

## 4. Anti-objetivos

- **Un deshacer que deja el estado a medias o distinto del de antes:** deshacer una acción debe devolver exactamente lo que había, aunque esa acción gastara temporales, venciera estados o cambiara de ronda.
- **Perder el combate por un toque:** «Terminar combate» por error no puede ser irrecuperable.
- **Decidir por el DM:** la app no marca muertos ni salta turnos sin que el DM lo haya marcado.
- **Que el `.state.json` crezca sin control.**

## 5. Enfoques considerados (decidido el 13/11)

**A. Cada entrada del historial guarda el estado de combate de justo antes (elegido).** Al registrar una acción se guarda una copia de lo que esa acción puede cambiar: el bloque de combate y los datos de combate de los tokens (HP actuales, temporales, estados, salvaciones, muerto). Deshacer restaura esa copia. Todo va en el `.state.json`, con tope de 100 entradas.

**B. Operaciones inversas** («−7 HP» se deshace con «+7 HP»). Descartado: unas 15 operaciones, varias no reversibles sin el estado previo (temporales, reordenado, vencimientos, terminar combate); es donde se cuelan errores.

**C. Copias del mapa entero solo en memoria.** Descartado: se pierden historial y deshacer al cerrar la app.

## 6. Diseño

### 6.1 Muerto

- `MapToken` gana `Dead` (bool, opcional; ausente = false). Se guarda en el `.state.json` y existe también fuera de combate.
- **Marcar / Revivir:** en el menú ⋯ de la fila de la tira, en la casilla de la tira retraída (por la tarjeta) y en la tarjeta del token (`TokenCard`, con o sin combate). Deshabilitado en solo lectura.
- **Turnos:** `NextTurn` salta las entradas cuyo token está muerto (sin descontar sus duraciones, porque su turno no empieza). Si al dar la vuelta se pasa por la última entrada, la ronda sube igual. Si todas las entradas están muertas, «Siguiente» no hace nada. `StartCombat` coloca el turno en la primera entrada viva. «Pasar el turno aquí» sigue permitiendo dar el turno a un muerto a mano.
- **Aspecto:** en el mapa, el token sigue en su sitio, más atenuado que a 0 HP y con una marca de muerto (cruz o calavera; el icono lo decide quien construye dentro del design system). En la tira expandida, la fila en gris con el nombre tachado; en la retraída, la casilla igual.
- **Salvaciones de muerte:** al marcar la 3.ª salvación fallida de un PJ, la app avisa «{token}: 3 fallos. ¿Marcar como muerto?» con «Marcar como muerto» y «Ahora no». No lo marca sola.
- **Revivir** quita la marca y no toca HP ni salvaciones.
- **Formato:** campo opcional nuevo; la versión del `.state.json` sigue siendo 2 (un fichero sin `dead` se lee igual). Una app de la fase 3 sin este campo lo ignora al leer y lo perdería al reescribir, pero esa versión no ha salido de la rama.

### 6.2 Historial

- `CombatState` gana `Log`: lista de entradas, la más antigua primero, con tope de 100 (al añadir la 101.ª se descarta la más antigua).
- Cada entrada: `Id`, `Round`, `Text` (texto ya redactado en español, p. ej. «Goblin 1: −7 HP (4/11)»), `Before` (la copia de §6.3) y `Undone` (bool).
- **Qué se apunta** (una entrada por acción del DM, solo con combate activo; ver §6.7): empezar combate (no deshacible), siguiente turno (incluido el cambio de ronda), pasar el turno a mano, añadir y sacar del combate, reordenar, daño, curación, HP temporales, añadir, editar y quitar estado, quitar/mantener un vencido, salvaciones de muerte, marcar muerto y revivir. Los vencimientos que provoca un «Siguiente» van dentro de la entrada de ese «Siguiente» («R3 · Turno de Bruno · se acaba Asustado en Goblin 2»).
- **Textos:** con el nombre del token y lo que cambió; los da quien construye, cortos y en español. El historial muestra nombres de tokens: no va a los registros (logs) de la app.
- **Vista:** en la tira expandida, una sección «Historial» plegable debajo de la lista de turnos (o una pestaña; lo decide quien construye), con lo más reciente arriba. Las entradas deshechas se ven tachadas.

### 6.3 Deshacer y rehacer

- **Qué guarda `Before`:** el bloque de combate sin el historial (ronda, turno actual, entradas) y, de cada token del mapa, sus campos de combate: `HpCurrent`, `HpTemp`, `Conditions`, `DeathSaves`, `Dead`. Para no duplicar datos, se guarda solo lo de los tokens que la acción cambió más el bloque de combate; quien construye puede guardar siempre todos los tokens si el tamaño es aceptable (con 100 entradas y ~20 tokens, decenas de KB).
- **Deshacer:** restaura el `Before` de la última entrada no deshecha y la marca `Undone`. Se puede pulsar varias veces seguidas.
- **Rehacer:** vuelve a aplicar la última deshecha (guardando, para ello, el estado de justo antes de deshacer en la propia entrada: `After`). Cualquier acción nueva descarta las entradas deshechas (como en un editor).
- **Tokens que ya no existen:** si un token se quitó del mapa después de la acción, su parte del `Before` se ignora al deshacer. Quitar un token del mapa no es una acción del historial.
- **Movimientos y trazos** no entran en el historial ni se ven afectados por deshacer.
- **Botones:** «Deshacer» y «Rehacer» en la cabecera de la tira expandida; «Deshacer» también en la tira retraída. Deshabilitados si no hay nada que deshacer o rehacer, o en solo lectura.

### 6.4 Combate terminado por error

- «Terminar combate» guarda el combate completo (con su historial) en `MapState.LastCombat` antes de borrarlo, y los campos de combate de los tokens que `End` cambia (duraciones, vencidos, salvaciones).
- Tras terminar, un aviso «Combate terminado» con «Deshacer» durante unos segundos; además, mientras haya `LastCombat` y no haya combate, el diálogo de «Empezar combate» ofrece «Recuperar el combate anterior».
- **Recuperar** restaura el combate y los campos de los tokens; se apunta en el historial como «Combate recuperado».
- `LastCombat` se borra al empezar un combate nuevo.

### 6.5 Barra inferior plegable

- Un botón pequeño, siempre visible en la esquina inferior derecha del área del mapa, oculta y muestra la barra de herramientas (`MapControls`).
- Plegada: solo queda ese botón; los desplegables de la barra (ajustes de cuadrícula) se cierran.
- La app recuerda el estado en el dispositivo (preferencias, como el tamaño de la tira).
- Con la barra plegada, el modo activo (Navegar, Dibujar, Medir) no cambia.

### 6.8 Lápiz sobre un token en modo Dibujar (aprobado el 13/11)

- En modo Dibujar, el lápiz que se apoya **sobre un token**:
  - **toque corto** (se levanta antes de 400 ms sin moverse más de ~8 px): abre la tarjeta del token, como un toque en Navegar;
  - **mantenerlo quieto ≥ 400 ms**: coge el token (se resalta) y el lápiz lo arrastra sin pintar; al levantarlo se guarda el movimiento igual que un arrastre en Navegar;
  - **moverlo más de ~8 px antes de los 400 ms**: pinta como hoy, con el trazo empezando en el punto donde se apoyó.
- El lápiz fuera de un token pinta como hoy; el dedo sigue desplazando y haciendo zoom; el modo sigue en Dibujar después.
- En solo lectura no se coge nada (el modo efectivo ya es Navegar).

### 6.6 Persistencia y errores

- Todo por `MapSession.Mutate`, como el resto del combate. La reparación al leer (`MapStateRepair`) descarta entradas del historial incoherentes (sin `Before`, ids repetidos) y recorta a 100; un `LastCombat` incoherente se descarta. Ninguna excepción por contenido.
- Los registros (logs) no incluyen textos del historial ni nombres.

### 6.7 Decisiones tomadas tras la revisión

1. **Fuera de combate no hay historial.** Marcar muerto, revivir, daño o estados hechos sin combate activo no se apuntan ni se pueden deshacer. **Con combate activo**, todo cambio de combate se apunta y es deshacible, también los hechos desde la tarjeta del token (sus HP editables y muerto/revivir pasan por el `CombatController`).
2. **«Empezar combate» se apunta pero no se deshace:** es la primera entrada y «Deshacer» se detiene en ella. Para anularlo está «Terminar combate» (que a su vez se puede recuperar).
3. **Deshacer con tokens que ya no están en el mapa:** tras restaurar el `Before`, se aplica la misma limpieza que al quitar un token del combate (`RemoveEntriesOf`): sus entradas desaparecen, el turno pasa a la siguiente entrada viva y las duraciones ancladas se reanclan o quedan sin duración.
4. **Muertos en los casos límite:**
   - Marcar muerto al token del turno actual no mueve el turno; el DM pulsa «Siguiente».
   - Si al empezar todas las entradas están muertas, el turno queda en la primera.
   - Un «Siguiente» que salta muertos es una sola entrada del historial; si cruza la vuelta, el texto lo dice («R4 · Turno de Lyra»).
   - «Pasar el turno aquí» a un muerto no descuenta duraciones (como ya pasa con `SetCurrent`).
5. **`LastCombat`:** es el combate completo (con su historial) más la copia de los campos de combate de todos los tokens, con el mismo tipo de copia que `Before`. Hay una sola: terminar otra vez la sustituye. Se descarta al recuperar y al empezar un combate nuevo. El aviso «Combate terminado · Deshacer» es un tipo de aviso nuevo con botón, que desaparece a los 10 segundos o con la siguiente acción; el aviso y «Recuperar el combate anterior» llaman a la misma operación.
6. **Un solo dispositivo edita el combate** (como en la fase 3, §5). Si llega por Syncthing un `.state.json` con otro historial, se toma tal cual; deshacer restaura sin más comprobaciones.
7. **Deshacer y rehacer vacían los avisos pendientes** (vencimientos, concentración, 3 fallos): pueden no corresponder ya al estado restaurado.
8. **`Before` guarda los campos de combate de todos los tokens del mapa**, no solo los afectados: es más simple y robusto, y el tamaño es aceptable con el tope de 100.

## 7. Verificación

- **Core:** saltar muertos en `NextTurn` (incluida la vuelta de ronda y «todos muertos»), `Start` en la primera viva; para cada tipo de acción, deshacer devuelve exactamente el estado anterior y rehacer el posterior; tope de 100; acción nueva tras deshacer descarta lo deshecho; deshacer con un token ya quitado del mapa; terminar y recuperar.
- **Serializador/reparación:** ida y vuelta de `dead`, `log`, `lastCombat`; reparaciones nuevas.
- **Screens (bUnit):** marcar y revivir desde la fila y desde la tarjeta, aspecto de muerto, aviso de 3 fallos, historial visible y tachado, Deshacer/Rehacer, aviso de combate terminado, recuperar desde el diálogo, barra plegable recordada.
- **JS:** la clasificación del gesto del lápiz (toque, coger, pintar) por tiempo y distancia, con `node --test`.
- **Checklist manual:** lápiz sobre un token en Dibujar (toque, mantener y arrastrar, pintar), marcar muerto en mesa, deshacer varias acciones, cerrar la app y deshacer, terminar por error y recuperar, plegar la barra.

## 8. Qué decide quien construya

- El icono de muerto y el estilo exacto de la fila y del token.
- Si el historial es sección plegable o pestaña en la tira expandida.
- Los textos concretos de cada entrada del historial (cortos, en español, con nombre y cambio).
- Si `Before` guarda solo los tokens afectados o todos.
- La duración del aviso «Combate terminado · Deshacer».
