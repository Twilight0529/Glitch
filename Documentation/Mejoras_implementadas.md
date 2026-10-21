# Implementación de las 30 ideas

Cada mejora se implementa y registra en un commit separado. Los archivos del Excel se conservan para el seguimiento manual.

## 15. Dirección visible del proyectil reflejado

Cada reflexión puede mostrar una flecha verde breve delante del proyectil durante 0,28 segundos. La señal sigue su velocidad real, incluidas correcciones posteriores de trayectoria, y conserva un tamaño de mundo legible aunque el sprite del proyectil sea pequeño. No reemplaza sus iconos ni cambia hacia dónde se refleja.

Se limita a cuatro señales simultáneas para no saturar la arena durante reflejos múltiples. El efecto se desvanece y se elimina al vencer, destruirse o desactivarse el proyectil. Su reloj respeta la pausa. Validación: compilación conjunta y revisión del seguimiento, límite y liberación del efecto. Pendiente comprobar legibilidad y duración en Play Mode.

## 11. Preparación y ejecución reales del jefe

La firma visual del jefe antes pasaba de amarillo a rojo mediante un reloj decorativo de entrada. Ese reloj no coincidía con los ataques que se repiten dentro del mismo estado. Ahora Expansion Shoot, Phase Blink, Pincer Barrage, Signal Jam, Orbit Barrage y Map Recompile exponen si su aviso real está activo. La firma reaparece en amarillo durante cada carga y muestra rojo al ejecutarse; su deformación también sigue esas fases en vez del porcentaje transcurrido del estado.

Se conserva el estilo y los sprites del slime. Los seis ataques revisados no aparentan una recuperación al final del reloj del estado si todavía están activos. Los demás estados mantienen sus señales nativas y su identidad; esta mejora no añade nuevas ventanas de vulnerabilidad ni altera duraciones o ataques.

Validación: compilación conjunta y revisión de cada flag o telegraph que alimenta la presentación. Pendiente comparar señales y repetición de cargas en Play Mode. La idea 13 sigue pendiente de definir: el aturdimiento actual suspende la persecución, pero las habilidades continúan actualizándose, por lo que no debe presentarse como una oportunidad segura general.

## 10. Alcance legible de los campos de Archive

El dibujo anterior usaba un radio independiente de 2,8 unidades que además se encogía al pulsar, mientras la fuerza alcanzaba 3,6 unidades. Ahora el borde permanece fijo en `influenceRadius`, incluyendo sus modificaciones en Caos. Un relleno tenue usa el mismo factor de caída radial que la física: se intensifica hacia el centro. El amarillo identifica el aviso y el rojo el campo activo; solo respira el núcleo.

Un sprite procedural compartido y cacheado reemplaza la superficie anterior. No se añaden capas decorativas ni se cambian las fuerzas, duraciones o radios efectivos. El borde representa el alcance de entrada del jugador: los objetos ya capturados pueden seguir orbitando fuera, y los obstáculos conservan su regla de captura más amplia.

Validación: compilación conjunta y revisión del uso del mismo radio y factor de caída en dibujo y fuerza. Pendiente comprobar los campos superpuestos y el contraste en Play Mode.

## 4. Margen al llegar a un sector

Ya existían una llegada de tres segundos antes de reinsertar al enemigo, una gracia de reinserción y seis segundos de recuperación del director. Sin embargo, las compuertas ambientales de Lab, las grúas de Storage y las grietas pasivas de Rupture podían iniciar operaciones sin consultar esas protecciones.

Ahora esos sistemas posponen nuevos eventos durante la supresión sensible de Breach y la ventana de llegada configurada mediante `sectorArrivalRecoverySeconds`. La ventana usa el tiempo de gameplay, se congela al pausar y se limpia al iniciar otra run. También funciona cuando el director híbrido está deshabilitado. Las operaciones ya iniciadas conservan su finalización normal: no se congela una carga a mitad de movimiento.

La mejora no añade otra pausa al control del jugador ni retrasa el primer mapa de una run. Las herramientas de debug pueden seguir forzando eventos para probarlos. Validación: siete pruebas del reloj de protección, límites y reinicio, compilación conjunta y revisión de los tres puntos de programación ambiental. Pendiente observar la llegada a cada sector en Play Mode.

## 7. Conservar pasos durante SpinMotion de Rupture

La formación orbital comprueba su próximo paso físico completo antes de mover los obstáculos. Una grilla reutilizable valida que las huellas barridas, con sus rotaciones y curvatura, mantengan conectado el espacio transitable. Si el paso cerraría un corredor o alcanzaría una pared, se detiene antes de aplicarlo y desacelera para invertir el giro, con una espera mínima de 0,4 segundos entre correcciones.

La protección directa del jugador se verifica aparte contra su collider real y las formas rotadas de los objetos móviles. El contacto con paredes u objetos estáticos no bloquea la formación por quedar fuera de la holgura conservadora de navegación. Phase Dash intangible conserva su excepción física. No se eliminan props ni se teletransportan: se conserva el radio inicial para evitar el salto causado por recortarlo al empezar el evento.

El movimiento de SpinMotion se aplica en FixedUpdate; las demás variantes de Rupture conservan su funcionamiento. Si la distribución inicial ya quedó desconectada por cambios anteriores, se pospone SpinMotion antes de anunciarlo y se restauran los controladores de objetos. La comprobación es conservadora: puede detener o invertir el giro antes de que un paso se estreche demasiado.

Validación: doce pruebas de conectividad, espacio del jugador, obstáculos diagonales y límites de holgura; compilación conjunta aprobada. Mil comprobaciones de la grilla reutilizada con 21 obstáculos tomaron aproximadamente 80 ms en la ejecución local (no representa el coste completo del evento). Pendiente Play Mode para revisar fluidez, duración de correcciones y frecuencia efectiva de aparición.

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
