using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Motor;

namespace DoomCompanion.Core.Validacion;

/// <summary>
/// Simula la partida "a punto fijo" asumiendo que todo monstruo se puede matar, todo objeto
/// se puede recoger y todo evento manual se puede marcar. Como los requisitos no tienen
/// negacion, la simulacion es exacta: lo que no se alcanza aca no se alcanza nunca.
/// </summary>
public static class ValidadorLogico
{
    public static List<Problema> Validar(Mapa mapa, Catalogo catalogo)
    {
        var p = new List<Problema>();
        void Error(string ruta, string msg) => p.Add(new(Severidad.Error, CategoriaProblema.Logica, ruta, msg));
        void Aviso(string ruta, string msg) => p.Add(new(Severidad.Advertencia, CategoriaProblema.Logica, ruta, msg));

        var sim = Simulacion.Ejecutar(mapa);
        var nombres = new Nombres(mapa, catalogo);

        // 1. Areas inalcanzables.
        foreach (var a in mapa.Areas.Where(a => !sim.Alcanzables.Contains(a.Id)))
            Error($"areas ({a.Id})", $"El área {nombres.Area(a.Id)} no es alcanzable.");

        // 2. Por que no se abren las puertas de la frontera.
        var cerradas = mapa.Puertas.Where(x => !sim.Abiertas.Contains(x.Id)).ToList();
        foreach (var puerta in cerradas)
        {
            var desdeAlc = sim.Alcanzables.Contains(puerta.Desde);
            var haciaAlc = sim.Alcanzables.Contains(puerta.Hacia);
            if (!desdeAlc && !haciaAlc) continue;

            var motivos = ExplicarBloqueo(puerta, mapa, sim, nombres);
            var titulo = $"La puerta {puerta.Id} ({nombres.Area(puerta.Desde)} ↔ {nombres.Area(puerta.Hacia)}) nunca se puede abrir";
            if (desdeAlc && haciaAlc)
                Aviso($"puertas ({puerta.Id})", $"{titulo}, aunque las dos áreas se alcanzan por otro camino: {string.Join("; ", motivos)}.");
            else
                Error($"puertas ({puerta.Id})", $"{titulo}: {string.Join("; ", motivos)}.");
        }

        // 3. Dependencias circulares entre puertas cerradas.
        foreach (var ciclo in BuscarCiclos(cerradas, mapa, sim))
            Error("puertas", $"Dependencia circular: {string.Join(" → ", ciclo)} → {ciclo[0]}. Cada puerta necesita algo que solo se consigue detrás de la siguiente.");

        // 4. Condicion de victoria.
        var cv = mapa.Escenario.CondicionVictoria;
        switch (cv.Tipo)
        {
            case TipoVictoria.LlegarAArea or TipoVictoria.DespejarArea when cv.Area is { } av && !sim.Alcanzables.Contains(av):
                Error("escenario.condicionVictoria", $"La salida no es alcanzable: el área {nombres.Area(av)} no se puede alcanzar.");
                break;
            case TipoVictoria.Evento when cv.Evento is { } ev && !sim.Eventos.Contains(ev):
                Error("escenario.condicionVictoria", $"La condición de victoria no se puede cumplir: el evento {nombres.Evento(ev)} nunca se activa.");
                break;
        }

        // 5. Advertencias de diseño.
        // Que valga la pena entrar a cada area: un callejon sin salida lleno de monstruos tiene
        // que tener algo que convenga (llave, arma buena, objeto de mision...).
        for (var i = 0; i < mapa.Areas.Count; i++)
        {
            var area = mapa.Areas[i];
            if (area.Id == mapa.Escenario.AreaInicial || area.Id == cv.Area) continue;

            var amenaza = Amenaza(area, catalogo);
            var valor = Valor(area, mapa, catalogo);
            var sinSalida = mapa.PuertasDe(area.Id).Count() <= 1;
            var ruta = $"areas[{i}] ({area.Id})";

            if (sinSalida && amenaza >= 4 && valor * 2 < amenaza)
                Aviso(ruta, $"El área {nombres.Area(area.Id)} no tiene otra salida, tiene mucha amenaza ({amenaza}) y poco para ganar (valor {valor}): " +
                            "no conviene entrar. Poné del otro lado de los monstruos una llave, un arma buena o algo valioso, o una salida.");
            else if (sinSalida && amenaza == 0 && valor == 0)
                Aviso(ruta, $"El área {nombres.Area(area.Id)} no tiene salida, ni monstruos, ni nada para agarrar.");
            else if (amenaza >= 8 && valor < 3)
                Aviso(ruta, $"El área {nombres.Area(area.Id)} tiene mucha amenaza ({amenaza}) y casi nada para agarrar (valor {valor}).");
        }

        foreach (var puerta in mapa.Puertas.Where(x => x.Tipo == TipoPuerta.Evento))
            if (puerta.Requisitos?.Recorrer().OfType<RequisitoEvento>().Any() != true)
                Aviso($"puertas ({puerta.Id})", "Es de tipo \"evento\" pero sus requisitos no mencionan ningún evento.");

        var activadosPorRecompensa = mapa.Areas.SelectMany(TodasLasRecompensas)
            .Where(r => r.Tipo == TipoRecompensa.ActivarEvento).Select(r => r.Evento).ToHashSet();
        var eventosUsados = mapa.Puertas.SelectMany(x => x.Requisitos?.Recorrer() ?? [])
            .OfType<RequisitoEvento>().Select(r => r.Evento).ToHashSet();
        if (cv.Evento is { } evv) eventosUsados.Add(evv);
        foreach (var e in mapa.Eventos)
        {
            if (!e.Manual && !activadosPorRecompensa.Contains(e.Id))
                Aviso($"eventos ({e.Id})", $"El evento {nombres.Evento(e.Id)} no es manual y ninguna recompensa lo activa.");
            if (!eventosUsados.Contains(e.Id))
                Aviso($"eventos ({e.Id})", $"El evento {nombres.Evento(e.Id)} no se usa en ningún requisito ni en la condición de victoria.");
        }

        foreach (var (llave, tipo) in new[] { ("llave-roja", TipoPuerta.Roja), ("llave-azul", TipoPuerta.Azul), ("llave-amarilla", TipoPuerta.Amarilla) })
        {
            var aparece = sim.FuentesDeObjeto(llave).Any();
            if (aparece && mapa.Puertas.All(x => x.Tipo != tipo))
                Aviso("puertas", $"Hay {nombres.Objeto(llave)} en el mapa pero ninguna puerta {tipo.ToString().ToLowerInvariant()}.");
        }

        return p;
    }

