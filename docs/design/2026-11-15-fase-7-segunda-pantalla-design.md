# DM Companion — Fase 7: segunda pantalla (TV)

- **Estado:** borrador para revisión. Reconexión al volver y alias decididos (15/11).
- **Orden de fases acordado el 15/11:** 6 (sonido) → 7 (TV) → 4 (niebla) → 5 (glosario) → 8 (diario).
- **Relacionado:** diseño global `2026-11-03-dm-companion-design.md` (§3 arquitectura, §5 vistas DM/jugador, §6 fase 7). Este documento sustituye allí la API `Presentation` de Android: no hay cable a la TV.

## 1. Intención

En la mesa hay una Smart TV con navegador y ningún cable desde la tablet. El autor quiere que los jugadores vean en la TV su propia vista: el mapa con los tokens, la tira de iniciativa durante el combate y, cuando él lo decida, una ficha de PJ o una nota de lugar con sus imágenes. Él sigue trabajando en la tablet (zoom, preparar el siguiente mapa, consultar notas) sin que la TV cambie, y sin que la TV destape nada secreto: el autor coloca los enemigos antes de la partida, a veces escondidos para emboscadas. El amigo de El autor dirige con la versión de PC y también necesita la TV.

## 2. Objetivos

- La tablet (y el PC) sirve una **página para la TV** en la red de casa; la Smart TV la abre con su navegador (dirección o QR) una vez por sesión. Sin internet: basta con estar en la misma wifi.
- **Vista de jugador**, independiente de la del DM y calculada en un solo sitio compartido: tokens ocultos fuera, vida de enemigos y aliados como «Ileso / Herido / Malherido», nada de `secret: true` ni `%% … %%`, sin paneles del DM.
- **Tokens ocultos** (adelantado de la fase 4): un token se marca oculto, en la tablet se ve atenuado y en la TV no existe hasta que se revela.
- **Encuadre propio de la TV**, que fija el autor: lo que ve en la tablet al pulsar «Fijar encuadre de TV»; luego la tablet se mueve libremente y un rectángulo muestra lo que ve la TV.
- **Mostrar en TV** una ficha de PJ o una nota de lugar a pantalla completa, con sus imágenes; «Volver al mapa» devuelve el encuadre fijado.
- **Imágenes del vault en las notas** (`![[faro.jpg]]`, `![[faro.jpg|400]]`), en la tablet y en la TV.
- **Combate en la TV**: tira de iniciativa con orden, turno, ronda, estados y muertos.
- Funciona en Android y en la versión de PC (y en el DevHost para desarrollar).

## 3. No objetivos

- Niebla de guerra (sigue en la fase 4), previsualización de la TV en la tablet (fase 4), glosario y diario en la TV (fases 5 y 8).
- Sonido por la TV.
- Interacción desde la TV: es solo para mirar; no se toca ni se manda nada desde ella.
- Google Cast / Chromecast, Miracast o duplicar pantalla.
- Varias TVs con contenidos distintos (varias pueden conectarse, pero todas ven lo mismo).
- Acceso desde fuera de la red local.

## 4. Anti-objetivos

- Que la TV muestre algo que solo debe ver el DM: un token oculto, un HP exacto de enemigo, un bloque secreto, una nota que no se ha enviado o el mapa que se está preparando.
- Que mover o hacer zoom en la tablet mueva la TV.
- Que cualquier dispositivo de la wifi pueda leer el vault: el servidor de la TV solo sirve la vista de jugador y los ficheros que esa vista usa en ese momento.
- Que la versión de PC deje de ser solo local por defecto (hoy escucha solo en loopback, `LoopbackGuard`).

## 5. Enfoques considerados (decidido el 15/11: A)

- **A (elegido): página propia de la TV que dibuja ella misma.** La app expone en la wifi una página ligera de solo lectura (sin Blazor) que recibe la vista de jugador por una conexión abierta y dibuja el mapa con Konva en modo solo lectura; fichas y notas llegan como HTML ya filtrado. Movimientos fluidos y nítidos a la resolución de la TV. Coste: un dibujante de mapa de solo lectura y un servidor HTTP pequeño propio en Android.
- **B (descartado): capturas de imagen desde la tablet.** Menos código en la TV, pero la tablet dibuja el mapa dos veces, el movimiento va a saltos y cada imagen a resolución de TV pesa en la wifi.
- **Descartados de entrada:** API `Presentation` (necesita cable o una pantalla inalámbrica que Android reconozca como segunda pantalla); TV como app Blazor (Blazor Server no corre en Android); duplicar pantalla (enseña la vista del DM); app receptora de Google Cast (registro en Google, alojamiento HTTPS e internet).

## 6. Diseño

### 6.1 Conexión

