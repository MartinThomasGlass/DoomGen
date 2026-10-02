# Guía para generar mapas de Doom Companion

Este documento explica cómo escribir un **archivo de mapa** para *Doom Companion*, una app de escritorio que acompaña partidas de **Doom: The Boardgame** (Fantasy Flight Games, 2004, caja base sin expansión). La app oculta el mapa (niebla de guerra) y lo va revelando a medida que los marines abren puertas. Muestra los textos, qué piezas físicas colocar en la mesa, los monstruos y las recompensas.

> **Instrucción para la IA:** cuando te pidan un mapa, respondé **únicamente con el JSON** del mapa, sin texto antes ni después y sin comentarios dentro del JSON. Respetá el JSON Schema, el catálogo y **todas** las reglas de diseño de este documento. Antes de responder, revisá la lista de control del final.

---

## 1. Cómo funciona el juego en la app

1. Al empezar solo se ve el **área inicial** y las puertas que salen de ella.
2. De cada puerta solo se ve su **tipo**. No se sabe qué hay del otro lado.
3. Al tocar una puerta:
   - si se cumplen sus requisitos, se abre, se revela el área del otro lado y se muestra su texto de entrada, las piezas a colocar, los monstruos y los objetos;
   - si no se cumplen, se muestra el texto `bloqueada` (una pista) y no se revela nada.
4. El plano muestra, como en DoomGen, cada pieza y cada ficha en su casilla: monstruos (con el color de la figura), marines, objetos y escenografía. Es un plano de preparación: la partida se juega en la mesa. En el plano solo se tocan las puertas, los teleportadores y los **encuentros y cadáveres**, que al revisarlos muestran su texto y dan lo que tengan (por ejemplo una llave).
5. Los marines matan monstruos. Cuando un área queda sin monstruos vivos se **despeja**: se muestra el texto `despejar` y se otorgan sus **recompensas** (si tiene). Un área sin monstruos se despeja apenas se entra.
6. Las recompensas pueden dar objetos al inventario compartido del equipo, desbloquear puertas (de cualquier área), activar eventos o revelar información.
7. Los objetos colocados en un área entran al inventario cuando los marines los recogen.
8. Los **eventos manuales** los marca el jugador desde la app cuando ocurre algo en la mesa. Los no manuales solo los activa una recompensa.

**Lo central es que valga la pena entrar a cada área:** detrás de los monstruos tiene que haber algo que convenga (una llave, un arma buena, el camino para seguir). Ver 4.3.

---

## 2. Formato del archivo

JSON con `"version": 1`. Estructura general:

```
mapa
├─ version: 1
├─ escenario: id, titulo, dificultad, marines, ambientacion, textos{...}, areaInicial, condicionVictoria
├─ eventos[]: id, nombre, descripcion, manual
├─ objetosMision[]: id, nombre, descripcion, ficha
├─ areas[]: id, nombre, tiles[], textos{entrar, despejar}, monstruos[], objetos[], fichas[], recompensas[], notasInvasor
└─ puertas[]: id, desde, hacia, tipo, textos{abrir, bloqueada}, requisitos, posicion
```

### 2.1 Escenario

| Campo | Obligatorio | Descripción |
|---|---|---|
| `id` | sí | Identificador del escenario. |
| `titulo` | sí | Título visible. |
| `dificultad` | sí | `facil`, `media`, `dificil` o `pesadilla`. |
| `marines` | sí | Cantidad de jugadores marine, de 1 a 3 (el invasor no cuenta). |
| `ambientacion` | no | Descripción corta de la ambientación. |
| `textos.introduccion` | sí | Briefing que se lee a los marines al empezar. |
| `textos.objetivos` | sí | Qué tienen que lograr. |
| `textos.victoria` | sí | Texto de victoria. |
| `textos.derrota` | sí | Texto de derrota. |
| `textos.notasInvasor` | no | Notas privadas para el jugador invasor (ocultas por defecto). |
| `areaInicial` | sí | Id del área donde empiezan los marines. |
| `inicioMarines` | no | Casillas de inicio de los marines (hasta 3: rojo, verde, azul), dentro del área inicial. Ver 2.9. |
| `condicionVictoria` | sí | `{"tipo": "llegarAArea", "area": "..."}`, `{"tipo": "despejarArea", "area": "..."}` o `{"tipo": "evento", "evento": "..."}`. |

