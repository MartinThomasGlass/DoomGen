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
          .AppendLine("| id | Nombre | Categoría | Cantidad | Nota |").AppendLine("|---|---|---|---|---|");
        foreach (var t in c.Tiles)
            sb.AppendLine($"| `{t.Id}` | {t.Nombre} | {t.Categoria} | {Cant(t.Cantidad)} | {Nota(t.Nota, t.AVerificar)} |");
        sb.AppendLine().AppendLine(FormasDePiezas(c));

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

    /// <summary>
    /// Cada pieza en todas sus rotaciones distintas, con las conexiones marcadas y la linea de
    /// puerta de cada conexion relativa a (x, y): asi la IA no tiene que rotar nada a mano.
    /// </summary>
    private static string FormasDePiezas(Catalogo c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Formas de las piezas en cada rotación").AppendLine()
          .AppendLine("Dibujos: `#` = casilla, `.` = vacío (no es parte de la pieza), `N`/`E`/`S`/`O` = casilla de una conexión en el lado norte/este/sur/oeste. ")
          .AppendLine("La columna \"Puerta\" da la `posicion` exacta de una puerta sobre esa conexión cuando la pieza está en (x, y) con esa rotación.")
          .AppendLine();

        foreach (var t in c.Tiles)
        {
            sb.AppendLine($"#### `{t.Id}` — {t.Nombre} (×{Cant(t.Cantidad)})").AppendLine();
            if (!t.TieneForma)
            {
                sb.AppendLine("Sin forma cargada en el catálogo: indicá `ancho` y `alto` aproximados en el mapa.").AppendLine();
                continue;
            }

            var rotaciones = new[] { 0, 90, 180, 270 }
                .Select(r => (Rotacion: r, Forma: GeometriaTiles.Rotar(t, r)!))
                .DistinctBy(x => string.Join("|", x.Forma.Filas) + "|" + string.Join(",", x.Forma.Conexiones.OrderBy(k => k.Lado).ThenBy(k => k.Desde)))
                .ToList();

            sb.AppendLine("| Rotación | Ancho × alto | Conexiones → Puerta (`posicion`) |").AppendLine("|---|---|---|");
            foreach (var (rot, forma) in rotaciones)
            {
                var conexiones = string.Join(" · ", forma.Conexiones.Select(k => $"{k} → {LineaRelativa(k, forma)}"));
                sb.AppendLine($"| {rot} | {forma.Ancho} × {forma.Alto} | {conexiones} |");
            }
            if (rotaciones.Count < 4)
                sb.AppendLine().AppendLine($"Las rotaciones que no aparecen son idénticas a alguna de las listadas.");

            sb.AppendLine().AppendLine("```");
            foreach (var (rot, forma) in rotaciones)
            {
                sb.AppendLine($"rotación {rot}:");
                foreach (var fila in Dibujar(forma)) sb.AppendLine("  " + fila);
            }
            sb.AppendLine("```").AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string LineaRelativa(ConexionTile k, FormaRotada f) => k.Lado switch
    {
        Lado.Norte => $"horizontal ({Mas("x", k.Desde)}, y)",
        Lado.Sur => $"horizontal ({Mas("x", k.Desde)}, {Mas("y", f.Alto)})",
        Lado.Oeste => $"vertical (x, {Mas("y", k.Desde)})",
        _ => $"vertical ({Mas("x", f.Ancho)}, {Mas("y", k.Desde)})",
    };

    private static string Mas(string eje, int n) => n == 0 ? eje : $"{eje}+{n}";

    private static IEnumerable<string> Dibujar(FormaRotada f)
    {
        var grilla = f.Filas.Select(x => x.ToCharArray()).ToArray();
        foreach (var k in f.Conexiones)
        {
            var letra = k.Lado switch { Lado.Norte => 'N', Lado.Este => 'E', Lado.Sur => 'S', _ => 'O' };
            (int C, int F)[] celdas = k.Lado switch
            {
                Lado.Norte => [(k.Desde, 0), (k.Desde + 1, 0)],
                Lado.Sur => [(k.Desde, f.Alto - 1), (k.Desde + 1, f.Alto - 1)],
                Lado.Oeste => [(0, k.Desde), (0, k.Desde + 1)],
                _ => [(f.Ancho - 1, k.Desde), (f.Ancho - 1, k.Desde + 1)],
            };
            foreach (var (col, fil) in celdas)
                grilla[fil][col] = grilla[fil][col] == '#' ? letra : '+';
        }
        return grilla.Select(x => new string(x));
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