- En el menú izquierdo, el botón **📺 TV** abre el panel «TV» (un panel lateral más, como «Cambiar vault»).
- El servidor de la TV está **apagado por defecto**. En el panel, «Activar TV» lo enciende; queda encendido hasta «Desactivar TV» o hasta cerrar la app. *(Decisión tomada: seguridad y batería; la versión de PC solo abre la red mientras está activado.)*
- Con la TV activada, el panel muestra la dirección (`http://<ip-de-la-tablet>:<puerto>/tv/<código>`) en grande y un **código QR** con ella, y el estado: «Sin TV conectada» / «TV conectada» (cuántas).
- El **código** es **fijo y lo elige el autor** en el panel (decidido el 15/11, tras probar en su TV Xiaomi con Google TV: tiene que poder teclearlo de memoria en una TV nueva). Se guarda en las preferencias del dispositivo y se reutiliza en cada activación y tras reiniciar la app. Formato: letras a–z sin tildes, números y guiones, de 4 a 32 caracteres; se compara sin distinguir mayúsculas. Por defecto la app propone el nombre de la campaña convertido a ese formato (p. ej. `campana-brumavela`) o `mesa`. Cambiarlo pide confirmación (las TVs con la dirección guardada dejan de entrar). El código solo evita que otro aparato de la wifi vea la vista de jugadores, que por diseño no lleva nada secreto. Sin el código correcto, el servidor responde 404.
- **Puerto** fijo por defecto (constante a elegir por quien construya, p. ej. 8590) con alternativa si está ocupado; la dirección mostrada es la real.
- **Varias IP** (wifi y otra interfaz): se muestra la de la wifi; si hay varias candidatas, se listan.
- Si la tablet se duerme o la wifi se corta, la TV conserva la última imagen, muestra un aviso discreto «Reconectando…» y se reconecta sola; al volver recibe el estado completo. **Decidido (15/11):** en Android no se mantiene el servidor con la pantalla apagada ni en segundo plano (sin servicio en primer plano); basta con reconectar al volver.
- **PC:** el mismo panel y el mismo flujo. Abrir la red en el PC dispara el aviso del cortafuegos de Windows la primera vez; la guía de la versión de PC lo explica. Si el amigo conecta la TV por HDMI como segunda pantalla, puede abrir la dirección local (`http://localhost:<puerto>/tv/<código>`) en una ventana del navegador en esa pantalla.

### 6.2 Qué muestra la TV

La TV está siempre en uno de tres estados, que decide la tablet:

1. **Espera** (al activar y hasta fijar un encuadre): fondo del design system con el nombre de la campaña. *(Decisión tomada: así se puede preparar sin que la TV muestre nada.)*
2. **Mapa**: el mapa fijado con su encuadre, tokens visibles, trazos y, si hay combate en ese mapa, la tira de iniciativa.
3. **Contenido**: una ficha de PJ o una nota de lugar a pantalla completa.

Elementos fijos en la TV: ninguno del DM (sin barra, menú ni avisos).

**Lista cerrada de lo que dibuja la TV en el estado Mapa** *(decisión tomada)*: imagen del mapa o lienzo en blanco, cuadrícula, trazos, tokens (retrato o inicial, anillo de color por bando, tamaño, cruz de muerto, marcas de estado como en la tablet) y resaltado del token con el turno. No dibuja: selección, medición, ajuste de cuadrícula, insignia de oculto ni nada de las herramientas de edición.

### 6.3 Encuadre

- «Fijar encuadre de TV» (en el panel TV y como botón en la barra del mapa) guarda como encuadre de la TV la zona del mapa que se ve en la tablet en ese momento, ajustada a la proporción de la TV conectada (si no hay ninguna, 16:9). La TV pasa al estado Mapa con ese mapa y ese encuadre.
- En la tablet, mientras ese mapa esté abierto, un **rectángulo fino** marca la zona que ve la TV.
- **Proporción:** cada TV informa del tamaño de su ventana al conectar y al cambiarlo. Manda la TV conectada más recientemente; las demás muestran el encuadre entero con márgenes del color de fondo. Si la proporción cambia, el encuadre conserva su centro y la zona fijada cabe entera (con márgenes, nunca recorta). *(Decisión tomada.)*
- Mover, hacer zoom o cambiar de mapa en la tablet **no** cambia la TV. Cambiar de mapa en la TV exige fijar el encuadre en el mapa nuevo.
- Dentro del encuadre, la TV se actualiza al momento: tokens movidos, revelados u ocultados, trazos, combate. La TV se alimenta del estado del mapa **en memoria** (el mismo que pinta la tablet, incluido deshacer/rehacer), no del fichero guardado. Un token arrastrado se actualiza en la TV al soltarlo, no durante el arrastre. *(Decisión tomada.)*
- La tira de iniciativa que se ve es la del combate del mapa que muestra la TV, aunque la tablet tenga abierto otro mapa.
- Si se borra o se renombra el mapa que muestra la TV, la TV pasa a Espera.
- El encuadre se recuerda por mapa mientras la app esté abierta; no se guarda en el vault. *(Decisión tomada: es un estado de la sesión de juego, no de la campaña.)*

