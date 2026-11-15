# DM Companion para PC (Windows)

Es la misma app de la tablet, pero en el PC: se abre en el navegador y trabaja sobre una carpeta de notas (vault de Obsidian). No necesita instalación ni conexión a internet.

## Abrirla

1. Descomprime el zip entero (clic derecho, **Extraer todo**): Windows crea una carpeta con el nombre del zip (`dm_companion_pc`) con todos los ficheros dentro. No abras `DMCompanion.exe` desde dentro del zip: sin descomprimir no funciona y avisa de ello.
2. Haz doble clic en `DMCompanion.exe`. Se abre una consola y, enseguida, el navegador normalmente en `http://localhost:5190/` (si ese puerto está ocupado, usa otro y lo muestra en la consola).
3. La primera vez Windows muestra el aviso de SmartScreen («Windows protegió su PC»): pulsa **Más información** y después **Ejecutar de todas formas**. Es normal, el programa no está firmado.
4. Si el navegador no se abre solo, copia la dirección que aparece en la consola y pégala en el navegador.
5. Si lo abres dos veces, la segunda vez no arranca otra copia: abre la misma dirección.

## La primera vez

- Pulsa **Probar con el vault de ejemplo**: copia un vault de demostración a `Documentos\DM Companion\Ejemplo` (solo si no existe, nunca sobrescribe) y lo abre.
- Para usar tus notas, elige **Abrir mi vault**, pega la ruta de la carpeta (por ejemplo `D:\Obsidian\Campana`; vale con comillas) y pulsa **Ir**.
- La app recuerda el último vault y no vuelve a preguntar.

## Qué funciona y qué no

- Funciona: notas, fichas, mapa, tokens, dibujo, medición, HP y combate (tira de turnos, daño y curación, estados y salvaciones de muerte) y sonido (fondo y efectos desde `_audio/`, escenas, mini control en la franja superior y botón de ambiente en las notas).
- No funciona todavía: glosario y vista de jugadores en TV.

Aviso: el fichero de estado del mapa (`.state.json`) pasa a la versión 2. Una versión anterior de la app lo abre en solo lectura, así que actualiza todas las instalaciones (PC y tablet) que compartan el mismo vault.

## Cerrarla

Cierra la consola con la X o con Ctrl+C. Los cambios se guardan solos; espera un segundo antes de cerrar. Cerrar la pestaña del navegador no cierra la app.

## Si algo falla: mándame los registros

En la app, pulsa **Abrir carpeta de registros** (o abre `%LOCALAPPDATA%\DmCompanion\logs`) y mándame el fichero del día (`dm-companion-AAAAMMDD.log`). Se guarda uno al día y se conservan los 7 más recientes. Los registros no incluyen el contenido de tus notas ni el texto de las excepciones (solo su tipo), pero sí pueden incluir nombres de notas y mapas de tu vault.

## Actualizar a una versión nueva

Cierra la app, borra la carpeta antigua y descomprime el zip nuevo en una carpeta nueva. Tus preferencias (`%APPDATA%\DmCompanion`) y el ejemplo copiado viven fuera de esa carpeta, así que se conservan.
