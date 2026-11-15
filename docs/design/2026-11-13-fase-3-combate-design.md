# DM Companion — Fase 3: Combate

- **Fecha:** 2026-11-13
- **Estado:** aprobado el 13/11 (revisado por un lector-constructor el 13/11; sus preguntas están resueltas en el texto y las marcadas «decisión tomada» son decisiones del propio diseño)
- **Depende de:** [spec global](2026-11-03-dm-companion-design.md) (§3 arquitectura, §6 fases, §9 sistemas de juego) y [fase 2](2026-11-12-fase-2-mapa-design.md) (mapa con tokens, `.state.json`, `MapSession`, HP actuales §7.9), integradas en `main`.

## 1. Intención

Llevar los combates de la mesa desde el mapa de la tablet, sin papel. Lo que hoy se le escapa al DM son **los estados y su duración en rondas**; además quiere el orden de turnos, los HP y lo que pasa a 0 HP a la vista.

La app **no tira dados ni aplica reglas por su cuenta**: todas las tiradas se hacen en la mesa, el DM apunta los resultados y decide. La app ordena, recuerda, cuenta rondas y avisa.

En esta fase solo se construye el combate de **D&D 5e (2024)**. Cosmere queda **preparado en el modelo** (fases rápida y lenta, reglas de daño y de salud a 0 por sistema) para añadirlo después sin rehacer nada (§9).

## 2. Objetivos

1. **Empezar combate** desde el mapa: elegir qué tokens entran (por defecto, todos los del mapa) y apuntar la iniciativa de cada uno; la app los ordena de mayor a menor.
2. **Orden manual:** el DM reordena arrastrando en cualquier momento; el orden manual manda sobre los números. Los empates los deshace el DM.
3. **Turnos y rondas:** turno actual marcado (en la tira y en el token del mapa), botón «Siguiente» y contador de rondas.
4. **Altas y bajas a mitad de combate:** añadir un token del mapa con su número (entra en el sitio que le toca) y sacar una entrada.
5. **Estados por token:** de la lista del sistema o texto libre, con duración opcional en rondas. Al vencer, la app **avisa** y el DM decide (quitar o mantener).
6. **HP:** daño y curación desde la tira y **HP temporales**, que se gastan primero.
7. **0 HP:** el token se **atenúa** pero sigue en el mapa y en la tira. Los PJ muestran **salvaciones de muerte** (3 éxitos / 3 fallos) que marca el DM.
8. **Concentración:** se apunta como un estado; al recibir daño, la app recuerda la salvación de Constitución con su CD.
9. **Terminar combate:** vacía la tira y las duraciones; los HP actuales y temporales se quedan.
10. **Persistencia:** todo vive en el `.state.json` del mapa y se guarda como el resto del mapa: cerrar la app a mitad de combate y volver deja el combate igual.

## 3. No objetivos (fuera de esta fase)

- Tirar dados en la app, incluida la iniciativa a partir del bonificador.
- Desempates automáticos, retrasar turno como acción con reglas, acciones legendarias y de guarida.
- Aplicar los efectos de los estados (velocidad, ventaja, incapacitación…). Un estado es una etiqueta con duración.
- Volver al turno anterior deshaciendo contadores. El DM puede **pasar el turno** a cualquier entrada sin efectos (§7.4).
- Cálculo de ataques, AC o daño, resistencias e inmunidades.
- El combate de Cosmere completo (§9) y la vista de la tira en la TV (fase 7).
- Áreas de efecto o marcas de hechizos en el mapa.

## 4. Anti-objetivos (sería un fracaso aunque funcionase)

- **Perder el combate:** un cierre de la app, un cambio de mapa o una sincronización no pueden perder el orden, la ronda, los estados ni los HP.
- **Decidir por el DM:** la app no quita estados, no salta turnos, no cambia el orden ni da a nadie por muerto por su cuenta.
- **Tocar las notas de personajes:** todo el estado de combate vive en el `.state.json` del mapa.
- **Lentitud al empezar con muchos tokens:** apuntar 8–10 iniciativas tiene que ser cuestión de segundos.

