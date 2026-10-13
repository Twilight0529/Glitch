# Implementación de las 30 ideas

Cada mejora se implementa y registra en un commit separado. Los archivos del Excel se conservan para el seguimiento manual.

## 9. Dirección del reorden magnético de Storage

La idea original hablaba de campos magnéticos que empujan o atraen al jugador. En la versión actual, el evento activo de Storage es el reorden magnético de carga; el código de campos circulares no se instancia. La mejora se aplica a ese sistema real: pequeñas flechas junto a los objetos indican su desplazamiento durante el aviso amarillo y acompañan el movimiento en rojo. Al regresar, apuntan a su posición inicial.

Se muestran como máximo cuatro indicadores, priorizando los objetos más cercanos al jugador. Se pueden ajustar o desactivar mediante `maxMagneticDirectionPreviews`. Las señales usan el destino calculado por el evento y respetan su desfase y los límites de la arena. Desaparecen al llegar, destruirse la carga, cancelarse el evento o cambiar de arena. No se crean campos nuevos ni se altera el movimiento de los objetos.

Validación: compilación conjunta y revisión del cálculo de destino, fases y limpieza de referencias. Pendiente evaluar tamaño, contraste y ubicación de las flechas en Play Mode. En el seguimiento del Excel conviene renombrar esta fila a «Mostrar la dirección del reorden magnético de Storage».

## 8. Espinas de Core por grupos

El evento reparte los obstáculos seleccionados entre dos grupos que se activan por turnos. Cada grupo tiene su propio aviso amarillo; solo el grupo activo aparece en rojo y causa daño. El anterior se desarma antes de encender el siguiente, incluso si un frame atraviesa el límite entre fases. Los obstáculos del grupo que todavía no avisa conservan su aspecto original.

Se conserva la duración total y la reserva de presión: con los valores actuales son 1,5 segundos de aviso, 2,75 activos, otro aviso de 1,5 y otros 2,75 activos. El aviso adicional consume parte del tiempo activo anterior. Se eligen hasta dos veces los candidatos configurados, pero como máximo una mitad está armada simultáneamente. Con un solo candidato se mantiene el ciclo anterior.

La distancia de seguridad al seleccionar obstáculos se mide desde su superficie, incluido el margen de espinas, para no considerar seguro un bloque largo cuyo centro esté lejos pero cuyo borde esté junto al jugador. Validación: trece pruebas de fases, límites, grupos y duraciones cortas; compilación conjunta aprobada. Pendiente ajustar la sensación de peligro en Play Mode.

## 6. Duración contextual de las compuertas de Lab

Antes de iniciar un cierre, Lab compara el área alcanzable desde el jugador con el área que quedaría al desplegar ambas compuertas. Incluye también los destinos de compuertas ya anunciadas para no evaluar cada cierre como si fuera el único. Usa copias de la grilla de navegación; probar o rechazar una posición no modifica la geometría real.

Si el cierre conserva al menos el 92 % del espacio disponible, respeta la duración sorteada de `gateActiveHoldRange`. Cuando restringe más el recorrido, reduce progresivamente esa duración hasta un 45 % del valor sorteado, con un piso contextual de tres segundos que nunca extiende una duración configurada más corta. Si conservaría menos del 55 % del área disponible, descarta esa posición y prueba otra.

Los límites se ajustan en `minimumGateReachableFraction` y `minimumContextualHoldSeconds`. El aviso, despliegue y retracción mantienen sus tiempos propios; el escáner se sincroniza con la duración final del cierre. La comprobación se realiza al programar el evento, no cada frame. Los desplazamientos posteriores del jugador y los eventos móviles pueden cambiar el área efectiva durante el aviso.

Validación: nueve pruebas de duración, rechazo, límites y copias de navegación, además de las once comprobaciones originales de conectividad. Compilación conjunta de gameplay aprobada. Pendiente Play Mode para evaluar frecuencia de cierres, especialmente en Ambient Overdrive.

## 5. Mostrar el origen y la dirección del barrido de Breach

El aviso previo identifica el borde de entrada del barrido con una banda amarilla dentro de los límites visibles y tres flechas animadas que indican su avance. Las flechas existentes hacia el portal siguen mostrando la ruta de escape. Al activarse el barrido, su frente letal y los fragmentos usan el rojo de peligro; el portal conserva su color propio.

La banda respeta un margen interior de 0,85 unidades para separarse de las paredes. No cambia la velocidad, el tiempo disponible ni las colisiones del barrido. Validación: compilación de gameplay y revisión de la dirección y el montaje del aviso. Pendiente comprobar encuadre y contraste en Play Mode para los cuatro bordes.

## 3. Coordinar combinaciones de amenazas

La admisión de eventos y la supresión de ataques mayores comparten el umbral de presión configurado del director. Antes, un evento como Core Spikes podía empezar encima de un ataque mayor porque su entrada exigía 0,8 de presión, mientras el bloqueo inverso empezaba en 0,6. Ahora se considera también la suma de los eventos ya reservados: varias amenazas pequeñas no pueden superar ese umbral encima de un estado mayor. Breach conserva su excepción de escape.

