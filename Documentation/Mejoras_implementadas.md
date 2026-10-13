# Implementación de las 30 ideas

Cada mejora se implementa y registra en un commit separado. Los archivos del Excel se conservan para el seguimiento manual.

## 17. Aviso de vencimiento de Phase Dash

Durante los últimos tres segundos, el icono, la barra y el tiempo del HUD pulsan hacia amarillo de advertencia. La carcasa existente del jugador comparte esa señal a dos pulsos por segundo; el cuerpo permanece visible. El aviso usa tiempo de gameplay, de modo que se congela al pausar junto con la duración del powerup. No cambia el enfriamiento, las colisiones ni la duración de Phase Dash.

Validación: compilación de gameplay, revisión de los límites del aviso (solo mientras está activo y quedan como máximo tres segundos) y del uso de la misma señal en HUD y personaje. Pendiente observar el contraste y la lectura en Play Mode.

## 1. Rutas transitables y nodos accesibles

El generador comprueba la conectividad antes de crear cada obstáculo. Una grilla conservadora reserva espacio para el jugador y recorre las celdas libres mediante BFS, sin atravesar esquinas en diagonal. Se rechazan las colocaciones que dividen el espacio libre. Los obstáculos compuestos se comprueban como conjunto; los cuadrados girados usan su huella de colisión rotada.

Al iniciar un evento de nodos se captura la geometría de colisión actual, incluidos obstáculos móviles y compuertas sólidas activas. Los nodos se colocan únicamente en puntos alcanzables desde la posición del jugador. Si no se encuentra ninguno, se cancela ese intento y se libera su presupuesto de presión. La captura se calcula una sola vez por evento, no por frame ni por candidato.

El margen de navegación se configura mediante `navigationClearance` en `ProceduralArenaGenerator` (0,55 unidades por defecto). La grilla utiliza celdas de hasta 0,3 unidades; considera las cajas envolventes de los obstáculos, por lo que puede rechazar un paso estrecho aunque una forma redondeada permita atravesarlo.

Validación: compilación de los scripts de gameplay y 11 comprobaciones automatizadas de caminos abiertos, barreras completas, pasos estrechos, obstáculos compuestos, objetivos separados por compuertas, reconstrucción del alcance, límites de arena y esquinas diagonales. En Unity pueden ejecutarse desde `GLITCH > Validation > Arena reachability`.

Pendiente de comprobación visual en Play Mode: densidad de obstáculos y ritmo en las cinco arenas. El arranque automatizado del Editor estuvo bloqueado por su licencia local. La comprobación garantiza la conectividad de la distribución al generarse y la accesibilidad de los nodos al aparecer; los eventos que después cambian la geometría pueden modificar esas rutas y se revisarán en sus propias mejoras.
