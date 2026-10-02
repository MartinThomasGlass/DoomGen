using System.Diagnostics;
using System.IO;
using System.Text.Json;
using DoomCompanion.Core;
using DoomCompanion.Core.Modelo;

namespace DoomCompanion.App.Servicios;

/// <summary>
/// Archivos de la app en %APPDATA%\DoomCompanion: catalogo editable y autoguardado.
/// No se guarda nada en la carpeta de instalacion porque Velopack la reemplaza al actualizar.
/// </summary>
public sealed class ServicioDatos
{
    public static string Carpeta { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DoomCompanion");

    public static string RutaCatalogo => Path.Combine(Carpeta, Recursos.Catalogo);
    public static string RutaAutoguardado => Path.Combine(Carpeta, "autoguardado.doomsave");

    /// <summary>Carga el catalogo del usuario si existe; si no, el que viene con la app.</summary>
    public static (Catalogo Catalogo, string? Error) CargarCatalogo()
    {
        if (!File.Exists(RutaCatalogo)) return (Recursos.CatalogoPorDefecto(), null);
        try
        {
            var catalogo = DoomCompanion.Core.Json.Leer<Catalogo>(File.ReadAllText(RutaCatalogo));
            var errores = catalogo.Validar();
            if (errores.Count == 0) return (catalogo, null);
            return (Recursos.CatalogoPorDefecto(),
                $"Tu catálogo ({RutaCatalogo}) tiene errores:\n\n• {string.Join("\n• ", errores)}\n\nSe usa el catálogo que viene con la app.");
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return (Recursos.CatalogoPorDefecto(),
                $"No se pudo leer tu catálogo ({RutaCatalogo}):\n{ex.Message}\n\nSe usa el catálogo que viene con la app.");
        }
    }

    /// <summary>Copia el catalogo por defecto a la carpeta de datos (si hace falta) y lo abre.</summary>
    public static void AbrirCatalogoParaEditar()
    {
        Directory.CreateDirectory(Carpeta);
        if (!File.Exists(RutaCatalogo))
            File.WriteAllText(RutaCatalogo, Recursos.LeerTexto(Recursos.Catalogo));
        try
        {
            Process.Start(new ProcessStartInfo(RutaCatalogo) { UseShellExecute = true });
        }
        catch (Exception)
        {
            Process.Start("notepad.exe", $"\"{RutaCatalogo}\"");
        }
    }

    // ------------------------------------------------------------------ Configuracion

    private static string RutaConfiguracion => Path.Combine(Carpeta, "configuracion.json");

    public sealed class Configuracion
    {
        public string? CarpetaImagenes { get; set; }
    }

    public static Configuracion LeerConfiguracion()
    {
        try
        {
            return File.Exists(RutaConfiguracion)
                ? DoomCompanion.Core.Json.Leer<Configuracion>(File.ReadAllText(RutaConfiguracion))
                : new Configuracion();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new Configuracion();
        }
    }

    public static void GuardarConfiguracion(Configuracion configuracion)
    {
        Directory.CreateDirectory(Carpeta);
        File.WriteAllText(RutaConfiguracion, DoomCompanion.Core.Json.Escribir(configuracion));
    }

    public static void AbrirCarpeta(string ruta)
    {
        Directory.CreateDirectory(ruta);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ruta}\"") { UseShellExecute = true });
    }
}