### 2.2 Áreas

Un área es una zona que se revela de una sola vez. Puede ocupar **varias piezas de mapa** (tiles).

| Campo | Descripción |
|---|---|
| `id` | Identificador único (`a1`, `a2`, ...). |
| `nombre` | Nombre visible. |
| `tiles[]` | Piezas físicas a colocar: `tipo` (id de pieza del catálogo), `x`, `y`, `rotacion` (0, 90, 180 o 270) y `nota` opcional. Ver 2.8. (`ancho`/`alto` solo hacen falta para piezas sin forma en el catálogo.) |
| `textos.entrar` | Se lee al revelar el área. |
| `textos.despejar` | Se lee al despejarla. |
| `monstruos[]` | `{"tipo": id de monstruo, "cantidad": n, "posiciones": [{"x", "y"}, ...]}`. Una posición por figura. |
| `objetos[]` | Objetos visibles en el área: `{"id": único, "tipo": id de objeto, "cantidad": n, "texto": opcional, "posiciones": [{"x", "y"}, ...]}`. Una posición por ficha. |
| `fichas[]` | Fichas de escenografía: `{"tipo": id de ficha, "cantidad": n, "nota": opcional, "posiciones": [{"x", "y", "rotacion"}, ...]}`. Los encuentros y cadáveres llevan además `texto` y `recompensas` (ver 2.10). |
| `recompensas[]` | Opcionales: se otorgan al despejar el área (ver 2.4). |
| `notasInvasor` | Opcional, solo para el invasor. |

### 2.3 Puertas y conexiones

Cada puerta conecta dos áreas (`desde`, `hacia`) y funciona en los dos sentidos.

| `tipo` | Pieza física | Notas |
|---|---|---|
| `normal` | Puerta normal | Sin requisitos implícitos. |
| `roja` / `azul` / `amarilla` | Puerta de seguridad | **Exige implícitamente** `llave-roja` / `llave-azul` / `llave-amarilla` en el inventario, además de lo que diga `requisitos`. |
| `evento` | Puerta normal | Compuerta que se abre por un evento. Sus `requisitos` deben mencionar al menos un evento. |
| `paso` | Ninguna | Pasillo abierto o abertura sin puerta física; suele llevar requisitos narrativos (por ejemplo, despejar el área). |
| `teleportador` | 2 fichas de teleportador | Conecta dos áreas lejanas. |

- `textos.abrir` (opcional): se lee al abrirla.
- `textos.bloqueada` (opcional): **pista** que se muestra si no se puede abrir. Tiene que orientar sin revelar lo que hay del otro lado.
- `posicion` (opcional): `{"x", "y", "orientacion": "horizontal" | "vertical"}`. La puerta ocupa 2 casillas sobre una línea de la cuadrícula. Si es horizontal, va sobre la línea `y` desde `x` hasta `x+2`; si es vertical, sobre la línea `x` desde `y` hasta `y+2`.

### 2.4 Recompensas

Todas llevan `tipo` y `texto` (lo que se lee al obtenerla).

| `tipo` | Campos extra | Efecto |
|---|---|---|
| `texto` | — | Solo narrativa o información. |
| `otorgarObjeto` | `objeto`, `cantidad` (opcional, 1 por defecto) | Agrega el objeto al inventario del equipo. |
| `desbloquearPuerta` | `puerta` | Esa puerta (de cualquier área) se abre aunque no se cumplan sus requisitos. |
| `activarEvento` | `evento` | Activa un evento. |
| `revelarInfo` | `area` (opcional) | Muestra el texto; si trae `area`, el nombre de esa área aparece en el mapa como "conocida" (sin revelar su contenido). |

### 2.5 Requisitos

Un requisito es un objeto con **exactamente una** de estas claves:

