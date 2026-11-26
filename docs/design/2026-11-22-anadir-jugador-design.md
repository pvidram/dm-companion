# Añadir jugador desde la app — diseño

Fecha: 22/11/2026. Estado: aprobado.

## 1. Intención

El DM quiere dar de alta los personajes de sus jugadores desde la app, sin escribir el YAML a mano: nombre, alias, la persona que lo juega y las estadísticas base; el resto (modificadores, salvaciones, habilidades, percepción pasiva) lo calcula la app como ya hace al mostrar la ficha.

Éxito: en la primera sesión de una campaña nueva, en la tablet, añade los cuatro PJ en un par de minutos, cada uno aparece en Jugadores con su ficha completa, y al ponerlos en el mapa la TV muestra su alias con su retrato.

## 2. Objetivos, no objetivos y antiobjetivos

**Objetivos**
- Botón **«Añadir jugador»** en el pie del panel Jugadores, siempre visible (también con la lista vacía; el texto de lista vacía lo menciona).
- Formulario (diálogo) con: **nombre** (obligatorio), **alias** (opcional), **jugador/a** (nombre de la persona, opcional) y las **estadísticas base del sistema de la campaña**, con los valores por defecto del sistema.
- **Formulario según el sistema** (enfoque A, elegido): cada `IGameSystem` declara qué estadísticas base pide al crear un PJ; el diálogo es genérico y pinta esos campos. D&D 5e: CA, PG, velocidad, nivel y las seis características. Cosmere: sus estadísticas base (atributos y máximos de recursos, según su `StatsTemplate`).
- Crea la nota `type: pc` en `Personajes/` con nombre de fichero desde el nombre (`NoteFileName.FromTitle`), con las estadísticas base rellenadas y el resto como hoy en la plantilla (competencias a «none»), más `alias` y `player`.
- Al crear, se abre la ficha del PJ nuevo en Jugadores.
- **Alias**: es el mismo concepto que el alias del token. Al poner el PJ en el mapa, el token toma el alias de la nota. **Con alias de PJ, el retrato sigue visible en la TV** (en los tokens genéricos o de criatura, el alias oculta el retrato, porque es un disfraz; en un PJ es un apodo).
- **Jugador/a**: solo se ve en la ficha del PJ (no en la lista de Jugadores, ni en el mapa ni en la TV).

**No objetivos**
- Editar un PJ con este formulario (se edita como hoy, con el editor de notas).
- Pedir competencias en salvaciones o habilidades, retrato, trasfondo u otros campos (solo estadísticas base).
- Escribir valores derivados en la nota (los calcula la app al leerla).

**Antiobjetivos**
- Sobrescribir un PJ existente: si ya hay una nota con ese nombre, aviso y no se escribe.
- Un formulario que no sirva en una campaña de Cosmere.
- Notas que Obsidian o la app no lean después (YAML mal formado con nombres con comillas, dos puntos, acentos…).

## 3. Restricciones

- Arquitectura: lo que pide cada sistema y cómo se escribe la nota en `DmCompanion.Core` (`IGameSystem` / `NoteTemplates`, testeable sin plataforma); el diálogo en Screens; componentes base del design system (`TextField`, `NumberStepper`, `Dialog`…).
- Reutilizar `VaultService` para escribir con `WritePrecondition.MustNotExist` (hoy `CreateNoteAsync` no admite contenido propio: hace falta una variante o construir el contenido y usar `SaveAsync`, como hace «Copiar al vault» del glosario).
- Las claves del frontmatter de las estadísticas son las que ya leen los sistemas (`stats.ac`, `stats.abilities.str`…, y las de Cosmere). Claves nuevas: `alias` y `player`.

## 4. Enfoques considerados

- **A. Formulario según el sistema** — *elegido*. Cada sistema declara sus estadísticas base de creación; un único diálogo genérico. Funciona en Cosmere y en sistemas futuros.
- **B. Formulario fijo de D&D 5e** — descartado: no serviría en Cosmere y obligaría a otro formulario después.

## 5. Comportamiento (lo decidido)

- «Añadir jugador» abre el diálogo con los valores por defecto del sistema (D&D 5e: CA 10, PG 10, velocidad 30, nivel 1, características 10, como la plantilla actual).
- «Crear» deshabilitado sin nombre; valores numéricos fuera de rango (negativos, características fuera de 1–30) → error en línea.
- Ya existe una nota con ese nombre → aviso en línea y no se crea.
- Error de disco → aviso en línea; el diálogo sigue abierto con lo escrito.
- Creado → se cierra el diálogo, la lista de Jugadores lo incluye y se selecciona su ficha.
- La ficha muestra «Jugador/a: …» si `player` existe. El alias se muestra en la ficha.
- Token desde un PJ con alias (al añadirlo al mapa por las vías actuales) → `Alias` del token prerrellenado con el de la nota; la TV muestra el alias y el retrato del PJ.

## 6. Decisiones de diseño

- Claves `alias` y `player` en inglés, como el resto del frontmatter (`type`, `name`, `portrait`, `stats`).
- El alias de la nota se copia al token **al crear el token** (no se sincroniza después: si se cambia el alias en la nota, los tokens ya puestos conservan el suyo; se puede editar en la tarjeta del token como hoy).
- La regla «alias oculta el retrato en la TV» deja de aplicarse a tokens cuya fuente es una nota `type: pc`.
- Si dos nombres distintos dan el mismo nombre de fichero (`NoteFileName.FromTitle`), se trata como «ya existe» y se avisa.

## 7. Lo que queda al constructor

- Forma exacta de la declaración en `IGameSystem` (p. ej. una lista de `StatDefinition` de creación con su valor por defecto y rango) y cómo se compone el YAML con lo que ya hace `StatsTemplate`.
- Lista concreta de estadísticas base de Cosmere a partir de su plantilla actual.
- Si `initiative_bonus` de D&D 5e se escribe o se deriva (comprobar qué hace hoy el combate si falta).
- Estado del diálogo en `ShellState` (patrón `NewNoteDialog`) y montaje en `DmShell`.
- Tests: Core (YAML generado por sistema, nombres raros, no sobrescribir, lectura de `alias`/`player`), Screens (botón, diálogo, ficha con jugador/a, alias al token, retrato en la proyección de TV).
