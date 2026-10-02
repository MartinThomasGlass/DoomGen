using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Validacion;

/// <summary>
/// Advertencias sobre si el mapa se puede armar con las piezas reales: superposiciones,
/// conexiones sin tapar, puertas fuera de una conexion, areas unidas sin puerta y piezas
/// sueltas dentro de un area. Solo considera piezas con forma en el catalogo.
/// </summary>
public static class ValidadorGeometria
{
    private const int MaxAvisosPorTipo = 8;

    public static List<Problema> Validar(Mapa mapa, Catalogo catalogo)
    {
        var p = new List<Problema>();
        void Aviso(string ruta, string msg) => p.Add(new(Severidad.Advertencia, CategoriaProblema.Inventario, ruta, msg));

        var piezas = mapa.Areas
            .SelectMany((a, ia) => a.Tiles.Select((t, it) => new Pieza(a, ia, t, it)))
            .ToList();

        // 1. Superposiciones (cualquier pieza, con o sin forma).
        var duenos = new Dictionary<Celda, Pieza>();
        var choques = new HashSet<(Pieza, Pieza)>();
        foreach (var pieza in piezas)
            foreach (var celda in GeometriaTiles.Celdas(pieza.Tile, catalogo))
                if (!duenos.TryAdd(celda, pieza) && choques.Add((duenos[celda], pieza)) && choques.Count <= MaxAvisosPorTipo)
                    Aviso(pieza.Ruta, $"La pieza se superpone con {duenos[celda].Descripcion} en la casilla ({celda.X}, {celda.Y}).");

        // 1b. Fichas con posicion: dentro de su area y sin encimarse.
        ValidarFichas(mapa, catalogo, Aviso);

        // Las demas verificaciones necesitan conocer las conexiones de todas las piezas.
        var conForma = piezas.Where(x => GeometriaTiles.TieneForma(x.Tile, catalogo)).ToList();
        if (conForma.Count < piezas.Count) return p;

        var lineas = new Dictionary<LineaConexion, List<Pieza>>();
        foreach (var pieza in conForma)
            foreach (var (_, linea) in GeometriaTiles.Conexiones(pieza.Tile, catalogo))
            {
                if (!lineas.TryGetValue(linea, out var lista)) lineas[linea] = lista = [];
                lista.Add(pieza);
            }

        var puertasConPosicion = mapa.Puertas.Where(x => x.Posicion is not null && x.Tipo != TipoPuerta.Teleportador).ToList();

        // 2. Conexiones libres y areas unidas sin puerta.
        var libres = 0;
        foreach (var (linea, lista) in lineas)
        {
            if (lista.Count == 1)
            {
                if (puertasConPosicion.Any(x => linea.Coincide(x.Posicion!))) continue; // lo informa la puerta
                if (++libres <= MaxAvisosPorTipo)
                    Aviso(lista[0].Ruta, $"Conexión libre en {linea}: no se une con ninguna pieza. Tapala con un callejón sin salida.");
            }
            else if (lista.Count == 2 && lista[0].Area.Id != lista[1].Area.Id
                     && !puertasConPosicion.Any(x => linea.Coincide(x.Posicion!)))
            {
                Aviso("puertas", $"Las áreas {lista[0].Area.Id} y {lista[1].Area.Id} se tocan por una conexión en {linea} sin puerta: " +
                                 "los marines podrían pasar sin abrir nada. Agregá una puerta (o de tipo \"paso\") en esa posición.");
            }
        }
        if (libres > MaxAvisosPorTipo)
            Aviso("areas", $"Hay {libres} conexiones libres en total.");

        // 3. Puertas: tienen que estar sobre una conexion entre piezas de sus dos areas.
        for (var i = 0; i < mapa.Puertas.Count; i++)
        {
            var puerta = mapa.Puertas[i];
            if (puerta.Tipo == TipoPuerta.Teleportador) continue;
            var ruta = $"puertas[{i}] ({puerta.Id})";
            if (puerta.Posicion is not { } pos)
            {
                Aviso(ruta, "La puerta no tiene \"posicion\": no se puede ubicar en el plano.");
                continue;
            }
            var linea = new LineaConexion(pos.X, pos.Y, pos.Orientacion);
            var ahi = lineas.GetValueOrDefault(linea) ?? [];
            var desde = ahi.Any(x => x.Area.Id == puerta.Desde);
            var hacia = ahi.Any(x => x.Area.Id == puerta.Hacia);
            if (!desde || !hacia)
            {
                var faltan = string.Join(" ni de ", new[] { desde ? null : puerta.Desde, hacia ? null : puerta.Hacia }.Where(x => x is not null));
                Aviso(ruta, $"La puerta en {linea} no está sobre una conexión de las piezas de {faltan}.");
            }
        }

        // 4. Piezas de una misma area que no se unen entre si.
        foreach (var area in mapa.Areas.Where(a => a.Tiles.Count > 1))
        {
            var propias = conForma.Where(x => x.Area == area).ToList();
            var grupo = new HashSet<Pieza> { propias[0] };
            bool cambio;
            do
            {
                cambio = false;
                foreach (var lista in lineas.Values.Where(l => l.Count == 2 && l.All(x => x.Area == area)))
                    if (grupo.Contains(lista[0]) ^ grupo.Contains(lista[1]))
                        cambio |= grupo.Add(lista[0]) | grupo.Add(lista[1]);
            } while (cambio);

            if (grupo.Count < propias.Count)
                Aviso($"areas ({area.Id}).tiles",
                    $"Las piezas del área {area.Id} no están todas unidas entre sí por conexiones ({grupo.Count} de {propias.Count} conectadas).");
        }

        return p;
    }

