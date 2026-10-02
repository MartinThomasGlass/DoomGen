using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core;

/// <summary>Opciones de serializacion compartidas y acceso a los recursos embebidos.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static T Leer<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Opciones) ?? throw new JsonException("El archivo está vacío.");

    public static string Escribir<T>(T valor) => JsonSerializer.Serialize(valor, Opciones);
}

public static class Recursos
{
    public const string Esquema = "esquema-mapa.v1.json";
    public const string Catalogo = "catalogo.json";
    public const string Ejemplo = "escenario-ejemplo.json";

    public static string LeerTexto(string nombre)
    {
        var asm = typeof(Recursos).Assembly;
        var recurso = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("." + nombre, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No se encontró el recurso embebido '{nombre}'.");
        using var stream = asm.GetManifestResourceStream(recurso)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static Catalogo CatalogoPorDefecto() => Json.Leer<Catalogo>(LeerTexto(Catalogo));
}
