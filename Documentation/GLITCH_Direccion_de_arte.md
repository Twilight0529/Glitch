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
- Todas las arenas comparten el suelo modular y el marco perimetral de la instalación. Los obstáculos internos son propios de cada sector y luego se tiñen, cubren o deterioran mediante su paleta y sus motivos; así existe continuidad sin homogeneizar los mapas.
- Lab usa el material común casi limpio, Storage lo calienta y desgasta, Rupture lo deja apenas visible bajo la corrupción, Core lo integra con circuitos y Archive lo enfría bajo sus anillos de datos.

## Kit visual implementado

- `lab_floor_panels.png`: piso modular repetible y de bajo contraste.
- `lab_obstacle_frame_v2.png`: panel opaco de nueve cortes para paredes y obstáculos.
- `lab_terminal_v2.png`: terminal cenital integrada a una placa de suelo.
- `storage_cargo_frame.png`: carga industrial reforzada, con cierres, rieles y señalización ámbar.
- `rupture_fragment_frame.png`: fragmento blindado partido por vacío y corrupción violeta.
- `core_data_frame.png`: módulo computacional simétrico con buses, capacitores e indicadores verdes.
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