### 6.4 Tokens ocultos

- Nuevo campo opcional `hidden: true` en el token, dentro del `.state.json` del mapa (se omite si es falso; compatible con ficheros existentes).
- Se oculta y se revela desde la tarjeta rápida del token («Ocultar a los jugadores» / «Revelar»). Al colocar un token nuevo, sale visible.
- En la tablet, el token oculto se ve atenuado con la insignia de ojo tachado (`TokenAvatar` ya tiene `IsHidden`; en Konva, igual).
- En la TV, el token oculto no existe: ni en el mapa ni en la tira de iniciativa.
- **Combate**: en la tira de la TV se omiten las entradas de tokens ocultos. Si el turno es de un token oculto, la TV no resalta a nadie (la ronda sí se ve). Al revelarlo, aparece en su sitio del orden.
- Ocultar y revelar entran en el historial de deshacer/rehacer del mapa como cualquier otra edición.

### 6.5 Alias

**Decidido (15/11):** todos los tokens (PJ, aliados y enemigos) tienen un campo opcional **alias**. Si tiene valor, es el nombre que se muestra en la TV (mapa y tira de iniciativa); si no, el nombre del token. Se edita en la tarjeta del token y se guarda en el `.state.json` (`alias`, omitido si está vacío). En la tablet sigue viéndose el nombre real, con el alias visible en la tarjeta. *(Que la tablet siga mostrando el nombre real es interpretación de Claude.)*

### 6.6 Vida y estados en la TV

- **PJ**: vida exacta (actual/máxima y temporales), como en la tablet.
- **Enemigos y aliados**: etiqueta según la vida actual frente a la máxima. *(Decisión tomada, umbral de «bloodied» de D&D:)* «Ileso» si está al máximo; «Herido» si está por encima de la mitad; «Malherido» si está a la mitad o menos y por encima de 0; a 0, sin etiqueta de vida (se ve «Muerto» o el estado que corresponda). Sin máximo conocido, no se muestra etiqueta.
- **Estados** (derribado, envenenado…) visibles para todos. **Muertos**: tachados, como en la tablet.
- La regla de la etiqueta vive en el sistema de juego (`ICombatRules` o equivalente), para que Cosmere pueda tener la suya.

### 6.7 Mostrar en TV

- Botón «📺 Mostrar en TV» en la ficha de un PJ (`type: pc`) y en las notas de lugar (`type: location`). No aparece en PNJ, monstruos ni el resto de tipos, ni cuando la TV está desactivada.
- La TV pasa al estado Contenido con esa ficha o nota, a pantalla completa, con letra grande y el estilo del design system.
- El contenido pasa siempre por el filtro de secretos: una nota `secret: true` no tiene el botón; los bloques `%% … %%` no salen, y tampoco las imágenes que haya dentro de ellos.
- Los enlaces se ven como texto normal (sin subrayado ni acción).
- En la tablet, mientras la TV muestra un contenido, sale un aviso fijo «En la TV: Faro Viejo» con «Volver al mapa» (que también está en el panel TV). «Volver al mapa» devuelve el último encuadre fijado o Espera si no hay.
- Si la nota mostrada cambia en disco (Syncthing o edición en la tablet), la TV se actualiza; si se borra, vuelve al mapa.

### 6.8 Imágenes del vault en las notas

- `![[ruta]]` y `![[ruta|ancho]]` (sintaxis de Obsidian) se muestran como imagen en el renderizado de notas, en la tablet y en la TV. La ruta se resuelve como Obsidian: relativa al vault o por nombre de fichero si es único.
- Solo ficheros de imagen dentro del vault (jpg, jpeg, png, webp, gif); fuera del vault o inexistente, se ve como un enlace roto, sin error.
- En la tablet se sirven por el mecanismo actual (`VaultFileUrls` / `VaultFileRequest`). El renderizador recibe quién resuelve las URLs de imagen: para la TV, las URLs apuntan al servidor de la TV y la lista blanca se calcula desde el modelo (las rutas que resolvió el renderizador), no parseando el HTML. *(Decisión tomada.)* En la TV, por el servidor de la TV, que solo entrega las imágenes que aparecen en el contenido o el mapa mostrados en ese momento (imagen del mapa, retratos de los tokens visibles e imágenes de la nota).

### 6.9 Arquitectura

