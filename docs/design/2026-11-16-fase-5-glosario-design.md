# DM Companion — Fase 5: glosario (SRD 5.2.1 en español)

- **Estado:** borrador para revisión.
- **Orden de fases:** 6 → 7 → 4 → **5** → 8.
- **Relacionado:**
  - Diseño global `2026-11-03-dm-companion-design.md`: §4 modelo de contenido, §5 interfaz, §6 fase 5 y §9 sistemas de juego.
  - Fase 2 `2026-11-12-fase-2-mapa-design.md`: tokens, tarjeta rápida, ficha.
  - Fase 3: estados de los tokens.
  - Fase 7 `2026-11-15-fase-7-segunda-pantalla-design.md`: «Mostrar en TV».
- **Decisiones (16/11):**
  - Todo el SRD dentro de la app, sin conexión, en español oficial.
  - Datos estructurados por categoría (enfoque B).
  - Un token de criatura del SRD apunta directamente al SRD.
  - Búsqueda en español e inglés.
  - Incluir los wikilinks al SRD y abrir la regla al tocar un estado.
  - Cosmere queda para más adelante.

## 1. Intención

En la mesa, el autor quiere consultar cualquier regla, estado, conjuro, objeto o criatura del SRD sin conexión y en español. También quiere poner una criatura del SRD en el mapa como token sin tener que escribirse antes una nota. Sus propias notas `rule` y `monster` del vault conviven con el SRD y mandan sobre él.

## 2. Objetivos

- **Todo el SRD 5.2.1 en español** embebido en la app, de solo lectura, sin conexión:
  - reglas y glosario de reglas;
  - estados;
  - criaturas;
  - conjuros;
  - objetos mágicos;
  - equipo;
  - clases y subclases;
  - especies;
  - trasfondos;
  - dotes;
  - venenos y trampas.
- **Panel Glosario** (botón 📖): búsqueda por nombre en español o en inglés, categorías, filtros sencillos y detalle por categoría.
- **Las notas del vault mandan.** Una nota `rule` o `monster` con el mismo nombre tapa la entrada del SRD, y el original sigue accesible. «Copiar al vault» crea esa nota.
- **Añadir al mapa** una criatura del SRD. El token apunta al SRD, y su tarjeta rápida y su ficha salen de los stats del SRD.
- **Wikilinks** de las notas que no encuentran nota en el vault caen en el glosario: `[[Bola de fuego]]`, `[[Cegado]]`.
- **Estados de los tokens:** tocar el chip abre su regla.
- **Mostrar en TV** cualquier entrada que no sea una criatura.
- Funciona en Android, en la versión de PC y en el DevHost.

## 3. No objetivos

- **Glosario de Cosmere.** El Cosmere RPG no tiene SRD abierto, y el de Plotweaver está anunciado para el segundo trimestre de 2027 y sin la ambientación Cosmere.
  - El texto del manual del autor **nunca** va en el APK ni en el repo.
  - Plan para más adelante, fuera de esta fase: un importador local que lea el PDF del autor y escriba un paquete de glosario en su vault.
  - En un vault Cosmere, el glosario de esta fase muestra solo las notas `rule` y `monster` del vault.
- **Visor de manuales en PDF (fase 5b, decisión del 16/11).** Va justo después de esta fase, con su propia spec y su propio plan. Abre los PDF de manuales que haya en el vault, navega por las secciones del índice de marcadores del PDF, permite filtrarlas y lleva a la página X. Irá en una pestaña «Manuales» dentro del mismo botón 📖. Así cada persona puede usar sus propios manuales (Cosmere incluido) sin que la app distribuya nada.
- Otros idiomas del SRD. Solo se guarda el nombre en inglés, para buscar.
- Filtros avanzados (daño, entorno, componentes con coste) y constructor de encuentros.
- Editar entradas del SRD dentro de la app. Para eso está «Copiar al vault».
- Descargar contenido en tiempo de ejecución (APIs como Open5e).

## 4. Fuente de datos e importador

### 4.1 Fuente

- Repositorio **`Ismaelbase/compendio-srd`**, carpeta `datos/`.
  - Licencias: código MIT, texto del SRD CC-BY 4.0. La atribución viene en `datos/atribucion.json`.
  - Es el SRD 5.2.1 oficial publicado por Wizards en español (08/12/2025), en JSON estructurado.
  - Trae cinco idiomas y correcciones documentadas frente al PDF.