| Forma | Se cumple si... |
|---|---|
| `{"todas": [r1, r2, ...]}` | se cumplen todos. |
| `{"alguna": [r1, r2, ...]}` | se cumple al menos uno. |
| `{"areaDespejada": "a3"}` | esa área fue despejada. |
| `{"objeto": "llave-azul", "cantidad": 1}` | el inventario tiene esa cantidad (`cantidad` es opcional). |
| `{"puertaAbierta": "p4"}` | esa puerta ya se abrió. |
| `{"evento": "ev-energia"}` | ese evento está activo. |

No existe la negación: un camino nunca se vuelve a cerrar. Una puerta sin `requisitos` (y que no sea de seguridad) se abre siempre.

### 2.6 Eventos y objetos de misión

- `eventos[]`: `{"id", "nombre", "descripcion", "manual": true|false}`. Si es manual, el jugador lo marca en la app; la `descripcion` tiene que explicar **cuándo** marcarlo (por ejemplo, "si un marine pasa un turno en la consola"). Si no es manual, solo lo activa una recompensa `activarEvento`.
- `objetosMision[]`: objetos propios del escenario que no están en el catálogo (discos de datos, muestras, etc.). Se colocan en `areas[].objetos` con su id como `tipo`, y se pueden pedir en requisitos. Indicá en `ficha` qué ficha física los representa (normalmente `encuentro`).

### 2.7 Identificadores

Usá solo minúsculas sin tildes, números, `-` y `_`, sin espacios. Convención: áreas `a1, a2...`, puertas `p1, p2...`, objetos colocados `o1, o2...` (únicos en todo el mapa), eventos `ev-nombre`.

### 2.8 Plano: coordenadas, rotación y conexiones

El mapa es una cuadrícula de casillas. `x` crece hacia la derecha (este) e `y` hacia abajo (sur). Las coordenadas no pueden ser negativas.

- **Posición de una pieza:** `(x, y)` es la casilla superior izquierda del rectángulo que ocupa la pieza **ya rotada**.
- **Rotación:** en grados, sentido horario. La rotación 0 es la orientación del catálogo (la misma de las imágenes de DoomGen). En la sección 3 tenés cada pieza dibujada en todas sus rotaciones, con sus medidas: no hace falta calcular nada.
- **Conexiones:** cada pieza tiene aberturas de 2 casillas sobre su borde (marcadas `N`/`E`/`S`/`O` en los dibujos). Las piezas **solo** se unen por ahí: dos piezas quedan conectadas cuando una conexión de una cae exactamente sobre la misma línea que una conexión de la otra, en lados opuestos (este de una con oeste de la otra, o sur con norte).
- **Línea de conexión:** cada conexión ocupa una línea de la cuadrícula de 2 casillas de largo. `horizontal (X, Y)` es la línea horizontal `y = Y` que va de `x = X` a `x = X+2`. `vertical (X, Y)` es la línea vertical `x = X` que va de `y = Y` a `y = Y+2`. La tabla de cada pieza da esa línea relativa a la posición `(x, y)` de la pieza.
- **Puertas:** una puerta entre dos áreas va sobre la línea de conexión donde se unen una pieza de cada área, y su `posicion` es exactamente esa línea (`{"x": X, "y": Y, "orientacion": "horizontal" | "vertical"}`). Todas las puertas llevan `posicion`, menos los teleportadores.
- **Dentro de un área:** sus piezas se unen entre sí por conexiones sin puerta.

Ejemplo: una `sala-10x5-tres` en `(10, 9)` con rotación 180 tiene su conexión `este@1` en la línea `vertical (20, 10)`. Un `pasillo-largo` en `(20, 10)` con rotación 0 tiene `oeste@0` en `vertical (20, 10)`: las dos piezas se unen ahí, y si son de áreas distintas la puerta va en `{"x": 20, "y": 10, "orientacion": "vertical"}`.

### 2.9 Fichas en el plano

Cada ficha va en una casilla del plano, con las mismas coordenadas que las piezas. `x`, `y` es la casilla superior izquierda que ocupa la ficha.

