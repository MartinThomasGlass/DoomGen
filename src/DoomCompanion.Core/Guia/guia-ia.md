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
4. Los marines matan monstruos. Cuando un área queda sin monstruos vivos se **despeja**: se muestra el texto `despejar` y se otorgan sus **recompensas**. Un área sin monstruos se despeja apenas se entra.
5. Las recompensas pueden dar objetos al inventario compartido del equipo, desbloquear puertas (de cualquier área), activar eventos o revelar información.
6. Los objetos colocados en un área entran al inventario cuando los marines los recogen.
7. Los **eventos manuales** los marca el jugador desde la app cuando ocurre algo en la mesa. Los no manuales solo los activa una recompensa.

**La mecánica central es que convenga despejar cada área.** Las recompensas son el motor del mapa.

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
| `condicionVictoria` | sí | `{"tipo": "llegarAArea", "area": "..."}`, `{"tipo": "despejarArea", "area": "..."}` o `{"tipo": "evento", "evento": "..."}`. |

### 2.2 Áreas

Un área es una zona que se revela de una sola vez. Puede ocupar **varias piezas de mapa** (tiles).

| Campo | Descripción |
|---|---|
| `id` | Identificador único (`a1`, `a2`, ...). |
| `nombre` | Nombre visible. |
| `tiles[]` | Piezas a colocar: `tipo` (id de tile del catálogo), `x`, `y` (casilla de la esquina superior izquierda), `rotacion` (0, 90, 180 o 270), y opcionalmente `ancho`, `alto` (en casillas, ya rotado) y `nota`. |
| `textos.entrar` | Se lee al revelar el área. |
| `textos.despejar` | Se lee al despejarla. |
| `monstruos[]` | `{"tipo": id de monstruo, "cantidad": n}`. |
| `objetos[]` | Objetos visibles en el área: `{"id": único, "tipo": id de objeto, "cantidad": n, "texto": opcional}`. |
| `fichas[]` | Fichas de escenografía a colocar: `{"tipo": id de ficha, "cantidad": n, "nota": opcional}`. |
| `recompensas[]` | Se otorgan al despejar el área (ver 2.4). |
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

### 4.3 Recompensas por despejar
- **Toda área tiene al menos una recompensa.** Tiene que convenir despejarla.
- La recompensa crece con el peligro del área: un área con monstruos fuertes debería dar armas, llaves, desbloqueos o atajos.
- Combiná tipos: objetos (`otorgarObjeto`), apertura de puertas de otras áreas (`desbloquearPuerta`), eventos (`activarEvento`) e información útil (`revelarInfo`).
- Usá recompensas para crear **rutas alternativas**: por ejemplo, una puerta de color que se abre con la llave **o** se desbloquea al despejar otra área.
- Al menos la mitad de las puertas que no son la primera deberían depender de algo que se gana despejando áreas (llave, evento, área despejada).

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

### 4.6 Piezas físicas
- No superes las puertas disponibles: puertas normales para `normal` y `evento`, y una sola puerta de seguridad de cada color.
- Cada conexión `teleportador` usa 2 fichas de teleportador.
- Las piezas de un mismo mapa no se superponen. Las áreas conectadas por una puerta se tocan justo en la línea donde está la puerta.
- Mientras el catálogo no tenga medidas de las piezas, indicá `ancho` y `alto` aproximados de cada tile para que la app pueda dibujar el plano.

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

Escenario de 6 áreas que usa todas las mecánicas: puertas de los 3 colores, compuerta por evento, paso, teleportador, requisitos `todas`/`alguna`/`puertaAbierta`, evento manual y no manual, objeto de misión y los cinco tipos de recompensa.

```json
{{EJEMPLO}}
```

---

## 8. Lista de control antes de responder

- [ ] El JSON es válido, sin comentarios, y respeta el schema (`version: 1`, campos obligatorios, sin campos extra).
- [ ] Todos los ids usados existen (catálogo, áreas, puertas, eventos, objetos de misión) y los ids propios no se repiten.
- [ ] Todas las áreas son alcanzables y la victoria se puede cumplir.
- [ ] Ninguna llave ni objeto requerido está solo detrás de su propia puerta; no hay ciclos.
- [ ] Cada área tiene al menos una recompensa.
- [ ] La amenaza sigue la curva de la dificultad pedida y no se superan las figuras disponibles para la cantidad de marines.
- [ ] Cada arma tiene su munición; no se superan las cantidades de la caja.
- [ ] Todos los textos están escritos y en español.

## 9. Cómo pedir un mapa

> Generá un mapa de dificultad **media** para **2 marines** con ambientación **laboratorio de clonación inundado**. Respondé solo con el JSON.
