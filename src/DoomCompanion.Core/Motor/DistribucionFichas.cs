using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Motor;

public enum ClaseFicha { Escenografia, Objeto, Marine, Monstruo }

/// <summary>Una ficha ubicada en el plano: que es, donde va y cuantas casillas ocupa.</summary>
public sealed record FichaEnPlano(
    ClaseFicha Clase,
    string Id,
    string Tipo,
    int X,
    int Y,
    int Ancho,
    int Alto,
    ColorFigura? Color = null,
    int Cantidad = 1,
    bool Automatica = false)
{
    public IEnumerable<Celda> Celdas()
    {
        for (var dy = 0; dy < Alto; dy++)
            for (var dx = 0; dx < Ancho; dx++)
                yield return new Celda(X + dx, Y + dy);
    }
}

/// <summary>
/// Calcula donde va cada ficha de un area: escenografia, objetos sin recoger, marines (en el
/// area inicial) y monstruos vivos. Respeta las posiciones del mapa y ubica el resto en casillas
/// libres, siempre de la misma forma para un mismo estado (para que el plano no "salte").
/// </summary>
public static class DistribucionFichas
{
    /// <summary>Casillas (ancho × alto) de un monstruo segun el catalogo y su rotacion.</summary>
    public static (int Ancho, int Alto) TamanoMonstruo(Catalogo catalogo, string tipo, int rotacion) =>
        (catalogo.BuscarMonstruo(tipo)?.Casillas ?? 1) switch
        {
            >= 4 => (2, 2),
            2 => rotacion is 90 or 270 ? (1, 2) : (2, 1),
            _ => (1, 1),
        };

    public static (int Ancho, int Alto) TamanoEscenografia(Catalogo catalogo, string tipo, PosicionFicha? pos)
    {
        var cat = catalogo.BuscarFicha(tipo);
        var (ancho, alto) = (pos?.Ancho ?? cat?.Ancho ?? 1, pos?.Alto ?? cat?.Alto ?? 1);
        return pos?.Rotacion is 90 or 270 ? (alto, ancho) : (ancho, alto);
    }

    public static IReadOnlyList<FichaEnPlano> Calcular(Mapa mapa, Catalogo catalogo, EstadoPartida estado, Area area)
    {
        var pendientes = new List<(ClaseFicha Clase, string Id, string Tipo, PosicionFicha? Pos, int Ancho, int Alto, ColorFigura? Color, int Cantidad, bool Rotable)>();

        // Escenografia.
        for (var i = 0; i < area.Fichas.Count; i++)
        {
            var f = area.Fichas[i];
            for (var k = 0; k < f.Cantidad; k++)
            {
                var pos = f.Posiciones?.ElementAtOrDefault(k);
                var (an, al) = TamanoEscenografia(catalogo, f.Tipo, pos);
                pendientes.Add((ClaseFicha.Escenografia, $"{area.Id}-f{i}-{k}", f.Tipo, pos, an, al, null, 1, false));
            }
        }

        // Objetos sin recoger.
        foreach (var o in area.Objetos.Where(o => !estado.ObjetosRecogidos.Contains(o.Id)))
        {
            var pos = o.X is int x && o.Y is int y ? new PosicionFicha { X = x, Y = y } : null;
            pendientes.Add((ClaseFicha.Objeto, o.Id, o.Tipo, pos, 1, 1, null, o.Cantidad, false));
        }

        // Marines en el area inicial.
        if (area.Id == mapa.Escenario.AreaInicial)
            for (var i = 0; i < estado.ColoresMarines.Count; i++)
            {
                var color = estado.ColoresMarines[i];
                pendientes.Add((ClaseFicha.Marine, "marine-" + color.ToString().ToLowerInvariant(), "marine",
                    mapa.Escenario.InicioMarines?.ElementAtOrDefault(i), 1, 1, color, 1, false));
            }

        // Monstruos vivos.
        foreach (var m in estado.Monstruos.GetValueOrDefault(area.Id) ?? [])
        {
            if (m.Muerto) continue;
            var pos = m.X is int x && m.Y is int y ? new PosicionFicha { X = x, Y = y, Rotacion = m.Rotacion } : null;
            var (an, al) = TamanoMonstruo(catalogo, m.Tipo, m.Rotacion);
            pendientes.Add((ClaseFicha.Monstruo, m.Id, m.Tipo, pos, an, al, m.Color, 1, an != al));
        }

        var celdasArea = area.Tiles.SelectMany(t => GeometriaTiles.Celdas(t, catalogo)).ToHashSet();
        // Las casillas de conexion y los callejones quedan para el final: ahi estan las puertas.
        var evitar = area.Tiles
            .SelectMany(t => t.Tipo == "callejon" ? GeometriaTiles.Celdas(t, catalogo) : GeometriaTiles.CeldasDeConexion(t, catalogo))
            .ToHashSet();
        var azar = new Random(HashEstable(area.Id));
        var orden = celdasArea.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        var mezcla = orden.Select(c => (c, azar.Next())).OrderBy(x => x.Item2).Select(x => x.c).ToList();
        var candidatas = mezcla.Where(c => !evitar.Contains(c)).Concat(mezcla.Where(evitar.Contains)).ToList();

        var ocupadas = new HashSet<Celda>();
        var resultado = new List<FichaEnPlano>();

        // 1. Las que traen posicion del mapa.
        foreach (var p in pendientes.Where(p => p.Pos is not null))
        {
            var ficha = new FichaEnPlano(p.Clase, p.Id, p.Tipo, p.Pos!.X, p.Pos.Y, p.Ancho, p.Alto, p.Color, p.Cantidad);
            foreach (var c in ficha.Celdas()) ocupadas.Add(c);
            resultado.Add(ficha);
        }

        // 2. El resto, en casillas libres (primero las fichas grandes, que cuestan mas ubicar).
        foreach (var p in pendientes.Where(p => p.Pos is null).OrderByDescending(p => p.Ancho * p.Alto))
        {
            FichaEnPlano? ubicada = null;
            var medidas = p.Rotable ? new[] { (p.Ancho, p.Alto), (p.Alto, p.Ancho) } : [(p.Ancho, p.Alto)];
            foreach (var c in candidatas)
            {
                foreach (var (an, al) in medidas)
                {
                    var prueba = new FichaEnPlano(p.Clase, p.Id, p.Tipo, c.X, c.Y, an, al, p.Color, p.Cantidad, Automatica: true);
                    if (prueba.Celdas().All(x => celdasArea.Contains(x) && !ocupadas.Contains(x)))
                    {
                        ubicada = prueba;
                        break;
                    }
                }
                if (ubicada is not null) break;
            }
            // Area llena: se apila en la primera casilla (mejor que no mostrarla).
            ubicada ??= new FichaEnPlano(p.Clase, p.Id, p.Tipo, orden[0].X, orden[0].Y, p.Ancho, p.Alto, p.Color, p.Cantidad, Automatica: true);
            foreach (var c in ubicada.Celdas()) ocupadas.Add(c);
            resultado.Add(ubicada);
        }

        return resultado.OrderBy(f => f.Clase).ToList();
    }

    private static int HashEstable(string s)
    {
        unchecked
        {
            var h = (int)2166136261;
            foreach (var c in s) h = (h ^ c) * 16777619;
            return h;
        }
    }
}
