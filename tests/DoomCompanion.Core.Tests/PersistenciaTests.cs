using DoomCompanion.Core.Motor;
using DoomCompanion.Core.Persistencia;
using static DoomCompanion.Core.Tests.Utilidades;

namespace DoomCompanion.Core.Tests;

public sealed class PersistenciaTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "doomcompanion-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void GuardarYCargarConservaElEstado()
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);
        motor.Iniciar();
        var monstruo = motor.MonstruosDe("a1")[0];
        motor.MarcarMonstruo("a1", monstruo.Id, true);
        motor.AgregarMonstruo("a1", "imp");
        motor.RecogerObjeto("o1", true);
        motor.AbrirPuerta("p1");
        motor.CambiarEvento("ev-sobrecarga", true);
        var ruta = Path.Combine(_dir, "partida" + ArchivoPartida.Extension);

        ArchivoPartida.Guardar(ruta, motor.Mapa, EjemploJson, motor.Estado);
        var carga = ArchivoPartida.Cargar(ruta, CatalogoBase);

        Assert.True(carga.Exito, carga.Mensaje);
        var e = carga.Motor!.Estado;
        Assert.Equal(["a1", "a2"], e.AreasReveladas.Order());
        Assert.Contains("p1", e.PuertasAbiertas);
        Assert.Contains("o1", e.ObjetosRecogidos);
        Assert.Contains("ev-sobrecarga", e.EventosActivos);
        Assert.Equal(2, e.Inventario["municion-balas"]);
        Assert.True(e.Monstruos["a1"].Single(m => m.Id == monstruo.Id).Muerto);
        Assert.Single(e.Monstruos["a1"], m => m.Agregado && m.Tipo == "imp");
        Assert.Equal(motor.Estado.SiguienteIdMonstruo, e.SiguienteIdMonstruo);
        Assert.Equal(motor.Estado.Historial.Count, e.Historial.Count);
        Assert.Equal(EjemploJson, carga.MapaJson);
    }

    [Fact]
    public void PartidaCargadaSigueFuncionando()
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);
        motor.Iniciar();
        motor.AbrirPuerta("p1");
        var ruta = Path.Combine(_dir, "p" + ArchivoPartida.Extension);
        ArchivoPartida.Guardar(ruta, motor.Mapa, EjemploJson, motor.Estado);

        var cargado = ArchivoPartida.Cargar(ruta, CatalogoBase).Motor!;
        cargado.RecogerObjeto("o3", true);

        Assert.True(cargado.AbrirPuerta("p2").Exito);
    }

    [Fact]
    public void MapaAlteradoDentroDelGuardadoSeRechaza()
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);
        motor.Iniciar();
        var ruta = Path.Combine(_dir, "p" + ArchivoPartida.Extension);
        ArchivoPartida.Guardar(ruta, motor.Mapa, EjemploJson, motor.Estado);
        File.WriteAllText(ruta, File.ReadAllText(ruta).Replace("Hangar de desembarco", "Hangar trucho"));

        var carga = ArchivoPartida.Cargar(ruta, CatalogoBase);

        Assert.False(carga.Exito);
        Assert.Contains("modificado", carga.Mensaje);
    }

    [Fact]
    public void ArchivoQueNoEsPartidaSeRechaza()
    {
        Directory.CreateDirectory(_dir);
        var ruta = Path.Combine(_dir, "x" + ArchivoPartida.Extension);
        File.WriteAllText(ruta, EjemploJson);

        var carga = ArchivoPartida.Cargar(ruta, CatalogoBase);

        Assert.False(carga.Exito);
    }

    [Fact]
    public void ReiniciarDejaElEstadoComoAlEmpezar()
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);
        motor.Iniciar();
        motor.AbrirPuerta("p1");
        motor.RecogerObjeto("o1", true);

        motor.Iniciar();

        Assert.Equal(["a1"], motor.Estado.AreasReveladas);
        Assert.Empty(motor.Estado.Inventario);
        Assert.Empty(motor.Estado.PuertasAbiertas);
    }
}
