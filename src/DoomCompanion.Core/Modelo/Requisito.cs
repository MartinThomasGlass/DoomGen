using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoomCompanion.Core.Modelo;

/// <summary>
/// Condicion para abrir una puerta. Es un arbol de "todas" (Y) y "alguna" (O) con hojas
/// areaDespejada, objeto, puertaAbierta y evento. No hay negacion a proposito: asi la logica
/// es monotona (nada vuelve a cerrar un camino) y la deteccion de softlocks es exacta.
/// </summary>
[JsonConverter(typeof(ConversorRequisito))]
public abstract record Requisito
{
    /// <summary>Recorre este requisito y todos sus hijos.</summary>
    public IEnumerable<Requisito> Recorrer()
    {
        yield return this;
        var hijos = this switch
        {
            RequisitoTodas t => t.Condiciones,
            RequisitoAlguna a => a.Condiciones,
            _ => [],
        };
        foreach (var hijo in hijos)
            foreach (var r in hijo.Recorrer())
                yield return r;
    }
}

public sealed record RequisitoTodas(IReadOnlyList<Requisito> Condiciones) : Requisito;
public sealed record RequisitoAlguna(IReadOnlyList<Requisito> Condiciones) : Requisito;
public sealed record RequisitoAreaDespejada(string Area) : Requisito;
public sealed record RequisitoObjeto(string Objeto, int Cantidad) : Requisito;
public sealed record RequisitoPuertaAbierta(string Puerta) : Requisito;
public sealed record RequisitoEvento(string Evento) : Requisito;

public sealed class ConversorRequisito : JsonConverter<Requisito>
{
    public override Requisito Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return Leer(doc.RootElement);
    }

    private static Requisito Leer(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new JsonException("Un requisito debe ser un objeto.");

        if (e.TryGetProperty("todas", out var todas))
            return new RequisitoTodas(todas.EnumerateArray().Select(Leer).ToList());
        if (e.TryGetProperty("alguna", out var alguna))
            return new RequisitoAlguna(alguna.EnumerateArray().Select(Leer).ToList());
        if (e.TryGetProperty("areaDespejada", out var area))
            return new RequisitoAreaDespejada(area.GetString()!);
        if (e.TryGetProperty("objeto", out var objeto))
        {
            var cantidad = e.TryGetProperty("cantidad", out var c) ? c.GetInt32() : 1;
            return new RequisitoObjeto(objeto.GetString()!, cantidad);
        }
        if (e.TryGetProperty("puertaAbierta", out var puerta))
            return new RequisitoPuertaAbierta(puerta.GetString()!);
        if (e.TryGetProperty("evento", out var evento))
            return new RequisitoEvento(evento.GetString()!);

        throw new JsonException("Requisito sin ninguna condición reconocida.");
    }

    public override void Write(Utf8JsonWriter writer, Requisito value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case RequisitoTodas t:
                writer.WritePropertyName("todas");
                EscribirLista(writer, t.Condiciones, options);
                break;
            case RequisitoAlguna a:
                writer.WritePropertyName("alguna");
                EscribirLista(writer, a.Condiciones, options);
                break;
            case RequisitoAreaDespejada d:
                writer.WriteString("areaDespejada", d.Area);
                break;
            case RequisitoObjeto o:
                writer.WriteString("objeto", o.Objeto);
                if (o.Cantidad != 1) writer.WriteNumber("cantidad", o.Cantidad);
                break;
            case RequisitoPuertaAbierta p:
                writer.WriteString("puertaAbierta", p.Puerta);
                break;
            case RequisitoEvento ev:
                writer.WriteString("evento", ev.Evento);
                break;
        }
        writer.WriteEndObject();
    }

    private void EscribirLista(Utf8JsonWriter writer, IReadOnlyList<Requisito> lista, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var r in lista) Write(writer, r, options);
        writer.WriteEndArray();
    }
}