    private static void ValidarFichas(Mapa mapa, Catalogo catalogo, Action<string, string> aviso)
    {
        for (var i = 0; i < mapa.Areas.Count; i++)
        {
            var area = mapa.Areas[i];
            var ruta = $"areas[{i}] ({area.Id})";
            var celdasArea = area.Tiles.SelectMany(t => GeometriaTiles.Celdas(t, catalogo)).ToHashSet();
            var fichas = new List<(string Que, string Ruta, int X, int Y, int Ancho, int Alto)>();

            for (var g = 0; g < area.Monstruos.Count; g++)
            {
                var grupo = area.Monstruos[g];
                var posiciones = grupo.Posiciones ?? [];
                if (posiciones.Count > grupo.Cantidad)
                    aviso($"{ruta}.monstruos[{g}].posiciones", $"Hay {posiciones.Count} posiciones para {grupo.Cantidad} figura(s).");
                var nombre = catalogo.BuscarMonstruo(grupo.Tipo)?.Nombre ?? grupo.Tipo;
                foreach (var p in posiciones)
                {
                    var (an, al) = DoomCompanion.Core.Motor.DistribucionFichas.TamanoMonstruo(catalogo, grupo.Tipo, p.Rotacion);
                    fichas.Add((nombre, $"{ruta}.monstruos[{g}]", p.X, p.Y, an, al));
                }
            }
            for (var o = 0; o < area.Objetos.Count; o++)
            {
                var obj = area.Objetos[o];
                var posiciones = obj.Posiciones ?? [];
                if (posiciones.Count > obj.Cantidad)
                    aviso($"{ruta}.objetos[{o}].posiciones", $"Hay {posiciones.Count} posiciones para {obj.Cantidad} ficha(s).");
                var nombre = catalogo.BuscarObjeto(obj.Tipo)?.Nombre ?? mapa.ObjetosMision.FirstOrDefault(m => m.Id == obj.Tipo)?.Nombre ?? obj.Tipo;
                foreach (var p in posiciones)
                    fichas.Add((nombre, $"{ruta}.objetos[{o}]", p.X, p.Y, 1, 1));
            }
            for (var f = 0; f < area.Fichas.Count; f++)
            {
                var ficha = area.Fichas[f];
                var posiciones = ficha.Posiciones ?? [];
                if (posiciones.Count > ficha.Cantidad)
                    aviso($"{ruta}.fichas[{f}].posiciones", $"Hay {posiciones.Count} posiciones para {ficha.Cantidad} ficha(s).");
                foreach (var p in posiciones)
                {
                    var (an, al) = DoomCompanion.Core.Motor.DistribucionFichas.TamanoEscenografia(catalogo, ficha.Tipo, p);
                    fichas.Add((catalogo.BuscarFicha(ficha.Tipo)?.Nombre ?? ficha.Tipo, $"{ruta}.fichas[{f}]", p.X, p.Y, an, al));
                }
            }
            if (area.Id == mapa.Escenario.AreaInicial)
                foreach (var p in mapa.Escenario.InicioMarines ?? [])
                    fichas.Add(("Marine", "escenario.inicioMarines", p.X, p.Y, 1, 1));

            var ocupadas = new Dictionary<Celda, string>();
            foreach (var (que, rutaFicha, x, y, an, al) in fichas)
            {
                var celdas = Enumerable.Range(0, an).SelectMany(dx => Enumerable.Range(0, al).Select(dy => new Celda(x + dx, y + dy))).ToList();
                if (celdas.Any(c => !celdasArea.Contains(c)))
                {
                    aviso(rutaFicha, $"{que} en ({x}, {y}) queda fuera de las piezas del área {area.Id}.");
                    continue;
                }
                var choque = celdas.FirstOrDefault(ocupadas.ContainsKey);
                if (ocupadas.ContainsKey(choque))
                    aviso(rutaFicha, $"{que} en ({x}, {y}) se encima con {ocupadas[choque]} en la casilla ({choque.X}, {choque.Y}).");
                foreach (var c in celdas) ocupadas.TryAdd(c, que);
            }
        }
    }

    private sealed record Pieza(Area Area, int IndiceArea, ColocacionTile Tile, int IndiceTile)
    {
        public string Ruta => $"areas[{IndiceArea}] ({Area.Id}).tiles[{IndiceTile}]";
        public string Descripcion => $"{Area.Id}.tiles[{IndiceTile}] ({Tile.Tipo})";
    }
}
