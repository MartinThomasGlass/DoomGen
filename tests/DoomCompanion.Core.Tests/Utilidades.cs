using System.Text.Json.Nodes;
using DoomCompanion.Core;
using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Tests;

internal static class Utilidades
{
    public static Catalogo CatalogoBase => Recursos.CatalogoPorDefecto();

    public static string EjemploJson => Recursos.LeerTexto(Recursos.Ejemplo);

    public static Mapa Ejemplo => Json.Leer<Mapa>(EjemploJson);

    /// <summary>Carga el ejemplo como arbol JSON editable para armar mapas rotos.</summary>
    public static JsonNode EjemploNodo => JsonNode.Parse(EjemploJson)!;

    public static JsonObject Area(JsonNode raiz, string id) =>
        raiz["areas"]!.AsArray().Select(a => a!.AsObject()).First(a => (string?)a["id"] == id);

    public static JsonObject Puerta(JsonNode raiz, string id) =>
        raiz["puertas"]!.AsArray().Select(a => a!.AsObject()).First(a => (string?)a["id"] == id);

    /// <summary>Mapa minimo de dos areas unidas por una puerta normal.</summary>
    public static Mapa MapaMinimo() => new()
    {
        Escenario = new Escenario
        {
            Id = "minimo",
            Titulo = "Mínimo",
            Marines = 2,
            AreaInicial = "a1",
            CondicionVictoria = new CondicionVictoria { Tipo = TipoVictoria.LlegarAArea, Area = "a2" },
            Textos = new TextosEscenario { Introduccion = "i", Objetivos = "o", Victoria = "v", Derrota = "d" },
        },
        Areas =
        [
            NuevaArea("a1"),
            NuevaArea("a2"),
        ],
        Puertas = [new Puerta { Id = "p1", Desde = "a1", Hacia = "a2", Tipo = TipoPuerta.Normal }],
    };

    public static Area NuevaArea(string id, params GrupoMonstruos[] monstruos) => new()
    {
        Id = id,
        Nombre = "Área " + id,
        Tiles = [new ColocacionTile { Tipo = "sala", X = 0, Y = 0, Rotacion = 0 }],
        Textos = new TextosArea { Entrar = "entrar " + id, Despejar = "despejar " + id },
        Monstruos = [.. monstruos],
        Recompensas = [new Recompensa { Tipo = TipoRecompensa.Texto, Texto = "premio " + id }],
    };
}
