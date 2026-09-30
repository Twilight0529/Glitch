# GLITCH — Dirección de arte

## Fantasía visual

Una instalación industrial de contención está siendo reescrita por una anomalía orgánica digital. El mundo combina arquitectura rígida, modular y funcional con una corrupción curva, viscosa y asimétrica.

## Jerarquía de lectura

1. Jugador y amenaza inmediata.
2. Peligros activos y objetivos interactivos.
3. Obstáculos que definen la ruta.
4. Arquitectura y materiales del sector.
5. Detalle ambiental.

El arte de fondo nunca debe competir en luminosidad o saturación con los tres primeros niveles.

## Gramática global

- Instalación: ángulos rectos, biseles, placas, uniones y tornillos.
- Jugador: geometría compacta, direccional y limpia.
- Anomalía: siluetas curvas, asimetría, masa oscura y ojos claros.
- Elementos interactivos: un núcleo de información cian o verde sobre una carcasa oscura.
- Peligros: bordes agresivos, puntas o cortes visibles incluso sin color.
- Escala: el detalle pequeño debe seguir siendo legible a la escala normal de cámara.
- Iluminación dibujada: dirección superior izquierda, sin sombras externas que falseen colliders.

## Color semántico

- Cian: información y orientación.
- Amarillo: anticipación o advertencia.
- Rojo: peligro activo o fallo.
- Verde: éxito, objetivo correcto o recompensa.
- Violeta: habilidad especial o alteración de reglas.
- Azul grisáceo: estructura neutral de la instalación.

Los colores semánticos se aplican desde el código. Los sprites base deben usar materiales oscuros y acentos contenidos para admitir esos cambios.

## Material común de la instalación

- Metal naval casi negro.
- Placas azul grisáceas desaturadas.
- Biseles con tres valores: sombra, base y borde iluminado.
- Tornillos y respiraderos como detalle secundario.
- Pantallas pequeñas; nunca grandes superficies de neón.
- Desgaste concentrado en juntas, no distribuido como ruido uniforme.

## Identidad por sector

### Containment Lab

Paneles clínicos, placas modulares, terminales empotradas, cápsulas y conductos ordenados. Composición ortogonal y repetición controlada. Cian como acento informativo.

### Storage Bay

Contenedores, rieles, grúas, juntas pesadas y superficies reparadas. Formas largas y agrupadas. Amarillo industrial solo en señalización no activa y con baja saturación.

### Rupture Zone

Fragmentos reconocibles de los otros sectores, bordes incompletos y vacío entre piezas. La corrupción rompe la cuadrícula sin convertir el mapa en ruido visual.

### Data Core

Placas de circuito, columnas, nodos y puentes simétricos. Mayor densidad geométrica, pero centros despejados para leer el movimiento.

### Null Archive

Bloques monumentales de datos, sellos, índices y grandes zonas negativas. Menos objetos, mayor peso visual y contraste reducido.

## Reglas de implementación

- El sprite artístico siempre está separado del collider.
- El contorno visible debe aproximarse al área de colisión, sin ocultarla ni ampliarla de manera engañosa.
- Los fondos usan sorting order menor que `-10`.
- Los obstáculos conservan sorting order `0`; sus detalles no superan los indicadores de gameplay.
- Los assets extensibles reservan el centro para nueve cortes y concentran el detalle en los bordes.
- Cada textura debe tener fallback geométrico para no romper una build si falta un recurso.
- No se agregan luces animadas para compensar una silueta débil: primero debe funcionar el dibujo estático.
- Las arenas comparten escala, perspectiva, iluminación y familia de materiales, pero no reutilizan piso, pared ni props principales. La coherencia proviene de reglas comunes; la identidad, de assets distintos.
- Cada sector se entrega como un `VisualKit` indivisible con piso, límite, obstáculo rectangular y prop redondeado. El generador solicita un único kit por arena para impedir mezclas accidentales.

## Kit visual implementado

