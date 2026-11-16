# DM Companion — Fase 8: diario de jugadores

- **Estado:** borrador para revisión.
- **Relacionado:**
  - Diseño global §4 (`Personajes/<PJ>/Diario/`) y §6.8.
  - Fase 5 (glosario: claves `srd:` y notas `monster`).
  - Fase 7 (contenido en la TV).
- **Decisiones (16/11):**
  - El diario es una **bitácora artesanal**: los jugadores le pasan al DM imágenes de dibujos o de entradas escritas a mano y el DM las guarda.
  - **Un diario por personaje.** La bitácora común es una **nota generada** en el vault, con todas las entradas seguidas y ordenadas por fecha de adición, que se regenera a mano.
  - Las imágenes llegan desde el menú **«Compartir» de Android** y desde **«Añadir imagen»** (selector de ficheros en el PC y galería en la tablet).
  - Una entrada puede llevar **varias imágenes**.
  - Una entrada se puede **enlazar a una criatura** del glosario, y la ficha de la criatura muestra «Lo que saben los jugadores».
  - **TV:** una entrada en pantalla completa y la bitácora entera como pase de diapositivas.
  - **Almacenamiento:** una nota por entrada (opción A).

## 1. Intención

En la mesa, un jugador dibuja al monstruo que acaban de ver o escribe a mano la crónica de la sesión, le hace una foto y se la manda al DM. Con dos toques, el DM la guarda en el diario de ese personaje. Al final de la campaña, la bitácora común reúne todo en orden, y en la TV se puede repasar lo vivido.

## 2. Objetivos

- Crear, editar y borrar entradas del diario de cada PJ, con una o más imágenes, título y nota opcionales y una criatura enlazada opcional.
- Recibir una o varias imágenes con «Compartir» de Android y guardarlas como una entrada.
- «Añadir imagen» con el selector de cada plataforma.
- Ver el diario de un PJ como una lista de entradas con miniaturas.
- Generar `Diario de la campaña.md` con todas las entradas ordenadas por fecha de adición.
- En la ficha de una criatura del glosario, la sección «Lo que saben los jugadores».
- TV: una entrada (imagen a imagen) y la bitácora completa como pase de diapositivas controlado desde la tablet.
- Funcionar en Android, en la versión de PC (sin «Compartir») y en el DevHost.

## 3. No objetivos

- Que los jugadores usen la app o escriban directamente en ella.
- Editar las imágenes: recortar, girar o filtrar.
- OCR del texto manuscrito.
- Un bestiario aparte del diario.
- Exportar la bitácora a PDF. Obsidian ya lo hace con la nota generada.
- Reducir el tamaño de las imágenes. Se guardan tal cual llegan.

## 4. Modelo en el vault

### 4.1 Entrada

- **Ruta:** `Personajes/<PJ>/Diario/<AAAA-MM-DD> <título>.md`.
  - `<PJ>` es la carpeta de la nota del PJ (`type: pc`). Si la nota del PJ no está en su propia carpeta, el diario va en `<carpeta de la nota>/<nombre de la nota>/Diario/`.
  - Si no hay título, se usa «Entrada».
  - Si el nombre ya existe, se añade « 2», « 3»…
  - Los caracteres no válidos en nombres de fichero se sustituyen por «-».
- **Frontmatter:**

```yaml
---
type: journal
added: 2026-11-16T21:34:00+01:00   # fecha y hora en que se añadió; ordena la bitácora
images:
  - "2026-11-16 Ankheg-1.jpg"
  - "2026-11-16 Ankheg-2.jpg"
creature: "srd:monstruos/ankheg"   # opcional: clave del glosario o [[nota monster]]
---
Nota opcional del DM, en Markdown.
```

- El **título** es el nombre del fichero sin la fecha. El **dueño** de la entrada es el PJ de la carpeta.
- **`images`:** los nombres de las imágenes, relativos a la carpeta `Diario/`, en orden.
- **Imágenes:** se copian junto a la nota como `<nombre de la nota>-<n>.<ext>`.
  - Se conserva la extensión original.
  - Se aceptan `jpg`, `jpeg`, `png`, `webp`, `gif` y `heic`.
  - `heic` se guarda, pero no se puede mostrar si el WebView no lo admite. En ese caso se ve una tarjeta «Formato no compatible», con el nombre del fichero.