- **Commit fijado:** `2d61b5dcabe91ab69d5c4a5a41822eb83c2038cc` (13/10/2026). Va anotado en cada paquete generado.
- Ficheros que se usan, con lo que contienen:

  | Fichero | Contenido |
  |---|---|
  | `criaturas.json` | 330 entradas |
  | `conjuros.json` | 339 |
  | `estados.json` | 15 |
  | `reglas.json` | 215: 141 de glosario, 42 reglas, 14 venenos, 8 trampas… |
  | `objetos.json` | 258 objetos mágicos |
  | `equipo.json` | 157 |
  | `personajes.json` | 54: 12 clases, 12 subclases, 9 especies, 4 trasfondos y 17 dotes |

- La fuente **no se copia al repo**. El importador la lee de una carpeta local que se le pasa como argumento.
- **Por comprobar en el plan:** si los `datos/*.json` ya aplican los ficheros `correcciones_*.json` del repo o hay que aplicarlos en el importador.

### 4.2 Forma de la fuente (lo que hay que mapear)

- Cada entrada trae `id` (clave inglesa estable), a veces `comun` (datos numéricos o enumerados) y un objeto por idioma (`es`, `en`…) con:
  - `nombre`;
  - `p` (página del PDF de ese idioma);
  - `bloques`, o `secciones` en las criaturas;
  - campos propios de la categoría.
- **Tipos de bloque:** `p`, `h`, `li`, `tabla`, `campos`, `recuadro`, `formula`, `ficha`, `columnas` y `nota`.
- **HTML dentro del texto:** solo `<b>`, `<i>` y `<strong>`.
- **Criaturas:**
  - `comun`: `ca`, `iniciativa`, `pg`, `dados_golpe`, `car[6]`, `mod[6]`, `salv[6]`, `vd`, `px`, `bc`, `percepcion_pasiva`, `tamano[]`, `tipo`…
  - `es.campos`: textos ya formateados (`vel`, `hab`, `sen`, `idi`…).
  - `es.secciones`, con `clave`: `rasgos`, `acciones`, `legendarias`…
- **Conjuros:**
  - `comun`: `nivel`, `escuela`, `clases[]`, `ritual`, `concentracion`, `componentes[]`.
  - `es`: `cabecera`, `tiempo`, `alcance`, `comp`, `dur`.
- **Objetos:**
  - `comun`: `categoria`, `rarezas[]`, `sintonizacion`.
  - `es`: `cabecera`.
- **Estados:** sus `id` coinciden con las claves de estado de `Dnd5eCombatRules` (`blinded`, `prone`…). Sirven para enlazar los chips de los tokens.

### 4.3 Importador `tools/DmCompanion.SrdImport`

- Proyecto de consola .NET de **solo desarrollo**. No lo referencia ningún host y no entra en el APK.
- Uso: `dotnet run --project tools/DmCompanion.SrdImport -- <carpeta datos> <carpeta salida>`.
- Para cada categoría escribe un paquete JSON propio en `src/DmCompanion.Core/Glossary/Packs/`:
  - solo con español, más el nombre en inglés;
  - texto saneado: se conservan negrita y cursiva como marcas del modelo de bloques, no como HTML;
  - con una cabecera `{ "source": "compendio-srd", "commit": "…", "srd": "5.2.1", "lang": "es" }`.
- Si se vuelve a ejecutar con el mismo commit, el resultado es idéntico byte a byte: orden estable y sin fechas. Así un diff de git solo muestra cambios reales.
- Las rarezas, escuelas, tipos y tamaños vienen en inglés en `comun`. El importador los conserva como **claves** y la app los muestra con etiquetas en español, igual que los estados.
  - Las etiquetas salen del propio texto español de la fuente cuando existe, como `cabecera` o `tipo`.
  - Si no, de una tabla en código.

### 4.4 Paquetes en la app

- **Recursos embebidos** en `DmCompanion.Core`, uno por categoría.
- Tamaño estimado: 5–8 MB sin comprimir. Se mide el APK antes y después; el objetivo es que crezca menos de 10 MB.
- **Carga perezosa:** una categoría se lee la primera vez que se busca o se abre algo de ella.
- La lista de nombres de todas las categorías se carga junta para la búsqueda global, sin leer los textos.
- **Atribución CC-BY 4.0:**
  - texto oficial de `atribucion.json` (es) al pie del glosario y en una sección «Acerca de» nueva en Diagnóstico (`/diag`), la única pantalla que hoy enseña la versión;
  - un `NOTICE.md` en la raíz del repo.

