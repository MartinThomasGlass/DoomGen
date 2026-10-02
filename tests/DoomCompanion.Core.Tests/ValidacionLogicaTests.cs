using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Validacion;
using static DoomCompanion.Core.Tests.Utilidades;

namespace DoomCompanion.Core.Tests;

public class ValidacionLogicaTests
{
    private static ResultadoImportacion Importar(Mapa mapa) => ImportadorMapa.Importar(Json.Escribir(mapa), CatalogoBase);

    private static IEnumerable<string> ErroresLogicos(ResultadoImportacion r) =>
        r.Problemas.Where(p => p.Severidad == Severidad.Error && p.Categoria == CategoriaProblema.Logica).Select(p => p.Mensaje);

    [Fact]
    public void MapaMinimoEsValido()
    {
        var r = Importar(MapaMinimo());

        Assert.True(r.SePuedeJugar);
        Assert.False(r.TieneProblemaLogico);
        Assert.Equal("Mapa válido.", r.Resumen);
    }

    [Fact]
    public void AreaSinPuertasEsInalcanzable()
    {
        var mapa = MapaMinimo();
        mapa.Areas.Add(NuevaArea("a3"));

        var r = Importar(mapa);

        Assert.True(r.TieneProblemaLogico);
        Assert.Equal("Se detectó un problema de lógica.", r.Resumen);
        Assert.Contains(ErroresLogicos(r), m => m.Contains("(a3)") && m.Contains("no es alcanzable"));
    }

    [Fact]
    public void LlaveDetrasDeSuPropiaPuertaEsSoftlock()
    {
        var mapa = MapaMinimo();
        mapa.Puertas[0].Tipo = TipoPuerta.Azul;
        mapa.Areas[1].Objetos.Add(new ObjetoEnArea { Id = "o1", Tipo = "llave-azul" });

        var r = Importar(mapa);

        Assert.True(r.TieneProblemaLogico);
        Assert.Contains(ErroresLogicos(r), m => m.Contains("SOFTLOCK") && m.Contains("Llave azul") && m.Contains("detrás de esta misma puerta"));
        Assert.Contains(ErroresLogicos(r), m => m.Contains("salida no es alcanzable"));
    }

    [Fact]
    public void LlaveOtorgadaPorRecompensaDetrasDeSuPuertaTambienEsSoftlock()
    {
        var mapa = MapaMinimo();
        mapa.Puertas[0].Tipo = TipoPuerta.Roja;
        mapa.Areas[1].Recompensas.Add(new Recompensa { Tipo = TipoRecompensa.OtorgarObjeto, Objeto = "llave-roja", Texto = "llave" });

        var r = Importar(mapa);

        Assert.Contains(ErroresLogicos(r), m => m.Contains("SOFTLOCK") && m.Contains("Llave roja"));
    }

    [Fact]
    public void DependenciaCircularEntreDosPuertasSeDetecta()
    {
        var mapa = MapaMinimo();
        mapa.Areas.Add(NuevaArea("a3"));
        // p1 (a1→a2) necesita la llave azul que esta en a3; p2 (a1→a3) necesita la amarilla que esta en a2.
        mapa.Puertas[0].Tipo = TipoPuerta.Azul;
        mapa.Puertas.Add(new Puerta { Id = "p2", Desde = "a1", Hacia = "a3", Tipo = TipoPuerta.Amarilla });
        mapa.Areas[2].Objetos.Add(new ObjetoEnArea { Id = "o1", Tipo = "llave-azul" });
        mapa.Areas[1].Objetos.Add(new ObjetoEnArea { Id = "o2", Tipo = "llave-amarilla" });

        var r = Importar(mapa);

        Assert.Contains(ErroresLogicos(r), m => m.StartsWith("Dependencia circular") && m.Contains("p1") && m.Contains("p2"));
    }

    [Fact]
    public void PuertaQueRequiereDespejarSuPropioDestinoEsSoftlock()
    {
        var mapa = MapaMinimo();
        mapa.Puertas[0].Requisitos = new RequisitoAreaDespejada("a2");

        var r = Importar(mapa);

        Assert.Contains(ErroresLogicos(r), m => m.Contains("SOFTLOCK") && m.Contains("despejar el área a la que lleva"));
    }

    [Fact]
    public void ObjetoRequeridoQueNoApareceEnElMapa()
    {
        var mapa = MapaMinimo();
        mapa.Puertas[0].Requisitos = new RequisitoObjeto("arma-bfg", 1);

        var r = Importar(mapa);

        Assert.Contains(ErroresLogicos(r), m => m.Contains("BFG") && m.Contains("no aparece en ningún área"));
    }