- `lab_floor_panels.png`: piso modular repetible y de bajo contraste.
- `lab_obstacle_frame_v2.png`: panel opaco de nueve cortes para paredes y obstáculos.
- `lab_terminal_v2.png`: terminal cenital integrada a una placa de suelo.
- `storage_floor_v1.png`: cubierta logística con rieles, módulos de carga y marcas ámbar.
- `storage_boundary_v1.png`: bulkhead fijo con vías de grúa y estructura pesada.
- `storage_cargo_frame.png`: carga industrial reforzada, con cierres, rieles y señalización ámbar.
- `rupture_floor_v1.png`: suelo de instalación fracturado por vacío y corrupción orgánica.
- `rupture_boundary_v1.png`: muro de emergencia quebrado, reparado y contaminado por la grieta.
- `rupture_fragment_frame.png`: fragmento blindado partido por vacío y corrupción violeta.
- `core_floor_v1.png`: piso computacional simétrico con buses e indicadores verdes.
- `core_boundary_v1.png`: perímetro de servidores con refrigeración y troncales protegidas.
- `core_data_frame.png`: módulo computacional simétrico con buses, capacitores e indicadores verdes.
- `archive_floor_v1.png`: losas monumentales con canales e indexación lavanda.
- `archive_boundary_v1.png`: arquitectura monumental fija con conductos de memoria sellados.
- `archive_data_slab_frame.png`: bloque monolítico de memoria con índices sellados y luces lavanda.

Todos los assets comparten vista ortográfica, metal naval, iluminación superior izquierda y detalle pixel-art moderno. Los obstáculos rectangulares usan la pieza de su sector. Pilares, cápsulas y núcleos utilizan siluetas temáticas transparentes: tanque octogonal en Lab, tambor reforzado en Storage, fragmento irregular en Rupture, capacitor escalonado en Core y sello poligonal en Archive. Ninguno vuelve al círculo genérico.

## Prompts finales de producción

### Piso

Textura cuadrada y repetible de un laboratorio de contención cenital 2D, con paneles industriales modulares, metal naval casi negro, placas azul grisáceas, biseles, juntas, tornillos y pequeños indicadores cian. Pixel-art moderno, iluminación superior izquierda, bajo contraste, sin perspectiva, texto, personajes, peligros ni bloom.

### Panel de obstáculos

Panel blindado cuadrado, opaco y de borde a borde, preparado para nueve cortes. Esquinas mecánicas y rieles detallados concentrados en el 18 % exterior; centro amplio y uniforme. Vista cenital ortográfica, metal naval, acero azul grisáceo, indicadores cian mínimos, sin texto, margen, transparencia, perspectiva ni brillo excesivo.

### Terminal

Pedestal compacto de contención integrado a una placa de suelo cuadrada opaca. Cuerpo octogonal, canal central cian, dos pantallas oscuras y cuatro abrazaderas. Vista cenital ortográfica, pixel-art moderno, metal naval y acero desaturado, sin texto, margen, perspectiva, hologramas, cables ni bloom.

### Carga de Storage

Contenedor industrial reforzado de borde a borde, preparado para nueve cortes. Placas centrales amplias, rieles dobles, trabas de esquina, asas embutidas, luces de inventario ámbar y señalización de riesgo sin texto. Metal naval compartido, acero cálido desgastado, vista cenital ortográfica y sin perspectiva ni bloom.

### Fragmento de Rupture

Pieza blindada de la instalación partida y reescrita por una anomalía digital orgánica. Marco interrumpido, placas desplazadas, grietas con vacío y venas magenta-violeta contenidas. Borde de colisión rectangular legible, centro calmo para nueve cortes, vista cenital y sin escombros flotantes.

### Módulo de Data Core

Módulo computacional de borde a borde con canales simétricos, placas de procesamiento, conectores de bus, bancos de capacitores e indicadores verdes pequeños. Metal naval y acero teal, centro amplio para nueve cortes, vista cenital ortográfica, sin texto ni bloom.

### Bloque de Null Archive

Losa monolítica de almacenamiento digital con bordes pesados en capas, índices sellados, muescas dispersas, canales de memoria oscuros y luces lavanda mínimas. Sensación antigua y silenciosa, centro casi vacío para nueve cortes, vista cenital y sin partículas ni símbolos legibles.

