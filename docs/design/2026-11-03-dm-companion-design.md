# DM Companion — Diseño

- **Fecha:** 2026-11-03
- **Estado:** aprobado (arquitectura global + fase 1 en detalle); §9 «Sistemas de juego» aprobada
- **Alcance de esta spec:** arquitectura de toda la app y diseño detallado de la **fase 1 (Vault)**. Las fases 2–8 tendrán cada una su propia spec y plan.

## 1. Objetivo

App Android para tablet (tablet de referencia: Android 15, 12", con lápiz activo) que ayuda a un DM a dirigir partidas de D&D 5e (2024): consultar historia y notas de campaña, mostrar el mapa con los tokens de PJ/NPC, llevar el combate (iniciativa, HP, estados), consultar un glosario de reglas y monstruos, poner ambiente sonoro y retransmitir a una TV la vista de los jugadores.

Uso personal (y, como mucho, compartida con algún amigo mediante `.apk`). Sin coste: sin tiendas, sin nube, sin cuentas.

## 2. Requisitos que condicionan la arquitectura

| Requisito | Consecuencia |
|---|---|
| El contenido se escribe sobre todo en Obsidian, pero también se edita y crea en la app | El vault Markdown es la **fuente de verdad**; la app lee y escribe los mismos `.md` |
| Sincronización bidireccional PC ↔ tablet | La hace una herramienta externa (**Syncthing-Fork**, o Obsidian Sync); la app no implementa sync |
| Se juega con y sin internet | **Offline-first**: todo es local en la tablet |
| Pantalla del DM + TV solo para jugadores | Dos vistas independientes (no espejo) vía API `Presentation` de Android |
| Mapa con cuadrícula, niebla, varios mapas y dibujo con lápiz | Canvas web (Konva.js) con pointer events |
| Edición 5e 2024 | Glosario base con el **SRD 5.2** (CC-BY-4.0) |

## 3. Arquitectura

**Tecnología:** .NET 10 (LTS) · .NET MAUI + Blazor Hybrid (`net10.0-android`) · Konva.js para el mapa · xUnit + AwesomeAssertions (fork Apache-2.0 de FluentAssertions, cuya v8 tiene licencia comercial).

**Alternativas descartadas:**
- *PWA (React/TS):* Chrome Android no permite abrir una carpeta arbitraria (`showDirectoryPicker`), lo que impide trabajar sobre el vault sincronizado.
- *Flutter / Kotlin:* mejor integración nativa, pero fuera del stack habitual (C#) sin un objetivo de aprendizaje que lo justifique.
- *SkiaSharp para el mapa:* más rendimiento, pero obliga a programar a mano gestos, hit-testing y capas.

### 3.1 Estructura de la solución

```
dm-companion/
├─ src/
│  ├─ DmCompanion.Core/            # .NET puro, sin UI ni Android
│  │   ├─ Vault/                   # entidades, parser Markdown+frontmatter, wikilinks, índice
│  │   ├─ Combat/                  # iniciativa, HP, estados, rondas
│  │   └─ Maps/                    # modelo de mapa, cuadrícula, tokens, niebla
│  ├─ DmCompanion.Infrastructure/  # IVaultStorage sobre System.IO, watcher
│  └─ DmCompanion.App/             # MAUI Blazor Hybrid
│      ├─ Platforms/Android/       # permisos, Presentation (TV), share intent
│      ├─ Components/              # Razor: paneles, mapa, tarjetas
│      └─ wwwroot/js/              # canvas del mapa (Konva.js) + interop
├─ tests/
│  ├─ DmCompanion.Core.Tests/
│  └─ DmCompanion.Infrastructure.Tests/
└─ samples/vault-demo/             # vault de ejemplo (tests y pruebas manuales)
```

**Principios:**
- Toda la lógica vive en `Core` y se testea sin emulador.
- La App solo es UI + puente con la plataforma, mediante interfaces (`IVaultStorage`, `ISecondScreen`, `IAudioPlayer`…) con implementación Android y dobles para tests.
- El interop C# ↔ JS del mapa se limita a «estado del mapa → JS» y «eventos JS → C#».
- La app no tiene base de datos: índice en memoria reconstruido al abrir el vault.

## 4. Modelo de contenido en el vault

Un vault (o carpeta de campaña) por campaña. La app se guía por el campo `type` del frontmatter; las carpetas son convención, no obligación.

```
Campaña Kraken/
├─ Historia/       # type: note | session
├─ Personajes/     # type: pc | npc
│   └─ <PJ>/Diario/   # entradas del diario del PJ (fase 8)
├─ Lugares/        # type: location
├─ Mapas/
│   ├─ Mazmorra del Kraken.md          # type: map (imagen, cuadrícula)
│   ├─ Mazmorra del Kraken.png
│   └─ Mazmorra del Kraken.state.json  # tokens, niebla, dibujos, combate
├─ Glosario/       # type: rule | monster
├─ _assets/        # retratos e imágenes
└─ _audio/         # pistas de ambiente (fase 6)
```

Ejemplo de entidad:

```markdown
---
type: npc
name: Capitana Ysolde
portrait: _assets/ysolde.png
secret: true
stats:
  ac: 15
  hp: 52
  speed: 30
  initiative_bonus: 2
  abilities: { str: 14, dex: 14, con: 12, int: 10, wis: 13, cha: 16 }
---
Lidera a los contrabandistas de [[Puerto Sal]]. Debe un favor a [[Varek]].

%% secreto: es hija del Kraken %%
```

**Reglas:**
- Frontmatter YAML estándar (editable desde *Properties* de Obsidian, sin plugins).
- `type` desconocido o ausente → se trata como `note`.
- Contenido secreto: `secret: true` (nota entera) o bloques `%% … %%` (fragmento). Nunca se muestran en ninguna vista de jugador (TV o previsualización).
- Wikilinks resueltos por nombre de fichero sin extensión, sin distinguir mayúsculas, como Obsidian. Soporta `[[Nota]]`, `[[Nota|alias]]`, `[[Nota#Sección]]`. Si hay varios ficheros con el mismo nombre, gana el de ruta más corta (comportamiento por defecto de Obsidian).
- Estado de mapa/combate en `<mapa>.state.json` junto al mapa: lo genera la app, no se edita a mano.
- Monstruos y reglas del SRD 5.2 van embebidos en la app como datos de solo lectura; «copiar al vault» crea una nota `monster`/`rule` editable que tiene prioridad sobre la del SRD.
- Conflictos de Syncthing (`*.sync-conflict-*`): se detectan y se resuelven a mano; nunca se fusionan automáticamente.
- Cada vault declara su sistema de juego en una nota `type: campaign` (por defecto `dnd5e`; ver §9).

## 5. Interfaz

Tablet en horizontal. **El mapa es la capa base permanente**; todo lo demás se superpone.

```
┌────┬─[🧝 18][👹 15][🧙 12][👹 9][⚔ R2]────────────┐  ← tira de iniciativa
│ 👥 │                                             │
│ 📜 │     MAPA (siempre visible, capa base)       │
│ 📖 │          ┌──────────────┐                   │
│ 🗺 │          │ panel DM     │ ← superpuesto,    │
│ 🎵 │          │              │   se oculta al    │
│ 📺 │          └──────────────┘   re-pulsar       │
└────┴─────────────────────────────────────────────┘
```

- **Menú izquierdo:** cada botón alterna (abre/cierra) su panel superpuesto: 👥 Jugadores (submenú por PJ → ficha completa), 📜 Notas, 📖 Glosario, 🗺 selector de mapa, 🎵 ambiente, 📺 TV.
- **Tokens:** retrato o inicial; anillo de color por bando (PJ / aliado / enemigo). Al tocarlo, **tarjeta rápida** (AC, HP con botones de daño/curación, velocidad, estados). En la tablet hay además «Ver más», que abre la ficha completa en un panel sobre el mapa.
- **Tira de iniciativa (arriba):** tokens ordenados por iniciativa, turno actual resaltado, contador de rondas, botón de siguiente turno. Tocar una ficha abre la misma tarjeta rápida.
- **Vista de la tablet = vista DM:** niebla semitransparente, HP exactos y tokens ocultos visibles. Botón «previsualizar TV» para ver exactamente lo que ven los jugadores.
- **Vista TV = vista jugador:** mapa sin contenido secreto, niebla opaca, HP de enemigos como «Ileso / Herido / Malherido», sin paneles del DM. El botón «📺 Mostrar en TV» de una ficha de PJ, nota, entrada de glosario o diario lleva ese contenido (filtrado) a la TV a pantalla completa; «Volver al mapa» la devuelve. Las fichas de NPC/monstruo no tienen ese botón.

## 6. Fases

Cada fase es usable en mesa por sí sola y tendrá su propia spec + plan.

1. **Vault:** notas, PJ y NPC, enlaces, edición. *(detallada en §7)*
2. **Mapa con tokens:** sistemas de juego (§9: nota de campaña y stats guiadas por datos), imagen, tarjeta rápida, ficha superpuesta, cuadrícula y medición, varios mapas enlazados desde notas. Spec: `2026-11-12-fase-2-mapa-design.md` (los sistemas de juego vienen de la fase 2 anterior, ver §9).
3. **Combate:** tira de iniciativa, HP y estados por token (en `state.json`, no en la ficha: dos goblins de la misma ficha llevan HP distintos).
4. **Niebla de guerra, dibujo con lápiz y previsualización TV.**
5. **Glosario:** SRD 5.2 + notas propias; añadir monstruo al mapa como token.
6. **Ambiente sonoro:** varias pistas simultáneas (volumen, bucle, fundido), escenas guardadas, `ambience:` en notas de lugar. Ficheros locales en `_audio/`; la app no incluye música.
7. **Segunda pantalla (TV):** vista jugador con API `Presentation` + «Mostrar en TV».
8. **Diario de jugadores:** entradas con imagen y texto por PJ (bestiario de lo que saben los jugadores), recepción de imágenes desde el menú «Compartir» de Android.

## 7. Fase 1 — Vault (detalle)

### 7.1 Alcance

- Elegir la carpeta del vault la primera vez; se recuerda entre sesiones (`Preferences`).
- Indexar: `type`, nombre, frontmatter, enlaces salientes y backlinks.
- **Panel 📜 Notas:** árbol de carpetas, búsqueda por nombre y texto, vista de lectura con wikilinks navegables y backlinks.
- **Panel 👥 Jugadores:** lista de PJ (`type: pc`) con retrato o inicial; ficha completa (stats del frontmatter + cuerpo renderizado).
- **Edición:** Markdown en crudo con vista previa; crear una nota nueva desde una plantilla por `type` (pc, npc, location, note).
- **Layout de capas:** el mapa es un fondo de marcador de posición; los paneles ya se superponen y se alternan como en el diseño final.
- **Fuera de alcance en la fase 1:** editor WYSIWYG, mapas reales, combate, vista TV, audio.

### 7.2 Acceso a la carpeta

- **Decisión:** permiso *All files access* (`MANAGE_EXTERNAL_STORAGE`) y uso de `System.IO` + `FileSystemWatcher` sobre rutas normales (p. ej. `/storage/emulated/0/Sync/Campaña Kraken`).
- **Motivo:** código simple y testeable con rutas reales. El permiso lo restringe Google Play, pero la app no se publica allí.
- **Descartado:** Storage Access Framework (URIs `content://`, sin `File.*`, watcher poco fiable, código acoplado a Android).
- Todo el acceso pasa por `IVaultStorage`: migrar a SAF en el futuro solo afectaría a Infrastructure.

### 7.3 Componentes de Core

- `FrontmatterParser`: separa el bloque YAML del cuerpo y lo deserializa (YamlDotNet). YAML inválido → resultado con error, no excepción.
- `WikilinkExtractor`: extrae `[[destino|alias#sección]]` del cuerpo, ignorando los que estén dentro de bloques de código.
- `SecretFilter`: devuelve el cuerpo sin bloques `%% … %%`; indica si una entidad es secreta.
- `VaultEntity`: ruta relativa, nombre, `type`, frontmatter, cuerpo, enlaces salientes, error de parseo opcional.
- `VaultIndex`: construcción a partir de entidades; resolución de enlaces (§4); backlinks; búsqueda; actualización incremental de una entidad.
- `NoteTemplates`: plantilla de frontmatter + cuerpo por `type`.

### 7.4 Flujo de datos

1. Al abrir: escaneo recursivo de `*.md` (ignorando `.obsidian/`, `.stfolder/`, `.trash/`) → parseo en paralelo → `VaultIndex`.
2. `FileSystemWatcher` → reindexa solo el fichero cambiado/creado/borrado → la UI se refresca.
3. Guardado: escritura en `<fichero>.tmp` y `File.Move` con sobrescritura, para no dejar ficheros a medias si Syncthing lee a la vez.

### 7.5 Gestión de errores

| Situación | Comportamiento |
|---|---|
| Frontmatter YAML inválido | La nota se muestra como texto con aviso ⚠; el índice sigue funcionando |
| Wikilink roto | Se pinta distinto y ofrece «crear nota» |
| Fichero `*.sync-conflict-*` | Aviso; vista de las dos versiones lado a lado; el usuario elige cuál queda |
| La nota cambió en disco durante la edición | Al guardar se compara la fecha de modificación; se pregunta si sobrescribir o recargar |
| Carpeta inaccesible (permiso revocado, ruta movida) | Pantalla para volver a elegir la carpeta |

### 7.6 Testing

- **`Core.Tests` (xUnit + AwesomeAssertions, con TDD):** frontmatter (válido, inválido, ausente), wikilinks (alias, sección, dentro de código), `SecretFilter`, resolución de enlaces con duplicados, backlinks, búsqueda, actualización incremental del índice.
- **`Infrastructure.Tests`:** contra una copia de `samples/vault-demo/` en una carpeta temporal: escaneo con exclusiones, escritura atómica, eventos del watcher, detección de conflictos y de cambios concurrentes.
- **App:** checklist manual en el emulador y en la tablet de referencia. Sin tests de UI automatizados en la v1 (coste alto en MAUI Android frente a lo que aportan).
- **`samples/vault-demo/`:** campaña mínima con 2 PJ, 2 NPC (uno secreto), 2 lugares, enlaces cruzados, un enlace roto y un YAML inválido.

## 8. Distribución

- Compilación de `.apk` e instalación por `adb install` o copiando el fichero. Sin Google Play.
- Pendiente de revisar al compartirla con amigos: la verificación de desarrolladores de Android para apps instaladas fuera de Play (la instalación por `adb` queda exenta).
- Repositorio solo local por ahora (sin remoto).

## 9. Sistemas de juego

La app nació para D&D 5e (2024), pero cada vault puede llevar una campaña de **otro sistema** (p. ej. Cosmere RPG). Un vault, un sistema.

### 9.1 Declaración en el vault

- Una **nota de campaña** con `type: campaign` declara el sistema en el frontmatter:

  ```markdown
  ---
  type: campaign
  system: dnd5e      # o cosmere
  ---
  ```

- Sin nota de campaña, o sin `system`, el sistema es **`dnd5e`**: los vaults existentes no cambian.
- Si hay varias notas `type: campaign`, gana la de **ruta más corta** (menos segmentos) y, a igual profundidad, la primera en **orden ordinal** de la ruta relativa (`StringComparer.Ordinal`, independiente de la cultura), para que el resultado sea el mismo en la tablet y en el PC. Las descartadas se listan como aviso en Diagnóstico.
- Un `system` desconocido no rompe nada: aviso en Diagnóstico, y la ficha cae a una **vista genérica** clave/valor del mapa `stats` del frontmatter.
- `NewNoteDialog` y `NoteTemplates` tienen la plantilla **«Campaña»** (`type: campaign`, `system: dnd5e`).

### 9.2 Definición de stats por sistema (guiada por datos)

- Cada sistema incluido en la app (`dnd5e`, `cosmere`) tiene una **`SystemDefinition`** en código: un record inmutable con la lista de stats. Cada stat lleva:
  - clave de frontmatter (ruta, p. ej. `stats.defenses.physical`);
  - etiqueta (en la definición, nunca en los componentes);
  - tipo: número, número con signo, puntuación con modificador, recurso actual/máximo, texto o **derivada**;
  - grupo de la ficha y `ShowInQuickCard`.
- Las **derivadas** se calculan con reglas en código del sistema (atributo base + reglas). Si el frontmatter trae un valor explícito, **manda el del frontmatter**, y la ficha lo marca como «manual».
- La ficha (`CharacterSheet`) y la tarjeta rápida (`QuickCard`) se pintan a partir de la definición; ya no tienen campos de D&D fijos.
- Las plantillas de PJ y PNJ generan el `stats` vacío a partir de la definición del sistema activo (en `unknown`, `stats: {}`).
- **Etiquetas:** todo en español, salvo las siglas **AC** y **HP** de D&D. No se añaden más siglas en inglés salvo que el autor lo pida.
- **Evolución futura (no ahora):** overrides desde el vault (renombrar etiquetas, añadir stats a una definición incluida). El modelo inmutable lo permite sin rehacer nada.

### 9.3 Reglas por sistema (`IGameSystem`)

```csharp
public interface IGameSystem
{
    string Id { get; }                       // "dnd5e", "cosmere"
    string DisplayName { get; }
    SystemDefinition Definition { get; }
    StatValues Evaluate(IReadOnlyDictionary<string, object?> frontmatter); // leídos + derivados
    GridRules Grid { get; }                  // unidad por casilla y regla de diagonales (fase 2)
    // Fase 3 (solo descrito aquí): TurnModel (Numeric | FastSlow), DamageTarget y recursos por token.
}
```

- `GameSystemRegistry` resuelve el id; un id desconocido da el sistema genérico (vista clave/valor, sin derivadas y con cuadrícula de 5 pies y diagonal = 1).
- **Fase 3**: D&D usa iniciativa numérica y el daño baja HP. Cosmere usa **turnos rápidos y lentos**, y el daño baja Salud tras el Desvío, además de Concentración e Investidura como recursos. El modelo de combate (fase 3) se diseña contra `IGameSystem`, no contra D&D.

### 9.4 D&D 5e (2024): `dnd5e`

- Básicas: AC, HP, Velocidad, Iniciativa (`initiative_bonus`), características FUE/DES/CON/INT/SAB/CAR (`abilities.str` … `abilities.cha`) con su modificador. Son las claves actuales, sin cambios.
- `level` (PJ) y `cr` (PNJ/monstruo); `proficiency_bonus` opcional.
- **Bonificador de competencia (BC):** `proficiency_bonus` si viene; si no, a partir de `level` (2 + ⌊(nivel − 1)/4⌋); si no, a partir de `cr` (tabla del SRD 5.2: VD 0–4 → +2, 5–8 → +3, 9–12 → +4, 13–16 → +5, 17–20 → +6, 21–24 → +7, 25–28 → +8, 29–30 → +9). Sin ninguno de los tres, las tiradas competentes muestran «—» con el motivo, y las no competentes, solo el modificador.
- **Salvaciones** (`saves.<car>`) y **habilidades** (`skills.<clave>`), cada una con valor `none | proficient | expertise`:
  - salvación = mod. + BC si es competente;
  - habilidad = mod. de su característica + BC (×2 con pericia);
  - **Percepción pasiva** = 10 + Percepción (`ShowInQuickCard`).
  - Un valor desconocido cuenta como `none` y genera un aviso en Diagnóstico (nunca una excepción).
- Las 18 habilidades (clave → característica → etiqueta): acrobatics→DES Acrobacias · animal_handling→SAB Trato con animales · arcana→INT Arcanos · athletics→FUE Atletismo · deception→CAR Engaño · history→INT Historia · insight→SAB Perspicacia · intimidation→CAR Intimidación · investigation→INT Investigación · medicine→SAB Medicina · nature→INT Naturaleza · perception→SAB Percepción · performance→CAR Interpretación · persuasion→CAR Persuasión · religion→INT Religión · sleight_of_hand→DES Juego de manos · stealth→DES Sigilo · survival→SAB Supervivencia. *(Etiquetas por contrastar con la edición en español del PHB 2024.)*
- Ficha expandida: grupo **«Salvaciones»** (6) y grupo **«Habilidades»** en orden alfabético en español, con la sigla de la característica al lado («Sigilo · DES +5»).
- Plantillas: las claves de hoy (ac, hp, speed, initiative_bonus, abilities), **idénticas**, más `level`, las 6 `saves` y las 18 `skills` a `none`. Un test garantiza que lo de hoy no cambia y que lo nuevo solo se añade.
- Cuadrícula: casilla de **5 pies**; cada diagonal cuenta como **1 casilla** (regla de cuadrícula de 2024; la alternancia 5/10 es una variante opcional). *(Por contrastar con el SRD 5.2.)*

### 9.5 Cosmere RPG: `cosmere` (PROVISIONAL)

Todo este apartado está **pendiente de contrastar con el manual del autor**; la fase 2 no se cierra sin hacerlo (checklist de cierre).

- Atributos: Fuerza, Velocidad, Intelecto, Voluntad, Percepción y Presencia (`attributes.str|spd|int|wil|awa|pre`).
- Defensas (derivadas): Física = 10 + Fuerza + Velocidad; Cognitiva = 10 + Intelecto + Voluntad; Espiritual = 10 + Percepción + Presencia.
- Recursos actual/máximo: Salud, Concentración e Investidura. También Desvío y movimiento.
- Habilidades con rango 0–5; valor = atributo + rango. Lista tentativa: Agilidad, Atletismo, Armas pesadas, Armas ligeras, Sigilo, Latrocinio, Artesanía, Deducción, Disciplina, Intimidación, Saber, Medicina, Engaño, Perspicacia, Liderazgo, Percepción, Persuasión y Supervivencia (nombres y atributo de cada una por confirmar).
- Sin equivalente confirmado a la Percepción pasiva: la QuickCard no muestra derivadas hasta comprobarlo.
- Cuadrícula: unidad y regla de diagonales por confirmar. Mientras tanto, 5 pies y diagonal = 1.
