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

    /// <summary>
    /// Mapa minimo de dos areas unidas por una puerta normal. Cada area es un callejon sin
    /// salida y los dos se enfrentan por su abertura: geometria valida y sin conexiones libres.
    /// </summary>
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
            NuevaArea("a1", new ColocacionTile { Tipo = "callejon", X = 0, Y = 0, Rotacion = 180 }),
            NuevaArea("a2", new ColocacionTile { Tipo = "callejon", X = 1, Y = 0, Rotacion = 0 }),
        ],
        Puertas =
        [
            new Puerta
            {
                Id = "p1", Desde = "a1", Hacia = "a2", Tipo = TipoPuerta.Normal,
                Posicion = new PosicionPuerta { X = 1, Y = 0, Orientacion = Orientacion.Vertical },
            },
        ],
    };

    /// <summary>Area con una recompensa de texto. Sin pieza indicada, un callejon lejos del resto.</summary>
    public static Area NuevaArea(string id, ColocacionTile? tile = null) => new()
    {
        Id = id,
        Nombre = "Área " + id,
        Tiles = [tile ?? new ColocacionTile { Tipo = "callejon", X = 20, Y = 20, Rotacion = 0 }],
        Textos = new TextosArea { Entrar = "entrar " + id, Despejar = "despejar " + id },
        Recompensas = [new Recompensa { Tipo = TipoRecompensa.Texto, Texto = "premio " + id }],
    };
}