## 5. Restricciones

- Arquitectura de la spec global y de la fase 2: lógica en `Core` testeada sin emulador; `Screens` y `UI` solo presentación; interop C# ↔ JS limitado a «estado → JS» y «eventos JS → C#».
- El combate se diseña contra `IGameSystem` (spec global §9.3), no contra D&D.
- Un único escritor en mesa (la tablet del DM), pero el `.state.json` se sincroniza con Syncthing: aplican la recarga externa, los conflictos y el cierre seguro de la fase 2 (§7.4 de la fase 2 y el arreglo de `MapSession` de esta semana).
- Tablet de referencia en horizontal (≈ 1280×800); también debe funcionar en el DevHost.

## 6. Enfoques considerados

**A. El combate dentro del `.state.json` del mapa (elegido).** Un bloque `combat` y campos de combate en cada token, editados por `MapEditor` y guardados por `MapSession`. Hereda el guardado diferido, la recarga externa con reintentos, los conflictos y el cierre sin pérdida. Un token borrado sale del combate en la misma operación. Coste: el formato sube a la versión 2 y hay que leer la versión 1 sin pérdida.

**B. Un fichero aparte por mapa (`<mapa>.combat.json`).** Separa combate y dibujo en los conflictos, pero obliga a duplicar toda la maquinaria de `MapSession` (la parte más delicada de la app) y a mantener dos ficheros coherentes. Descartado.

**Descartado también:** reglas de D&D escritas directamente en el modelo de combate, a adaptar luego para Cosmere. Va contra la spec global y contra la decisión de dejar Cosmere preparado.

Se eligió **A** el 13/11: el riesgo principal de la app es perder trabajo, y A reutiliza lo que ya está probado contra eso.

## 7. Diseño

### 7.1 Modelo en el `.state.json` (versión 2)

`MapState.CurrentVersion` pasa a **2**. Campos nuevos (los nombres JSON siguen el estilo del fichero actual, camelCase):

```jsonc
{
  "version": 2,
  "grid": { … }, "strokes": [ … ],
  "tokens": [
    {
      "id": "t1", "name": "Goblin 1", … , "hp": 7, "hpCurrent": 4,
      "hpTemp": 0,                         // nuevo, opcional (ausente = 0)
      "conditions": [                      // nuevo, opcional (ausente = [])
        { "id": "c1", "key": "frightened", "label": "Asustado",
          "level": null,                   // solo para estados con nivel (Agotamiento)
          "duration": { "entryId": "e2", "turnsLeft": 1 },   // null = sin duración
          "expired": false }
      ],
      "deathSaves": { "successes": 0, "failures": 0 }        // nuevo, opcional; solo se usa en PJ a 0 HP
    }
  ],
  "combat": {                              // nuevo; ausente o null = no hay combate
    "round": 2,
    "currentEntryId": "e2",
    "entries": [                           // el orden del array ES el orden de turnos
      { "id": "e1", "tokenId": "t3", "initiative": 18, "phase": null },
      { "id": "e2", "tokenId": "t1", "initiative": 15, "phase": null }
    ]
  }
}
```

