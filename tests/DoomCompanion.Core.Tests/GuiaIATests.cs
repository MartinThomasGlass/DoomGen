using System.Text.RegularExpressions;
using DoomCompanion.Core.Guia;
using DoomCompanion.Core.Validacion;
using static DoomCompanion.Core.Tests.Utilidades;

namespace DoomCompanion.Core.Tests;

public class GuiaIATests
{
    [Fact]
    public void LaGuiaIncluyeTodasLasSecciones()
    {
        var guia = GeneradorGuiaIA.Generar(CatalogoBase);

        Assert.DoesNotContain("{{", guia);
        Assert.Contains("## 6. JSON Schema completo", guia);
        Assert.Contains("\"$schema\": \"https://json-schema.org/draft/2020-12/schema\"", guia);
        Assert.Contains("Estación de bombeo Fobos-3", guia);
        Assert.Contains("| `cyberdemon` | Cyberdemon | 3 | 1 / 2 / 3 |", guia);
        Assert.Contains("#### `sala-9x5` — Sala 9×5 con tres salidas (×1)", guia);
        Assert.Contains("| 0 | 9 × 5 | norte@3 → horizontal (x+3, y) · oeste@2 → vertical (x, y+2) · este@2 → vertical (x+9, y+2) |", guia);
        Assert.Contains("...NN....", guia);
        Assert.Contains("Sin softlocks", guia);
        Assert.Contains("Curva de dificultad", guia);
        Assert.Contains("Distribución del equipo", guia);
    }

    [Fact]
    public void ElEjemploEmbebidoEnLaGuiaEsImportable()
    {
        var guia = GeneradorGuiaIA.Generar(CatalogoBase);
        var bloques = Regex.Matches(guia, "```json\\r?\\n(.*?)```", RegexOptions.Singleline);
        var ejemplo = bloques[^1].Groups[1].Value;

        var r = ImportadorMapa.Importar(ejemplo, CatalogoBase);

        Assert.True(r.SePuedeJugar);
        Assert.Empty(r.Problemas);
    }

    [Fact]
    public void LaGuiaReflejaUnCatalogoEditado()
    {
        var catalogo = CatalogoBase;
        catalogo.Tiles.Add(new DoomCompanion.Core.Modelo.TileCatalogo { Id = "sala-nueva", Nombre = "Sala nueva", Categoria = "sala", Cantidad = 2, Forma = ["###", "###"] });

        var guia = GeneradorGuiaIA.Generar(catalogo);

        Assert.Contains("| `sala-nueva` | Sala nueva | sala | 2 |", guia);
        Assert.Contains("#### `sala-nueva` — Sala nueva (×2)", guia);
    }
}