### Piso de Storage

Textura opaca y repetible de una cubierta logística cenital: placas rectangulares grandes, rieles de carga empotrados, marcas de alineación de pallets, juntas gastadas, escotillas reforzadas e indicadores ámbar contenidos. Sin cajas, peligros, texto, perspectiva, marco ni punto focal central.

### Piso de Rupture

Textura opaca y repetible de la instalación rota por una anomalía digital orgánica: placas navales desplazadas, grietas estrechas de vacío, juntas interrumpidas y venas magenta-violeta escasas. Contraste bajo, distribución homogénea, sin escombros flotantes, símbolos, marco ni foco central.

### Piso de Data Core

Textura opaca y repetible de infraestructura computacional cenital: baldosas simétricas, buses de circuito embutidos, canales de procesamiento, conectores y pequeños indicadores verdes. Grandes superficies calmas para desplazamiento, sin pantallas, dígitos, marco, perspectiva ni bloom.

### Piso de Null Archive

Textura opaca y repetible de archivo digital monumental: losas de metal-piedra muy grandes, canales de memoria sellados, muescas de índice y uniones concéntricas sutiles con luces lavanda mínimas. Amplio espacio negativo, sin símbolos flotantes, texto, marco, perspectiva ni bloom.

### Pared de Storage

Bulkhead fijo de almacén, opaco y preparado para nueve cortes: rieles longitudinales, vías de grúa empotradas, refuerzos, ventilación e indicadores ámbar. Debe leerse como arquitectura y no como carga móvil; sin cajas, asas, perspectiva, texto ni bloom.

### Pared de Rupture

Muro fijo de contención parcialmente consumido por la grieta: nervios rotos, parches de emergencia, segmentos ausentes, vacío oscuro y corrupción violeta contenida en el borde. Silueta de colisión continua, sin escombros flotantes, texto ni brillo excesivo.

### Pared de Data Core

Perímetro fijo de servidores con troncales simétricas, canales protegidos, rejillas de refrigeración, bancos de conectores e indicadores verdes mínimos. Centro amplio para nueve cortes; sin procesador independiente, pantallas, dígitos, texto ni bloom.

### Pared de Null Archive

Marco monumental de archivo con dinteles pesados en capas, conductos de memoria sellados, muescas de índice, contrafuertes simétricos e indicadores lavanda mínimos. Debe distinguirse de las losas móviles; sin símbolos, texto, partículas ni perspectiva.

## Gramática visual de gameplay

La lectura de amenazas tiene una secuencia común en todas las arenas: amarillo para preparación y advertencia, rojo para ejecución y peligro activo, y retorno breve hacia un tono neutro durante la recuperación. La identidad de cada estado del enemigo no depende de sumar colores nuevos: se conserva mediante su silueta, glifo y patrón de movimiento.

Si la anomalía queda cubierta por un obstáculo, o si el jugador atraviesa uno mediante Phase Dash, el obstáculo reduce temporalmente su opacidad y luego recupera exactamente su aspecto original. El jugador y la anomalía conservan sus siluetas limpias, sin placas ni fondos adicionales debajo de sus cuerpos.

Para revisar esta gramática en una build de desarrollo: `F10` abre el panel de legibilidad, `Page Up/Page Down` recorren los estados de la anomalía y `F8` fuerza un evento compatible de la arena actual. Estas herramientas no se muestran ni responden en una build final.

### Lectura de objetos interactivos

Los objetos de gameplay conservan sus siluetas geométricas originales y se diferencian mediante color, escala, movimiento y contexto. Los powerups mantienen sus iconos específicos de velocidad, escudo, Compact y Phase Dash; los objetivos y eventos usan sus formas propias sin marcos ni glifos superpuestos.

Los peligros comunican primero su alcance y su fase: amarillo durante el aviso y rojo durante la activación. Los proyectiles mantienen una forma simple y su patrón de movimiento es el principal indicador del tipo de ataque.
