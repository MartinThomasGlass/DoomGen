using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Motor;
using DoomCompanion.Core.Validacion;
using static DoomCompanion.Core.Tests.Utilidades;

namespace DoomCompanion.Core.Tests;

public class DistribucionFichasTests
{
    private static MotorJuego NuevoMotorEjemplo(params ColorFigura[] colores)
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);
        motor.Iniciar(colores.Length > 0 ? colores : null);
        return motor;
    }

    [Fact]
    public void LasFichasDelMapaVanDondeDiceElMapa()
    {
        var motor = NuevoMotorEjemplo();
        var area = motor.Mapa.BuscarArea("a1")!;

        var fichas = DistribucionFichas.Calcular(motor.Mapa, motor.Catalogo, motor.Estado, area);

        Assert.Contains(fichas, f => f is { Clase: ClaseFicha.Monstruo, Tipo: "zombie", X: 17, Y: 10, Automatica: false });
        Assert.Contains(fichas, f => f is { Clase: ClaseFicha.Objeto, Id: "o2", X: 19, Y: 9 });
        Assert.Contains(fichas, f => f is { Clase: ClaseFicha.Escenografia, Tipo: "obstaculo-1x2", X: 14, Y: 11, Ancho: 2, Alto: 1 });
        Assert.Contains(fichas, f => f is { Clase: ClaseFicha.Marine, Color: ColorFigura.Verde, X: 12, Y: 11 });
    }

    [Fact]
    public void SinPosicionesLaAppLasUbicaDentroDelAreaSinEncimarlas()
    {
        var mapa = Ejemplo;
        foreach (var a in mapa.Areas)
        {
            foreach (var g in a.Monstruos) g.Posiciones = null;
            foreach (var o in a.Objetos) (o.X, o.Y) = (null, null);
            foreach (var f in a.Fichas) f.Posiciones = null;
        }
        mapa.Escenario.InicioMarines = null;
        var motor = new MotorJuego(mapa, CatalogoBase);
        motor.Iniciar([ColorFigura.Rojo, ColorFigura.Verde, ColorFigura.Azul]);

        foreach (var area in mapa.Areas)
        {
            motor.Estado.AreasReveladas.Add(area.Id);
            if (!motor.Estado.Monstruos.ContainsKey(area.Id))
                motor.Estado.Monstruos[area.Id] = area.Monstruos.SelectMany(g => Enumerable.Range(0, g.Cantidad)
                    .Select(i => new MonstruoEnJuego { Id = $"{area.Id}-{g.Tipo}-{i}", Tipo = g.Tipo })).ToList();

            var fichas = DistribucionFichas.Calcular(mapa, CatalogoBase, motor.Estado, area);
            var celdasArea = area.Tiles.SelectMany(t => GeometriaTiles.Celdas(t, CatalogoBase)).ToHashSet();
            var todas = fichas.SelectMany(f => f.Celdas()).ToList();

            Assert.All(todas, c => Assert.Contains(c, celdasArea));
            Assert.Equal(todas.Count, todas.Distinct().Count());
            Assert.All(fichas, f => Assert.True(f.Automatica));
        }
    }

    [Fact]
    public void LaUbicacionAutomaticaSiempreDaLoMismo()
    {
        var mapa = Ejemplo;
        foreach (var g in mapa.Areas.SelectMany(a => a.Monstruos)) g.Posiciones = null;
        var m1 = new MotorJuego(mapa, CatalogoBase);
        m1.Iniciar();
        var m2 = new MotorJuego(mapa, CatalogoBase);
        m2.Iniciar();

        var f1 = DistribucionFichas.Calcular(mapa, CatalogoBase, m1.Estado, mapa.Areas[0]).Select(f => (f.Tipo, f.X, f.Y));
        var f2 = DistribucionFichas.Calcular(mapa, CatalogoBase, m2.Estado, mapa.Areas[0]).Select(f => (f.Tipo, f.X, f.Y));

        Assert.Equal(f1, f2);
    }

    [Fact]
    public void MonstruosMuertosYObjetosRecogidosNoAparecen()
    {
        var motor = NuevoMotorEjemplo();
        var zombie = motor.MonstruosDe("a1").First(m => m.Tipo == "zombie");
        motor.MarcarMonstruo("a1", zombie.Id, true);
        motor.RecogerObjeto("o1", true);

        var fichas = DistribucionFichas.Calcular(motor.Mapa, motor.Catalogo, motor.Estado, motor.Mapa.BuscarArea("a1")!);

        Assert.DoesNotContain(fichas, f => f.Id == zombie.Id);
        Assert.DoesNotContain(fichas, f => f.Id == "o1");
    }

    [Fact]
    public void ElDemonOcupaDosCasillasYElCyberdemonCuatro()
    {
        Assert.Equal((2, 1), DistribucionFichas.TamanoMonstruo(CatalogoBase, "demon", 0));
        Assert.Equal((1, 2), DistribucionFichas.TamanoMonstruo(CatalogoBase, "demon", 90));
        Assert.Equal((2, 2), DistribucionFichas.TamanoMonstruo(CatalogoBase, "cyberdemon", 0));
        Assert.Equal((1, 1), DistribucionFichas.TamanoMonstruo(CatalogoBase, "imp", 90));
    }
}

