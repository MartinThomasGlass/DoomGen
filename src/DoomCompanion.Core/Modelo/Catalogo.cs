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
}

public sealed class TileCatalogo
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int? Cantidad { get; set; }
    public int? Ancho { get; set; }
    public int? Alto { get; set; }
    public bool AVerificar { get; set; }
    public string? Nota { get; set; }
}

public sealed class PuertaCatalogo
{
    public TipoPuerta Tipo { get; set; }
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
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
