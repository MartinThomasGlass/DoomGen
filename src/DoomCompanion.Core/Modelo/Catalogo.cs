namespace DoomCompanion.Core.Modelo;

/// <summary>Catalogo editable de tiles y fichas fisicas de la caja.</summary>
public sealed class Catalogo
{
    public int Version { get; set; } = 1;
    public string? Nota { get; set; }
    public List<TileCatalogo> Tiles { get; set; } = [];
    public List<PuertaCatalogo> Puertas { get; set; } = [];
    public List<MonstruoCatalogo> Monstruos { get; set; } = [];
    public List<ObjetoCatalogo> Objetos { get; set; } = [];
    public List<GrupoEquipo> GruposEquipo { get; set; } = [];
    public List<FichaCatalogo> Fichas { get; set; } = [];
    public List<PreparacionMarines> PreparacionMarines { get; set; } = [];
    public string? ArmasBasicas { get; set; }

    public TileCatalogo? BuscarTile(string id) => Tiles.FirstOrDefault(t => t.Id == id);
    public MonstruoCatalogo? BuscarMonstruo(string id) => Monstruos.FirstOrDefault(m => m.Id == id);
    public ObjetoCatalogo? BuscarObjeto(string id) => Objetos.FirstOrDefault(o => o.Id == id);
    public FichaCatalogo? BuscarFicha(string id) => Fichas.FirstOrDefault(f => f.Id == id);

    /// <summary>Errores de un catalogo editado a mano (ids repetidos, formas o conexiones mal escritas).</summary>
    public List<string> Validar()
    {
        var errores = new List<string>();
        void Repetidos(IEnumerable<string> ids, string que)
        {
            foreach (var g in ids.GroupBy(x => x).Where(g => g.Count() > 1))
                errores.Add($"Id de {que} repetido: \"{g.Key}\".");
        }
        Repetidos(Tiles.Select(t => t.Id), "tile");
        Repetidos(Monstruos.Select(m => m.Id), "monstruo");
        Repetidos(Objetos.Select(o => o.Id), "objeto");
        Repetidos(Fichas.Select(f => f.Id), "ficha");
        foreach (var t in Tiles)
            errores.AddRange(t.Validar().Select(e => $"Tile \"{t.Id}\": {e}"));
        return errores;
    }
}

public sealed class TileCatalogo
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int? Cantidad { get; set; }

    /// <summary>
    /// Forma en rotacion 0, fila por fila de arriba hacia abajo: '#' = casilla, '.' = vacio.
    /// Rotacion 0 es la orientacion en que aparece la pieza en la hoja de referencia.
    /// </summary>
    public List<string>? Forma { get; set; }

    /// <summary>Aberturas de 2 casillas por donde la pieza se une a otras (en rotacion 0).</summary>
    public List<ConexionTile> Conexiones { get; set; } = [];

    /// <summary>Medidas sin forma (solo para dibujar). Si hay forma, se ignoran.</summary>
    public int? Ancho { get; set; }
    public int? Alto { get; set; }
    public bool AVerificar { get; set; }
    public string? Nota { get; set; }

    /// <summary>Archivo de imagen dentro de la carpeta de imagenes (formato DoomGen: 64 px por casilla y 1 casilla de margen).</summary>
    public string? Imagen { get; set; }

    public bool TieneForma => Forma is { Count: > 0 };

    public List<string> Validar()
    {
        var errores = new List<string>();
        if (!TieneForma)
        {
            if (Conexiones.Count > 0) errores.Add("tiene conexiones pero no tiene forma.");
            return errores;
        }
        var forma = Forma!;
        var ancho = forma[0].Length;
        if (forma.Any(f => f.Length != ancho)) errores.Add("todas las filas de la forma tienen que tener el mismo largo.");
        if (forma.Any(f => f.Any(c => c is not ('#' or '.')))) errores.Add("la forma solo puede tener '#' (casilla) y '.' (vacío).");
        if (!forma.Any(f => f.Contains('#'))) errores.Add("la forma no tiene ninguna casilla.");
        if (errores.Count > 0) return errores;

        bool Casilla(int col, int fila) =>
            fila >= 0 && fila < forma.Count && col >= 0 && col < ancho && forma[fila][col] == '#';

        foreach (var c in Conexiones)
        {
            var largoLado = c.Lado is Lado.Norte or Lado.Sur ? ancho : forma.Count;
            if (c.Desde < 0 || c.Desde + 2 > largoLado)
            {
                errores.Add($"la conexión {c} se sale del lado (largo {largoLado}).");
                continue;
            }
            var (c1, c2) = c.Lado switch
            {
                Lado.Norte => ((c.Desde, 0), (c.Desde + 1, 0)),
                Lado.Sur => ((c.Desde, forma.Count - 1), (c.Desde + 1, forma.Count - 1)),
                Lado.Oeste => ((0, c.Desde), (0, c.Desde + 1)),
                _ => ((ancho - 1, c.Desde), (ancho - 1, c.Desde + 1)),
            };
            if (!Casilla(c1.Item1, c1.Item2) || !Casilla(c2.Item1, c2.Item2))
                errores.Add($"la conexión {c} no coincide con casillas del borde de la forma.");
        }
        return errores;
    }
}

