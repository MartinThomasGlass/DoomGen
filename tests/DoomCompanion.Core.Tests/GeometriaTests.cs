using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Validacion;
using static DoomCompanion.Core.Tests.Utilidades;

namespace DoomCompanion.Core.Tests;

public class GeometriaTests
{
    private static TileCatalogo Pieza(string id) => CatalogoBase.BuscarTile(id)!;

    [Fact]
    public void ElCatalogoIncluidoEsValidoYSumaLas58PiezasDelManual()
    {
        var catalogo = CatalogoBase;

        Assert.Empty(catalogo.Validar());
        Assert.All(catalogo.Tiles, t => Assert.True(t.TieneForma, t.Id));
        Assert.Equal(58, catalogo.Tiles.Sum(t => t.Cantidad));
        Assert.Equal(12, catalogo.Tiles.Where(t => t.Categoria == "sala").Sum(t => t.Cantidad));
        Assert.Equal(14, catalogo.Tiles.Where(t => t.Categoria == "pasillo").Sum(t => t.Cantidad));
        Assert.Equal(5, catalogo.Tiles.Where(t => t.Categoria == "curva").Sum(t => t.Cantidad));
        Assert.Equal(6, catalogo.Tiles.Where(t => t.Categoria == "interseccion").Sum(t => t.Cantidad));
        Assert.Equal(21, catalogo.Tiles.Where(t => t.Categoria == "callejon").Sum(t => t.Cantidad));
    }

    [Fact]
    public void RotarUnaCurvaGiraFormaYConexionesEnSentidoHorario()
    {
        // rotacion 0: ##. / ### / ###  con norte@0 y este@1
        var r90 = GeometriaTiles.Rotar(Pieza("curva-chica"), 90)!;
        var r270 = GeometriaTiles.Rotar(Pieza("curva-chica"), 270)!;

        Assert.Equal(["###", "###", "##."], r90.Filas);
        Assert.Equal(["este@0", "sur@0"], r90.Conexiones.Select(c => c.ToString()).Order());
        Assert.Equal([".##", "###", "###"], r270.Filas);
        Assert.Equal(["norte@1", "oeste@1"], r270.Conexiones.Select(c => c.ToString()).Order());
    }

    [Fact]
    public void RotarUnaSalaIrregularCambiaSusMedidas()
    {
        var r180 = GeometriaTiles.Rotar(Pieza("sala-9x5"), 180)!;
        var r90 = GeometriaTiles.Rotar(Pieza("sala-9x5"), 90)!;

        Assert.Equal([".#######.", "#########", "#########", ".#######.", "....##..."], r180.Filas);
        Assert.Equal(["este@1", "oeste@1", "sur@4"], r180.Conexiones.Select(c => c.ToString()).Order());
        Assert.Equal((5, 9), (r90.Ancho, r90.Alto));
    }

    [Fact]
    public void CuatroRotacionesVuelvenAlOrigen()
    {
        foreach (var t in CatalogoBase.Tiles)
        {
            var r0 = GeometriaTiles.Rotar(t, 0)!;
            var r360 = GeometriaTiles.Rotar(t, 360)!;
            Assert.Equal(r0.Filas, r360.Filas);
            Assert.Equal(r0.Conexiones.Select(c => c.ToString()), r360.Conexiones.Select(c => c.ToString()));
        }
    }

    [Fact]
    public void CeldasYConexionesEnCoordenadasDelMapa()
    {
        var tile = new ColocacionTile { Tipo = "pasillo-largo", X = 20, Y = 10, Rotacion = 90 };

        var celdas = GeometriaTiles.Celdas(tile, CatalogoBase);
        var conexiones = GeometriaTiles.Conexiones(tile, CatalogoBase);

        Assert.Equal(12, celdas.Count);
        Assert.Contains(new Celda(25, 11), celdas);
        Assert.Contains((Lado.Oeste, new LineaConexion(20, 10, Orientacion.Vertical)), conexiones);
        Assert.Contains((Lado.Este, new LineaConexion(26, 10, Orientacion.Vertical)), conexiones);
    }