    /// <summary>Suma de amenaza × cantidad de los monstruos del area.</summary>
    public static int Amenaza(Area area, Catalogo catalogo) =>
        area.Monstruos.Sum(m => (catalogo.BuscarMonstruo(m.Tipo)?.Amenaza ?? 1) * m.Cantidad);

    /// <summary>
    /// Lo que ganan los marines en el area: objetos que hay (los de mision valen mucho) y
    /// recompensas por despejarla.
    /// </summary>
    public static int Valor(Area area, Mapa mapa, Catalogo catalogo)
    {
        const int valorMision = 6;
        int ValorObjeto(string id) =>
            catalogo.BuscarObjeto(id)?.Valor ?? (mapa.ObjetosMision.Any(o => o.Id == id) ? valorMision : 1);

        var objetos = area.Objetos.Sum(o => ValorObjeto(o.Tipo) * o.Cantidad);
        var recompensas = TodasLasRecompensas(area).Sum(r => r.Tipo switch
        {
            TipoRecompensa.OtorgarObjeto when r.Objeto is not null => ValorObjeto(r.Objeto) * (r.Cantidad ?? 1),
            TipoRecompensa.DesbloquearPuerta or TipoRecompensa.ActivarEvento => 4,
            TipoRecompensa.RevelarInfo => 1,
            _ => 0,
        });
        return objetos + recompensas;
    }

    /// <summary>Recompensas por despejar el area y las de sus encuentros y cadaveres.</summary>
    internal static IEnumerable<Recompensa> TodasLasRecompensas(Area area) =>
        area.Recompensas.Concat(area.Fichas.SelectMany(f => f.Recompensas));

