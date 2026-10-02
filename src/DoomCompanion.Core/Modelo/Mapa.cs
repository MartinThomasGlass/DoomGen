using System.Text.Json.Serialization;

namespace DoomCompanion.Core.Modelo;

/// <summary>Archivo de mapa completo (formato version 1).</summary>
public sealed class Mapa
{
    public const int VersionActual = 1;

    public int Version { get; set; } = VersionActual;
    public Escenario Escenario { get; set; } = new();
    public List<Evento> Eventos { get; set; } = [];
    public List<ObjetoMision> ObjetosMision { get; set; } = [];
    public List<Area> Areas { get; set; } = [];
    public List<Puerta> Puertas { get; set; } = [];

    public Area? BuscarArea(string id) => Areas.FirstOrDefault(a => a.Id == id);
    public Puerta? BuscarPuerta(string id) => Puertas.FirstOrDefault(p => p.Id == id);
    public Evento? BuscarEvento(string id) => Eventos.FirstOrDefault(e => e.Id == id);

    public IEnumerable<Puerta> PuertasDe(string areaId) =>
        Puertas.Where(p => p.Desde == areaId || p.Hacia == areaId);

    public IEnumerable<(Area Area, ObjetoEnArea Objeto)> TodosLosObjetos() =>
        Areas.SelectMany(a => a.Objetos.Select(o => (a, o)));
}

public enum Dificultad { Facil, Media, Dificil, Pesadilla }

public sealed class Escenario
{
    public string Id { get; set; } = "";
    public string Titulo { get; set; } = "";
    public Dificultad Dificultad { get; set; } = Dificultad.Media;
    public int Marines { get; set; } = 2;
    public string? Ambientacion { get; set; }
    public string? Autor { get; set; }
    public TextosEscenario Textos { get; set; } = new();
    public string AreaInicial { get; set; } = "";
    public CondicionVictoria CondicionVictoria { get; set; } = new();
}

public sealed class TextosEscenario
{
    public string Introduccion { get; set; } = "";
    public string Objetivos { get; set; } = "";
    public string Victoria { get; set; } = "";
    public string Derrota { get; set; } = "";
    public string? NotasInvasor { get; set; }
}

public enum TipoVictoria { LlegarAArea, DespejarArea, Evento }

public sealed class CondicionVictoria
{
    public TipoVictoria Tipo { get; set; }
    public string? Area { get; set; }
    public string? Evento { get; set; }
}

public sealed class Evento
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    /// <summary>true: lo marca el jugador desde la app; false: solo lo activa una recompensa.</summary>
    public bool Manual { get; set; } = true;
}

public sealed class ObjetoMision
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? Ficha { get; set; }
}

public sealed class Area
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public List<ColocacionTile> Tiles { get; set; } = [];
    public TextosArea Textos { get; set; } = new();
    public List<GrupoMonstruos> Monstruos { get; set; } = [];
    public List<ObjetoEnArea> Objetos { get; set; } = [];
    public List<FichaEnArea> Fichas { get; set; } = [];
    public List<Recompensa> Recompensas { get; set; } = [];
    public string? NotasInvasor { get; set; }
}

public sealed class ColocacionTile
{
    public string Tipo { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Rotacion { get; set; }
    public int? Ancho { get; set; }
    public int? Alto { get; set; }
    public string? Nota { get; set; }
}

public sealed class TextosArea
{
    public string Entrar { get; set; } = "";
    public string Despejar { get; set; } = "";
}

public sealed class GrupoMonstruos
{
    public string Tipo { get; set; } = "";
    public int Cantidad { get; set; } = 1;
}

public sealed class ObjetoEnArea
{
    public string Id { get; set; } = "";
    public string Tipo { get; set; } = "";
    public int Cantidad { get; set; } = 1;
    public string? Texto { get; set; }
}

public sealed class FichaEnArea
{
    public string Tipo { get; set; } = "";
    public int Cantidad { get; set; } = 1;
    public string? Nota { get; set; }
}

public enum TipoRecompensa { Texto, OtorgarObjeto, DesbloquearPuerta, ActivarEvento, RevelarInfo }

public sealed class Recompensa
{
    public TipoRecompensa Tipo { get; set; }
    public string Texto { get; set; } = "";
    public string? Objeto { get; set; }
    public int? Cantidad { get; set; }
    public string? Puerta { get; set; }
    public string? Evento { get; set; }
    public string? Area { get; set; }
}

public enum TipoPuerta { Normal, Roja, Azul, Amarilla, Evento, Paso, Teleportador }

public sealed class Puerta
{
    public string Id { get; set; } = "";
    public string Desde { get; set; } = "";
    public string Hacia { get; set; } = "";
    public TipoPuerta Tipo { get; set; }
    public TextosPuerta? Textos { get; set; }
    public Requisito? Requisitos { get; set; }
    public PosicionPuerta? Posicion { get; set; }

    /// <summary>Area del otro lado de la puerta, vista desde <paramref name="areaId"/>.</summary>
    public string Otra(string areaId) => areaId == Desde ? Hacia : Desde;

    public bool Conecta(string areaId) => Desde == areaId || Hacia == areaId;

    /// <summary>Llave que exige implicitamente el tipo de puerta (puertas de seguridad).</summary>
    [JsonIgnore]
    public string? LlaveImplicita => Tipo switch
    {
        TipoPuerta.Roja => "llave-roja",
        TipoPuerta.Azul => "llave-azul",
        TipoPuerta.Amarilla => "llave-amarilla",
        _ => null,
    };

    /// <summary>Requisitos efectivos: la llave implicita del tipo mas los requisitos declarados.</summary>
    [JsonIgnore]
    public Requisito? RequisitosEfectivos
    {
        get
        {
            var llave = LlaveImplicita;
            if (llave is null) return Requisitos;
            var reqLlave = new RequisitoObjeto(llave, 1);
            return Requisitos is null ? reqLlave : new RequisitoTodas([reqLlave, Requisitos]);
        }
    }
}

public sealed class TextosPuerta
{
    public string? Abrir { get; set; }
    public string? Bloqueada { get; set; }
}

public enum Orientacion { Horizontal, Vertical }

public sealed class PosicionPuerta
{
    public int X { get; set; }
    public int Y { get; set; }
    public Orientacion Orientacion { get; set; }
}
