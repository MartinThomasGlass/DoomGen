using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Motor;
using static DoomCompanion.Core.Tests.Utilidades;

namespace DoomCompanion.Core.Tests;

public class MotorJuegoTests
{
    private static MotorJuego NuevoMotorEjemplo()
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);
        motor.Iniciar();
        return motor;
    }

    private static void MatarTodo(MotorJuego motor, string areaId, List<Mensaje>? mensajes = null)
    {
        foreach (var m in motor.MonstruosDe(areaId).Where(m => !m.Muerto).ToList())
        {
            var r = motor.MarcarMonstruo(areaId, m.Id, true);
            mensajes?.AddRange(r.Mensajes);
        }
    }

    private static void RecogerTodo(MotorJuego motor, string areaId)
    {
        foreach (var o in motor.Mapa.BuscarArea(areaId)!.Objetos)
            motor.RecogerObjeto(o.Id, true);
    }

    // ------------------------------------------------------------ Niebla de guerra

    [Fact]
    public void AlIniciarSoloSeVeElAreaInicialYSusPuertas()
    {
        var motor = NuevoMotorEjemplo();

        Assert.Equal(["a1"], motor.Estado.AreasReveladas);
        Assert.Equal(["p1", "p2", "p7"], motor.PuertasVisibles.Select(p => p.Id).Order());
        Assert.Equal(3, motor.MonstruosDe("a1").Count);
        Assert.Empty(motor.MonstruosDe("a2"));
    }

    [Fact]
    public void IniciarDevuelveBriefingObjetivosYEntrada()
    {
        var motor = new MotorJuego(Ejemplo, CatalogoBase);

        var r = motor.Iniciar();

        Assert.Equal([TipoMensaje.Briefing, TipoMensaje.Objetivo, TipoMensaje.Entrada], r.Mensajes.Select(m => m.Tipo));
        Assert.Contains("Pieza: Sala 10×5 con salida al sur en (10, 9), rotación 0°", r.Mensajes[2].Detalle);
        Assert.Contains("Pieza: Callejón sin salida 1×2 en (14, 14), rotación 90°", r.Mensajes[2].Detalle);
        Assert.Contains("2 × Zombie", r.Mensajes[2].Detalle);
    }

    [Fact]
    public void NoSePuedeAbrirUnaPuertaNoVisible()
    {
        var motor = NuevoMotorEjemplo();

        var r = motor.AbrirPuerta("p6");

        Assert.False(r.Exito);
        Assert.DoesNotContain("p6", motor.Estado.PuertasAbiertas);
    }

    // ------------------------------------------------------------ Puertas y requisitos

    [Fact]
    public void PuertaNormalSinRequisitosRevelaElOtroLado()
    {
        var motor = NuevoMotorEjemplo();

        var r = motor.AbrirPuerta("p1");

        Assert.True(r.Exito);
        Assert.Contains("a2", motor.Estado.AreasReveladas);
        Assert.Equal(TipoMensaje.PuertaAbierta, r.Mensajes[0].Tipo);
        Assert.Contains(r.Mensajes, m => m.Tipo == TipoMensaje.Entrada && m.Titulo == "Pasillo de mantenimiento");
        Assert.Equal(3, motor.MonstruosDe("a2").Count);
    }

    [Fact]
    public void PuertaBloqueadaMuestraPistaYNoRevelaNada()
    {
        var motor = NuevoMotorEjemplo();

        var r = motor.AbrirPuerta("p2");

        Assert.False(r.Exito);
        var m = Assert.Single(r.Mensajes);
        Assert.Equal(TipoMensaje.PuertaBloqueada, m.Tipo);
        Assert.Contains("técnicos de mantenimiento", m.Texto);
        Assert.DoesNotContain("a3", motor.Estado.AreasReveladas);
    }

    [Fact]
    public void PuertaDeColorSeAbreConLaLlaveEnElInventario()
    {
        var motor = NuevoMotorEjemplo();
        motor.AbrirPuerta("p1");
        motor.RecogerObjeto("o3", true); // llave amarilla en a2

        var r = motor.AbrirPuerta("p2");

        Assert.True(r.Exito);
        Assert.Contains("a3", motor.Estado.AreasReveladas);
    }

    [Fact]
    public void ObjetoNoRecogidoNoCuentaParaRequisitos()
    {
        var motor = NuevoMotorEjemplo();
        motor.AbrirPuerta("p1");
        motor.RecogerObjeto("o3", true);
        motor.RecogerObjeto("o3", false);

        Assert.False(motor.AbrirPuerta("p2").Exito);
        Assert.Equal(0, motor.Estado.Inventario.GetValueOrDefault("llave-amarilla"));
    }

    [Fact]
    public void PasoRequiereAreaDespejada()
    {
        var motor = NuevoMotorEjemplo();
        motor.AbrirPuerta("p1");

        Assert.False(motor.AbrirPuerta("p3").Exito);
        MatarTodo(motor, "a2");
        Assert.True(motor.AbrirPuerta("p3").Exito);
        Assert.Contains("a4", motor.Estado.AreasReveladas);
    }

    [Fact]
    public void TeleportadorRequierePuertaAbierta()
    {
        var motor = NuevoMotorEjemplo();
        Assert.False(motor.AbrirPuerta("p7").Exito);

        motor.AbrirPuerta("p1");
        MatarTodo(motor, "a2");
        motor.AbrirPuerta("p3");

        Assert.True(motor.AbrirPuerta("p7").Exito);
    }

    [Fact]
    public void AlgunaSeCumpleConEventoManual()
    {
        var motor = NuevoMotorEjemplo();
        motor.AbrirPuerta("p1");
        MatarTodo(motor, "a2");
        motor.AbrirPuerta("p3");
        Assert.False(motor.AbrirPuerta("p5").Exito);

        motor.CambiarEvento("ev-sobrecarga", true);

        Assert.True(motor.AbrirPuerta("p5").Exito);
        Assert.Contains("a5", motor.Estado.AreasReveladas);
    }

    [Fact]
    public void EventoNoManualNoSePuedeMarcarAMano()
    {
        var motor = NuevoMotorEjemplo();

        Assert.False(motor.CambiarEvento("ev-energia", true).Exito);
        Assert.DoesNotContain("ev-energia", motor.Estado.EventosActivos);
    }

    [Fact]
    public void TodasExigeCadaCondicion()
    {
        var motor = NuevoMotorEjemplo();
        // Llegar a a5 por la compuerta de energia, con la sobrecarga manual.
        motor.AbrirPuerta("p1");
        MatarTodo(motor, "a2");
        motor.AbrirPuerta("p3");
        motor.CambiarEvento("ev-sobrecarga", true);
        motor.AbrirPuerta("p5");
        MatarTodo(motor, "a5"); // otorga la llave roja

        // Tiene la llave roja y a5 despejada, pero le falta el disco de datos.
        Assert.False(motor.AbrirPuerta("p6").Exito);

        motor.AjustarInventario("disco-datos", 1);
        Assert.True(motor.AbrirPuerta("p6").Exito);
    }

    // ------------------------------------------------------------ Monstruos y despeje

    [Fact]
    public void MatarTodosLosMonstruosDespejaYDaRecompensas()
    {
        var motor = NuevoMotorEjemplo();
        var mensajes = new List<Mensaje>();

        MatarTodo(motor, "a1", mensajes);

        Assert.Contains("a1", motor.Estado.AreasDespejadas);
        Assert.Equal([TipoMensaje.Despeje, TipoMensaje.Recompensa, TipoMensaje.Recompensa], mensajes.Select(m => m.Tipo));
        Assert.Equal(1, motor.Estado.Inventario["arma-escopeta"]);
        Assert.Contains("a3", motor.Estado.AreasConocidas);
        Assert.True(motor.AreaConocida("a3"));
    }

    [Fact]
    public void DespejeSoloOcurreUnaVez()
    {
        var motor = NuevoMotorEjemplo();
        MatarTodo(motor, "a1");
        var m = motor.MonstruosDe("a1")[0];

        motor.MarcarMonstruo("a1", m.Id, false);
        var r = motor.MarcarMonstruo("a1", m.Id, true);

        Assert.Empty(r.Mensajes);
        Assert.Equal(1, motor.Estado.Inventario["arma-escopeta"]);
    }

    [Fact]
    public void MonstruoAgregadoImpideElDespeje()
    {
        var motor = NuevoMotorEjemplo();
        motor.AgregarMonstruo("a1", "imp");

        foreach (var m in motor.MonstruosDe("a1").Where(m => !m.Agregado).ToList())
            motor.MarcarMonstruo("a1", m.Id, true);
        Assert.DoesNotContain("a1", motor.Estado.AreasDespejadas);
        Assert.Equal(4, motor.MonstruosDe("a1").Count);

        MatarTodo(motor, "a1");
        Assert.Contains("a1", motor.Estado.AreasDespejadas);
    }

    [Fact]
    public void QuitarElUltimoMonstruoVivoAgregadoDespeja()
    {
        var motor = NuevoMotorEjemplo();
        motor.AgregarMonstruo("a1", "imp");
        foreach (var m in motor.MonstruosDe("a1").Where(m => !m.Agregado).ToList())
            motor.MarcarMonstruo("a1", m.Id, true);
        Assert.DoesNotContain("a1", motor.Estado.AreasDespejadas);

        var agregado = motor.MonstruosDe("a1").Single(m => m.Agregado);
        var r = motor.QuitarMonstruo("a1", agregado.Id);

        Assert.Contains("a1", motor.Estado.AreasDespejadas);
        Assert.Contains(r.Mensajes, x => x.Tipo == TipoMensaje.Despeje);
    }

    [Fact]
    public void NoSePuedenQuitarMonstruosDelMapa()
    {
        var motor = NuevoMotorEjemplo();
        var original = motor.MonstruosDe("a1")[0];

        Assert.False(motor.QuitarMonstruo("a1", original.Id).Exito);
    }

    [Fact]
    public void AreaSinMonstruosSeDespejaAlEntrar()
    {
        var mapa = MapaMinimo();
        var motor = new MotorJuego(mapa, CatalogoBase);

        var r = motor.Iniciar();

        Assert.Contains("a1", motor.Estado.AreasDespejadas);
        Assert.Contains(r.Mensajes, m => m.Tipo == TipoMensaje.Recompensa && m.Texto == "premio a1");
    }

    // ------------------------------------------------------------ Recompensas

    [Fact]
    public void DesbloquearPuertaDeOtraAreaPermiteAbrirlaSinLlave()
    {
        var motor = NuevoMotorEjemplo();
        motor.AbrirPuerta("p1");
        motor.RecogerObjeto("o3", true);
        motor.AbrirPuerta("p2");
        Assert.False(motor.AbrirPuerta("p4").Exito);

        MatarTodo(motor, "a3"); // desbloquea p4 y activa ev-energia

        Assert.Contains("ev-energia", motor.Estado.EventosActivos);
        Assert.True(motor.AbrirPuerta("p4").Exito);
        Assert.Equal(0, motor.Estado.Inventario.GetValueOrDefault("llave-azul"));
    }

    [Fact]
    public void VictoriaPorEventoSeDeclaraSola()
    {
        var mapa = MapaMinimo();
        mapa.Eventos.Add(new Evento { Id = "ev-fin", Nombre = "Fin", Manual = true });
        mapa.Escenario.CondicionVictoria = new CondicionVictoria { Tipo = TipoVictoria.Evento, Evento = "ev-fin" };
        var motor = new MotorJuego(mapa, CatalogoBase);
        motor.Iniciar();

        var r = motor.CambiarEvento("ev-fin", true);

        Assert.Equal(ResultadoPartida.Victoria, motor.Estado.Resultado);
        Assert.Contains(r.Mensajes, m => m.Tipo == TipoMensaje.Victoria && m.Texto == "v");
    }

    [Fact]
    public void VictoriaPorDespejarArea()
    {
        var mapa = MapaMinimo();
        mapa.Areas[1].Monstruos.Add(new GrupoMonstruos { Tipo = "imp", Cantidad = 1 });
        mapa.Escenario.CondicionVictoria = new CondicionVictoria { Tipo = TipoVictoria.DespejarArea, Area = "a2" };
        var motor = new MotorJuego(mapa, CatalogoBase);
        motor.Iniciar();
        motor.AbrirPuerta("p1");

        MatarTodo(motor, "a2");

        Assert.Equal(ResultadoPartida.Victoria, motor.Estado.Resultado);
    }

    [Fact]
    public void LlegarALaSalidaAvisaYVictoriaManualMuestraTexto()
    {
        var motor = new MotorJuego(MapaMinimo(), CatalogoBase);
        motor.Iniciar();

        var r = motor.AbrirPuerta("p1");
        Assert.Contains(r.Mensajes, m => m.Tipo == TipoMensaje.Objetivo && m.Titulo == "Salida a la vista");

        var fin = motor.DeclararResultado(ResultadoPartida.Derrota);
        Assert.Equal("d", fin.Mensajes.Single().Texto);
        Assert.Equal(ResultadoPartida.Derrota, motor.Estado.Resultado);
    }

    // ------------------------------------------------------------ Inventario

    [Fact]
    public void InventarioNoBajaDeCero()
    {
        var motor = NuevoMotorEjemplo();
        motor.AjustarInventario("municion-balas", 2);
        motor.AjustarInventario("municion-balas", -5);

        Assert.False(motor.Estado.Inventario.ContainsKey("municion-balas"));
    }

    [Fact]
    public void RecogerDosVecesNoDuplica()
    {
        var motor = NuevoMotorEjemplo();
        motor.RecogerObjeto("o1", true);
        motor.RecogerObjeto("o1", true);

        Assert.Equal(2, motor.Estado.Inventario["municion-balas"]);
    }

    [Fact]
    public void NoSePuedeRecogerEnUnAreaOculta()
    {
        var motor = NuevoMotorEjemplo();

        Assert.False(motor.RecogerObjeto("o7", true).Exito);
    }

    // ------------------------------------------------------------ Partida completa

    [Fact]
    public void ElEjemploSePuedeCompletarDePuntaAPunta()
    {
        var motor = NuevoMotorEjemplo();

        MatarTodo(motor, "a1"); RecogerTodo(motor, "a1");
        Assert.True(motor.AbrirPuerta("p1").Exito);
        MatarTodo(motor, "a2"); RecogerTodo(motor, "a2");
        Assert.True(motor.AbrirPuerta("p2").Exito);
        MatarTodo(motor, "a3"); RecogerTodo(motor, "a3");
        Assert.True(motor.AbrirPuerta("p4").Exito);
        MatarTodo(motor, "a5"); RecogerTodo(motor, "a5");
        Assert.True(motor.AbrirPuerta("p6").Exito);

        Assert.Contains("a6", motor.Estado.AreasReveladas);
        Assert.Equal(ResultadoPartida.Victoria, motor.DeclararResultado(ResultadoPartida.Victoria).Mensajes.Single().Tipo switch
        {
            TipoMensaje.Victoria => ResultadoPartida.Victoria,
            _ => ResultadoPartida.Derrota,
        });
        Assert.NotEmpty(motor.Estado.Historial);
    }
}