- **Entradas, no tokens, en el orden:** una entrada apunta a un token. En D&D hay una entrada por token; el modelo admite más de una (jefes de Cosmere con turno rápido y lento) y el campo `phase` (null en D&D; reservado para `fast`/`slow`, §9).
- **`initiative`** es el número apuntado (puede faltar si el DM añadió la entrada sin número). Solo sirve para colocar entradas nuevas; el orden real es el del array.
- **Duración anclada a una entrada:** `entryId` es la entrada en cuyo **inicio de turno** se descuenta; `turnsLeft` es cuántos inicios de turno de esa entrada faltan. Por defecto, la entrada del propio token afectado. Así funciona igual para D&D y para las fases de Cosmere (§9).
- **Estados fuera de combate:** un token puede llevar estados sin combate (sin duración). Si se añade un estado con duración sin combate activo, la UI no ofrece duración.
- **Compatibilidad:**
  - Leer un fichero de versión 1 da un estado de versión 2 con los campos nuevos vacíos, sin pérdida; se reescribe como versión 2 en el siguiente guardado. Test obligatorio.
  - Una app antigua que lea un fichero de versión 2 lo abre en **solo lectura con aviso** (comportamiento actual de `FutureVersion`). No hay exportación a versión 1: el vault del autor solo lo abre su tablet (en el PC lo lee Obsidian, que no usa el `.state.json`), y el amigo con la versión de PC tiene su propio vault. El README y la guía de la versión de PC avisan de que conviene actualizar todas las instalaciones que compartan un vault.
  - Integridad al leer: entradas cuyo token no existe, ids repetidos, `currentEntryId` inexistente o duraciones ancladas a entradas inexistentes se **corrigen al leer** (se descartan o se reasigna el turno a la primera entrada) y se informa en Diagnóstico, nunca con excepción. Valores fuera de rango (negativos) se acotan.

### 7.2 Core (`DmCompanion.Core/Combat/` y `Maps/`)

- Modelos inmutables (`record`): `CombatState`, `CombatEntry`, `TokenCondition`, `ConditionDuration`, `DeathSaves`; campos nuevos en `MapToken` y `MapState`.
- Operaciones puras y testeadas (en un `CombatEditor` o como métodos de `MapEditor`; lo decide quien construya, §10). Cada operación devuelve el nuevo estado y, cuando proceda, **avisos** para la UI:
  - `StartCombat(entradas con número)`: ordena de mayor a menor; empates en el orden en que se apuntaron; ronda 1; turno en la primera entrada; descuenta el inicio de turno de esa primera entrada (§7.4).
  - `AddEntry(tokenId, initiative?)`: inserta tras la última entrada con número mayor o igual; sin número, al final.
  - `MoveEntry(entryId, newIndex)`: reordenado manual. No cambia `initiative`.
  - `RemoveEntry(entryId)`: si era la actual, el turno pasa a la siguiente (sin descuentos); las duraciones ancladas a ella se reanclan a la entrada del propio token afectado si existe, y si no, se quedan sin duración.
  - `NextTurn()`: pasa a la siguiente entrada; al dar la vuelta, `round + 1`. Al **empezar** el turno de la nueva entrada, descuenta `turnsLeft` de las duraciones ancladas a ella; las que llegan a 0 pasan a `expired = true` y generan un aviso «A {token} se le acaba {estado}».
  - `SetCurrent(entryId)`: pasar el turno a una entrada sin descuentos ni cambio de ronda (corrección manual).
  - `EndCombat()`: borra `combat`, quita todas las duraciones y marcas `expired` (los estados quedan sin duración) y pone a cero las salvaciones de muerte. HP actuales y temporales se quedan.
  - Estados: `AddCondition`, `UpdateCondition` (nivel, duración, ancla), `RemoveCondition`, `KeepExpired(id, turnsLeft?)` (mantener sin duración o con rondas nuevas).
  - HP: `ApplyDamage(tokenId, amount)`, `ApplyHealing(tokenId, amount)`, `SetTempHp(tokenId, value)` y el `SetHp` actual. Las reglas de cómo se aplica el daño las da el sistema (§7.3). Si el token lleva el estado de concentración y recibe daño > 0, aviso «Salvación de Constitución para mantener la concentración: CD {max(10, ⌊daño/2⌋), hasta 30}».
  - Salvaciones de muerte: `SetDeathSaves(tokenId, successes, failures)` acotado a 0–3; se ponen a cero al pasar de 0 HP a más.