    private static List<string> ExplicarBloqueo(Puerta puerta, Mapa mapa, Simulacion sim, Nombres nombres)
    {
        var destino = sim.Alcanzables.Contains(puerta.Desde) ? puerta.Hacia : puerta.Desde;
        var motivos = new List<string>();
        foreach (var f in EvaluadorRequisitos.Faltantes(puerta.RequisitosEfectivos, sim))
        {
            switch (f)
            {
                case RequisitoObjeto o:
                {
                    var fuentes = sim.FuentesDeObjeto(o.Objeto).ToList();
                    var total = sim.TotalEnMapa(o.Objeto);
                    var nombre = nombres.Objeto(o.Objeto);
                    if (fuentes.Count == 0)
                        motivos.Add($"requiere {nombre}, que no aparece en ningún área ni recompensa");
                    else if (total < o.Cantidad)
                        motivos.Add($"requiere {o.Cantidad} × {nombre} y en todo el mapa hay {total}");
                    else if (fuentes.Contains(destino) && fuentes.All(x => !sim.Alcanzables.Contains(x)))
                        motivos.Add($"SOFTLOCK: {nombre} solo se consigue detrás de esta misma puerta (en {nombres.Area(destino)})");
                    else
                        motivos.Add($"requiere {nombre}, que solo se consigue en áreas inalcanzables ({string.Join(", ", fuentes.Select(nombres.Area))})");
                    break;
                }
                case RequisitoAreaDespejada d when d.Area == destino:
                    motivos.Add($"SOFTLOCK: requiere despejar el área a la que lleva ({nombres.Area(d.Area)})");
                    break;
                case RequisitoAreaDespejada d:
                    motivos.Add($"requiere despejar {nombres.Area(d.Area)}, que es inalcanzable");
                    break;
                case RequisitoPuertaAbierta pa:
                    motivos.Add($"requiere que la puerta {pa.Puerta} esté abierta, y esa puerta nunca se abre");
                    break;
                case RequisitoEvento e:
                {
                    var fuentes = sim.FuentesDeEvento(e.Evento).ToList();
                    motivos.Add(fuentes.Count == 0
                        ? $"requiere el evento {nombres.Evento(e.Evento)}, que no es manual y ninguna recompensa lo activa"
                        : $"requiere el evento {nombres.Evento(e.Evento)}, que solo se activa en áreas inalcanzables ({string.Join(", ", fuentes.Select(nombres.Area))})");
                    break;
                }
            }
        }
        if (motivos.Count == 0) motivos.Add("sus requisitos no se cumplen nunca");
        return motivos;
    }

    /// <summary>
    /// Grafo puerta → puertas que custodian lo que necesita. Devuelve ciclos de 2 o mas puertas
    /// (el ciclo de una sola puerta ya se informa como SOFTLOCK).
    /// </summary>
    private static List<List<string>> BuscarCiclos(List<Puerta> cerradas, Mapa mapa, Simulacion sim)
    {
        var ids = cerradas.Select(x => x.Id).ToHashSet();
        var grafo = new Dictionary<string, HashSet<string>>();
        foreach (var puerta in cerradas)
        {
            var dep = new HashSet<string>();
            foreach (var f in EvaluadorRequisitos.Faltantes(puerta.RequisitosEfectivos, sim))
            {
                IEnumerable<string> areas = f switch
                {
                    RequisitoObjeto o => sim.FuentesDeObjeto(o.Objeto),
                    RequisitoAreaDespejada d => [d.Area],
                    RequisitoEvento e => sim.FuentesDeEvento(e.Evento),
                    _ => [],
                };
                if (f is RequisitoPuertaAbierta pa && ids.Contains(pa.Puerta)) dep.Add(pa.Puerta);
                foreach (var area in areas.Where(a => !sim.Alcanzables.Contains(a)))
                    foreach (var guardiana in cerradas.Where(x => x.Conecta(area)))
                        dep.Add(guardiana.Id);
            }
            grafo[puerta.Id] = dep;
        }

        var ciclos = new List<List<string>>();
        var firmas = new HashSet<string>();
        var visitados = new HashSet<string>();
        foreach (var inicio in grafo.Keys)
        {
            var pila = new List<string>();
            Dfs(inicio);

            void Dfs(string nodo)
            {
                var idx = pila.IndexOf(nodo);
                if (idx >= 0)
                {
                    var ciclo = pila.Skip(idx).ToList();
                    if (ciclo.Count >= 2 && firmas.Add(string.Join(",", ciclo.Order())))
                        ciclos.Add(ciclo);
                    return;
                }
                if (visitados.Contains(nodo)) return;
                pila.Add(nodo);
                foreach (var sig in grafo.GetValueOrDefault(nodo) ?? []) Dfs(sig);
                pila.RemoveAt(pila.Count - 1);
                visitados.Add(nodo);
            }
        }
        return ciclos;
    }

    private sealed class Nombres(Mapa mapa, Catalogo catalogo)
    {
        public string Area(string id) => mapa.BuscarArea(id) is { } a ? $"\"{a.Nombre}\" ({id})" : id;
        public string Evento(string id) => mapa.BuscarEvento(id) is { } e ? $"\"{e.Nombre}\"" : id;
        public string Objeto(string id) =>
            catalogo.BuscarObjeto(id)?.Nombre ?? mapa.ObjetosMision.FirstOrDefault(o => o.Id == id)?.Nombre ?? id;
    }
}

