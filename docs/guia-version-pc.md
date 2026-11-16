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

- Funciona: notas, fichas, mapa, tokens, dibujo, medición, HP, combate (tira de turnos, daño y curación, estados y salvaciones de muerte), sonido (fondo y efectos desde `_audio/`, escenas, mini control en la franja superior y botón de ambiente en las notas) y vista de jugadores en la TV.
- No funciona todavía: glosario.

Aviso: el fichero de estado del mapa (`.state.json`) pasa a la versión 2. Una versión anterior de la app lo abre en solo lectura, así que actualiza todas las instalaciones (PC y tablet) que compartan el mismo vault.

## Ver la partida en la TV

Los jugadores pueden ver su propia vista en una Smart TV con navegador, sin cables: basta con que la TV y el PC estén en la misma red de casa (no hace falta internet).

1. En el menú de la izquierda, pulsa **TV**. En **Código de la TV** elige una palabra fácil de recordar (de 4 a 32 letras sin tildes, números o guiones, por ejemplo `brumavela`) y pulsa **Guardar**; el panel propone el nombre de la campaña. Después pulsa **Activar TV**: el panel muestra una dirección del tipo `http://192.168.1.20:8590/tv/brumavela/` y un código QR con ella. No importa si escribes mayúsculas o minúsculas.
2. La primera vez, Windows pregunta si permite que `DMCompanion.exe` acceda a la red (aviso del **Firewall de Windows Defender**). Marca **Redes privadas** y pulsa **Permitir acceso**. Si la red de casa está configurada como pública, cámbiala a privada en la configuración de Windows (Red e Internet). Si lo cancelaste por error, ve a **Firewall de Windows Defender › Permitir una aplicación a través del firewall** y marca `DMCompanion.exe` en redes privadas.
3. En la TV, abre el navegador y escribe la dirección tal cual (o escanea el QR con un móvil para comprobarla). La TV muestra el nombre de la campaña hasta que fijes un encuadre del mapa.
4. **Favorito en la TV:** una vez abierta la dirección, guárdala como favorito o marcador del navegador de la TV (o como acceso directo en la pantalla de inicio, si lo permite). La próxima sesión bastará con abrir el favorito, sin teclear nada.
5. Debajo de la dirección, el panel dice si hay alguna TV conectada («Sin TV conectada», «TV conectada»). Si el panel lista varias direcciones, prueba con la siguiente cuando la primera no cargue.
6. La TV queda activada hasta que pulses **Desactivar TV** o cierres la app. El código es fijo: se guarda en este equipo y es el mismo al volver a activar la TV o al reabrir la app, así que la TV puede entrar siempre por la misma dirección. Si lo cambias (el panel pide confirmación), las TVs con la dirección anterior dejan de entrar y hay que abrir la nueva. Mientras está desactivada, el PC no acepta conexiones de la red.

Para enseñar en la TV una ficha de PJ o una nota de lugar, ábrela y pulsa **Mostrar en TV**; la ficha sale con el retrato y las estadísticas (no sale en notas secretas ni en PNJ o monstruos; los bloques `%% … %%` nunca llegan a la TV). Mientras se ve, arriba aparece «En la TV: …» con **Volver al mapa**, que devuelve la TV al último encuadre fijado.

Si la wifi se corta un momento, la TV conserva la última imagen, muestra «Reconectando…» en una esquina y vuelve sola.

**TV conectada por HDMI como segunda pantalla:** también vale. Activa la TV, abre una ventana nueva del navegador, arrástrala a la pantalla de la TV y escribe la misma dirección cambiando la IP por `localhost` (por ejemplo `http://localhost:8590/tv/brumavela/`). Pon esa ventana a pantalla completa con F11.

## Niebla y vista de jugadores

Para que los jugadores no vean en la TV lo que aún no han descubierto:

1. Abre un mapa y pulsa **Niebla** en la barra de abajo. Elige **Trazar** y haz clic en las esquinas de la zona (el ratón hace de lápiz). Para cerrarla, haz clic cerca de la primera esquina. **Deshacer esquina** quita la última y **Cancelar** descarta el trazado.
2. La zona nueva queda **tapada**: la TV no la muestra ni lo que haya dentro (los tokens tampoco). Con **Destapar**, un clic dentro de la zona la destapa o la vuelve a tapar. Con **Goma**, un clic dentro la borra. Si varias zonas se solapan, Destapar y Goma actúan sobre la última que creaste.
3. **Deshacer** y **Rehacer** valen también para la niebla.
4. **Vista de jugadores** (el ojo del grupo «Mapa»): cubre el mapa con lo que ve la TV, entero, solo para mirar. Mientras está puesta, el resto de botones de la barra están deshabilitados; vuelve a pulsar el ojo para quitarla. Se quita sola al cerrar el mapa y funciona aunque la TV esté desactivada.

Aviso: un mapa con niebla guarda el `.state.json` en la versión 3; una versión anterior de la app lo abre en solo lectura. Un mapa sin niebla sigue en la versión 2.

## Cerrarla

Cierra la consola con la X o con Ctrl+C. Los cambios se guardan solos; espera un segundo antes de cerrar. Cerrar la pestaña del navegador no cierra la app.

## Si algo falla: mándame los registros

En la app, pulsa **Abrir carpeta de registros** (o abre `%LOCALAPPDATA%\DmCompanion\logs`) y mándame el fichero del día (`dm-companion-AAAAMMDD.log`). Se guarda uno al día y se conservan los 7 más recientes. Los registros no incluyen el contenido de tus notas ni el texto de las excepciones (solo su tipo), pero sí pueden incluir nombres de notas y mapas de tu vault.

## Actualizar a una versión nueva

Cierra la app, borra la carpeta antigua y descomprime el zip nuevo en una carpeta nueva. Tus preferencias (`%APPDATA%\DmCompanion`) y el ejemplo copiado viven fuera de esa carpeta, así que se conservan.
