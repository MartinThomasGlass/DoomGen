namespace DoomCompanion.Core.Motor;

/// <summary>Todo lo que cambia durante una partida. Se guarda tal cual en el archivo de progreso.</summary>
public sealed class EstadoPartida
{
    public HashSet<string> AreasReveladas { get; set; } = [];
    public HashSet<string> AreasDespejadas { get; set; } = [];
    /// <summary>Areas de las que se conoce el nombre por una recompensa "revelarInfo".</summary>
    public HashSet<string> AreasConocidas { get; set; } = [];
    public HashSet<string> PuertasAbiertas { get; set; } = [];
    /// <summary>Puertas liberadas por recompensa: se abren sin importar sus requisitos.</summary>
    public HashSet<string> PuertasDesbloqueadas { get; set; } = [];
    public HashSet<string> EventosActivos { get; set; } = [];
    /// <summary>Ids de los objetos colocados en areas que ya se recogieron.</summary>
    public HashSet<string> ObjetosRecogidos { get; set; } = [];
    /// <summary>Monstruos por area (se crean al revelar el area).</summary>
    public Dictionary<string, List<MonstruoEnJuego>> Monstruos { get; set; } = [];
    /// <summary>Inventario compartido del equipo: id de objeto → cantidad.</summary>
    public Dictionary<string, int> Inventario { get; set; } = [];
    public List<EntradaHistorial> Historial { get; set; } = [];
    public ResultadoPartida? Resultado { get; set; }
    public int SiguienteIdMonstruo { get; set; } = 1;
    /// <summary>Colores de los marines en juego: definen que figuras de invasor estan disponibles.</summary>
    public List<ColorFigura> ColoresMarines { get; set; } = [ColorFigura.Rojo, ColorFigura.Verde];
}

/// <summary>Color de las figuras (marines e invasores vienen en estos tres colores).</summary>
public enum ColorFigura { Rojo, Verde, Azul }

public sealed class MonstruoEnJuego
{
    public string Id { get; set; } = "";
    public string Tipo { get; set; } = "";
    public bool Muerto { get; set; }
    /// <summary>Agregado a mano durante la partida (aparicion del invasor).</summary>
    public bool Agregado { get; set; }
    /// <summary>Color de la figura a usar en la mesa.</summary>
    public ColorFigura Color { get; set; }
    /// <summary>Casilla indicada por el mapa (sin esto la app la ubica).</summary>
    public int? X { get; set; }
    public int? Y { get; set; }
    public int Rotacion { get; set; }
}

public enum ResultadoPartida { Victoria, Derrota }

public enum TipoMensaje
{
    Briefing,
    Entrada,
    Despeje,
    Recompensa,
    PuertaAbierta,
    PuertaBloqueada,
    Evento,
    Objetivo,
    Victoria,
    Derrota,
    Info,
}

/// <summary>Texto para mostrar/leer en voz alta como consecuencia de una accion.</summary>
public sealed record Mensaje(TipoMensaje Tipo, string Titulo, string Texto, string? Detalle = null);

public sealed class EntradaHistorial
{
    public DateTime Momento { get; set; }
    public TipoMensaje Tipo { get; set; }
    public string Titulo { get; set; } = "";
    public string Texto { get; set; } = "";
    public string? Detalle { get; set; }
}

public sealed class ResultadoAccion
{
    public bool Exito { get; init; }
    public IReadOnlyList<Mensaje> Mensajes { get; init; } = [];

    public static ResultadoAccion Ok(params Mensaje[] mensajes) => new() { Exito = true, Mensajes = mensajes };
    public static ResultadoAccion Ok(List<Mensaje> mensajes) => new() { Exito = true, Mensajes = mensajes };
    public static ResultadoAccion Falla(params Mensaje[] mensajes) => new() { Exito = false, Mensajes = mensajes };
}