- **`added`** lo pone la app al crear la entrada y no cambia al editarla.
- **Modelo y lectura en Core** (`Journal/JournalEntry.cs`, `Journal/JournalReader.cs`):
  - un frontmatter inválido o una imagen listada que no existe son **resultados con aviso**, no excepciones;
  - la entrada se muestra con lo que haya.

### 4.2 Bitácora común

- **Ruta:** `Diario de la campaña.md`, en la raíz del vault.
- **Frontmatter:** `type: journal-log` y `generated: true`.
- **Contenido:**
  - una sección por entrada, ordenadas por `added` (si empatan, por la ruta de la entrada);
  - el encabezado de cada sección es `## <PJ> · <título> · <fecha local dd/MM/aaaa>`;
  - debajo, las imágenes como `![[ruta relativa]]` y, a continuación, la nota de la entrada;
  - al principio, la línea «Generado por DM Companion el <fecha>. Se sobrescribe al regenerar».
- **Reglas de escritura (el vault es sagrado):**
  - Si el fichero no existe, se crea con `WritePrecondition.MustNotExist`.
  - Si existe con `generated: true`, se sobrescribe con `UnchangedSince(fecha leída)`.
  - Si existe **sin** `generated: true`, **no se toca** y se muestra el aviso «Ya existe una nota «Diario de la campaña» que no generó la app. Renómbrala para generar la bitácora».
- La generación del Markdown es una función pura en Core (`Journal/JournalLog.cs`).

## 5. Añadir y editar entradas

### 5.1 «Compartir» de Android

- **Recepción:** la actividad de la App declara filtros `ACTION_SEND` y `ACTION_SEND_MULTIPLE` para `image/*`.
- **Copia temporal:**
  - al recibir el intent, la App copia los flujos (`content://`) a una carpeta temporal de la caché de la app;
  - deja la lista en un servicio de Screens (`IncomingShare`) detrás de una interfaz de plataforma (`IShareInbox`);
  - nada se escribe en el vault hasta que el DM guarda.
- **Formulario:** la app abre «Nueva entrada» sobre el mapa, con:
  - las imágenes recibidas, en orden y con la opción de quitar o reordenar;
  - el PJ, elegido de una lista de las notas `type: pc` (por defecto, el último PJ usado en la sesión);
  - el título, la nota y la criatura opcional (buscador del glosario de la fase 5);
  - «Guardar» y «Descartar».
- **Sin vault abierto:** si llega un «Compartir» sin vault abierto, primero se pide abrir uno y luego se muestra el formulario.
- **Limpieza:** la carpeta temporal se vacía al guardar, al descartar y al arrancar la app.
- **Lógica en Screens:** la interpretación del intent (`Intent.ClipData` o `EXTRA_STREAM`) es la única parte específica de Android y vive en `Platforms/Android/`. El resto va en Screens.

### 5.2 «Añadir imagen»

- En el diario de un PJ, el botón «Nueva entrada» abre el mismo formulario vacío, con «Añadir imagen».
- **Tablet:** se usa `IImagePicker`, que ya existe; se amplía para elegir varias imágenes.
- **PC y DevHost:** `InputFile` del navegador con `multiple`, como hace el diálogo de imágenes actual.

### 5.3 Editar y borrar

- **Editar:** el título, que renombra la nota y sus imágenes; la nota; la criatura; y las imágenes (añadir, quitar o reordenar).
- **Quitar una imagen** de una entrada borra su fichero. Es una operación explícita de la app y pide confirmación.
- **Borrar una entrada** borra la nota y sus imágenes, con la confirmación «¿Borrar la entrada «X» y sus N imágenes?».
- **Guardar:** las escrituras usan las precondiciones de siempre. Si la nota cambió fuera de la app, se avisa y se vuelve a cargar; no se sobrescribe.

## 6. Interfaz

- **👥 Jugadores → PJ:** nueva pestaña «Diario».
  - Muestra las entradas de la más reciente a la más antigua: la miniatura de la primera imagen, el título, la fecha, el número de imágenes y un chip con la criatura si la tiene.
  - Tocar una entrada abre su detalle: las imágenes grandes, que se pasan con un toque o deslizando; la nota; y los botones «Editar», «Borrar» y «Mostrar en TV».
