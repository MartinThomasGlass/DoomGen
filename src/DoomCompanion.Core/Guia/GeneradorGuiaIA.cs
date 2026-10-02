using System.Text;
using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Guia;

/// <summary>Arma la guia .md para pegarle a una IA externa y pedirle mapas.</summary>
public static class GeneradorGuiaIA
{
    public static string Generar(Catalogo catalogo) =>
        Recursos.LeerTexto("guia-ia.md")
            .Replace("{{CATALOGO}}", TablasCatalogo(catalogo))
            .Replace("{{PREPARACION}}", TablaPreparacion(catalogo))
            .Replace("{{ESQUEMA}}", Recursos.LeerTexto(Recursos.Esquema).Trim())
            .Replace("{{EJEMPLO}}", Recursos.LeerTexto(Recursos.Ejemplo).Trim());

    private static string TablasCatalogo(Catalogo c)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(c.Nota)) sb.AppendLine($"> {c.Nota}").AppendLine();

        sb.AppendLine("### Tiles (piezas de mapa)").AppendLine()
          .AppendLine("| id | Nombre | Cantidad | Medidas (casillas) | Nota |").AppendLine("|---|---|---|---|---|");
        foreach (var t in c.Tiles)
        {
            var medidas = t.Ancho is int an && t.Alto is int al ? $"{an}×{al}" : "sin cargar";
            sb.AppendLine($"| `{t.Id}` | {t.Nombre} | {Cant(t.Cantidad)} | {medidas} | {Nota(t.Nota, t.AVerificar)} |");
        }

        sb.AppendLine().AppendLine("### Puertas físicas").AppendLine()
          .AppendLine("| Tipo | Nombre | Cantidad | Nota |").AppendLine("|---|---|---|---|");
        foreach (var p in c.Puertas)
            sb.AppendLine($"| `{p.Tipo.ToString().ToLowerInvariant()}` | {p.Nombre} | {p.Cantidad} | {Nota(p.Nota, p.AVerificar)} |");

        sb.AppendLine().AppendLine("### Monstruos").AppendLine()
          .AppendLine("| id | Nombre | En la caja | Disponibles con 1 / 2 / 3 marines | Casillas | Amenaza | Nota |")
          .AppendLine("|---|---|---|---|---|---|---|");
        foreach (var m in c.Monstruos)
            sb.AppendLine($"| `{m.Id}` | {m.Nombre} | {m.Cantidad} | {m.Disponibles(1)} / {m.Disponibles(2)} / {m.Disponibles(3)} | {m.Casillas} | {m.Amenaza} | {Nota(m.Nota, false)} |");
        sb.AppendLine().AppendLine("La amenaza es un peso orientativo para balancear, no un valor oficial.");

        sb.AppendLine().AppendLine("### Objetos (equipo)").AppendLine()
          .AppendLine("| id | Nombre | Categoría | Cantidad | Munición | Nota |").AppendLine("|---|---|---|---|---|---|");
        foreach (var o in c.Objetos)
        {
            var municion = o.Categoria == CategoriaObjeto.Arma ? (o.Municion is { } m ? $"`{m}`" : "no usa") : "";
            var cant = Cant(o.Cantidad) + (o.Estimado ? " (estimada)" : "");
            sb.AppendLine($"| `{o.Id}` | {o.Nombre} | {o.Categoria.ToString().ToLowerInvariant()} | {cant} | {municion} | {Nota(o.Nota, false)} |");
        }
        if (c.GruposEquipo.Count > 0)
        {
            sb.AppendLine().AppendLine("Totales de fichas de equipo según el manual: " +
                string.Join(", ", c.GruposEquipo.Select(g => $"{g.Nombre.ToLowerInvariant()}: {g.Total}")) + ".");
        }
        if (!string.IsNullOrWhiteSpace(c.ArmasBasicas)) sb.AppendLine().AppendLine(c.ArmasBasicas);

        sb.AppendLine().AppendLine("### Fichas de escenografía").AppendLine()
          .AppendLine("| id | Nombre | Cantidad | Nota |").AppendLine("|---|---|---|---|");
        foreach (var f in c.Fichas)
            sb.AppendLine($"| `{f.Id}` | {f.Nombre} | {Cant(f.Cantidad)} | {Nota(f.Nota, f.AVerificar)} |");

        return sb.ToString().TrimEnd();
    }

    private static string TablaPreparacion(Catalogo c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Cada marine empieza con (no lo coloques en el mapa):").AppendLine()
          .AppendLine("| Marines | Munición (balas) | Heridas | Armadura | Cartas de marine |").AppendLine("|---|---|---|---|---|");
        foreach (var p in c.PreparacionMarines)
            sb.AppendLine($"| {p.Marines} | {p.Municion} | {p.Heridas} | {p.Armadura} | {p.Cartas} |");
        return sb.ToString().TrimEnd();
    }

    private static string Cant(int? n) => n?.ToString() ?? "a verificar";

    private static string Nota(string? nota, bool aVerificar) =>
        ((nota ?? "") + (aVerificar ? " (a verificar)" : "")).Replace("|", "/").Trim();
}