La reserva de Containment Pulse incluye los peligros adicionales de sectores avanzados. Si se interrumpe la creación de nuevos pulsos, mantiene la presión hasta que terminan los peligros que ya aparecieron. Luego libera la reserva, sin dejarla ocupada hasta un vencimiento artificial.

Validación: doce pruebas independientes de umbrales, combinaciones acumuladas, duraciones, escalado de sectores y valores límite; compilación conjunta de gameplay aprobada. Pendiente observar las combinaciones en Play Mode.

## 12. Evitar repeticiones inmediatas de estados

La selección normal recuerda cuándo terminó cada especial y lo excluye durante diez segundos desde su salida, incluso si entre ambos usos aparece BasePursuit. Se configura mediante `specialStateRepeatCooldown` en el apartado Anomaly State Machine del enemigo. La memoria se reinicia al comenzar el ritmo de una run. Las prioridades, desbloqueos y pesos de los estados disponibles permanecen vigentes, y WeaveHunter conserva su restricción específica de 45 segundos.

Si no queda un especial elegible, el enemigo usa su persecución base en vez de recurrir a un ataque que eluda la espera. Las herramientas de QA, la selección manual de versus y Destroyer de emergencia pueden forzar estados por sus propias reglas.

Validación: nueve comprobaciones independientes de disponibilidad inicial, salida inmediata, límites del enfriamiento, historial independiente, renovación de la espera, configuración deshabilitada, reloj detenido y reinicio. Compilación conjunta de gameplay aprobada. Pendiente observar la distribución de ataques y el ritmo en Play Mode.

## 2. Recuperación del enemigo atascado

La salida de un atasco conserva un destino temporal, en lugar de dejar que la persecución lo sobrescriba en el siguiente frame. Se prueban direcciones alrededor del enemigo mediante CircleCast, incluido el retroceso, con su radio de colisión y los límites de la arena. Se mantiene la velocidad del estado actual. Al llegar, agotarse el tiempo o bloquearse la salida se reconstruye la ruta hacia el jugador.

La detección distingue el desplazamiento real del acercamiento al jugador: rodear un obstáculo puede alejar al enemigo de su objetivo sin constituir un atasco. Destroyer de emergencia permanece como último recurso después de recuperaciones fallidas. Si A* no encuentra una ruta, se descarta la anterior para evitar seguir waypoints obsoletos.

El escape se cancela al cambiar de estado, entrar en Breach, desactivar al enemigo o pasar a control versus. Los movimientos especiales de nivel tres y la fusión de Split mantienen su propio control. La animación continúa actualizándose mientras el enemigo escapa.

Validación: compilación de gameplay y revisión del orden de actualización, limpieza de estados y comprobación de salida con el radio del enemigo. Pendiente Play Mode para comprobar esquinas, obstáculos móviles y tasas de activación de Destroyer en Rupture.

## 17. Aviso de vencimiento de Phase Dash

Durante los últimos tres segundos, el icono, la barra y el tiempo del HUD pulsan hacia amarillo de advertencia. La carcasa existente del jugador comparte esa señal a dos pulsos por segundo; el cuerpo permanece visible. El aviso usa tiempo de gameplay, de modo que se congela al pausar junto con la duración del powerup. No cambia el enfriamiento, las colisiones ni la duración de Phase Dash.

Validación: compilación de gameplay, revisión de los límites del aviso (solo mientras está activo y quedan como máximo tres segundos) y del uso de la misma señal en HUD y personaje. Pendiente observar el contraste y la lectura en Play Mode.

## 1. Rutas transitables y nodos accesibles

El generador comprueba la conectividad antes de crear cada obstáculo. Una grilla conservadora reserva espacio para el jugador y recorre las celdas libres mediante BFS, sin atravesar esquinas en diagonal. Se rechazan las colocaciones que dividen el espacio libre. Los obstáculos compuestos se comprueban como conjunto; los cuadrados girados usan su huella de colisión rotada.

Al iniciar un evento de nodos se captura la geometría de colisión actual, incluidos obstáculos móviles y compuertas sólidas activas. Los nodos se colocan únicamente en puntos alcanzables desde la posición del jugador. Si no se encuentra ninguno, se cancela ese intento y se libera su presupuesto de presión. La captura se calcula una sola vez por evento, no por frame ni por candidato.

El margen de navegación se configura mediante `navigationClearance` en `ProceduralArenaGenerator` (0,55 unidades por defecto). La grilla utiliza celdas de hasta 0,3 unidades; considera las cajas envolventes de los obstáculos, por lo que puede rechazar un paso estrecho aunque una forma redondeada permita atravesarlo.

Validación: compilación de los scripts de gameplay y 11 comprobaciones automatizadas de caminos abiertos, barreras completas, pasos estrechos, obstáculos compuestos, objetivos separados por compuertas, reconstrucción del alcance, límites de arena y esquinas diagonales. En Unity pueden ejecutarse desde `GLITCH > Validation > Arena reachability`.

Pendiente de comprobación visual en Play Mode: densidad de obstáculos y ritmo en las cinco arenas. El arranque automatizado del Editor estuvo bloqueado por su licencia local. La comprobación garantiza la conectividad de la distribución al generarse y la accesibilidad de los nodos al aparecer; los eventos que después cambian la geometría pueden modificar esas rutas y se revisarán en sus propias mejoras.
