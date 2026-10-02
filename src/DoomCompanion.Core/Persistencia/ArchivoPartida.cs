using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Motor;
using DoomCompanion.Core.Validacion;

namespace DoomCompanion.Core.Persistencia;

/// <summary>
/// Archivo de progreso (.doomsave). Incluye una copia del mapa original para poder retomar la
/// partida sin reimportarlo, y su hash para detectar si el mapa fue modificado.
/// </summary>
public sealed class ArchivoPartida
{
    public const string Extension = ".doomsave";
    public const string FormatoEsperado = "doom-companion-partida";

    public string Formato { get; set; } = FormatoEsperado;
    public int Version { get; set; } = 1;
    public DateTime Guardado { get; set; }
    public string TituloEscenario { get; set; } = "";
    public string HashMapa { get; set; } = "";
    public string MapaJson { get; set; } = "";
    public EstadoPartida Estado { get; set; } = new();

    public static string CalcularHash(string mapaJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(mapaJson)));

    public static void Guardar(string ruta, Mapa mapa, string mapaJson, EstadoPartida estado)
    {
        var archivo = new ArchivoPartida
        {
            Guardado = DateTime.Now,
            TituloEscenario = mapa.Escenario.Titulo,
            HashMapa = CalcularHash(mapaJson),
            MapaJson = mapaJson,
            Estado = estado,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ruta))!);
        // Escritura atomica: si se corta a la mitad no se pierde el guardado anterior.
        var temporal = ruta + ".tmp";
        File.WriteAllText(temporal, Json.Escribir(archivo), Encoding.UTF8);
        File.Move(temporal, ruta, overwrite: true);
    }

    /// <summary>Carga un progreso: valida formato, hash y que el mapa embebido siga siendo valido.</summary>
    public static ResultadoCarga Cargar(string ruta, Catalogo catalogo)
    {
        ArchivoPartida archivo;
        try
        {
            archivo = Json.Leer<ArchivoPartida>(File.ReadAllText(ruta));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return ResultadoCarga.Error($"No se pudo leer el archivo de partida: {ex.Message}");
        }

        if (archivo.Formato != FormatoEsperado)
            return ResultadoCarga.Error("El archivo no es una partida guardada de Doom Companion.");
        if (archivo.Version != 1)
            return ResultadoCarga.Error($"Versión de partida no soportada ({archivo.Version}).");
        if (CalcularHash(archivo.MapaJson) != archivo.HashMapa)
            return ResultadoCarga.Error("El mapa guardado dentro de la partida fue modificado o está dañado.");

        var importacion = ImportadorMapa.Importar(archivo.MapaJson, catalogo);
        if (!importacion.SePuedeJugar)
            return ResultadoCarga.Error("El mapa de esta partida ya no es válido con el catálogo actual:\n" +
                string.Join("\n", importacion.ErroresDeFormato.Select(p => "• " + p)));

        var mapa = importacion.Mapa!;
        var huerfanos = ReferenciasHuerfanas(archivo.Estado, mapa).ToList();
        if (huerfanos.Count > 0)
            return ResultadoCarga.Error("La partida hace referencia a elementos que no existen en el mapa: " + string.Join(", ", huerfanos));

        return new ResultadoCarga
        {
            Motor = new MotorJuego(mapa, catalogo, archivo.Estado),
            MapaJson = archivo.MapaJson,
            Importacion = importacion,
        };
    }

    private static IEnumerable<string> ReferenciasHuerfanas(EstadoPartida e, Mapa mapa)
    {
        var areas = mapa.Areas.Select(a => a.Id).ToHashSet();
        var puertas = mapa.Puertas.Select(p => p.Id).ToHashSet();
        var eventos = mapa.Eventos.Select(x => x.Id).ToHashSet();
        var objetos = mapa.TodosLosObjetos().Select(x => x.Objeto.Id).ToHashSet();

        return e.AreasReveladas.Concat(e.AreasDespejadas).Concat(e.AreasConocidas).Concat(e.Monstruos.Keys).Where(id => !areas.Contains(id))
            .Concat(e.PuertasAbiertas.Concat(e.PuertasDesbloqueadas).Where(id => !puertas.Contains(id)))
            .Concat(e.EventosActivos.Where(id => !eventos.Contains(id)))
            .Concat(e.ObjetosRecogidos.Where(id => !objetos.Contains(id)))
            .Distinct();
    }
}

public sealed class ResultadoCarga
{
    public MotorJuego? Motor { get; init; }
    public string? MapaJson { get; init; }
    public ResultadoImportacion? Importacion { get; init; }
    public string? Mensaje { get; init; }

    public bool Exito => Motor is not null;

    public static ResultadoCarga Error(string mensaje) => new() { Mensaje = mensaje };
}
