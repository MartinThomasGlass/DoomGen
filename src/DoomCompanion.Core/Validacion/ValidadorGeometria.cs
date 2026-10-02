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

    private sealed record Pieza(Area Area, int IndiceArea, ColocacionTile Tile, int IndiceTile)
    {
        public string Ruta => $"areas[{IndiceArea}] ({Area.Id}).tiles[{IndiceTile}]";
        public string Descripcion => $"{Area.Id}.tiles[{IndiceTile}] ({Tile.Tipo})";
    }
}
