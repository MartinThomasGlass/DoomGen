# Doom Companion

Aplicación de escritorio para Windows que acompaña partidas de **Doom: The Boardgame** (Fantasy Flight Games, 2004) jugadas en mesa con las piezas físicas.

La app guarda el mapa del escenario y lo va revelando de a poco (niebla de guerra). Muestra los textos para leer en voz alta, qué pieza colocar en la mesa y dónde, qué monstruos y objetos aparecen, y lleva el inventario del equipo. También otorga las recompensas por despejar cada área.

Todo funciona en local: sin internet, sin servidor y sin IA integrada. Los mapas se escriben en JSON. Se pueden hacer a mano o pedírselos a una IA externa (por ejemplo, Claude) con la **guía para IA** que exporta la app.

---

## Índice

1. [Instalar y compilar](#1-instalar-y-compilar)
2. [Cómo se juega con la app](#2-cómo-se-juega-con-la-app)
3. [Generar mapas con una IA](#3-generar-mapas-con-una-ia)
4. [Formato del archivo de mapa](#4-formato-del-archivo-de-mapa)
5. [Validación al importar](#5-validación-al-importar)
6. [Catálogo de piezas](#6-catálogo-de-piezas)
7. [Archivos y datos](#7-archivos-y-datos)
8. [Estructura del proyecto](#8-estructura-del-proyecto)

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

## 2. Cómo se juega con la app

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
4. El panel **Colocar en la mesa** dice qué piezas poner (tipo, posición en la cuadrícula y rotación), qué monstruos, qué objetos y qué fichas.
5. **Clic en una puerta** (en el mapa o en el botón *Abrir* del panel):
   - si se cumplen sus requisitos, se abre y se revela el área del otro lado con su texto;
   - si no, aparece la pista de puerta bloqueada y no se revela nada.
6. **Monstruos:** cada uno tiene su casilla. Marcá los que mueren. Con **+ Agregar aparición** se suman monstruos que trae el invasor (los agregados se pueden quitar con ✕).
7. Cuando no quedan monstruos vivos, el área queda **despejada**: se lee su texto y se otorgan sus **recompensas** (objetos al inventario, puertas desbloqueadas, eventos, información).
8. **Objetos:** se marcan como recogidos y pasan al inventario compartido. El inventario también se ajusta a mano con − / +, por ejemplo para descontar munición.
9. **Eventos manuales:** se marcan en el panel *Eventos* cuando pasan en la mesa. Pueden abrir puertas o dar la victoria.
10. **Victoria / Derrota:** botones en la barra superior. Si la condición de victoria es despejar un área o activar un evento, la victoria se declara sola.

Otros detalles:

- **Notas del invasor:** están ocultas. El botón pide confirmación antes de mostrarlas, y también muestra las notas de cada área revelada.
- **Historial:** registra todo lo que pasó. Con doble clic se vuelve a leer una entrada en grande.
- **Guardar / Cargar:** archivos `.doomsave`. Además, la partida se **autoguarda después de cada acción**, y al abrir la app aparece *Continuar la última partida*.
- **Reiniciar:** vuelve el escenario al principio (pide confirmación).

---

## 3. Generar mapas con una IA

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

## 4. Formato del archivo de mapa

La referencia completa está en la guía para IA y en [`datos/esquema-mapa.v1.json`](datos/esquema-mapa.v1.json) (JSON Schema draft 2020-12). El ejemplo completo está en [`datos/ejemplos/escenario-ejemplo.json`](datos/ejemplos/escenario-ejemplo.json).

```
mapa (version: 1)
├─ escenario: id, titulo, dificultad, marines (1-3), ambientacion,
│             textos { introduccion, objetivos, victoria, derrota, notasInvasor },
│             areaInicial, condicionVictoria
├─ eventos[]:        id, nombre, descripcion, manual
├─ objetosMision[]:  id, nombre, descripcion, ficha
├─ areas[]:   id, nombre, tiles[] { tipo, x, y, rotacion },
│             textos { entrar, despejar }, monstruos[], objetos[], fichas[],
│             recompensas[], notasInvasor
└─ puertas[]: id, desde, hacia, tipo, textos { abrir, bloqueada }, requisitos, posicion
```

- **Tipos de puerta:** `normal`, `roja`, `azul`, `amarilla` (exigen la llave de su color sin necesidad de declararla), `evento`, `paso`, `teleportador`.
- **Requisitos:** un árbol con `todas` / `alguna`, cuyas hojas pueden ser `areaDespejada`, `objeto` (+`cantidad`), `puertaAbierta` y `evento`. No existe la negación: así nunca se cierra un camino y la validación de softlocks es exacta.
- **Recompensas:** `texto`, `otorgarObjeto`, `desbloquearPuerta` (sirve para puertas de cualquier área), `activarEvento`, `revelarInfo` (si trae `area`, su nombre aparece en el mapa como "conocida").
- **Condición de victoria:** `llegarAArea`, `despejarArea` o `evento`.
- **Plano:** cada pieza va en `(x, y)` (esquina superior izquierda de la pieza ya rotada) con `rotacion` 0/90/180/270 en sentido horario. Las piezas se unen solo por sus **conexiones** (aberturas de 2 casillas). La `posicion` de una puerta es la línea de conexión donde se unen una pieza de cada área: `horizontal` = línea `y`, de `x` a `x+2`; `vertical` = línea `x`, de `y` a `y+2`.

---

## 5. Validación al importar

1. **Esquema:** errores en español con la ruta exacta. Por ejemplo: `areas[1] (a2).tiles[0].rotacion: Valor no permitido 45. Valores válidos: [0,90,180,270].`
2. **Referencias:** que no haya ids repetidos y que existan todas las áreas, puertas, eventos y elementos del catálogo que se mencionan.
3. **Lógica** (simulación optimista hasta que no cambie nada):
   - todas las áreas son alcanzables;
   - la salida o la victoria se pueden alcanzar;
   - ninguna llave u objeto requerido está solo detrás de su propia puerta (**SOFTLOCK**);
   - no hay dependencias circulares entre puertas;
   - no faltan objetos ni eventos que nunca se activan;
   - como advertencia: áreas sin recompensa.
4. **Piezas y balance** (siempre advertencias):
   - figuras disponibles según la cantidad de marines;
   - piezas, puertas y fichas por encima de lo que trae la caja;
   - armas sin su munición.
5. **Plano** (siempre advertencias):
   - piezas superpuestas;
   - conexiones libres, que hay que tapar con un callejón sin salida;
   - puertas que no están sobre una conexión entre sus dos áreas, o que no tienen posición;
   - áreas que se tocan por una conexión sin puerta;
   - piezas de una misma área que no quedan unidas entre sí.

Por defecto solo se muestra el resumen. **Ver detalle (spoiler)** pide confirmación antes de revelar el detalle. Los errores de formato se muestran directamente porque hacen falta para corregir el archivo.

---

## 6. Catálogo de piezas

Archivo: [`datos/catalogo.json`](datos/catalogo.json). Contiene la caja base (sin expansión).

### Piezas de mapa

Las formas, las conexiones y las cantidades salen de la hoja de referencia visual de la caja. Suman las 58 del manual:

| id | Pieza | Cantidad |
|---|---|---|
| `sala-10x9` | Sala grande 10×9 | 1 |
| `sala-10x5-sur` | Sala 10×5 con salida al sur | 1 |
| `sala-10x5` | Sala 10×5 de paso (franjas en los bordes) | 1 |
| `sala-9x5` | Sala 9×5 con tres salidas (irregular) | 1 |
| `sala-5x5` | Sala 5×5 de cuatro salidas (en molinete) | 3 |
| `sala-4x4` | Sala 4×4 | 5 |
| `interseccion-cruz` | Intersección en cruz 4×4 | 2 |
| `interseccion-t` | Intersección en T 4×3 | 4 |
| `curva-grande` | Curva grande 4×4 en L | 2 |
| `curva-chica` | Curva chica 3×3 | 3 |
| `pasillo-corto` | Pasillo corto 2×3 | 10 |
| `pasillo-largo` | Pasillo largo 2×6 | 4 |
| `callejon` | Callejón sin salida 1×2 | 21 |

Cada pieza tiene:

- **`forma`:** filas de arriba hacia abajo, con `#` para cada casilla y `.` para el vacío.
- **`conexiones`:** aberturas de 2 casillas, indicadas con el lado y la primera columna o fila (contando desde 0).

La **rotación 0** es la orientación en que la pieza aparece en la hoja de referencia.

Hay dos cosas para confirmar con las piezas reales:

- **Sala grande 10×9:** está marcada `"aVerificar"`, porque en la imagen sus conexiones se leían con menos claridad.
- **Callejones:** la hoja muestra 22 y el manual dice 21. El catálogo usa 21.

### Resto de los componentes

- **Puertas:** 11 normales y 3 de seguridad.
- **Monstruos:** trite, zombie e imp ×12; demon, archvile, mancubus y hell knight ×6; cyberdemon ×3.
- **Fichas:** 18 obstáculos, 6 encuentros y 6 teleportadores.
- **Equipo:** 45 de munición, 21 armas, 12 de salud y 15 de otro tipo. El manual da solo esos totales por grupo; el reparto por tipo (cuántas escopetas, cuántas celdas, etc.) es una estimación balanceada, marcada con `"estimado": true`.
- **Figuras por color:** las figuras vienen en 3 colores, y las del color de un marine que no juega vuelven a la caja. La app calcula las disponibles como `cantidad ÷ 3 × marines`.

Para editarlo: **⋯ Más → Editar catálogo de piezas…**. Se copia a `%APPDATA%\DoomCompanion\catalogo.json` y se abre. Después de guardar, usá **Recargar catálogo**: la app revisa que las formas y las conexiones sean coherentes. Si cambiás el catálogo, volvé a exportar la guía para IA.

## 7. Archivos y datos

| Qué | Dónde |
|---|---|
| Catálogo editable | `%APPDATA%\DoomCompanion\catalogo.json` (si no existe, se usa el que viene con la app) |
| Autoguardado | `%APPDATA%\DoomCompanion\autoguardado.doomsave` |
| Partidas guardadas | Donde elijas (`.doomsave`). Cada archivo incluye una copia del mapa y su hash, así que no hace falta conservar el `.json` original. |

---

## 8. Estructura del proyecto

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
