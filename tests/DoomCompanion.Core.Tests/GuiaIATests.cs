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
        catalogo.Tiles[0].Ancho = 4;
        catalogo.Tiles[0].Alto = 6;

        var guia = GeneradorGuiaIA.Generar(catalogo);

        Assert.Contains("| `sala` | Sala | 12 | 4×6 |", guia);
    }
}