- Los tokens de nota no guardan aquí sus HP máximos (vienen de la nota, como hoy); los genéricos sí. Sin cambios en ese reparto.
- **De dónde sale el máximo** *(decisión tomada tras la revisión)*: Core no lee notas. Las operaciones de HP reciben el máximo como parámetro (`int?`), igual que el `SetHp` actual. En `Screens`, un servicio resuelve el máximo de cada token del mapa con la misma lógica que hoy usa `TokenCard` (`HitPointsRule` del sistema activo sobre la nota, o `Hp` en los genéricos) y lo ofrece a la tira, a la tarjeta y al estado que se envía a Konva; se recalcula cuando cambia el vault o el mapa.
  - `HpCurrent == null` significa «al máximo».
  - **Sin máximo conocido** (nota borrada o sin HP): el daño resta desde el valor actual si lo hay (si no lo hay, la tira muestra «—» y el daño pide antes fijar los HP); la curación no tiene tope; el token solo se atenúa si `HpCurrent == 0`.
  - «A 0 HP» se decide solo con `HpCurrent == 0`, sin necesitar el máximo.
- **Borrar un token del mapa** con una entrada de combate aplica las reglas de `RemoveEntry` (turno a la siguiente, duraciones reancladas) en la misma operación, sin aviso. Las operaciones de combate no entran en la pila de deshacer, que sigue siendo solo de trazos.

### 7.3 Reglas por sistema (`IGameSystem.Combat`)

El sistema que rige el combate es el **sistema activo de la campaña** (`CampaignResolver`, nota de campaña del vault), el mismo que ya usa la tarjeta del token. Todos los tokens de un mapa, de nota o genéricos, se rigen por él.

`IGameSystem` gana una propiedad `Combat` de tipo `ICombatRules` (nombre orientativo):

- `Conditions`: catálogo de estados del sistema (`key`, etiqueta en español, si lleva nivel y su máximo).
- `ApplyDamage(hpCurrent, hpTemp, amount)` → nuevos HP y temporales. D&D: primero los temporales, luego los actuales, mínimo 0.
- `ApplyHealing(…)`: D&D: suma a los actuales hasta el máximo; no toca los temporales.
- `SetTempHp`: D&D: los temporales no se acumulan; el valor nuevo sustituye al anterior (la UI propone el mayor de los dos; decide el DM).
- `AtZeroHp`: qué se muestra a 0 HP. D&D: atenuado; los PJ (`side = pc`) con salvaciones de muerte.
- `ConcentrationKey` y regla de la CD (D&D: la de §7.2). Sin concentración en el sistema, sin aviso.
- `Phases`: D&D y genérico, ninguna; Cosmere, rápida y lenta (§9).
- **D&D (`dnd5e`):** catálogo con las condiciones del SRD 5.2: Agarrado, Apresado, Asustado, Aturdido, Cegado, Derribado, Ensordecido, Envenenado, Hechizado, Incapacitado, Inconsciente, Invisible, Paralizado, Petrificado y Agotamiento (nivel 1–6), más «Concentración». *(Etiquetas por contrastar con la edición en español del PHB 2024, como el resto de §9.4.)*
- **Genérico y Cosmere (por ahora):** sin catálogo (solo estados libres), daño y curación simples sobre los HP, sin temporales especiales ni salvaciones de muerte, sin concentración, sin fases.

### 7.4 Turnos y avisos

- El **descuento** de una duración ocurre al **empezar** el turno de su entrada ancla: con `NextTurn` y con la primera entrada de `StartCombat`. `SetCurrent` y `RemoveEntry` no descuentan.
- «N rondas» al poner un estado se guarda como `turnsLeft = N` anclado a la entrada del token afectado (o a la que elija el DM). Si se pone durante el turno del propio afectado, el turno en curso no cuenta.
- *(Decisión tomada tras la revisión.)* «N rondas» significa siempre **N inicios de turno de la entrada ancla**, sin importar si el ancla ya actuó en la ronda en curso. Ejemplo: «Asustado 1» puesto a Goblin 2 cuando ya actuó vence al empezar su turno de la ronda siguiente; puesto antes de que actúe, vence al empezar su turno de esta ronda. Es como funcionan los rastreadores de combate habituales y es lo que muestra el chip («Asustado 1» = le queda un inicio de turno). Si en mesa no encaja, el DM cambia las rondas o el ancla.
- Un estado vencido **no se quita**: queda marcado como vencido hasta que el DM elige «Quitar» o «Mantener» (sin duración o con rondas nuevas).
- Avisos: al empezar un turno con vencimientos, al aplicar daño a alguien concentrado. Se muestran con el patrón de avisos existente y no bloquean; los vencidos siguen marcados aunque el aviso se cierre.