- **👥 Jugadores (raíz):** sección «Bitácora» con:
  - «Generar bitácora común», que dice «Bitácora generada: N entradas» o muestra el aviso de §4.2;
  - «Ver bitácora en TV»;
  - «Abrir nota de la bitácora», si existe.
- **Glosario:** la ficha de una criatura, del SRD o del vault, añade la sección «Lo que saben los jugadores».
  - Tiene las miniaturas de las entradas cuya `creature` apunta a esa criatura (clave `srd:` o enlace a la nota) y el PJ de cada una.
  - Tocar una miniatura abre la entrada.
  - Si una nota del vault tapa la entrada del SRD (fase 5), cuentan las entradas que apuntan a cualquiera de las dos.
- **Índice:** `JournalIndex`, en Screens, se reconstruye al abrir el vault y se actualiza con `FileChanged`, como el resto de índices.

## 7. TV

- **«Mostrar en TV» en una entrada:**
  - pantalla completa con la imagen ajustada a la pantalla y un pie «<PJ> · <título> · imagen i de N»;
  - en la tablet, «Anterior» y «Siguiente» pasan de imagen;
  - si la entrada tiene nota, la última «página» es la nota en texto.
- **«Ver bitácora en TV»:**
  - recorre todas las entradas en el orden de la bitácora (por `added`, de la más antigua a la más reciente), imagen a imagen;
  - en la tablet hay una barra con «Anterior», «Siguiente», «Entrada x de M» y «Volver al mapa»;
  - usa el índice vivo, no la nota generada.
- **Imágenes:** se sirven con la lista blanca de ficheros de la TV de la fase 7, que se amplía con las rutas de `Diario/`.
- **Comportamiento:** el contenido se queda en la TV aunque cambie el vault. Al cambiar de vault, la TV vuelve al mapa, igual que en las fases 5 y 5b.

## 8. Errores

- **Imagen recibida que no se puede leer o con un tipo no admitido:** se descarta con el aviso «No se pudo leer N imagen(es)» y la entrada se guarda con el resto. Si no queda ninguna, no se guarda.
- **Imagen listada que falta en el vault:** se muestra la tarjeta «Imagen no encontrada: <nombre>». En la TV se salta.
- **Conflictos de Syncthing** (`*.sync-conflict-*`) de las entradas: salen en Diagnóstico, como los demás.
- **Errores de contenido:** ninguno es una excepción no capturada.

## 9. Tests

- **Core:**
  - `JournalReader`: frontmatter válido, inválido y con imágenes que faltan.
  - Nombres de fichero: título vacío, caracteres no válidos y colisión.
  - `JournalLog`: orden, empate, formato del encabezado e imágenes.
  - Decisión de sobrescritura: crear, `generated: true` y nota ajena.
- **Infrastructure:**
  - crear una entrada con varias imágenes en un vault temporal;
  - renombrar la entrada y sus imágenes;
  - borrar la entrada y sus imágenes;
  - no sobrescribir una bitácora ajena.
- **Screens:**
  - `JournalIndex` con `FileChanged`;
  - `IncomingShare`, que lleva los ficheros temporales al vault al guardar y los limpia al descartar;
  - entradas por criatura, incluido el tapado del SRD.
- **bUnit:** el formulario (PJ por defecto, quitar y reordenar imágenes, guardar y descartar), la pestaña Diario, el detalle, la sección del glosario, los avisos de la bitácora y la barra del pase de diapositivas.
- **DevHost:** un test de integración que crea una entrada con `InputFile` y comprueba los ficheros.
- **Android:** la recepción del intent se prueba en la tablet, con la checklist.

## 10. Checklist de la tablet

- Compartir una foto desde WhatsApp → DM Companion → guardar en el diario de un PJ.
- Compartir 3 fotos de la Galería a la vez → una entrada con 3 imágenes en orden.
- Compartir con la app cerrada y sin vault abierto.
- «Nueva entrada» → «Añadir imagen» desde la galería.
- Editar el título (los ficheros se renombran), quitar una imagen y borrar una entrada.
- Enlazar una entrada al Ankheg del SRD → la ficha del Ankheg muestra «Lo que saben los jugadores».
- «Generar bitácora común» → la nota aparece en Obsidian en el PC tras sincronizar, con las imágenes en orden.
- Una entrada en la TV, imagen a imagen, y la bitácora completa como pase de diapositivas.
- En la versión de PC: «Añadir imagen» con el selector de ficheros.
