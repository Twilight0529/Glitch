# QA automatizado de gameplay

Esta herramienta existe únicamente en el Editor y en Development Builds. No se inicia sola ni modifica la progresión.

## Uso

1. Iniciar una run y cerrar el tutorial o menú de pausa.
2. Presionar `F10` para abrir el panel de desarrollo.
3. Elegir **Actual F7** para revisar la arena visible o **Total Shift+F7** para recorrer las cinco arenas.
4. Se puede cancelar con `F6`.

La prueba actual activa los nueve controladores de eventos disponibles y recorre todos los estados de la anomalía. La prueba total también reconstruye Lab, Storage, Rupture, Core y Archive de forma consecutiva, y al terminar vuelve al tema y al estado originales.

Mientras corre, el jugador recibe inmunidad técnica y se suspenden únicamente las pausas tutoriales y el presupuesto de eventos. Estas anulaciones se desactivan al terminar, cancelar o destruir el componente.

## Qué valida

- Raíces duplicadas o ausentes después de reconstruir una arena.
- Controladores y objetos temporales que pertenecen a un mapa anterior.
- Rutas entre el jugador, la anomalía y los objetivos mediante una malla independiente.
- Jugador, enemigo, pickups u objetivos dentro de obstáculos.
- Recompensas u objetivos superpuestos con peligros activos.
- Estados y eventos que rechazan su activación.
- Atascos prolongados de la anomalía y activaciones inesperadas de Destroyer.
- Errores y excepciones emitidos por Unity durante la prueba.
- FPS, frames lentos, memoria asignada y picos de objetos, colliders, proyectiles y pickups.

## Informes

Cada ejecución crea una versión legible `.txt` y otra estructurada `.json` en:

`Application.persistentDataPath/qa-reports`

El resultado distingue entre **CORRECTO**, **CON AVISOS**, **CON FALLOS** y **CANCELADO**. Los avisos señalan casos que requieren inspección; los fallos representan cobertura rota, una ruta inválida, errores de runtime o residuos de una transición.
