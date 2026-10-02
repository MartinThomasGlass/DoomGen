using System.Windows.Media;
using DoomCompanion.Core.Modelo;

namespace DoomCompanion.App.ViewModels;

// Proyecciones de solo lectura del estado del motor para la interfaz. Se reconstruyen despues
// de cada accion; las acciones se hacen siempre por comandos del MainViewModel.

public enum EstadoArea { Revelada, Despejada, Conocida }

public sealed class AreaVM
{
    public required string Id { get; init; }
    public required string Nombre { get; init; }
    public required EstadoArea Estado { get; init; }
    public string EstadoTexto => Estado switch
    {
        EstadoArea.Despejada => "DESPEJADA",
        EstadoArea.Conocida => "SIN EXPLORAR",
        _ => "CON ENEMIGOS",
    };
    public Brush EstadoColor => Estado switch
    {
        EstadoArea.Despejada => Colores.Despejada,
        EstadoArea.Conocida => Colores.Conocida,
        _ => Colores.ConEnemigos,
    };
    public bool EsConocida => Estado == EstadoArea.Conocida;
    /// <summary>Color del area en el mapa (para el panel lateral).</summary>
    public Brush ColorArea { get; init; } = Brushes.Gray;
    public bool EsDespejada => Estado == EstadoArea.Despejada;
    public string Preparacion { get; init; } = "";
    public string TextoEntrar { get; init; } = "";
    public string TextoDespejar { get; init; } = "";
    public string? NotasInvasor { get; init; }
    public IReadOnlyList<MonstruoVM> Monstruos { get; init; } = [];
    public IReadOnlyList<ObjetoVM> Objetos { get; init; } = [];
    public IReadOnlyList<PuertaVM> Puertas { get; init; } = [];
    public IReadOnlyList<string> Recompensas { get; init; } = [];
    public string ResumenMonstruos
    {
        get
        {
            var vivos = Monstruos.Count(m => !m.Muerto);
            return Monstruos.Count == 0 ? "Sin monstruos" : $"{vivos} vivo(s) de {Monstruos.Count}";
        }
    }
}

public sealed class MonstruoVM
{
    public required string AreaId { get; init; }
    public required string Id { get; init; }
    public required string Nombre { get; init; }
    public bool Muerto { get; init; }
    public bool Agregado { get; init; }
    /// <summary>Color de la figura a usar en la mesa.</summary>
    public Brush ColorFigura { get; init; } = Brushes.Gray;
    public string NombreColor { get; init; } = "";
    public string Etiqueta => $"{Nombre} {NombreColor}" + (Agregado ? "  (aparición)" : "");
}

public sealed class ObjetoVM
{
    public required string Id { get; init; }
    public required string Nombre { get; init; }
    public int Cantidad { get; init; }
    public string? Texto { get; init; }
    public bool Recogido { get; init; }
    public string Etiqueta => Cantidad > 1 ? $"{Cantidad} × {Nombre}" : Nombre;
}

public sealed class PuertaVM
{
    public required string Id { get; init; }
    public required TipoPuerta Tipo { get; init; }
    public required string NombreTipo { get; init; }
    public required string Destino { get; init; }
    public bool Abierta { get; init; }
    public Brush Color => Colores.DePuerta(Tipo);
    public string Accion => Abierta ? "Abierta" : "Abrir";
}

public sealed class ItemInventarioVM
{
    public required string Id { get; init; }
    public required string Nombre { get; init; }
    public int Cantidad { get; init; }
}

public sealed class GrupoInventarioVM
{
    public required string Nombre { get; init; }
    public required IReadOnlyList<ItemInventarioVM> Items { get; init; }
}

public sealed class EventoVM
{
    public required string Id { get; init; }
    public required string Nombre { get; init; }
    public string? Descripcion { get; init; }
    public bool Activo { get; init; }
}

public sealed class ColorMarineVM
{
    public required DoomCompanion.Core.Motor.ColorFigura Color { get; init; }
    public required string Nombre { get; init; }
    public required Brush Pincel { get; init; }
    public bool Activo { get; init; }
}

public sealed class OpcionVM
{
    public required string Id { get; init; }
    public required string Nombre { get; init; }
    public override string ToString() => Nombre;
}

public static class Colores
{
    private static SolidColorBrush B(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public static readonly Brush ConEnemigos = B("#E53935");
    public static readonly Brush Despejada = B("#43A047");
    public static readonly Brush Conocida = B("#757575");

    public static readonly Brush Normal = B("#9E9E9E");
    public static readonly Brush Roja = B("#E53935");
    public static readonly Brush Azul = B("#1E88E5");
    public static readonly Brush Amarilla = B("#FDD835");
    public static readonly Brush Evento = B("#AB47BC");
    public static readonly Brush Paso = B("#A1887F");
    public static readonly Brush Teleportador = B("#26C6DA");

    /// <summary>Gris del area inicial (sin tinte, como el START de DoomGen).</summary>
    public static readonly Color Inicio = Color.FromRgb(0x9E, 0xA2, 0xA8);

    private static readonly Color[] PaletaAreas =
    [
        Color.FromRgb(0xB0, 0x3C, 0xC8), // violeta
        Color.FromRgb(0x3D, 0x55, 0xE0), // azul
        Color.FromRgb(0xC8, 0x2E, 0x2E), // rojo
        Color.FromRgb(0xC9, 0xB8, 0x1E), // amarillo
        Color.FromRgb(0x3E, 0x9B, 0x48), // verde
        Color.FromRgb(0x1F, 0xA7, 0xB8), // turquesa
        Color.FromRgb(0xE0, 0x7B, 0x1A), // naranja
        Color.FromRgb(0x8A, 0x9A, 0x2E), // oliva
        Color.FromRgb(0xD8, 0x4C, 0x8F), // rosa
    ];

    /// <summary>Color propio de cada area: el inicial es gris y el resto rota por la paleta.</summary>
    public static Color DeArea(Mapa mapa, string areaId)
    {
        if (areaId == mapa.Escenario.AreaInicial) return Inicio;
        var indice = mapa.Areas.Where(a => a.Id != mapa.Escenario.AreaInicial).ToList().FindIndex(a => a.Id == areaId);
        return indice < 0 ? Inicio : PaletaAreas[indice % PaletaAreas.Length];
    }

    public static Brush DePuerta(TipoPuerta t) => t switch
    {
        TipoPuerta.Roja => Roja,
        TipoPuerta.Azul => Azul,
        TipoPuerta.Amarilla => Amarilla,
        TipoPuerta.Evento => Evento,
        TipoPuerta.Paso => Paso,
        TipoPuerta.Teleportador => Teleportador,
        _ => Normal,
    };
}
