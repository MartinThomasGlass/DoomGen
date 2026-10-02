using System.Text.Json.Nodes;
using DoomCompanion.Core.Validacion;
using static DoomCompanion.Core.Tests.Utilidades;

namespace DoomCompanion.Core.Tests;

public class ValidacionEsquemaTests
{
    [Fact]
    public void ElEjemploIncluidoEsValidoYSinProblemas()
    {
        var r = ImportadorMapa.Importar(EjemploJson, CatalogoBase);

        Assert.True(r.SePuedeJugar, string.Join("\n", r.Problemas));
        Assert.Empty(r.Problemas);
        Assert.Equal("Mapa válido.", r.Resumen);
    }

    [Fact]
    public void JsonMalFormadoIndicaLinea()
    {
        var r = ImportadorMapa.Importar("{\n \"version\": 1,\n \"escenario\": { ", CatalogoBase);

        var p = Assert.Single(r.Problemas);
        Assert.False(r.SePuedeJugar);
        Assert.Contains("no es un JSON válido", p.Mensaje);
        Assert.Contains("línea", p.Mensaje);
    }

    [Fact]
    public void CampoObligatorioFaltanteIndicaRutaExacta()
    {
        var raiz = EjemploNodo;
        Area(raiz, "a3")["textos"]!.AsObject().Remove("despejar");

        var r = ImportadorMapa.Importar(raiz.ToJsonString(), CatalogoBase);

        var p = Assert.Single(r.Problemas);
        Assert.Equal(CategoriaProblema.Esquema, p.Categoria);
        Assert.Equal("areas[2] (a3).textos", p.Ruta);
        Assert.Contains("despejar", p.Mensaje);
        Assert.Contains("Falta el campo obligatorio", p.Mensaje);
    }

    [Fact]
    public void RotacionInvalidaIndicaValoresPermitidos()
    {
        var raiz = EjemploNodo;
        Area(raiz, "a2")["tiles"]![0]!["rotacion"] = 45;

        var r = ImportadorMapa.Importar(raiz.ToJsonString(), CatalogoBase);

        var p = Assert.Single(r.Problemas);
        Assert.Equal("areas[1] (a2).tiles[0].rotacion", p.Ruta);
        Assert.Contains("Valor no permitido", p.Mensaje);
        Assert.Contains("90", p.Mensaje);
    }

    [Fact]
    public void TipoIncorrectoSeInformaEnEspanol()
    {
        var raiz = EjemploNodo;
        raiz["escenario"]!["marines"] = "dos";

        var r = ImportadorMapa.Importar(raiz.ToJsonString(), CatalogoBase);

        var p = Assert.Single(r.Problemas);
        Assert.Equal("escenario.marines", p.Ruta);
        Assert.Contains("número entero", p.Mensaje);
        Assert.Contains("texto", p.Mensaje);
    }

    [Fact]
    public void CampoDesconocidoSeRechaza()
    {
        var raiz = EjemploNodo;
        Puerta(raiz, "p1")["color"] = "rojo";

        var r = ImportadorMapa.Importar(raiz.ToJsonString(), CatalogoBase);

        Assert.False(r.SePuedeJugar);
        var p = Assert.Single(r.Problemas);
        Assert.Equal("puertas[0] (p1).color", p.Ruta);
        Assert.Contains("Campo no reconocido", p.Mensaje);
    }

    [Fact]
    public void RequisitoConDosCondicionesSeRechaza()
    {
        var raiz = EjemploNodo;
        Puerta(raiz, "p3")["requisitos"] = new JsonObject { ["areaDespejada"] = "a2", ["evento"] = "ev-energia" };

        var r = ImportadorMapa.Importar(raiz.ToJsonString(), CatalogoBase);

        var p = Assert.Single(r.Problemas);
        Assert.Equal("puertas[2] (p3).requisitos", p.Ruta);
        Assert.Contains("exactamente una condición", p.Mensaje);
    }

    [Fact]
    public void RecompensaOtorgarObjetoSinObjetoSeRechaza()
    {
        var raiz = EjemploNodo;
        Area(raiz, "a1")["recompensas"]![0]!.AsObject().Remove("objeto");

        var r = ImportadorMapa.Importar(raiz.ToJsonString(), CatalogoBase);

        var p = Assert.Single(r.Problemas);
        Assert.Equal("areas[0] (a1).recompensas[0]", p.Ruta);
        Assert.Contains("objeto", p.Mensaje);
    }

    [Fact]
    public void IdConMayusculasOEspaciosSeRechaza()
    {
        var raiz = EjemploNodo;
        raiz["eventos"]![0]!["id"] = "Energía Auxiliar";

        var r = ImportadorMapa.Importar(raiz.ToJsonString(), CatalogoBase);

        Assert.Contains(r.Problemas, p => p.Ruta.StartsWith("eventos[0]") && p.Mensaje.Contains("Identificador inválido"));
    }

    [Fact]
    public void VersionDistintaDeUnoSeRechaza()
    {
        var raiz = EjemploNodo;
        raiz["version"] = 2;

        var r = ImportadorMapa.Importar(raiz.ToJsonString(), CatalogoBase);

        var p = Assert.Single(r.Problemas);
        Assert.Equal("version", p.Ruta);
    }
}