## 5. Modelo (Core, `Core/Glossary/`)

- `GlossaryCategory`: `Rule`, `Condition`, `Creature`, `Spell`, `MagicItem`, `Equipment`, `CharacterOption`.
  - `CharacterOption` lleva un subtipo: clase, subclase, especie, trasfondo o dote.
  - `Rule` lleva un subtipo: regla, término del glosario, veneno o trampa.
- `GlossaryEntry` (record), solo para entradas del SRD; las notas del vault se listan con `GlossaryListing.VaultPath` y se leen con el lector de notas. Campos comunes:
  - `Key`, por ejemplo `srd:criaturas/aboleth`;
  - `Category` y `Subtype?`;
  - `Name`, `NameEn?`, `Page?`;
  - `Blocks`;
  - `Detail?`.
- **Detalles tipados:**
  - `CreatureStats`:
    - CA, PG, dados de golpe;
    - iniciativa (modificador y puntuación), características (6), modificadores (6), salvaciones (6);
    - VD (texto: «1/4»), PX, BC, percepción pasiva;
    - tamaños, tipo y alineamiento;
    - textos de velocidad, habilidades, sentidos e idiomas;
    - secciones (rasgos, acciones, acciones adicionales, reacciones, legendarias…).
  - `SpellInfo`: nivel, escuela, clases, ritual, concentración, componentes, más los textos de cabecera, tiempo, alcance, componentes y duración.
  - `ItemInfo`: categoría, rarezas, sintonización (y por quién) y cabecera.
  - El resto solo lleva bloques.
- **`GlossaryBlock`:** párrafo, encabezado, elemento de lista, tabla (con filas, título y si es vertical), campos (pares etiqueta/valor), recuadro, fórmula y nota. El texto va en tramos con negrita y cursiva. **No se guarda HTML**, así que no hay nada que inyectar en el WebView.
- **`CreatureStats` → stats `dnd5e`.** Un adaptador devuelve el mismo diccionario de frontmatter que una nota `npc`, con todo bajo `stats:` y los escalares como texto, igual que los deja YamlDotNet:
  - `ac`, `hp`, `speed`, `initiative_bonus`, `abilities.*`, `cr`, `proficiency_bonus`;
  - las salvaciones donde difieran del modificador.

  Así `IGameSystem.Evaluate`, la tarjeta rápida y la ficha funcionan sin cambios.
- **«Copiar al vault»** usa ese mismo adaptador para escribir el frontmatter de la nota `monster`.

### 5.1 `GlossaryIndex`

- Se construye por vault abierto, a partir del sistema de la campaña (§9 global):
  - `dnd5e` → paquetes del SRD + notas del vault;
  - cualquier otro sistema → solo notas del vault.
- `IGameSystem` gana `IReadOnlyList<GlossaryCategory> GlossaryPacks`. En `dnd5e` lleva todas las categorías; en `cosmere` y en el genérico, ninguna.
- **Entradas del vault:**
  - las notas `type: rule` pasan a `Rule` y las `type: monster` a `Creature`;
  - sus stats salen del frontmatter, como hoy;
  - se actualizan con el watcher del vault, como el resto del índice.
- **Prioridad:**
  - si una nota del vault y una entrada del SRD de la misma categoría tienen el mismo nombre (sin distinguir tildes ni mayúsculas), la lista muestra la del vault con la marca «Tuya»;
  - la del SRD queda accesible desde el detalle («Ver original del SRD»);
  - se compara con el nombre en español y también con el nombre en inglés.
  - si el frontmatter de la nota trae `source` con una clave SRD válida (lo escribe «Copiar al vault», p. ej. `srd:conjuros/fireball`), la nota tapa esa clave, sea cual sea su categoría; sin `source`, o con una clave que no existe, vale el emparejamiento por nombre dentro de la misma categoría.
- **Búsqueda:**
  - por subcadena del nombre normalizado (sin tildes ni mayúsculas, `ñ` incluida) en español y en inglés;
  - orden: coincidencia exacta, luego al principio del nombre, luego en cualquier parte; a igualdad, alfabético en español;
  - como mucho 200 resultados.