    [Fact]
    public void CantidadInsuficienteDeUnObjeto()
    {
        var mapa = MapaMinimo();
        mapa.Areas[0].Objetos.Add(new ObjetoEnArea { Id = "o1", Tipo = "municion-celdas", Cantidad = 1 });
        mapa.Puertas[0].Requisitos = new RequisitoObjeto("municion-celdas", 3);

        var r = Importar(mapa);

        Assert.Contains(ErroresLogicos(r), m => m.Contains("3 × Celdas de energía") && m.Contains("hay 1"));
    }

    [Fact]
    public void EventoNoManualSinRecompensaBloquea()
    {
        var mapa = MapaMinimo();
        mapa.Eventos.Add(new Evento { Id = "ev1", Nombre = "Generador", Manual = false });
        mapa.Puertas[0].Tipo = TipoPuerta.Evento;
        mapa.Puertas[0].Requisitos = new RequisitoEvento("ev1");

        var r = Importar(mapa);

        Assert.Contains(ErroresLogicos(r), m => m.Contains("\"Generador\"") && m.Contains("ninguna recompensa lo activa"));
    }

    [Fact]
    public void EventoManualSeConsideraAlcanzable()
    {
        var mapa = MapaMinimo();
        mapa.Eventos.Add(new Evento { Id = "ev1", Nombre = "Palanca", Manual = true });
        mapa.Puertas[0].Tipo = TipoPuerta.Evento;
        mapa.Puertas[0].Requisitos = new RequisitoEvento("ev1");

        var r = Importar(mapa);

        Assert.False(r.TieneProblemaLogico);
    }

    [Fact]
    public void AlgunaConUnaRamaPosibleAlcanza()
    {
        var mapa = MapaMinimo();
        mapa.Areas[0].Objetos.Add(new ObjetoEnArea { Id = "o1", Tipo = "llave-amarilla" });
        mapa.Puertas[0].Requisitos = new RequisitoAlguna([new RequisitoObjeto("arma-bfg", 1), new RequisitoObjeto("llave-amarilla", 1)]);

        var r = Importar(mapa);

        Assert.False(r.TieneProblemaLogico, string.Join("\n", r.Problemas));
    }

    [Fact]
    public void DesbloquearPuertaPorRecompensaEvitaLaLlave()
    {
        var mapa = MapaMinimo();
        mapa.Puertas[0].Tipo = TipoPuerta.Roja;
        mapa.Areas[0].Recompensas.Add(new Recompensa { Tipo = TipoRecompensa.DesbloquearPuerta, Puerta = "p1", Texto = "abrís" });

        var r = Importar(mapa);

        Assert.False(r.TieneProblemaLogico);
    }

    [Fact]
    public void AreaSinRecompensaEsAdvertenciaNoError()
    {
        var mapa = MapaMinimo();
        mapa.Areas[1].Recompensas.Clear();

        var r = Importar(mapa);

        Assert.True(r.SePuedeJugar);
        Assert.False(r.TieneProblemaLogico);
        Assert.Equal("Mapa válido (1 advertencia).", r.Resumen);
        Assert.Contains(r.Problemas, p => p.Severidad == Severidad.Advertencia && p.Mensaje.Contains("no da ninguna recompensa"));
    }

    [Fact]
    public void PuertaImposibleEntreAreasAlcanzablesEsAdvertencia()
    {
        var mapa = MapaMinimo();
        mapa.Puertas.Add(new Puerta { Id = "p2", Desde = "a1", Hacia = "a2", Tipo = TipoPuerta.Normal, Requisitos = new RequisitoObjeto("arma-bfg", 1) });

        var r = Importar(mapa);

        Assert.False(r.TieneProblemaLogico);
        Assert.Contains(r.Problemas, p => p.Severidad == Severidad.Advertencia && p.Mensaje.Contains("p2") && p.Mensaje.Contains("por otro camino"));
    }

    [Fact]
    public void VictoriaPorEventoQueNuncaSeActivaEsError()
    {
        var mapa = MapaMinimo();
        mapa.Eventos.Add(new Evento { Id = "ev-fin", Nombre = "Fin", Manual = false });
        mapa.Escenario.CondicionVictoria = new CondicionVictoria { Tipo = TipoVictoria.Evento, Evento = "ev-fin" };

        var r = Importar(mapa);

        Assert.Contains(ErroresLogicos(r), m => m.Contains("condición de victoria no se puede cumplir"));
    }
}

public class ValidacionReferenciasTests
{
    private static ResultadoImportacion Importar(Mapa mapa) => ImportadorMapa.Importar(Json.Escribir(mapa), CatalogoBase);

