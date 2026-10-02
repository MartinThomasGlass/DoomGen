# Doom Companion

Aplicación de escritorio para Windows que acompaña partidas de **Doom: The Boardgame** (Fantasy Flight Games, 2004) jugadas en mesa con las piezas físicas.

La app guarda el mapa del escenario y lo va revelando de a poco (niebla de guerra). Dibuja el plano como DoomGen, con cada pieza y cada ficha en su casilla. Muestra los textos para leer en voz alta y lleva el inventario del equipo.

Todo funciona en local: sin internet, sin servidor y sin IA integrada. Los mapas se escriben en JSON. Se pueden hacer a mano o pedírselos a una IA externa (por ejemplo, Claude) con la **guía para IA** que exporta la app.

---

## Índice

1. [Instalar y compilar](#1-instalar-y-compilar)
2. [Imágenes de las piezas (DoomGen)](#2-imágenes-de-las-piezas-doomgen)
3. [Cómo se juega con la app](#3-cómo-se-juega-con-la-app)
4. [Generar mapas con una IA](#4-generar-mapas-con-una-ia)
5. [Formato del archivo de mapa](#5-formato-del-archivo-de-mapa)
6. [Validación al importar](#6-validación-al-importar)
7. [Catálogo de piezas](#7-catálogo-de-piezas)
8. [Archivos y datos](#8-archivos-y-datos)
9. [Estructura del proyecto](#9-estructura-del-proyecto)

---

## 1. Instalar y compilar

### Requisitos para compilar

- Windows 10 u 11 (x64).
- [SDK de .NET 10](https://dotnet.microsoft.com/download).

Quien instala la app **no** necesita .NET: el instalador incluye todo.

### Generar el instalador

Desde la raíz del repositorio:

```bash
powershell -ExecutionPolicy Bypass -File instalador\compilar-instalador.ps1 -Version 1.0.0
```

El script:

1. corre los tests (si alguno falla, no genera nada);
2. publica la app autocontenida para `win-x64`;
3. la empaqueta con [Velopack](https://velopack.io). `vpk` está declarado como herramienta local en `dotnet-tools.json` y se restaura solo.

Resultado en la carpeta `Releases\`:

| Archivo | Para qué sirve |
|---|---|
| `DoomCompanion-win-Setup.exe` | **Instalador.** Instala en el perfil del usuario, sin pedir permisos de administrador. Crea accesos directos en el escritorio y en el menú Inicio, y se puede desinstalar desde "Agregar o quitar programas". |
| `DoomCompanion-win-Portable.zip` | Versión sin instalar: se descomprime y se ejecuta `DoomCompanion.exe`. |

El instalador no está firmado digitalmente. La primera vez, Windows SmartScreen puede mostrar "Windows protegió su PC". Para continuar: **Más información → Ejecutar de todos modos**.

### Desarrollo

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project src/DoomCompanion.App
```

---

## 2. Imágenes de las piezas (DoomGen)

El plano usa las imágenes del set de DoomGen: piezas de mapa, puertas, monstruos en sus tres colores, marines, objetos y escenografía. **Las imágenes no vienen con la app** (son arte de Fantasy Flight Games): la app las lee de una carpeta de tu PC.

- **Ubicación que busca sola:** `Escritorio\DoomGen\doom`, `%APPDATA%\DoomCompanion\imagenes` o una carpeta `imagenes` al lado del ejecutable.
- **Otra ubicación:** **⋯ Más → Elegir carpeta de imágenes de DoomGen…** y elegí la carpeta que tiene `4x4_room.png`, `imp_red.png`, etc.
- **Si falta alguna imagen:** el plano dibuja esa pieza o ficha por su cuenta (placas de metal, fichas redondas con sigla).

## 3. Cómo se juega con la app

La interfaz está pensada para un monitor o una TV horizontal:

- mapa a la izquierda;
- área seleccionada en el centro;
- inventario, eventos e historial a la derecha.

| Atajo | Acción |
|---|---|
| **F11** | Pantalla completa (Esc para salir) |
| **Ctrl +** / **Ctrl −** | Agrandar o achicar toda la interfaz |
| **Enter** / **Espacio** | Siguiente texto del lector |
| **Esc** | Cerrar el lector |
| **Ctrl O** | Importar mapa |
| **Ctrl S** | Guardar partida |

### Flujo de una partida

1. **Importar mapa** (o *Jugar el escenario de ejemplo*). La app valida el archivo y muestra "Mapa válido" o "Se detectó un problema de lógica", sin revelar nada.
2. Se lee el **briefing** y los **objetivos** en el lector a pantalla grande.
3. Solo se ve el **área inicial** y sus puertas. De cada puerta se ve solo el tipo:
   - normal (gris);
   - de seguridad roja, azul o amarilla;
   - por evento (violeta);
   - paso abierto (marrón, punteado);
   - teleportador (círculo celeste).
4. **El plano muestra la escena como DoomGen:** cada monstruo con el color de su figura, los marines en su posición de inicio, los objetos y la escenografía, una ficha por casilla. Es un plano de preparación: lo que pasa después se juega en la mesa, y el plano no cambia al matar monstruos ni al levantar objetos. El panel **Colocar en la mesa** dice lo mismo en texto.
   - En el plano se tocan solo las puertas, los teleportadores y los **encuentros (?) y cadáveres**. Al revisar uno se lee su texto y, la primera vez, se obtiene lo que tenga (por ejemplo una llave). Los que faltan revisar brillan; los revisados quedan atenuados.
5. **Clic en una puerta** (en el mapa o en el botón *Abrir* del panel):
   - si se cumplen sus requisitos, se abre y se revela el área del otro lado con su texto;
   - si no, aparece la pista de puerta bloqueada y no se revela nada.
6. **Monstruos:** marcá en el panel los que mueren (sirve para despejar el área). Con **+ Agregar aparición** se suman monstruos que trae el invasor (los agregados se pueden quitar con ✕). En **Colores de los marines** elegís qué colores juegan: la app reparte entre esos colores el color de cada monstruo, y te dice qué figura poner.
7. Cuando no quedan monstruos vivos, el área queda **despejada**: se lee su texto y se otorgan sus **recompensas** (objetos al inventario, puertas desbloqueadas, eventos, información).
8. **Objetos:** si querés llevar el inventario en la app, se marcan como recogidos en el panel. Lo importante para las puertas son las llaves, que suelen venir de encuentros, cadáveres o recompensas. El inventario también se ajusta a mano con − / +, por ejemplo para descontar munición.
9. **Eventos manuales:** se marcan en el panel *Eventos* cuando pasan en la mesa. Pueden abrir puertas o dar la victoria.
10. **Victoria / Derrota:** botones en la barra superior. Si la condición de victoria es despejar un área o activar un evento, la victoria se declara sola.

Otros detalles:

- **Notas del invasor:** están ocultas. El botón pide confirmación antes de mostrarlas, y también muestra las notas de cada área revelada.
- **Historial:** registra todo lo que pasó. Con doble clic se vuelve a leer una entrada en grande.
- **Guardar / Cargar:** archivos `.doomsave`. Además, la partida se **autoguarda después de cada acción**, y al abrir la app aparece *Continuar la última partida*.
- **Reiniciar:** vuelve el escenario al principio (pide confirmación).

---

## 4. Generar mapas con una IA

1. En la app: **⋯ Más → Exportar guía para IA…** (o el botón de la pantalla de inicio). Se genera un `.md` con:
   - el formato completo del mapa;
   - el JSON Schema;
   - el catálogo de piezas con cantidades;
   - las reglas de diseño (alcanzabilidad, sin softlocks, recompensas por despejar, curva de dificultad, distribución de equipo, límites físicos de la caja);
   - un ejemplo completo y válido.
2. En un chat con Claude, pegá la guía (o adjuntá el archivo) y pedí, por ejemplo:

   > Generá un mapa de dificultad **difícil** para **3 marines** con ambientación **refinería de plasma en una luna helada**. Respondé solo con el JSON.

3. Guardá la respuesta como `mi-mapa.json` e importala. La app tolera que la respuesta venga dentro de un bloque ```` ```json ```` o con texto alrededor.
4. Si la validación encuentra errores, el botón **📋 Copiar errores** copia la lista junto con la instrucción de corregirlos. Se puede pegar tal cual en el mismo chat.

La guía usa **tu catálogo actual**. Si editás el catálogo (por ejemplo, para cargar las medidas de las piezas), volvé a exportar la guía.

---

## 5. Formato del archivo de mapa

La referencia completa está en la guía para IA y en [`datos/esquema-mapa.v1.json`](datos/esquema-mapa.v1.json) (JSON Schema draft 2020-12). El ejemplo completo está en [`datos/ejemplos/escenario-ejemplo.json`](datos/ejemplos/escenario-ejemplo.json).

```
mapa (version: 1)
├─ escenario: id, titulo, dificultad, marines (1-3), ambientacion,
│             textos { introduccion, objetivos, victoria, derrota, notasInvasor },
│             areaInicial, condicionVictoria, inicioMarines[] { x, y }
├─ eventos[]:        id, nombre, descripcion, manual
├─ objetosMision[]:  id, nombre, descripcion, ficha
├─ areas[]:   id, nombre, tiles[] { tipo, x, y, rotacion },
│             textos { entrar, despejar },
│             monstruos[] { tipo, cantidad, posiciones[] }, objetos[] { id, tipo, cantidad, posiciones[] },
│             fichas[] { tipo, cantidad, posiciones[], texto, recompensas[] }, recompensas[], notasInvasor
└─ puertas[]: id, desde, hacia, tipo, textos { abrir, bloqueada }, requisitos, posicion
```

- **Tipos de puerta:** `normal`, `roja`, `azul`, `amarilla` (exigen la llave de su color sin necesidad de declararla), `evento`, `paso`, `teleportador`.
- **Requisitos:** un árbol con `todas` / `alguna`, cuyas hojas pueden ser `areaDespejada`, `objeto` (+`cantidad`), `puertaAbierta` y `evento`. No existe la negación: así nunca se cierra un camino y la validación de softlocks es exacta.
- **Recompensas:** `texto`, `otorgarObjeto`, `desbloquearPuerta` (sirve para puertas de cualquier área), `activarEvento`, `revelarInfo` (si trae `area`, su nombre aparece en el mapa como "conocida").
- **Condición de victoria:** `llegarAArea`, `despejarArea` o `evento`.
- **Fichas:** cada monstruo, objeto, ficha de escenografía y marine lleva su casilla en `posiciones` (el demon y las fichas alargadas aceptan `"rotacion": 90`). Una ficha por casilla. Las que no la traen, la app las ubica sola. Los encuentros y cadáveres llevan `texto` y `recompensas`.
- **Plano:** cada pieza va en `(x, y)` (esquina superior izquierda de la pieza ya rotada) con `rotacion` 0/90/180/270 en sentido horario. Las piezas se unen solo por sus **conexiones** (aberturas de 2 casillas). La `posicion` de una puerta es la línea de conexión donde se unen una pieza de cada área: `horizontal` = línea `y`, de `x` a `x+2`; `vertical` = línea `x`, de `y` a `y+2`.

---

## 6. Validación al importar

1. **Esquema:** errores en español con la ruta exacta. Por ejemplo: `areas[1] (a2).tiles[0].rotacion: Valor no permitido 45. Valores válidos: [0,90,180,270].`
2. **Referencias:** que no haya ids repetidos y que existan todas las áreas, puertas, eventos y elementos del catálogo que se mencionan.
3. **Lógica** (simulación optimista hasta que no cambie nada):
   - todas las áreas son alcanzables;
   - la salida o la victoria se pueden alcanzar;
   - ninguna llave u objeto requerido está solo detrás de su propia puerta (**SOFTLOCK**);
   - no hay dependencias circulares entre puertas;
   - no faltan objetos ni eventos que nunca se activan;
   - como advertencia, áreas que no valen la pena: un callejón sin salida con mucha amenaza y poco botín, o un área con mucha amenaza y casi nada para agarrar. Se compara la amenaza de los monstruos con el valor de lo que hay (llaves, armas, objetos de misión, recompensas).
4. **Piezas y balance** (siempre advertencias):
   - figuras disponibles según la cantidad de marines;
   - piezas, puertas y fichas por encima de lo que trae la caja;
   - armas sin su munición.
5. **Plano** (siempre advertencias):
   - piezas superpuestas;
   - conexiones libres, que hay que tapar con un callejón sin salida;
   - puertas que no están sobre una conexión entre sus dos áreas, o que no tienen posición;
   - áreas que se tocan por una conexión sin puerta;
   - piezas de una misma área que no quedan unidas entre sí;
   - fichas fuera de su área o encimadas.

Por defecto solo se muestra el resumen. **Ver detalle (spoiler)** pide confirmación antes de revelar el detalle. Los errores de formato se muestran directamente porque hacen falta para corregir el archivo.

---

## 7. Catálogo de piezas

Archivo: [`datos/catalogo.json`](datos/catalogo.json). Contiene la caja base (sin expansión).

### Piezas de mapa

Las formas y conexiones salen de las imágenes de DoomGen (64 px por casilla), y la **rotación 0 es la orientación de esas imágenes**.

| id | Pieza | Cantidad |
|---|---|---|
| `sala-grande` | Sala grande 9×10 | 1 |
| `sala-10x5-tres` | Sala 10×5 de tres salidas | 1 |
| `sala-10x5-dos` | Sala 10×5 de dos salidas (franjas en los bordes) | 1 |
| `sala-9x5` | Sala 9×5 de tres salidas (irregular) | 1 |
| `sala-5x5` | Sala 5×5 de cuatro salidas (en molinete) | 3 |
| `sala-4x4` | Sala 4×4 | 5 |
| `interseccion-cruz` | Intersección en cruz 4×4 | 2 |
| `interseccion-t` | Intersección en T 4×3 | 4 |
| `curva-grande` | Curva grande 4×4 en L | 2 |
| `curva-chica` | Curva chica 3×3 | 3 |
| `pasillo-corto` | Pasillo corto 3×2 | 10 |
| `pasillo-largo` | Pasillo largo 6×2 | 4 |
| `callejon` | Callejón sin salida 1×2 | 22 |

Cada pieza tiene `forma` (filas con `#` = casilla y `.` = vacío), `conexiones` (aberturas de 2 casillas: lado y primera columna o fila, desde 0) e `imagen`. Los callejones son 22 según DoomGen y la hoja visual; el manual dice 21.

### Resto de los componentes

- **Puertas:** 11 normales y 3 de seguridad.
- **Monstruos:** trite, zombie e imp ×12; demon, archvile, mancubus y hell knight ×6; cyberdemon ×3.
- **Escenografía:** obstáculos de 1×1, 1×2 y 1×3, residuos, cadáveres, ductos, barriles, encuentros y teleportadores rojos, azules y amarillos. Muchas son de doble cara: no se pueden usar las dos caras de la misma ficha a la vez.
- **Equipo** (cantidades del set de DoomGen): 3 de cada arma; munición 21 de balas, 15 de cohetes y 9 de celdas; 12 botiquines, 15 fichas de armadura (las mismas de la armadura inicial de los marines), 3 berserk y 3 adrenalinas. Cada objeto tiene un **valor** orientativo que usa la validación.
- **Figuras por color:** las figuras vienen en 3 colores, y las del color de un marine que no juega vuelven a la caja. La app calcula las disponibles como `cantidad ÷ 3 × marines`.

Para editarlo: **⋯ Más → Editar catálogo de piezas…**. Se copia a `%APPDATA%\DoomCompanion\catalogo.json` y se abre. Después de guardar, usá **Recargar catálogo**: la app revisa que las formas y las conexiones sean coherentes. Si cambiás el catálogo, volvé a exportar la guía para IA.

## 8. Archivos y datos

| Qué | Dónde |
|---|---|
| Catálogo editable | `%APPDATA%\DoomCompanion\catalogo.json` (si no existe, se usa el que viene con la app) |
| Autoguardado | `%APPDATA%\DoomCompanion\autoguardado.doomsave` |
| Partidas guardadas | Donde elijas (`.doomsave`). Cada archivo incluye una copia del mapa y su hash, así que no hace falta conservar el `.json` original. |

---

## 9. Estructura del proyecto

```
DoomCompanion/
├─ datos/                         Esquema, catálogo y ejemplo (se embeben en la app)
├─ src/
│  ├─ DoomCompanion.Core/         Lógica sin interfaz
│  │  ├─ Modelo/                  Mapa, requisitos, catálogo
│  │  ├─ Validacion/              Esquema, referencias, lógica, inventario, importador
│  │  ├─ Motor/                   Motor de juego y estado de la partida
│  │  ├─ Persistencia/            Guardado y carga (.doomsave)
│  │  └─ Guia/                    Generador de la guía para IA
│  └─ DoomCompanion.App/          Interfaz WPF (MVVM con CommunityToolkit.Mvvm)
├─ tests/DoomCompanion.Core.Tests/  Tests xUnit de puertas, requisitos, recompensas, validación, guardado y guía
└─ instalador/compilar-instalador.ps1
```
