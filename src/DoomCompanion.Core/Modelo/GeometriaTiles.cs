namespace DoomCompanion.Core.Modelo;

public readonly record struct Celda(int X, int Y);

/// <summary>Tramo de borde de casilla, de (X1, Y1) a (X2, Y2) en coordenadas de la cuadricula.</summary>
public readonly record struct Segmento(int X1, int Y1, int X2, int Y2);

/// <summary>
/// Linea de la cuadricula de 2 casillas de largo donde se unen dos piezas (y donde va una
/// puerta). Horizontal: sobre la linea Y, de X a X+2. Vertical: sobre la linea X, de Y a Y+2.
/// Es la misma convencion que <see cref="PosicionPuerta"/>.
/// </summary>
public readonly record struct LineaConexion(int X, int Y, Orientacion Orientacion)
{
    public bool Coincide(PosicionPuerta p) => p.X == X && p.Y == Y && p.Orientacion == Orientacion;

    public override string ToString() => $"({X}, {Y}) {Orientacion.ToString().ToLowerInvariant()}";
}

/// <summary>Forma de una pieza ya rotada.</summary>
public sealed record FormaRotada(int Ancho, int Alto, IReadOnlyList<string> Filas, IReadOnlyList<ConexionTile> Conexiones);

/// <summary>
/// Geometria de las piezas: rotacion de formas y conexiones, casillas ocupadas y lineas de
/// conexion en coordenadas del mapa. La rotacion es en sentido horario y (x, y) es la esquina
/// superior izquierda de la pieza ya rotada.
/// </summary>
public static class GeometriaTiles
{
    private const int TamanoPorDefecto = 4;

    public static FormaRotada? Rotar(TileCatalogo tile, int rotacion)
    {
        if (!tile.TieneForma) return null;
        var filas = tile.Forma!;
        var ancho = filas[0].Length;
        var alto = filas.Count;

        var celdas = new List<(int C, int F)>();
        for (var f = 0; f < alto; f++)
            for (var c = 0; c < ancho; c++)
                if (filas[f][c] == '#') celdas.Add((c, f));

        var conexiones = tile.Conexiones.Select(x => (x.Lado, Celdas: CeldasDeConexion(x, ancho, alto))).ToList();

        var pasos = ((rotacion % 360 + 360) % 360) / 90;
        for (var i = 0; i < pasos; i++)
        {
            // Giro horario: (c, f) -> (alto - 1 - f, c).
            var a = alto;
            celdas = celdas.Select(p => (a - 1 - p.F, p.C)).ToList();
            conexiones = conexiones
                .Select(x => (Siguiente(x.Lado), x.Celdas.Select(p => (a - 1 - p.F, p.C)).ToArray()))
                .ToList();
            (ancho, alto) = (alto, ancho);
        }

        var grilla = Enumerable.Range(0, alto).Select(_ => new char[ancho]).ToArray();
        foreach (var fila in grilla) Array.Fill(fila, '.');
        foreach (var (c, f) in celdas) grilla[f][c] = '#';

        return new FormaRotada(
            ancho,
            alto,
            grilla.Select(f => new string(f)).ToList(),
            conexiones.Select(x => new ConexionTile
            {
                Lado = x.Lado,
                Desde = x.Lado is Lado.Norte or Lado.Sur ? x.Celdas.Min(p => p.C) : x.Celdas.Min(p => p.F),
            }).ToList());
    }

    private static (int C, int F)[] CeldasDeConexion(ConexionTile x, int ancho, int alto) => x.Lado switch
    {
        Lado.Norte => [(x.Desde, 0), (x.Desde + 1, 0)],
        Lado.Sur => [(x.Desde, alto - 1), (x.Desde + 1, alto - 1)],
        Lado.Oeste => [(0, x.Desde), (0, x.Desde + 1)],
        _ => [(ancho - 1, x.Desde), (ancho - 1, x.Desde + 1)],
    };

    private static Lado Siguiente(Lado l) => l switch
    {
        Lado.Norte => Lado.Este,
        Lado.Este => Lado.Sur,
        Lado.Sur => Lado.Oeste,
        _ => Lado.Norte,
    };

    /// <summary>True si la geometria de la pieza es conocida (forma en el catalogo).</summary>
    public static bool TieneForma(ColocacionTile t, Catalogo catalogo) => catalogo.BuscarTile(t.Tipo)?.TieneForma == true;

