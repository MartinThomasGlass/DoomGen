using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;

namespace DoomCompanion.Core.Validacion;

/// <summary>Valida el texto de un mapa contra el JSON Schema, con errores en español.</summary>
public static partial class ValidadorEsquema
{
    private static readonly Lazy<JsonSchema> Esquema = new(() =>
    {
        ConfigurarMensajes();
        return JsonSchema.FromText(Recursos.LeerTexto(Recursos.Esquema));
    });

    public static List<Problema> Validar(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            var donde = ex.LineNumber is long linea
                ? $" (línea {linea + 1}, posición {ex.BytePositionInLine + 1})"
                : "";
            return [new Problema(Severidad.Error, CategoriaProblema.Esquema, "(archivo)",
                $"El archivo no es un JSON válido{donde}. Revisá comas, comillas y llaves.")];
        }

        using (doc)
        {
            var resultado = Esquema.Value.Evaluate(doc.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
                IncludeApplicatorErrors = false,
            });
            if (resultado.IsValid) return [];

            var problemas = new List<Problema>();
            var vistos = new HashSet<string>();
            foreach (var detalle in resultado.Details ?? [])
            {
                // La salida en lista incluye ramas que fallan dentro de un oneOf/if que en conjunto
                // es valido: solo interesan los errores cuya cadena de padres es toda invalida.
                if (detalle.IsValid || AncestroValido(detalle)) continue;
                var camino = detalle.EvaluationPath.ToString();
                if (camino.Contains("/if")) continue;

                var ruta = FormatearRuta(detalle.InstanceLocation.ToString(), doc.RootElement);
                if (RamaOneOf().IsMatch(camino))
                {
                    // El oneOf no produce mensaje propio: se informa una vez por ubicacion.
                    if (vistos.Add(ruta + "|oneOf"))
                        problemas.Add(new Problema(Severidad.Error, CategoriaProblema.Esquema, ruta, MensajeOneOf));
                    continue;
                }
                if (detalle.Errors is null || detalle.Errors.Count == 0) continue;

                foreach (var (_, mensaje) in detalle.Errors)
                {
                    var texto = Traducir(mensaje);
                    if (vistos.Add(ruta + "|" + texto))
                        problemas.Add(new Problema(Severidad.Error, CategoriaProblema.Esquema, ruta, texto));
                }
            }
            if (problemas.Count == 0)
                problemas.Add(new Problema(Severidad.Error, CategoriaProblema.Esquema, "(archivo)",
                    "El archivo no respeta el formato de mapa."));
            return problemas;
        }
    }

    /// <summary>Convierte "/areas/2/tiles/0/rotacion" en "areas[2] (a3).tiles[0].rotacion".</summary>
    internal static string FormatearRuta(string puntero, JsonElement raiz)
    {
        if (string.IsNullOrEmpty(puntero) || puntero == "/" || puntero == "#") return "(raíz del archivo)";
        var sb = new StringBuilder();
        JsonElement? actual = raiz;
        foreach (var crudo in puntero.TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segmento = crudo.Replace("~1", "/").Replace("~0", "~");
            if (int.TryParse(segmento, out var indice) && actual is { ValueKind: JsonValueKind.Array } arr)
            {
                sb.Append('[').Append(indice).Append(']');
                actual = indice < arr.GetArrayLength() ? arr[indice] : null;
                if (actual is { ValueKind: JsonValueKind.Object } obj
                    && obj.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    sb.Append(" (").Append(id.GetString()).Append(')');
            }
            else
            {
                if (sb.Length > 0) sb.Append('.');
                sb.Append(segmento);
                actual = actual is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(segmento, out var hijo)
                    ? hijo
                    : null;
            }
        }
        return sb.ToString();
    }

    private static string Traducir(string mensaje)
    {
        // Los nombres de tipos JSON llegan en ingles dentro de los tokens.
        return TipoJson().Replace(mensaje, m => m.Value switch
        {
            "string" => "texto",
            "integer" => "número entero",
            "number" => "número",
            "object" => "objeto",
            "array" => "lista",
            "boolean" => "verdadero/falso",
            "null" => "nulo",
            _ => m.Value,
        });
    }

    [GeneratedRegex(@"\b(string|integer|number|object|array|boolean|null)\b")]
    private static partial Regex TipoJson();

    [GeneratedRegex(@"/oneOf/\d+$")]
    private static partial Regex RamaOneOf();

    private const string MensajeOneOf =
        "Un requisito debe tener exactamente una condición: todas, alguna, areaDespejada, objeto (con cantidad opcional), puertaAbierta o evento. Para combinar varias usá \"todas\" o \"alguna\".";

    private static bool AncestroValido(EvaluationResults detalle)
    {
        for (var p = detalle.Parent; p is not null; p = p.Parent)
            if (p.IsValid) return true;
        return false;
    }

    private static void ConfigurarMensajes()
    {
        ErrorMessages.Required = "Falta el campo obligatorio [[missing]].";
        ErrorMessages.Type = "Tipo incorrecto: se esperaba [[expected]] y llegó [[received]].";
        ErrorMessages.Enum = "Valor no permitido [[received]]. Valores válidos: [[values]].";
        ErrorMessages.Const = "Debe valer exactamente [[value]].";
        ErrorMessages.Minimum = "El valor [[received]] es menor que el mínimo permitido ([[limit]]).";
        ErrorMessages.Maximum = "El valor [[received]] es mayor que el máximo permitido ([[limit]]).";
        ErrorMessages.MinItems = "Debe tener al menos [[limit]] elemento(s) y tiene [[received]].";
        ErrorMessages.MaxItems = "Puede tener como máximo [[limit]] elemento(s) y tiene [[received]].";
        ErrorMessages.MinLength = "No puede estar vacío.";
        ErrorMessages.MaxLength = "Es demasiado largo (máximo [[limit]] caracteres).";
        ErrorMessages.Pattern = "Identificador inválido: solo se permiten minúsculas sin tildes, números, guiones y guiones bajos (sin espacios), y debe empezar con letra o número.";
        ErrorMessages.AdditionalProperties = "Campo no reconocido: [[failed]]. Revisá que esté bien escrito y en el lugar correcto.";
        ErrorMessages.FalseSchema = "Campo no reconocido en esta posición. Revisá que esté bien escrito y en el lugar correcto.";
        ErrorMessages.OneOf = MensajeOneOf;
        ErrorMessages.DependentRequired = "\"cantidad\" solo se puede usar junto con \"objeto\".";
        ErrorMessages.MinProperties = "No puede estar vacío.";
    }
}