    [Fact]
    public void CatalogoConFormaMalEscritaSeInforma()
    {
        var t = new TileCatalogo
        {
            Id = "rara",
            Forma = ["##", "#"],
            Conexiones = [new ConexionTile { Lado = Lado.Este, Desde = 5 }],
        };

        var errores = t.Validar();

        Assert.Contains(errores, e => e.Contains("mismo largo"));
    }

    [Fact]
    public void ConexionQueNoCaeSobreLaFormaSeInforma()
    {
        var t = new TileCatalogo
        {
            Id = "rara",
            Forma = ["##.", "###"],
            Conexiones = [new ConexionTile { Lado = Lado.Norte, Desde = 1 }],
        };

        Assert.Contains(t.Validar(), e => e.Contains("no coincide con casillas del borde"));
    }
}

public class ValidacionGeometriaTests
{
    private static IEnumerable<Problema> Avisos(Mapa mapa) =>
        ValidadorGeometria.Validar(mapa, CatalogoBase);

    [Fact]
    public void ElMapaMinimoNoTieneAvisosDeGeometria()
    {
        Assert.Empty(Avisos(MapaMinimo()));
    }

    [Fact]
    public void PiezasSuperpuestas()
    {
        var mapa = MapaMinimo();
        mapa.Areas[1].Tiles[0].X = 0; // las dos piezas en (0, 0)

        Assert.Contains(Avisos(mapa), p => p.Mensaje.Contains("se superpone"));
    }

    [Fact]
    public void ConexionLibreSinTapar()
    {
        var mapa = MapaMinimo();
        mapa.Areas[1].Tiles.Add(new ColocacionTile { Tipo = "pasillo-corto", X = 5, Y = 5, Rotacion = 0 });

        var avisos = Avisos(mapa).ToList();

        Assert.Equal(2, avisos.Count(p => p.Mensaje.StartsWith("Conexión libre")));
        Assert.Contains(avisos, p => p.Mensaje.Contains("no están todas unidas"));
    }

    [Fact]
    public void PuertaFueraDeUnaConexion()
    {
        var mapa = MapaMinimo();
        mapa.Puertas[0].Posicion = new PosicionPuerta { X = 7, Y = 7, Orientacion = Orientacion.Horizontal };

        var avisos = Avisos(mapa).ToList();

        Assert.Contains(avisos, p => p.Ruta == "puertas[0] (p1)" && p.Mensaje.Contains("no está sobre una conexión"));
        Assert.Contains(avisos, p => p.Mensaje.Contains("sin puerta"));
    }

    [Fact]
    public void PuertaSinPosicion()
    {
        var mapa = MapaMinimo();
        mapa.Puertas[0].Posicion = null;

        Assert.Contains(Avisos(mapa), p => p.Mensaje.Contains("no tiene \"posicion\""));
    }

    [Fact]
    public void TeleportadorNoNecesitaPosicion()
    {
        var mapa = MapaMinimo();
        mapa.Puertas.Add(new Puerta { Id = "p2", Desde = "a1", Hacia = "a2", Tipo = TipoPuerta.Teleportador });

        Assert.Empty(Avisos(mapa));
    }

    [Fact]
    public void PiezasDeUnAreaUnidasPorConexionNoGeneranAviso()
    {
        // Area a2: callejon (abre al oeste) + ... reemplazada por pasillo corto horizontal tapado.
        var mapa = MapaMinimo();
        mapa.Areas[1].Tiles =
        [
            new ColocacionTile { Tipo = "pasillo-corto", X = 1, Y = 0, Rotacion = 90 },  // ocupa (1..3, 0..1)
            new ColocacionTile { Tipo = "callejon", X = 4, Y = 0, Rotacion = 0 },        // tapa el este
        ];

        Assert.Empty(Avisos(mapa));
    }
}