- **Filtros:**
  - criaturas: VD desde/hasta y tipo;
  - conjuros: nivel, clase, ritual y concentración;
  - objetos mágicos: rareza;
  - el resto, sin filtros.
- `TryResolve(string name)`: lo usa el resolvedor de wikilinks. Busca un nombre exacto normalizado en español o inglés, en este orden:
  1. estados;
  2. reglas;
  3. conjuros;
  4. objetos mágicos;
  5. equipo;
  6. opciones de personaje;
  7. criaturas.

  Si hay empate dentro de una categoría, gana la primera por `Key` en orden ordinal.

## 6. Interfaz (Screens)

### 6.1 Panel Glosario

- El botón 📖 del menú izquierdo abre y cierra un panel superpuesto, como Notas.
- **Cabecera:**
  - caja de búsqueda;
  - chips de categoría: Todo · Reglas · Estados · Criaturas · Conjuros · Objetos mágicos · Equipo · Personajes;
  - fila de filtros de la categoría elegida.
- **Lista:**
  - nombre, más una línea de contexto según la categoría:
    - «VD 10 · Aberración Grande»;
    - «Nivel 3 · Evocación · Mago, Hechicero»;
    - «Infrecuente · Armadura»;
    - «Dote»…
  - la marca «Tuya» en las entradas del vault;
  - sin búsqueda, la categoría elegida en orden alfabético.
- **Detalle**, con «Atrás» a la lista:
  - **Criatura:** bloque de stats.
    - Cabecera: tamaño, tipo y alineamiento.
    - CA, PG, velocidad e iniciativa.
    - Tabla de características con modificador y salvación.
    - Habilidades, sentidos, idiomas y VD.
    - Secciones de rasgos, acciones, etc.
  - **Conjuro:** cabecera, tiempo, alcance, componentes, duración y texto.
  - **Objeto:** cabecera y texto.
  - **Resto:** bloques.
  - Las tablas se desplazan en horizontal dentro del panel si no caben.
  - Al pie: «SRD 5.2.1, p. N» y la atribución.
  - Las notas del vault se muestran con el lector de notas que ya existe (`NoteReader`).
- **Acciones del detalle:**
  - **Añadir al mapa:** solo criaturas y con un mapa abierto en modo editable. Abre el diálogo de token que ya existe, relleno con nombre, CA, PG, bando «enemigo» y tamaño (Diminuto/Pequeño/Mediano = 1, Grande = 2, Enorme y Gargantuesco = 3, porque el mapa admite tokens de 1 a 3 casillas y ampliarlo cambiaría el `.state.json`). El token se crea con `Source = Key`.
  - **Copiar al vault:** crea `Glosario/<Nombre>.md`.
    - Una criatura pasa a `type: monster`, con los stats en el frontmatter (§5) y el texto en el cuerpo, en Markdown. Lo demás pasa a `type: rule`, con el texto en Markdown.
    - Si ya existe una nota con ese nombre, no se sobrescribe: se ofrece abrirla.
    - Hecho esto, la entrada aparece como «Tuya».
  - **Mostrar en TV:** todas las categorías menos criaturas, con la TV activa (fase 7).
  - **Ver original del SRD:** solo cuando una nota del vault tapa una entrada del SRD.

### 6.2 Tokens del SRD

- `MapToken.Source` (texto) admite dos formas: una ruta del vault (hoy) o una `Key` con prefijo `srd:`.
- **Un único resolvedor**, `TokenSourceResolver`, devuelve nota del vault, entrada del SRD o «no encontrada». Lo usan:
  - la tarjeta del token;
  - la ficha («Ver más»);
  - la proyección de la TV (retrato e inicial).
- Una criatura del SRD **no tiene retrato**: se pinta la inicial, como un token sin retrato. «Ver más» abre la ficha con sus stats y su bloque de texto.
- Copiar al vault una criatura que ya tiene tokens en mapas **no cambia esos tokens**: siguen apuntando al SRD. Así no se reescriben mapas a escondidas.
- **Compatibilidad:**
  - no cambia la versión del `.state.json`;
  - una app anterior ve el token del SRD como «nota no encontrada», pero conserva nombre, CA y PG, que ya se guardan en el token.

### 6.3 Wikilinks

