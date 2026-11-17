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

## Verificación

La compilación de gameplay se comprueba con el ensamblado completo. Los contrastes de la paleta se validan por separado; esto no sustituye una revisión visual ni certifica toda la accesibilidad del juego. No había una instancia de Unity abierta durante la implementación. Pendiente revisar menús, pausa, resultados, tutoriales y HUD en Play Mode, incluyendo escalas de usuario y ventanas pequeñas.