### 7.5 Interfaz

Bocetos aprobados en la sesión del 13/11 (navegador): mezcla de las opciones A y B.

- **«Empezar combate»** en los controles del mapa (`MapControls`), solo con un mapa abierto y sin combate activo. Abre un diálogo con los tokens del mapa (todos marcados), un campo numérico por token y un botón «Empezar». Al escribir un número, el foco salta al siguiente campo para apuntar rápido. Un token sin número entra al final.
- **Tira de turnos** con dos tamaños y un botón (◂ / ▸) para cambiar entre ellos sin perder nada:
  - **Expandida:** panel a la derecha del mapa (el mapa se estrecha). Cabecera con «Ronda N», «Siguiente ▸» y un menú con «Añadir al combate» y «Terminar combate». Una fila por entrada con asa de arrastre, número, nombre, HP («4/7», con «+5» si tiene temporales), estados como chips (con rondas restantes; vencidos resaltados) y, en PJ a 0 HP, las salvaciones de muerte. Debajo, para la entrada seleccionada: «− Daño», «+ Curar», «HP temp.» y «+ Estado». Tocar una fila selecciona su token en el mapa; mantener pulsada o un menú de fila ofrece «Pasar el turno aquí» y «Sacar del combate».
  - **Retraída:** franja fina arriba del mapa con la ronda, una casilla por entrada (número y nombre) y «Siguiente ▸». La entrada actual resaltada, las de 0 HP atenuadas y un punto ● en las que tienen un estado vencido. Tocar una casilla abre la tarjeta del token.
  - Al empezar un combate, la tira sale **expandida**; después la app recuerda en el dispositivo el último tamaño elegido (preferencias de la app).
- **En el mapa:** el token del turno actual con un anillo destacado; los tokens a 0 HP atenuados (también fuera de combate); un indicador pequeño con el número de estados y una marca si alguno está vencido.
- **Tarjeta del token (`TokenCard`):** muestra los HP temporales y los estados; los HP actuales se siguen editando como hoy (§7.9 de la fase 2).
- **Daño y curación:** piden un número con teclado numérico. El valor escrito es siempre positivo; el botón decide si resta o suma.
- **Estados:** selector con el catálogo del sistema y un campo de texto libre; nivel si el estado lo lleva; duración en rondas opcional (solo con combate activo) y, plegado, «cuenta en el turno de…» para cambiar el ancla.
- Todo en español; componentes del design system (`DmCompanion.UI`) y estilo existente.

### 7.6 Guardado, sincronización y errores

- Todas las operaciones pasan por `MapSession.Mutate` como las de la fase 2: guardado diferido, solo lectura, recarga externa con reintentos, conflictos (§7.4 de la fase 2) y guardado al cerrar.
- **Recarga externa con combate en curso:** el combate es parte del estado del mapa y sigue exactamente las reglas de la fase 2, sin fusión por campos. Sin cambios locales pendientes, se aplica la versión externa entera (combate incluido). Con cambios locales pendientes, gana lo local con aviso y se sobrescribe el fichero. Un conflicto de Syncthing (`.sync-conflict`) se resuelve con el diálogo de conflictos existente, que resume cada versión; el resumen añade si hay combate y en qué ronda. En mesa solo escribe la tablet, así que esto es un caso raro.
- Un `.state.json` en solo lectura (versión futura, inválido) deshabilita también los controles de combate.
- Errores de contenido (datos incoherentes en el fichero) son resultados con aviso en Diagnóstico, no excepciones. Registros sin texto de notas ni de excepciones, como en el resto de la app.