- **Monstruos:** una posición por figura en `posiciones`. Tamaño según la columna "Casillas" del catálogo:
  - 1 casilla: trite, zombie, imp, archvile;
  - 2 casillas: demon, que ocupa (x, y) y (x+1, y), o con `"rotacion": 90` ocupa (x, y) y (x, y+1);
  - 4 casillas: mancubus, hell knight y cyberdemon, que ocupan un bloque de 2×2 desde (x, y).
- **Objetos:** una posición por ficha en `posiciones` (1 casilla cada una). Con `"cantidad": 2` van dos posiciones distintas.
- **Escenografía:** una posición por ficha en `posiciones`. Las fichas de 1×2 o 1×3 son verticales en rotación 0; con `"rotacion": 90` quedan horizontales.
- **Marines:** `escenario.inicioMarines`, hasta 3 casillas dentro del área inicial.
- **Color de los monstruos:** no se indica. La app lo reparte entre los colores de los marines que juegan.

Reglas de ubicación:

- Toda ficha tiene que caer dentro de las casillas de su área. **Nunca dos fichas en la misma casilla**: si hay 2 fichas de munición, van en 2 casillas.
- No tapes las casillas de conexión ni los callejones con escenografía: ahí van las puertas.
- Los monstruos van entre la entrada y lo que custodian. Lo valioso (llaves, armas buenas, objetos de misión) va del otro lado de los monstruos, lejos de la puerta por la que se entra.
- Los marines empiezan juntos, lejos de los monstruos del área inicial.
- Si falta alguna posición, la app ubica esa ficha sola en una casilla libre. Igual conviene indicarlas todas.

### 2.10 Encuentros y cadáveres

Los encuentros (`encuentro`, el signo de pregunta) y los cadáveres (`cadaver`, 1×2) son fichas de escenografía que los marines **revisan**. En la app se tocan: se lee su `texto` y, la primera vez, se otorgan sus `recompensas` (los mismos tipos que en 2.4). Ejemplo:

```json
{
  "tipo": "cadaver", "cantidad": 1, "posiciones": [{ "x": 22, "y": 10, "rotacion": 90 }],
  "texto": "El cuerpo de un técnico. Todavía tiene enganchada al cinturón una tarjeta amarilla.",
  "recompensas": [{ "tipo": "otorgarObjeto", "objeto": "llave-amarilla", "texto": "Obtienen la tarjeta amarilla." }]
}
```

- Son ideales para llaves, objetos de misión y pistas. Un objeto de misión casi siempre va en un encuentro.
- Un cadáver o encuentro sin recompensa igual puede tener texto: una pista, ambientación o una advertencia.
- Ponelos del otro lado de los monstruos, como todo lo valioso.

---

## 3. Catálogo de piezas y fichas

Estos son los **únicos** ids válidos para `tiles[].tipo`, `monstruos[].tipo`, `objetos[].tipo` y `fichas[].tipo` (más los ids de `objetosMision` del propio mapa). Las cantidades son las de la caja: no las superes.

{{CATALOGO}}

---

## 4. Reglas de diseño (obligatorias)

### 4.1 Alcanzabilidad
- Toda área tiene que poder alcanzarse desde `areaInicial` abriendo puertas.
- La condición de victoria tiene que poder cumplirse: si es `llegarAArea` o `despejarArea`, esa área tiene que ser alcanzable; si es `evento`, ese evento tiene que poder activarse.
- Asumí que los marines pueden matar todo y recoger todo lo que alcanzan.

### 4.2 Sin softlocks
- **Nunca** pongas una llave u objeto requerido **solo** detrás de la puerta que lo pide, ni lo otorgues solo como recompensa de un área detrás de esa puerta.
- Nada de dependencias circulares: si la puerta A necesita algo que está detrás de la puerta B, B no puede necesitar algo que está detrás de A.
- Una puerta no puede requerir despejar el área a la que lleva.
- Todo evento no manual tiene que ser activado por alguna recompensa alcanzable.
- Si un requisito pide `cantidad` N de un objeto, tiene que haber al menos N en áreas alcanzables antes de esa puerta.
- Recorré mentalmente el mapa desde el área inicial y verificá que siempre haya al menos una puerta que se pueda abrir hasta llegar a la victoria.

