# Pulido visual de presentación

Punto de partida funcional: `9776f13`. La pasada se separa en commits de presentación para poder revertirla sin retirar las mejoras de gameplay anteriores.

## Criterio

Se conserva la identidad industrial oscura y el acento digital de GLITCH. No se sustituyen iconos, sprites de arenas, controles, textos de mecánicas, colisiones ni reglas. Se reduce la competencia entre superficies, marcos, movimiento decorativo y lectura.

- Paneles de azul naval casi opaco, borde fino neutral y un único acento corto de color.
- Separación entre superficie y controles mediante valores, no mediante varios marcos luminosos.
- Fuente expresiva para el logo y las letras de rendimiento; fuente secundaria para lectura y acciones.
- Hover por contraste y acento, con áreas de clic estables. Sin temblor ni barridos sobre las etiquetas.
- Glitch del logo ocasional; títulos de secciones estables. Se conservan las transiciones de página y el feedback de selección.
- El fondo de la arena del menú conserva su identidad y movimiento. Se retiran haces y orbitadores adicionales de la composición de la interfaz principal.

## Integración en partida

Pausa, confirmaciones, controles, resultados individuales y Versus usan las mismas superficies y botones que el menú. Los resultados conservan su clasificación, causa, estadísticas y bloqueo inicial de input; las llamadas de audio se mantienen sin duplicar confirmaciones. La fuente expresiva queda reservada a la letra de rendimiento.

El HUD mantiene posiciones, iconos, valores, semántica de color y barra de amenaza. La cabina pasa a una superficie más uniforme, con divisores neutrales y sin barrido ornamental permanente. Los avisos conservan su acento semántico y los tutoriales mantienen el encuadre de foco: solo se retoca la superficie del diálogo y la tipografía, no su posición ni validación.

## Verificación final

La compilación de gameplay se comprueba con el ensamblado completo. Los contrastes de la paleta se validan por separado; esto no sustituye una revisión visual ni certifica toda la accesibilidad del juego. No había una instancia de Unity abierta durante la implementación. Pendiente revisar menús, pausa, resultados, tutoriales y HUD en Play Mode, incluyendo escalas de usuario y ventanas pequeñas.

Se comprobaron once relaciones de contraste/tamaño de la paleta y se volvió a compilar la lógica de las validaciones anteriores. Las funciones que dibujan los iconos principales y los del HUD se compararon contra `9776f13`: no cambiaron. No se alteran assets de arenas o física.
