# TV: el mapa ocupa todo el espacio también en combate

## Intención
El mapa de la TV debe verse lo más grande posible, con barras negras solo cuando las proporciones lo exijan.

## Causa
El servidor (`TvSession.FrameFor` → `TvFrame.FitToAspect`) amplía el encuadre a la proporción de la ventana completa. En combate la tira de iniciativa resta altura al escenario, que pasa a ser más ancho que el encuadre: `stageTransform` encaja por alto y salen barras laterales.

## Enfoque
- Cliente: nueva función pura `fitFrameToViewport(frame, viewport)` en `tv-layout.js`, con la misma semántica que `TvFrame.FitToAspect`: crece el lado corto alrededor del centro hasta la proporción del escenario; nunca recorta.
- `tv-map.js` (`draw()`) la aplica antes de `stageTransform` y del recorte (`layer.clip`). Lo ampliado muestra el contenido alrededor del encuadre del DM, en lugar de barras.
- La vista previa de jugadores de la tablet usa el mismo `tvMap`, así que queda igual que la TV.
- Fuera de la imagen del mapa no se pinta nada: se ve el fondo del contenedor, como hasta ahora.

## Sin tocar
`TvFrame.FitToAspect` y `TvSession` (servidor), el protocolo de mensajes, la tira de iniciativa y `availableViewport`.