### 4.3 Que valga la pena entrar a cada área
- **Cada área con monstruos tiene que valer la pena.** Tiene que hacer al menos una de estas cosas:
  - llevar hacia adelante, con otra puerta hacia el resto del mapa;
  - tener algo valioso: una llave, un arma buena, armadura, un objeto de misión o una recompensa importante.
- **Nada de salas grandes llenas de monstruos, sin salida y con equipo que no sirve.** Un callejón sin salida con mucha amenaza tiene que esconder algo que convenga: típicamente una llave o un arma pesada.
- **Lo valioso va detrás de los monstruos**, del lado opuesto a la puerta por la que se entra (ver 2.9).
- **El botín acompaña a la amenaza.** La app mide cada área con la amenaza de sus monstruos (catálogo) y el **valor** de lo que se consigue (columna "valor" de los objetos; los objetos de misión valen 6; desbloquear una puerta o activar un evento, 4). Un callejón sin salida no debería tener un valor menor que la mitad de su amenaza.
- **Las recompensas por despejar son una herramienta, no una obligación.** Sirven para crear rutas alternativas: por ejemplo, una puerta de color que se abre con la llave **o** se desbloquea al despejar otra área.

### 4.4 Curva de dificultad
Medí cada área con la **amenaza** de sus monstruos: la suma de `amenaza × cantidad` (ver la tabla de monstruos del catálogo). Presupuesto orientativo por área para **2 marines** (multiplicá por 0,6 con 1 marine y por 1,4 con 3):

| Dificultad | Primeras áreas | Áreas intermedias | Área final |
|---|---|---|---|
| `facil` | 2–4 | 4–6 | 7–9 |
| `media` | 3–5 | 5–8 | 9–12 |
| `dificil` | 4–6 | 7–10 | 12–15 |
| `pesadilla` | 5–8 | 9–13 | 15–20 |

- La dificultad sube a lo largo del camino principal, con algún respiro (un área tranquila con buen equipo) en el medio.
- El área inicial es la más tranquila.
- Monstruos grandes (mancubus, hell knight, cyberdemon) solo en salas donde entren: ocupan 4 casillas.
- Cyberdemon solo en el área final y solo en dificultad `media` o mayor.
- **Respetá las figuras disponibles**: las figuras vienen en 3 colores, y las del color de un marine que no juega vuelven a la caja. Disponibles = (cantidad ÷ 3) × marines. Ningún área puede pedir más de eso.
- Áreas totales recomendadas: 5–7 (`facil`), 6–8 (`media`), 7–10 (`dificil`/`pesadilla`).

### 4.5 Distribución del equipo
- **Cada arma va acompañada de su munición** (ver la columna "munición" del catálogo), en la misma área o en una anterior del camino.
- Munición: entre 1 y 3 fichas por área. Más en las áreas previas a una pelea fuerte.
- Botiquines: aproximadamente 1 cada 2 áreas, y siempre uno antes del área final.
- Armas pesadas (rifle de plasma, BFG) en la segunda mitad del mapa. Como máximo un BFG por mapa.
- La escopeta o la ametralladora son buenas recompensas tempranas.
- Armadura, adrenalina y berserk: pocas, como recompensa de áreas difíciles.
- No superes las cantidades de la caja (sumando objetos colocados y objetos otorgados por recompensa).