- El resolvedor de `[[…]]` del vault, si no encuentra una nota, prueba `GlossaryIndex.TryResolve`. Admite alias (`[[Cegado|ciego]]`). Un `#Sección` se ignora en las entradas del SRD.
- En la vista DM, el enlace abre la entrada en el panel Glosario. Su estilo es distinto del de un enlace a una nota y del de un enlace roto.
- En la TV y en la vista de jugadores:
  - un enlace al SRD que no sea una criatura se pinta como texto, sin enlace (la TV no navega);
  - un enlace a una criatura se pinta como texto, igual que hoy un enlace a un PNJ.

### 6.4 Estados

- En la tarjeta del token y en la tira de combate, tocar el **nombre** del chip de un estado con `Key` abre su entrada en el panel Glosario.
  - Hoy el nombre (`ConditionTag`) no hace nada al tocarlo. Quitar (✕) y editar (lápiz) son botones aparte y no cambian.
  - `ConditionTag` (design system) gana un `OnClick` opcional. Con él, el nombre se pinta como botón con su `aria-label` («Ver regla: Cegado»).
- Los estados personalizados (sin `Key`) y «concentración» (no es un estado del SRD) no llevan ese botón.

### 6.5 TV

- «Mostrar en TV» de una entrada del glosario manda a la TV su título y sus bloques, con el mismo marco y la misma tipografía que una nota de la fase 7.
- No hay contenido secreto que filtrar. Las criaturas no tienen el botón.

## 7. Errores

- Un paquete corrupto o ausente no rompe nada:
  - la categoría queda vacía;
  - se avisa en Diagnóstico («El paquete de conjuros no se pudo leer»);
  - el log lleva solo el tipo de la excepción.
- Una nota `rule` o `monster` con frontmatter inválido se trata como hoy (resultado, no excepción) y sigue apareciendo en el glosario con lo que se pueda leer.
- Un `Source` con `srd:` que no existe (por ejemplo, porque cambió la fuente) se trata como «no encontrada»: el token conserva su nombre, CA y PG.
- «Copiar al vault» nunca sobrescribe. Si falla la escritura, se avisa y el vault queda como estaba.

## 8. Tests

- **Importador**, sobre un extracto pequeño de la fuente en `tests/…/Fixtures`, con un aboleth, una bola de fuego, un estado, un objeto, una tabla y una dote:
  - mapeo por categoría;
  - saneado de `<b>`, `<i>` y `<strong>`;
  - solo español más el nombre en inglés;
  - salida estable byte a byte.
- **Core:**
  - carga de cada paquete embebido con su recuento (330 criaturas, 339 conjuros, 15 estados, 258 objetos…);
  - búsqueda: tildes, `ñ`, inglés y orden;
  - prioridad del vault;
  - un vault `cosmere` sin el SRD;
  - el adaptador de criatura a stats `dnd5e` (el aboleth da CA 17, PG 150 e iniciativa +7);
  - `TokenSourceResolver`;
  - `TryResolve` con el orden de categorías;
  - el paquete corrupto como resultado.
- **Infrastructure:** «Copiar al vault» sobre una copia temporal: crea, no sobrescribe y el frontmatter se puede volver a leer como `monster`.
- **bUnit:**
  - panel: búsqueda, chips y filtros;
  - detalle de criatura y de conjuro;
  - «Añadir al mapa» con el diálogo relleno;
  - «Copiar al vault»;
  - «Ver original del SRD»;
  - wikilink al SRD;
  - chip de estado;
  - «Mostrar en TV» ausente en las criaturas.
- **APK:** compila sin avisos nuevos. Se apunta el tamaño antes y después.

## 9. Checklist de la tablet

- Buscar «bola de fuego» y «fireball»: las dos dan Bola de fuego.
- Filtrar criaturas por VD de 1 a 3.
- Abrir el aboleth y leer su bloque de stats sin que se corte.
- Añadir un goblin del SRD al mapa y combatir con él. Comprobar que dos goblins llevan PG distintos.
- Copiar un monstruo al vault, editarlo en Obsidian y ver que manda el tuyo y que «Ver original» funciona.
- Un `[[Derribado]]` en una nota abre la regla.
- Tocar el estado de un token abre su regla.
- Mostrar un conjuro en la TV.
- Abrir un vault Cosmere: el glosario solo muestra las notas.
- Funciona en modo avión.
