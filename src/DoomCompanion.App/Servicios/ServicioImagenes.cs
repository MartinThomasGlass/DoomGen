using System.IO;
using System.Windows.Media.Imaging;

namespace DoomCompanion.App.Servicios;

/// <summary>
/// Imagenes de piezas y fichas en formato DoomGen (64 px por casilla; las piezas de mapa con 1
/// casilla de margen para las pestañas). No vienen con la app: se leen de una carpeta del
/// usuario, por ejemplo la carpeta "doom" de DoomGen. Si falta una imagen, el mapa dibuja la
/// ficha por su cuenta.
/// </summary>
public static class ServicioImagenes
{
    /// <summary>Archivo que tiene que existir para reconocer una carpeta de imagenes valida.</summary>
    private const string ArchivoDePrueba = "4x4_room.png";

    private static readonly Dictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string? Carpeta { get; private set; }

    public static bool HayImagenes => Carpeta is not null;

    public static bool EsCarpetaValida(string? carpeta) =>
        !string.IsNullOrWhiteSpace(carpeta) && File.Exists(Path.Combine(carpeta, ArchivoDePrueba));

    /// <summary>Carpeta configurada, o la primera de las ubicaciones habituales que tenga imagenes.</summary>
    public static string? BuscarCarpeta(string? configurada)
    {
        var candidatas = new[]
        {
            configurada,
            Path.Combine(ServicioDatos.Carpeta, "imagenes"),
            Path.Combine(AppContext.BaseDirectory, "imagenes"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DoomGen", "doom"),
        };
        return candidatas.FirstOrDefault(EsCarpetaValida);
    }

    public static void Usar(string? carpeta)
    {
        Carpeta = EsCarpetaValida(carpeta) ? carpeta : null;
        Cache.Clear();
    }

    public static BitmapSource? Cargar(string? archivo)
    {
        if (Carpeta is null || string.IsNullOrWhiteSpace(archivo)) return null;
        if (Cache.TryGetValue(archivo, out var guardada)) return guardada;

        BitmapSource? imagen = null;
        var ruta = Path.Combine(Carpeta, archivo);
        if (File.Exists(ruta))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(ruta);
                bmp.EndInit();
                bmp.Freeze();
                imagen = bmp;
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
            {
                imagen = null;
            }
        }
        Cache[archivo] = imagen;
        return imagen;
    }

    /// <summary>Medida de la imagen en casillas (64 px por casilla).</summary>
    public static (int Ancho, int Alto) Casillas(BitmapSource imagen) =>
        ((int)Math.Round(imagen.PixelWidth / 64.0), (int)Math.Round(imagen.PixelHeight / 64.0));
}