/// <summary>Simulacion optimista a punto fijo usada por la validacion logica.</summary>
internal sealed class Simulacion : IContextoRequisitos
{
    private readonly Mapa _mapa;

    public HashSet<string> Alcanzables { get; } = [];
    public HashSet<string> Abiertas { get; } = [];
    public HashSet<string> Despejadas { get; } = [];
    public HashSet<string> Desbloqueadas { get; } = [];
    public HashSet<string> Eventos { get; } = [];
    public Dictionary<string, int> Inventario { get; } = [];

    private Simulacion(Mapa mapa) => _mapa = mapa;

    public static Simulacion Ejecutar(Mapa mapa)
    {
        var sim = new Simulacion(mapa);
        foreach (var e in mapa.Eventos.Where(e => e.Manual)) sim.Eventos.Add(e.Id);
        if (mapa.BuscarArea(mapa.Escenario.AreaInicial) is null) return sim;

        sim.Alcanzar(mapa.Escenario.AreaInicial);
        bool cambio;
        do
        {
            cambio = false;
            foreach (var puerta in mapa.Puertas)
            {
                if (sim.Abiertas.Contains(puerta.Id)) continue;
                if (!sim.Alcanzables.Contains(puerta.Desde) && !sim.Alcanzables.Contains(puerta.Hacia)) continue;
                if (!sim.Desbloqueadas.Contains(puerta.Id) && !EvaluadorRequisitos.Cumple(puerta.RequisitosEfectivos, sim)) continue;

                sim.Abiertas.Add(puerta.Id);
                sim.Alcanzar(puerta.Desde);
                sim.Alcanzar(puerta.Hacia);
                cambio = true;
            }
        } while (cambio);
        return sim;
    }

    /// <summary>Entrar a un area implica (optimistamente) despejarla y recoger todo.</summary>
    private void Alcanzar(string areaId)
    {
        if (!Alcanzables.Add(areaId)) return;
        var area = _mapa.BuscarArea(areaId);
        if (area is null) return;

        Despejadas.Add(areaId);
        foreach (var o in area.Objetos)
            Inventario[o.Tipo] = Inventario.GetValueOrDefault(o.Tipo) + o.Cantidad;
        foreach (var r in ValidadorLogico.TodasLasRecompensas(area))
        {
            switch (r.Tipo)
            {
                case TipoRecompensa.OtorgarObjeto when r.Objeto is not null:
                    Inventario[r.Objeto] = Inventario.GetValueOrDefault(r.Objeto) + (r.Cantidad ?? 1);
                    break;
                case TipoRecompensa.DesbloquearPuerta when r.Puerta is not null:
                    Desbloqueadas.Add(r.Puerta);
                    break;
                case TipoRecompensa.ActivarEvento when r.Evento is not null:
                    Eventos.Add(r.Evento);
                    break;
            }
        }
    }

    public IEnumerable<string> FuentesDeObjeto(string objetoId) =>
        _mapa.Areas.Where(a =>
                a.Objetos.Any(o => o.Tipo == objetoId) ||
                ValidadorLogico.TodasLasRecompensas(a).Any(r => r.Tipo == TipoRecompensa.OtorgarObjeto && r.Objeto == objetoId))
            .Select(a => a.Id);

    public IEnumerable<string> FuentesDeEvento(string eventoId) =>
        _mapa.Areas.Where(a => ValidadorLogico.TodasLasRecompensas(a).Any(r => r.Tipo == TipoRecompensa.ActivarEvento && r.Evento == eventoId))
            .Select(a => a.Id);

    public int TotalEnMapa(string objetoId) =>
        _mapa.Areas.Sum(a =>
            a.Objetos.Where(o => o.Tipo == objetoId).Sum(o => o.Cantidad) +
            ValidadorLogico.TodasLasRecompensas(a).Where(r => r.Tipo == TipoRecompensa.OtorgarObjeto && r.Objeto == objetoId).Sum(r => r.Cantidad ?? 1));

    bool IContextoRequisitos.AreaDespejada(string areaId) => Despejadas.Contains(areaId);
    int IContextoRequisitos.CantidadEnInventario(string objetoId) => Inventario.GetValueOrDefault(objetoId);
    bool IContextoRequisitos.PuertaAbierta(string puertaId) => Abiertas.Contains(puertaId);
    bool IContextoRequisitos.EventoActivo(string eventoId) => Eventos.Contains(eventoId);
}