### 4.6 Piezas físicas y plano
- No superes las piezas de la caja (cantidad de cada `tipo` de tile en todo el mapa), ni las puertas: puertas normales para `normal` y `evento`, y una sola puerta de seguridad de cada color.
- Cada conexión `teleportador` usa 2 fichas de teleportador.
- **Ninguna casilla puede estar ocupada por dos piezas.** Verificá las casillas de cada pieza con los dibujos de la sección 3.
- Las piezas se unen **solo por conexiones** (ver 2.8). Las piezas de un área tienen que formar un bloque conectado.
- Donde se unen piezas de dos áreas distintas tiene que haber una puerta (o un `paso`) con esa `posicion`. Si no, los marines pasarían sin abrir nada.
- **Toda conexión que no se una con otra pieza se tapa con un `callejon`** (callejón sin salida) en la rotación que corresponda: no puede quedar ninguna abertura libre.
- **Teleportadores:** unen áreas que quedan lejos entre sí y que no tienen ya un camino corto entre ellas. Un teleportador que lleva al mismo lugar al que ya lleva un pasillo es redundante y no aporta nada. Van como una conexión `teleportador` (sin `posicion`) más una ficha `teleportador-<color>` en cada una de las dos áreas, del mismo color.
- Pensá el plano en papel antes de escribir el JSON: ubicá primero el área inicial, después las áreas vecinas a través de sus conexiones, y al final los callejones.
- Los pasillos y las curvas sirven para ajustar distancias: un pasillo corto suma 3 casillas, uno largo 6, y una sala 4×4 o una cruz pasan derecho sumando 4.

### 4.7 Textos
- Todo en español, en segunda persona del plural ("ustedes"), con tono de horror de ciencia ficción, en 2 a 4 oraciones.
- `entrar`: describe el lugar y deja entrever a los monstruos presentes.
- `despejar`: cierra la pelea y adelanta la recompensa.
- `bloqueada`: una pista útil que no revele el contenido del otro lado.
- `notasInvasor`: consejos tácticos para el invasor (dónde usar apariciones, qué amenaza reservar).
- Objetivos claros: los marines tienen que saber qué buscar.

---

## 5. Preparación de los marines (referencia del manual)

{{PREPARACION}}

---

## 6. JSON Schema completo

```json
{{ESQUEMA}}
```

---

## 7. Ejemplo completo y válido

Escenario de 6 áreas que usa todas las mecánicas: puertas de los 3 colores, compuerta por evento, paso, teleportador, requisitos `todas`/`alguna`/`puertaAbierta`, evento manual y no manual, objeto de misión y los cinco tipos de recompensa. El plano usa piezas reales: no hay superposiciones, todas las puertas están sobre conexiones y cada conexión sobrante está tapada con un callejón.

```json
{{EJEMPLO}}
```

---

## 8. Lista de control antes de responder

- [ ] El JSON es válido, sin comentarios, y respeta el schema (`version: 1`, campos obligatorios, sin campos extra).
- [ ] Todos los ids usados existen (catálogo, áreas, puertas, eventos, objetos de misión) y los ids propios no se repiten.
- [ ] Todas las áreas son alcanzables y la victoria se puede cumplir.
- [ ] Ninguna llave ni objeto requerido está solo detrás de su propia puerta; no hay ciclos.
- [ ] Cada área con monstruos vale la pena: lleva hacia adelante o tiene algo valioso detrás de los monstruos. Ningún callejón sin salida con mucha amenaza y poco botín.
- [ ] La amenaza sigue la curva de la dificultad pedida y no se superan las figuras disponibles para la cantidad de marines.
- [ ] Cada arma tiene su munición; no se superan las cantidades de la caja.
- [ ] Ninguna pieza se superpone con otra; las piezas se unen solo por conexiones; toda conexión libre está tapada con un callejón.
- [ ] Toda puerta (salvo teleportadores) tiene `posicion` sobre la conexión donde se unen sus dos áreas, y no hay áreas que se toquen por una conexión sin puerta.
- [ ] Cada monstruo, objeto, ficha de escenografía y marine tiene su casilla dentro de su área, sin encimarse (una ficha por casilla).
- [ ] Los encuentros y cadáveres tienen `texto`, y lo que dan está en `recompensas`. Los teleportadores unen áreas lejanas.
- [ ] Todos los textos están escritos y en español.

## 9. Cómo pedir un mapa

> Generá un mapa de dificultad **media** para **2 marines** con ambientación **laboratorio de clonación inundado**. Respondé solo con el JSON.