    [Fact]
    public void IdDeAreaRepetidoBloqueaLaImportacion()
    {
        var mapa = MapaMinimo();
        mapa.Areas[1].Id = "a1";

        var r = Importar(mapa);

        Assert.False(r.SePuedeJugar);
        Assert.Contains(r.Problemas, p => p.Categoria == CategoriaProblema.Referencia && p.Mensaje.Contains("repetido"));
    }

    [Fact]
    public void PuertaHaciaAreaInexistente()
    {
        var mapa = MapaMinimo();
        mapa.Puertas[0].Hacia = "a9";

        var r = Importar(mapa);

        Assert.Contains(r.Problemas, p => p.Ruta == "puertas[0] (p1).hacia" && p.Mensaje.Contains("\"a9\" no existe"));
    }

    [Fact]
    public void MonstruoFueraDelCatalogo()
    {
        var mapa = MapaMinimo();
        mapa.Areas[1].Monstruos.Add(new GrupoMonstruos { Tipo = "cacodemon", Cantidad = 1 });

        var r = Importar(mapa);

        Assert.Contains(r.Problemas, p => p.Ruta == "areas[1] (a2).monstruos[0].tipo" && p.Mensaje.Contains("cacodemon"));
    }

    [Fact]
    public void RequisitoConEventoInexistente()
    {
        var mapa = MapaMinimo();
        mapa.Puertas[0].Requisitos = new RequisitoTodas([new RequisitoAreaDespejada("a1"), new RequisitoEvento("nada")]);

        var r = Importar(mapa);

        Assert.Contains(r.Problemas, p => p.Ruta == "puertas[0] (p1).requisitos" && p.Mensaje.Contains("\"nada\" no existe"));
    }

    [Fact]
    public void ObjetoDeMisionEsValidoComoRequisito()
    {
        var mapa = MapaMinimo();
        mapa.ObjetosMision.Add(new ObjetoMision { Id = "muestra", Nombre = "Muestra" });
        mapa.Areas[0].Objetos.Add(new ObjetoEnArea { Id = "o1", Tipo = "muestra" });
        mapa.Puertas[0].Requisitos = new RequisitoObjeto("muestra", 1);

        var r = Importar(mapa);

        Assert.True(r.SePuedeJugar);
        Assert.False(r.TieneProblemaLogico);
    }
}

public class ValidacionInventarioTests
{
    private static ResultadoImportacion Importar(Mapa mapa) => ImportadorMapa.Importar(Json.Escribir(mapa), CatalogoBase);

    [Fact]
    public void MasFigurasQueLasDisponiblesSegunMarines()
    {
        var mapa = MapaMinimo();
        mapa.Escenario.Marines = 1;
        mapa.Areas[1].Monstruos.Add(new GrupoMonstruos { Tipo = "cyberdemon", Cantidad = 2 });

        var r = Importar(mapa);

        Assert.True(r.SePuedeJugar);
        Assert.Contains(r.Problemas, p => p.Categoria == CategoriaProblema.Inventario && p.Mensaje.Contains("solo hay 1 figuras"));
    }

    [Fact]
    public void MasPuertasDeSeguridadQueLasDeLaCaja()
    {
        var mapa = MapaMinimo();
        mapa.Areas[0].Objetos.Add(new ObjetoEnArea { Id = "o1", Tipo = "llave-roja" });
        mapa.Puertas[0].Tipo = TipoPuerta.Roja;
        mapa.Puertas.Add(new Puerta { Id = "p2", Desde = "a2", Hacia = "a1", Tipo = TipoPuerta.Roja });

        var r = Importar(mapa);

        Assert.Contains(r.Problemas, p => p.Categoria == CategoriaProblema.Inventario && p.Mensaje.Contains("2 puertas de seguridad roja"));
    }

    [Fact]
    public void ArmaSinSuMunicion()
    {
        var mapa = MapaMinimo();
        mapa.Areas[0].Objetos.Add(new ObjetoEnArea { Id = "o1", Tipo = "arma-plasma" });

        var r = Importar(mapa);

        Assert.Contains(r.Problemas, p => p.Categoria == CategoriaProblema.Inventario && p.Mensaje.Contains("Rifle de plasma") && p.Mensaje.Contains("Celdas"));
    }

    [Fact]
    public void FigurasDisponiblesDependenDeLosColoresEnJuego()
    {
        var imp = CatalogoBase.BuscarMonstruo("imp")!;
        var cyber = CatalogoBase.BuscarMonstruo("cyberdemon")!;

        Assert.Equal(4, imp.Disponibles(1));
        Assert.Equal(8, imp.Disponibles(2));
        Assert.Equal(12, imp.Disponibles(3));
        Assert.Equal(1, cyber.Disponibles(1));
        Assert.Equal(3, cyber.Disponibles(3));
    }
}
