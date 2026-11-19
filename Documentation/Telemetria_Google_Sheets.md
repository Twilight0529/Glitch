# Telemetría de GLITCH en Google Sheets

La integración está implementada en el proyecto, pero el envío queda **desactivado** hasta que publiques tu receptor y completes su configuración. No se creó ni se accedió a una hoja de tu cuenta desde Codex.

## Qué te permite analizar

Cada partida registrada deja una fila, aunque no sea un récord del leaderboard. Incluye versión, plataforma, estilo/clase, puntaje, supervivencia, recogidas, powerups, Parrys, Firewall, brechas, contratos y mejoras. El contexto de finalización conserva la causa, la arena, el estado del jefe y la posición del jugador aceptados al terminar.

Un identificador aleatorio local permite agrupar instalaciones, y otro distingue aperturas de la aplicación. El campo local `sessionId` identifica en realidad una **run**; en la hoja se llama `runId`. No se envían nombre del ranking, correo, UID/token de Firebase, contraseñas, archivos, trazas ni datos del dispositivo distintos de la plataforma de Unity.

El panel `GLITCH_Resumen` muestra:

- Partidas, instalaciones y sesiones de aplicación recibidas.
- Puntaje medio y supervivencia media/mediana.
- Porcentaje de abandonos y de muertes antes de 60 segundos.
- Parrys por minuto y porcentaje de instalaciones vistas en más de un día.
- Desglose por arena/causa de final, estado del jefe, clase y versión/plataforma.

Eso ayuda a detectar muertes tempranas, dificultades por arena y habilidades poco usadas. **No mide satisfacción, no demuestra causalidad y no calcula retención D1/D7.** Las instalaciones no equivalen a personas, las sesiones sin ninguna run recibida no se cuentan, las distribuciones de final no son el tiempo de exposición a cada estado y una caída abrupta del proceso puede no generar un final. No es un sistema de crash reporting ni de medición de FPS. Los datos enviados por el cliente no son prueba antitrampas.

## Configuración, paso a paso

### 1. Crear la hoja y pegar el receptor

1. En tu navegador, abrí Google Sheets y creá una **hoja en blanco** llamada, por ejemplo, `GLITCH - Telemetría`.
2. Dejala **privada**: no hace falta publicar el documento ni compartirlo con los jugadores.
3. Dentro de la hoja, abrí **Extensiones → Apps Script**.
4. En el archivo `Código.gs`/`Code.gs`, reemplazá la función de ejemplo por el contenido completo de [Code.gs](../Tools/GoogleSheetsTelemetry/Code.gs).
5. Guardá el proyecto, por ejemplo como `GLITCH Telemetry`.
6. En el selector de funciones de Apps Script, elegí **`setupTelemetry`** y pulsá **Ejecutar**.
7. Revisá y autorizá los permisos que Google solicite para este script que acabás de crear. No tenés que darle tu contraseña a GLITCH ni a Codex.
8. Volvé a la hoja: deben existir `GLITCH_Runs` y `GLITCH_Resumen`. Si no aparece el menú **GLITCH**, recargá la hoja.

`setupTelemetry` no borra las runs existentes. Si encuentra encabezados incompatibles, se detiene en lugar de sobreescribirlos. Está diseñado para ejecutarse desde una hoja vinculada al script.

### 2. Obtener la clave del receptor

1. Volvé al editor de Apps Script.
2. Abrí **Configuración del proyecto** (engranaje).
3. En **Propiedades de la secuencia de comandos / Script properties**, buscá:
   - `SPREADSHEET_ID`: lo completó el configurador. No hay que copiarlo al juego.
   - `INGEST_KEY`: una cadena aleatoria generada por el configurador. Copiá su valor para el JSON de Unity.

No uses una contraseña personal ni un token OAuth como `INGEST_KEY`. Esta clave es solo una barrera ligera contra envíos casuales: **queda dentro de la build y se puede extraer**. La hoja privada y sus permisos no dependen de mantener esa clave como un secreto de servidor.

### 3. Publicar el receptor