- **Core**: la **proyección a vista de jugador** (`PlayerView` o similar): a partir del estado del mapa, el combate, el sistema de juego y el encuadre, produce un modelo serializable sin nada secreto. Es la única pieza que decide qué ven los jugadores, y se prueba sin dispositivo. El renderizado de notas para la TV usa el filtro de secretos existente (`SecretFilter`) y el renderizador de notas con las imágenes nuevas.
- **Screens**: el servicio de la TV (estado Espera/Mapa/Contenido, encuadre, código, avisos de cambio), el panel TV, el rectángulo del encuadre, los botones de las fichas y notas. La página de la TV (HTML, JS y CSS) vive en `wwwroot` de Screens y no usa Blazor.
- **Transporte por plataforma**, detrás de una interfaz en `Screens/Platform` (p. ej. `ITvServer`: arrancar en un puerto, parar, saber las direcciones y cuántas TVs hay conectadas). Sirve la página, los ficheros de la lista blanca y un canal de empuje del servidor a la TV con el estado completo al conectar y después cada cambio.
  - **PC**: con Kestrel, en un puerto aparte del de la app y solo mientras la TV está activada; la app del DM sigue solo en loopback. Comparte los servicios (y el estado de la TV) con la app; las rutas de la app nunca responden por el puerto de la TV ni las de la TV por el de la app. *(Decisión tomada: si un segundo listener en caliente no es viable, un `WebApplication` aparte que reciba los singletons ya creados.)* (En Windows, `HttpListener` necesita permisos de administrador para escuchar fuera de localhost.)
  - **Android**: servidor HTTP pequeño en proceso (ASP.NET Core no está disponible en Android). Al servir (no pedir) HTTP no hace falta configuración de tráfico sin cifrar; sí el permiso para leer la IP de la wifi. **Primera tarea del plan: prueba de concepto del servidor en la tablet con la Smart TV real** (página, QR y canal de empuje), porque es lo más arriesgado y decide SSE o WebSocket. *(Decisión tomada.)*
  - **DevHost**: como el PC.
- **Dibujo del mapa en la TV**: un dibujante de solo lectura con Konva. `map-canvas.js` (~950 líneas) incluye edición; quien construya decide si extrae a un módulo común el dibujo de imagen, cuadrícula, tokens y trazos o escribe uno aparte, pero el aspecto de tokens y trazos debe coincidir con la tablet.
- **Navegadores de TV**: los de las Smart TV suelen ir varias versiones por detrás. La página de la TV usa JavaScript sin módulos ni sintaxis reciente que no soporte un Chromium de hace unos años, y un canal de empuje compatible (Server-Sent Events o WebSocket, a decidir).

### 6.10 Decisiones tomadas

- Servidor de la TV apagado por defecto y con código en la dirección (fijo y elegido por el autor, §6.1).
- Estado Espera con el nombre de la campaña hasta fijar el primer encuadre.
- Encuadre recordado en memoria por mapa, no en el vault.
- Umbrales de «Ileso / Herido / Malherido» (§6.6).
- El alias solo cambia el nombre en la TV; la tablet muestra el nombre real (§6.5).
- Turno de un token oculto: la TV no resalta a nadie.
- La TV refleja el estado en memoria; los arrastres se ven al soltar.
- Lista cerrada de lo que dibuja la TV (§6.2) y márgenes en vez de recorte (§6.3).
- Prueba de concepto con la TV real como primera tarea.

## 7. Verificación

- **Core**: tests de la proyección: token oculto ausente en mapa y tira; alias en lugar del nombre; etiquetas de vida en cada umbral; PJ con vida exacta; muertos y estados; nota secreta rechazada, bloques `%% %%` e imágenes dentro de ellos fuera; encuadre ajustado a la proporción.
- **Screens/bUnit**: panel TV (activar, dirección, estado), «Fijar encuadre», «Mostrar en TV» solo en PJ y lugares, aviso «En la TV» y «Volver al mapa», ocultar/revelar en la tarjeta.
- **Servidor**: sin código o con código erróneo, 404; fichero fuera de la lista blanca, 404; al conectar recibe el estado completo; un cambio llega a la TV.
- **PC**: con la TV desactivada, nada escucha fuera de loopback.
- **JS**: tests de las piezas puras del dibujante de la TV.
- **Checklist manual**: en la Smart TV del autor (QR, encuadre, emboscada revelada, combate, ficha y nota con imagen, reconexión al dormirse la tablet) y en el PC.

## 8. Qué decide quien construya

- Nombres de tipos, el formato exacto de los mensajes a la TV y si el canal es SSE o WebSocket.
- La librería o el código para el QR (sin dependencias de internet en tiempo de ejecución).
- Cómo se implementa el servidor pequeño de Android, el puerto por defecto y la alternativa si está ocupado.
- Si el dibujo del mapa de la TV comparte código con `map-canvas.js`.
- Disposición visual del panel TV y de la vista Contenido en la TV, dentro del design system.
