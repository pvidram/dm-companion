# Calibrar la cuadrícula con un rectángulo — diseño corto

Fecha: 22/11/2026. Estado: aprobado.

- **Intención:** ajustar la cuadrícula a la de la imagen del mapa en un gesto: dibujar un rectángulo sobre unas casillas visibles y decir cuántas son. Hoy hay que tantear con los campos numéricos o con «Ajustar con los dedos».
- **Qué se hace:**
  - Botón **«Calibrar con un rectángulo»** en los controles de la cuadrícula, junto a «Ajustar con los dedos»; deshabilitado durante el combate, como el ajuste actual.
  - Se arrastra un rectángulo con dedo o lápiz (dos dedos siguen haciendo zoom y cancelan el rectángulo en curso). Dibujar otro sustituye al anterior; no hay esquinas que arrastrar.
  - Al entrar, una barra (con «Hecho» deshabilitado hasta que haya rectángulo) con **columnas** y **filas** (enteros ≥ 1, por defecto 1×1) y **vista previa en vivo** de la cuadrícula; **«Hecho»** guarda, **«Cancelar»** deja la cuadrícula como estaba.
- **Decisiones de Claude:** tamaño de casilla = media de ancho÷columnas y alto÷filas (tolera un rectángulo algo irregular); desplazamiento = esquina superior izquierda del rectángulo módulo el tamaño (rectángulo dibujado en cualquier dirección); calibrar enciende la cuadrícula si estaba apagada; al guardar se redondea a 1 decimal y se valida el rango 20–200 de tamaño como hoy (fuera de rango: aviso en la barra y no se guarda); no se puede deshacer (como el ajuste con los dedos); «Cancelar» antes de «Hecho» restaura.
- **Enfoque:** el cálculo vive en JS (función pura en un módulo nuevo, testeada con `node --test`) para que la vista previa sea instantánea; el gesto copia el patrón de `measureGesture`; el guardado reutiliza el camino de `FinishGridAdjustAsync` (validación y una mutación). Descartado: calcular en C# en cada cambio (viaje de ida y vuelta por cada número, vista previa a saltos).
- **Fórmula de partida:** `GridGeometry.Calibrate` de la etiqueta `archivo/fase-2-mapa-v1`, generalizada a columnas × filas.
- **No objetivos:** rectángulo girado, esquinas editables, calibrar sin imagen.
- **Queda al constructor:** nombres del módulo y del modo, dónde vive la barra (patrón `GridAdjustBar`), tests (JS para la fórmula y el gesto, bUnit para la barra y el guardado) y una checklist corta para la tablet.
