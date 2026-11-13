# Checklist manual del design system

Para que App lo ejecute en DevHost (`dotnet watch --project src/DmCompanion.DevHost`, http://localhost:5180) y en la tablet. Los tests automáticos no cubren lo visual ni el foco real del navegador.

## Cableado del host

- [ ] El host enlaza `ui.css` (`_content/DmCompanion.UI/ui.css`).
- [ ] Llama a `AddDmCompanionUI` en el registro de servicios.
- [ ] Incluye el ensamblado de UI en `AdditionalAssemblies` del router.
- [ ] `/styleguide` se abre y muestra todos los componentes.

## Aspecto

- [ ] Fuentes: los títulos salen en EB Garamond y el cuerpo en Source Sans 3, no en fuentes del sistema (inspeccionar «Computed → font-family» y la pestaña Network/Fonts).
- [ ] Los iconos se ven y toman el color del texto que los rodea (también en hover y deshabilitados).
- [ ] Los anillos de bando se distinguen sin depender del color: PJ continuo, aliado discontinuo, enemigo doble.
- [ ] El contorno del anillo se ve sobre madera (raíl y franja) y sobre pergamino.
- [ ] Legibilidad con poca luz (brillo bajo, sala a oscuras): texto, bordes y foco siguen leyéndose.

## Táctil

- [ ] Objetivos de al menos 48 px en todos los controles.
- [ ] Daño, curación y siguiente turno de al menos 56 px.

## Pergaminos y overlays

- [ ] Cada pergamino (lateral, ancho, completo) se abre y se cierra.
- [ ] Esc cierra solo el overlay más interno, no los de debajo.
- [ ] Al cerrar, el foco vuelve al elemento que lo abrió.
- [ ] El foco también vuelve al abridor cuando el overlay se desmonta con `@if` (sin pasar por el botón de cerrar).
- [ ] Dialog: Tab y Shift+Tab nunca salen de él, tampoco tras hacer clic en el fondo del diálogo.

## Mapa (fase 2)

- [ ] La barra del mapa queda a la derecha y sigue visible con un pergamino lateral abierto; un pergamino ancho o completo la tapa.
- [ ] Las herramientas activas (Regla, Cuadrícula, Calibrar) se distinguen sin color (fondo y borde interior), también con poca luz.
- [ ] La cuadrícula al 70 % de opacidad se lee sobre un mapa claro, uno oscuro y uno muy detallado, sin tapar el dibujo. Si no, ajustar `--map-grid-opacity` o los grosores.
- [ ] La regla y el rectángulo de calibración se leen sobre cualquier zona del mapa; la etiqueta de distancia se lee siempre.
- [ ] El token oculto se reconoce a simple vista en el canvas y en las listas (`TokenAvatar`), y no aparece en la TV.
- [ ] Los ± del stepper se pulsan con el dedo sin fallos; teclear «70,3» con el teclado español funciona.
- [ ] El aviso «Deshacer» no desaparece mientras se toca o tiene el foco, y se lee en un lector de pantalla (TalkBack).
- [ ] Un aviso mostrado con un diálogo abierto no se ve (`Toast` 25 < `Dialog` 30): App cierra el diálogo antes de mostrarlo.

## Vista TV (`IsTv`)

- [ ] Texto y espaciado visiblemente ~1,75 veces mayores.
- [ ] No hay botones de cerrar ni acciones en los pergaminos.
- [ ] No aparece la etiqueta «Solo DM» ni ningún fragmento secreto.

## Ventana de PC redimensionable (DevHost)

- [ ] Sin scroll horizontal a anchos estrechos.
- [ ] El raíl hace scroll cuando la ventana es baja.
- [ ] El hover solo se aplica con ratón (no se queda «pegado» al tocar).