## 8. Verificación y testing

- **Core:** cada operación de §7.2 y §7.4 con sus casos límite (empates, alta sin número, quitar la entrada actual, vuelta de ronda, vencimiento al empezar combate, ancla eliminada, temporales que absorben parte del daño, curar desde 0 HP pone a cero las salvaciones, CD de concentración con daños 1, 21 y 70).
- **Serializador:** lectura de versión 1 sin pérdida; ida y vuelta de versión 2; ficheros incoherentes corregidos con aviso; versión 3 → solo lectura.
- **`MapSession`:** combate guardado y recuperado tras cerrar y reabrir; recarga externa con combate activo.
- **Screens y UI (bUnit):** diálogo de empezar (foco que salta, orden resultante), tira expandida y retraída, cambio de tamaño recordado, aviso de vencimiento y botones Quitar/Mantener, daño/curación/temporales, salvaciones de muerte, terminar combate.
- **Interop/JS:** el estado que llega a Konva incluye atenuado, anillo de turno e indicador de estados.
- **Checklist manual nueva** (DevHost y tablet): apuntar 10 iniciativas rápido, arrastrar en la tablet con el dedo sin mover el mapa, cerrar la app a mitad de combate, sincronizar con el PC.

## 9. Cosmere: qué queda preparado y qué falta

Investigación del 13/11 sobre las *Stormlight Starter Rules* oficiales (texto en la biblioteca de Fantasy Grounds, licencia de Brotherwise: `https://www.fantasygrounds.com/library/cosmererpg/stormlight-starter-rules/`, partes 1, 2, 3 y 7). Es un subconjunto del Handbook.

- **Ronda en cuatro fases:** PJ rápidos (2 acciones) → PNJ rápidos → PJ lentos (3 acciones) → PNJ lentos. Entre PJ el orden lo eligen los jugadores; entre PNJ, el DM. Los jefes tienen turno rápido y lento. → En el modelo: `phase` en cada entrada y varias entradas por token. La tira de Cosmere se agruparía por fase con reordenado manual dentro de cada una; el número de iniciativa no se usaría.
- **Desvío:** reduce el daño de cada golpe (no es una reserva); no se aplica al daño espiritual ni al vital. → `ApplyDamage` del sistema, con tipo de daño.
- **Salud a 0:** los PJ quedan inconscientes y sufren **lesiones** (tirada en la mesa), sin salvaciones de muerte; los esbirros caen con la primera lesión. → `AtZeroHp` del sistema.
- **Duraciones:** «hasta el inicio de esa fase en la ronda siguiente» o «hasta el final de tu siguiente turno». → La duración anclada a una entrada cubre la primera; la segunda («final de turno») requerirá un momento de descuento al terminar turno.
- **Recursos por criatura:** Concentración (Focus), Investidura, reacción disponible, lesiones.
- **Pendiente de preguntar a un DM con el manual:** máximo y recuperación de Investidura, tabla de movimiento por Velocidad, dado de recuperación, lista completa de estados y tipos de daño del Handbook (y nombres en español), atributo de cada habilidad, cómo funcionan en la práctica las duraciones por fase y los esbirros a 0, valores de Desvío de las armaduras.
- La sección §9.5 de la spec global se actualizará con lo confirmado cuando se construya Cosmere; esta fase no la toca.

## 10. Qué decide quien construya

- Si las operaciones de combate viven en un `CombatEditor` aparte o en `MapEditor`, y cómo se reparten los ficheros (límite de ~300 líneas por fichero).
- Generación de ids de entradas y estados (como los de tokens).
- Detalles visuales dentro del design system: iconos, colores del anillo de turno y del atenuado, forma del indicador de estados.
- Cómo se ofrece «Añadir al combate» al crear un token nuevo durante un combate (además del menú de la tira).
- El gesto exacto para el menú de fila (mantener pulsado o botón ⋯), siempre que arrastrar para reordenar no mueva el mapa.