public enum Lado { Norte, Este, Sur, Oeste }

/// <summary>
/// Abertura de 2 casillas sobre un lado del rectangulo de la pieza. <see cref="Desde"/> es la
/// columna (lados norte/sur) o la fila (lados este/oeste) de la primera casilla, empezando en 0.
/// </summary>
public sealed class ConexionTile
{
    public Lado Lado { get; set; }
    public int Desde { get; set; }

    public override string ToString() => $"{Lado.ToString().ToLowerInvariant()}@{Desde}";
}

public sealed class PuertaCatalogo
{
    public TipoPuerta Tipo { get; set; }
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
    public string? Imagen { get; set; }
    public bool AVerificar { get; set; }
    public string? Nota { get; set; }
}

public sealed class MonstruoCatalogo
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    /// <summary>Total de figuras en la caja (sumando los 3 colores).</summary>
    public int Cantidad { get; set; }
    public int Casillas { get; set; } = 1;
    /// <summary>Peso orientativo de peligro, para balancear (no es un valor oficial).</summary>
    public int Amenaza { get; set; } = 1;
    /// <summary>Texto corto para la ficha en el plano (por ejemplo "IMP").</summary>
    public string? Sigla { get; set; }
    /// <summary>Nombre base de la imagen; se le agrega _red, _green o _blue segun el color.</summary>
    public string? Imagen { get; set; }
    public string? Nota { get; set; }

    /// <summary>
    /// Figuras disponibles segun la cantidad de marines: las figuras vienen en 3 colores y las
    /// del color de un marine que no se usa vuelven a la caja.
    /// </summary>
    public int Disponibles(int marines) => Cantidad / 3 * Math.Clamp(marines, 1, 3);
}

public enum CategoriaObjeto { Llave, Arma, Municion, Salud, Armadura, Mision, Otro }

public sealed class ObjetoCatalogo
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public CategoriaObjeto Categoria { get; set; }
    public int? Cantidad { get; set; }
    public bool Estimado { get; set; }
    public string? Grupo { get; set; }
    /// <summary>Para armas: id del objeto de municion que usa (null = no usa municion).</summary>
    public string? Municion { get; set; }
    /// <summary>Que tan valioso es para los marines (orientativo, para balancear).</summary>
    public int Valor { get; set; } = 1;
    /// <summary>Texto corto para la ficha en el plano (por ejemplo "ESC").</summary>
    public string? Sigla { get; set; }
    public string? Imagen { get; set; }
    public string? Nota { get; set; }
}

public sealed class GrupoEquipo
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int Total { get; set; }
}

public sealed class FichaCatalogo
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int? Cantidad { get; set; }
    /// <summary>Casillas que ocupa la ficha (por defecto 1×1).</summary>
    public int Ancho { get; set; } = 1;
    public int Alto { get; set; } = 1;
    public string? Imagen { get; set; }
    public bool AVerificar { get; set; }
    public string? Nota { get; set; }
}

public sealed class PreparacionMarines
{
    public int Marines { get; set; }
    public int Municion { get; set; }
    public int Heridas { get; set; }
    public int Armadura { get; set; }
    public int Cartas { get; set; }
}