    /// <summary>
    /// Casillas que ocupa la pieza en el mapa. Sin forma en el catalogo usa un rectangulo con las
    /// medidas del catalogo, o las del mapa, o 4×4.
    /// </summary>
    public static IReadOnlyList<Celda> Celdas(ColocacionTile t, Catalogo catalogo)
    {
        var cat = catalogo.BuscarTile(t.Tipo);
        if (cat is not null && Rotar(cat, t.Rotacion) is { } forma)
        {
            var lista = new List<Celda>();
            for (var f = 0; f < forma.Alto; f++)
                for (var c = 0; c < forma.Ancho; c++)
                    if (forma.Filas[f][c] == '#') lista.Add(new Celda(t.X + c, t.Y + f));
            return lista;
        }

        int ancho, alto;
        if (cat is { Ancho: int an, Alto: int al })
            (ancho, alto) = t.Rotacion is 90 or 270 ? (al, an) : (an, al);
        else
            (ancho, alto) = (t.Ancho ?? TamanoPorDefecto, t.Alto ?? TamanoPorDefecto);

        var rect = new List<Celda>(ancho * alto);
        for (var f = 0; f < alto; f++)
            for (var c = 0; c < ancho; c++)
                rect.Add(new Celda(t.X + c, t.Y + f));
        return rect;
    }

    /// <summary>
    /// Tramos de pared de la pieza (bordes de casilla de 1 de largo que dan afuera), sin los
    /// tramos que ocupan sus conexiones: ahi la pieza queda abierta.
    /// </summary>
    public static IReadOnlyList<Segmento> Paredes(ColocacionTile t, Catalogo catalogo)
    {
        var celdas = Celdas(t, catalogo).ToHashSet();
        var aberturas = new HashSet<Segmento>();
        foreach (var (_, l) in Conexiones(t, catalogo))
        {
            if (l.Orientacion == Orientacion.Horizontal)
            {
                aberturas.Add(new Segmento(l.X, l.Y, l.X + 1, l.Y));
                aberturas.Add(new Segmento(l.X + 1, l.Y, l.X + 2, l.Y));
            }
            else
            {
                aberturas.Add(new Segmento(l.X, l.Y, l.X, l.Y + 1));
                aberturas.Add(new Segmento(l.X, l.Y + 1, l.X, l.Y + 2));
            }
        }

        var paredes = new List<Segmento>();
        void Agregar(Segmento s)
        {
            if (!aberturas.Contains(s)) paredes.Add(s);
        }
        foreach (var c in celdas)
        {
            if (!celdas.Contains(c with { Y = c.Y - 1 })) Agregar(new Segmento(c.X, c.Y, c.X + 1, c.Y));
            if (!celdas.Contains(c with { Y = c.Y + 1 })) Agregar(new Segmento(c.X, c.Y + 1, c.X + 1, c.Y + 1));
            if (!celdas.Contains(c with { X = c.X - 1 })) Agregar(new Segmento(c.X, c.Y, c.X, c.Y + 1));
            if (!celdas.Contains(c with { X = c.X + 1 })) Agregar(new Segmento(c.X + 1, c.Y, c.X + 1, c.Y + 1));
        }
        return paredes;
    }

    /// <summary>Casillas de la pieza que forman parte de una conexion (las del borde abierto).</summary>
    public static IReadOnlyList<Celda> CeldasDeConexion(ColocacionTile t, Catalogo catalogo) =>
        Conexiones(t, catalogo).SelectMany(x => x.Lado switch
        {
            Lado.Norte => new[] { new Celda(x.Linea.X, x.Linea.Y), new Celda(x.Linea.X + 1, x.Linea.Y) },
            Lado.Sur => [new Celda(x.Linea.X, x.Linea.Y - 1), new Celda(x.Linea.X + 1, x.Linea.Y - 1)],
            Lado.Oeste => [new Celda(x.Linea.X, x.Linea.Y), new Celda(x.Linea.X, x.Linea.Y + 1)],
            _ => [new Celda(x.Linea.X - 1, x.Linea.Y), new Celda(x.Linea.X - 1, x.Linea.Y + 1)],
        }).Distinct().ToList();

    /// <summary>Conexiones de la pieza en coordenadas del mapa (vacio si no tiene forma).</summary>
    public static IReadOnlyList<(Lado Lado, LineaConexion Linea)> Conexiones(ColocacionTile t, Catalogo catalogo)
    {
        if (catalogo.BuscarTile(t.Tipo) is not { } cat || Rotar(cat, t.Rotacion) is not { } forma) return [];
        return forma.Conexiones.Select(c => (c.Lado, c.Lado switch
        {
            Lado.Norte => new LineaConexion(t.X + c.Desde, t.Y, Orientacion.Horizontal),
            Lado.Sur => new LineaConexion(t.X + c.Desde, t.Y + forma.Alto, Orientacion.Horizontal),
            Lado.Oeste => new LineaConexion(t.X, t.Y + c.Desde, Orientacion.Vertical),
            _ => new LineaConexion(t.X + forma.Ancho, t.Y + c.Desde, Orientacion.Vertical),
        })).ToList();
    }
}