public class ColoresDeFigurasTests
{
    [Fact]
    public void LosColoresPorDefectoDependenDeLaCantidadDeMarines()
    {
        Assert.Equal([ColorFigura.Rojo], MotorJuego.ColoresPorDefecto(1));
        Assert.Equal([ColorFigura.Rojo, ColorFigura.Verde, ColorFigura.Azul], MotorJuego.ColoresPorDefecto(3));
    }

    [Fact]
    public void LosMonstruosDeUnTipoSeRepartenEntreLosColoresEnJuego()
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);
        motor.Iniciar([ColorFigura.Rojo, ColorFigura.Azul]);
        motor.AgregarMonstruo("a1", "zombie");

        var zombies = motor.MonstruosDe("a1").Where(m => m.Tipo == "zombie").Select(m => m.Color).ToList();

        Assert.Equal([ColorFigura.Rojo, ColorFigura.Azul, ColorFigura.Rojo], zombies);
    }

    [Fact]
    public void CambiarLosColoresReasignaLosMonstruos()
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);
        motor.Iniciar([ColorFigura.Rojo, ColorFigura.Verde]);

        motor.CambiarColoresMarines([ColorFigura.Azul]);

        Assert.All(motor.MonstruosDe("a1"), m => Assert.Equal(ColorFigura.Azul, m.Color));
        Assert.Equal([ColorFigura.Azul], motor.Estado.ColoresMarines);
    }

    [Fact]
    public void ReiniciarConservaLosColoresElegidos()
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);
        motor.Iniciar();
        motor.CambiarColoresMarines([ColorFigura.Verde, ColorFigura.Azul]);

        motor.Iniciar(motor.Estado.ColoresMarines);

        Assert.Equal([ColorFigura.Verde, ColorFigura.Azul], motor.Estado.ColoresMarines);
    }
}

public class ValidacionPosicionesTests
{
    private static IEnumerable<Problema> Avisos(Mapa mapa) => ValidadorGeometria.Validar(mapa, CatalogoBase);

    [Fact]
    public void FichaFueraDeSuArea()
    {
        var mapa = Ejemplo;
        mapa.Areas[0].Monstruos[0].Posiciones![0] = new PosicionFicha { X = 40, Y = 40 };

        Assert.Contains(Avisos(mapa), p => p.Mensaje.Contains("Zombie en (40, 40) queda fuera"));
    }

    [Fact]
    public void FichasEncimadas()
    {
        var mapa = Ejemplo;
        mapa.Areas[0].Objetos[0].X = 17;
        mapa.Areas[0].Objetos[0].Y = 10; // donde esta un zombie

        Assert.Contains(Avisos(mapa), p => p.Mensaje.Contains("se encima con Zombie"));
    }

    [Fact]
    public void CyberdemonQueNoEntraEnLaPieza()
    {
        var mapa = Ejemplo;
        var a6 = mapa.BuscarArea("a6")!;
        a6.Monstruos[0].Posiciones = [new PosicionFicha { X = 39, Y = 4 }]; // la sala termina en x=39, y=4

        Assert.Contains(Avisos(mapa), p => p.Mensaje.Contains("Cyberdemon en (39, 4) queda fuera"));
    }

    [Fact]
    public void MasPosicionesQueFiguras()
    {
        var mapa = Ejemplo;
        mapa.Areas[0].Monstruos[1].Posiciones!.Add(new PosicionFicha { X = 15, Y = 13 });

        Assert.Contains(Avisos(mapa), p => p.Mensaje.Contains("posiciones para 1 figura"));
    }
}