1. En Apps Script, pulsá **Implementar → Nueva implementación**.
2. Elegí el tipo **Aplicación web**.
3. En **Ejecutar como**, elegí **Yo / el propietario**.
4. En **Quién tiene acceso**, elegí **Cualquier persona**, sin exigir inicio de sesión. El juego no inicia sesión en tu cuenta de Google.
5. Implementá y copiá la URL de la aplicación web. Debe empezar por `https://script.google.com/macros/s/` y terminar en **`/exec`**.
6. Abrila en una ventana privada del navegador: debe responder un JSON con `GLITCH telemetry`. Ese GET solo confirma acceso al endpoint; la escritura se comprueba con una run real.

No uses la URL de la hoja, la del editor ni la que termina en `/dev`. Si tu cuenta educativa/Workspace no permite acceso anónimo, necesitás consultar al administrador o elegir un receptor admitido por tu organización; no se resuelve desprotegiendo el documento. Las opciones de ejecución y acceso pertenecen al [despliegue web de Apps Script](https://developers.google.com/apps-script/guides/web).

### 4. Configurar Unity

Editá [google-sheets-telemetry.json](../Assets/Resources/google-sheets-telemetry.json):

```json
{
  "enabled": true,
  "endpoint": "https://script.google.com/macros/s/ID_DE_TU_IMPLEMENTACION/exec",
  "ingestKey": "PEGA_EL_VALOR_COMPLETO_DE_INGEST_KEY",
  "sendFromEditor": false,
  "batchSize": 5,
  "requestTimeoutSeconds": 15
}
```

Reemplazá ambos valores de ejemplo por los reales. Reiniciá Play Mode para recargar la configuración; para distribuirla, generá una build nueva. No se agrega SDK de Google ni archivo de credenciales.

Para probar dentro de Unity, cambiá temporalmente **`sendFromEditor` a `true`**. Para distribuir, conviene volver a `false`, así tus sesiones de editor no contaminan el análisis. Las runs marcadas con el banco de QA se excluyen del envío incluso si habilitás el editor.

### 5. Dar consentimiento y comprobar una run

1. Iniciá el juego y abrí **Opciones → Datos de juego**.
2. Activá **Compartir partidas**. Viene desactivado por defecto.
3. Jugá una run normal y terminá la partida o abandonala mediante el menú.
4. Con conexión y el receptor disponible, debería aparecer una fila en `GLITCH_Runs` después del envío. No depende del récord de Firebase: una run de un punto también es válida.
5. En Google Sheets, elegí **GLITCH → Actualizar resumen** para recalcular `GLITCH_Resumen`.

No se sube automáticamente el historial local anterior a la activación. El resumen se actualiza a pedido, no con cada POST, para evitar releer y recalcular toda la hoja por cada partida.

## Funcionamiento y límites

El registro local `telemetry/runs.jsonl` y `summary.json` sigue funcionando independientemente. Al cerrar una run elegible, se genera un resumen para la cola `telemetry/sheets_pending.json`, en `Application.persistentDataPath`. No se envían eventos por frame ni se consulta la hoja desde el juego.

- Hasta 100 runs pendientes; si se llena, conserva las más nuevas. Retención de pendientes de 30 días.
- Lotes de hasta 5; timeout de 15 segundos por defecto, acotado entre 5 y 30.
- Reintentos con espera creciente y variación aleatoria, desde unos 30 segundos hasta unos 15 minutos. No se bloquea el gameplay ni se espera una respuesta al cerrar la aplicación.
- La cola se guarda al agregar una run y al confirmar envíos. Al volver a abrir, se reintentan pendientes válidos si siguen habilitados el servicio y el consentimiento.
- Solo se retiran IDs reconocidos del lote enviado: aceptados —incluidos duplicados ya guardados— o rechazados explícitamente por datos inválidos. Los fallos de conexión, permisos, configuración o cuota conservan los pendientes dentro de sus límites.
- El receptor usa un [bloqueo de script](https://developers.google.com/apps-script/reference/lock/lock-service) para evitar carreras entre clientes y busca los IDs antes de agregar filas. Un reintento tras perder la respuesta no duplica la run.
- Por lote nuevo realiza una escritura rectangular de valores y prepara el formato de IDs/fechas; expande filas cuando hace falta. No hay lecturas periódicas desde los clientes.
- El receptor está acotado a 2.500 filas nuevas por día y 50.000 filas de historial. Son límites de esta implementación, no una promesa sobre la [cuota disponible de Google](https://developers.google.com/apps-script/guides/services/quotas). Al alcanzarlos conserva los datos y rechaza nuevas filas para reintento; archivá/cambiá de hoja o revisá los límites como desarrollador.

La respuesta de [ContentService se redirige a `script.googleusercontent.com`](https://developers.google.com/apps-script/guides/content); el cliente admite esos redirects. **Esta integración directa es para builds nativas/Editor.** En WebGL el envío queda deshabilitado: hace falta un proxy con CORS adecuado, no un `fetch` opaco que impida verificar la confirmación.

## Privacidad, QA y seguridad

El envío requiere simultáneamente configuración válida y consentimiento. Desactivarlo aborta el intento activo y borra la cola pendiente; además cambia la generación del consentimiento, para no restaurar una cola antigua aunque falle el borrado del archivo. Reinicia el identificador local para una futura activación. Un POST que ya haya llegado al servidor puede quedar registrado aunque luego se aborte la espera.

**Los datos ya recibidos en Google no se borran al resetear progreso ni al desactivar el envío.** Se administran y eliminan desde la hoja privada del desarrollador. El historial remoto no tiene purga automática. El resumen agrupa solo el historial que conserve esa hoja. Reset opciones desactiva el consentimiento; borrar progreso elimina los pendientes y reinicia el identificador, pero conserva la preferencia de envío como las demás opciones no relacionadas con tutoriales.

Los identificadores son seudónimos, no garantía de anonimato absoluto. El proveedor de red puede procesar información de la conexión fuera de las columnas guardadas por este script. No se envían coordenadas fuera del mundo del juego ni ubicación geográfica.

Un receptor público de Apps Script con clave embebida es apropiado para una prueba académica o un grupo pequeño de testers, no una defensa fuerte contra un atacante que tenga la build. Incluye validación de tamaño/tipos, límites, bloqueo y neutralización de fórmulas en texto externo; no autentica de manera fuerte la veracidad del jugador. Para una publicación masiva, conviene un backend con autenticación, controles de abuso y administración de borrado/retención.

## Si no llegan filas

1. Comprobá `enabled`, URL `/exec`, clave completa, consentimiento y `sendFromEditor` si estás en Play Mode.
2. Confirmá que no sea una run del banco de QA.
3. Revisá el estado en **Datos de juego** y la consola. No se imprimen URL, clave, payload ni respuesta cruda.
4. Una página HTML de login indica acceso restringido o un endpoint incorrecto; no es una confirmación de recepción.
5. `Clave del receptor incorrecta`: compará `INGEST_KEY` del script con el JSON. `Revisar configuración del receptor`: ejecutá el configurador y revisá la pestaña **Ejecuciones** de Apps Script. `Límite del receptor`: revisá filas, tope diario y cuotas.
6. Si cambiaste código de Apps Script, publicá una **nueva versión** desde Administrar implementaciones. Guardar el archivo solo no actualiza la versión desplegada.
7. No borres `GLITCH_Runs` para “refrescar” el panel: los IDs almacenados ahí permiten deduplicar reintentos. Si archivás historial, un cliente con un pendiente de ese mismo historial podría volver a insertarlo; conservá una ventana suficiente para la retención local o cambiá también de despliegue/clave.

## Verificaciones realizadas

- Pruebas deterministas de la cola: duplicados, respuestas parciales/perdidas, confirmaciones de IDs no enviados, retención y límite offline.
- Pruebas del receptor real con dobles de los servicios de Google: validación, permiso ligero, deduplicación tras fallo posterior a escritura, concurrencia, capacidad, crecimiento de filas, protección de fórmulas y agregación.
- Compilación conjunta del código de gameplay.

**Pendiente hasta configurar tu cuenta:** prueba extremo a extremo contra tu despliegue real y revisión de la pantalla de consentimiento en Play Mode. Las pruebas con dobles no validan permisos, cuotas, redirects ni latencia de tu cuenta de Google.
