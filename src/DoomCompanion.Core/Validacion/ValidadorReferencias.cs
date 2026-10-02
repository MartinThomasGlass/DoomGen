using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Validacion;

/// <summary>Ids unicos y referencias a areas, puertas, eventos y elementos del catalogo.</summary>
public static class ValidadorReferencias
{
    public static List<Problema> Validar(Mapa mapa, Catalogo catalogo)
    {
        var p = new List<Problema>();
        void Error(string ruta, string msg) => p.Add(new(Severidad.Error, CategoriaProblema.Referencia, ruta, msg));

        var areas = mapa.Areas.Select(a => a.Id).ToHashSet();
        var puertas = mapa.Puertas.Select(x => x.Id).ToHashSet();
        var eventos = mapa.Eventos.Select(e => e.Id).ToHashSet();
        var objetosMision = mapa.ObjetosMision.Select(o => o.Id).ToHashSet();

        bool ObjetoExiste(string id) => catalogo.BuscarObjeto(id) is not null || objetosMision.Contains(id);

        Duplicados(mapa.Areas.Select((a, i) => (a.Id, $"areas[{i}]")), "área", Error);
        Duplicados(mapa.Puertas.Select((x, i) => (x.Id, $"puertas[{i}]")), "puerta", Error);
        Duplicados(mapa.Eventos.Select((e, i) => (e.Id, $"eventos[{i}]")), "evento", Error);
        Duplicados(mapa.ObjetosMision.Select((o, i) => (o.Id, $"objetosMision[{i}]")), "objeto de misión", Error);
        Duplicados(mapa.Areas.SelectMany((a, i) => a.Objetos.Select((o, j) => (o.Id, $"areas[{i}] ({a.Id}).objetos[{j}]"))),
            "objeto colocado", Error);

        foreach (var (om, i) in mapa.ObjetosMision.Select((o, i) => (o, i)))
            if (catalogo.BuscarObjeto(om.Id) is not null)
                Error($"objetosMision[{i}] ({om.Id})", $"El id \"{om.Id}\" ya existe en el catálogo; usá otro id para el objeto de misión.");

        var esc = mapa.Escenario;
        if (!areas.Contains(esc.AreaInicial))
            Error("escenario.areaInicial", $"El área inicial \"{esc.AreaInicial}\" no existe.");

        var cv = esc.CondicionVictoria;
        if (cv.Area is { } av && !areas.Contains(av))
            Error("escenario.condicionVictoria.area", $"El área \"{av}\" no existe.");
        if (cv.Evento is { } ev && !eventos.Contains(ev))
            Error("escenario.condicionVictoria.evento", $"El evento \"{ev}\" no existe.");

        for (var i = 0; i < mapa.Areas.Count; i++)
        {
            var a = mapa.Areas[i];
            var ra = $"areas[{i}] ({a.Id})";

            for (var j = 0; j < a.Tiles.Count; j++)
                if (catalogo.BuscarTile(a.Tiles[j].Tipo) is null)
                    Error($"{ra}.tiles[{j}].tipo", $"El tile \"{a.Tiles[j].Tipo}\" no está en el catálogo. Tiles válidos: {string.Join(", ", catalogo.Tiles.Select(t => t.Id))}.");

            for (var j = 0; j < a.Monstruos.Count; j++)
                if (catalogo.BuscarMonstruo(a.Monstruos[j].Tipo) is null)
                    Error($"{ra}.monstruos[{j}].tipo", $"El monstruo \"{a.Monstruos[j].Tipo}\" no está en el catálogo. Válidos: {string.Join(", ", catalogo.Monstruos.Select(m => m.Id))}.");

            for (var j = 0; j < a.Objetos.Count; j++)
                if (!ObjetoExiste(a.Objetos[j].Tipo))
                    Error($"{ra}.objetos[{j}].tipo", $"El objeto \"{a.Objetos[j].Tipo}\" no está en el catálogo ni en objetosMision.");

            for (var j = 0; j < a.Fichas.Count; j++)
                if (catalogo.BuscarFicha(a.Fichas[j].Tipo) is null)
                    Error($"{ra}.fichas[{j}].tipo", $"La ficha \"{a.Fichas[j].Tipo}\" no está en el catálogo. Válidas: {string.Join(", ", catalogo.Fichas.Select(f => f.Id))}.");

            for (var j = 0; j < a.Recompensas.Count; j++)
            {
                var r = a.Recompensas[j];
                var rr = $"{ra}.recompensas[{j}]";
                if (r.Objeto is { } o && !ObjetoExiste(o))
                    Error($"{rr}.objeto", $"El objeto \"{o}\" no está en el catálogo ni en objetosMision.");
                if (r.Puerta is { } pu && !puertas.Contains(pu))
                    Error($"{rr}.puerta", $"La puerta \"{pu}\" no existe.");
                if (r.Evento is { } e && !eventos.Contains(e))
                    Error($"{rr}.evento", $"El evento \"{e}\" no existe.");
                if (r.Area is { } ar && !areas.Contains(ar))
                    Error($"{rr}.area", $"El área \"{ar}\" no existe.");
            }
        }

        for (var i = 0; i < mapa.Puertas.Count; i++)
        {
            var x = mapa.Puertas[i];
            var rp = $"puertas[{i}] ({x.Id})";
            if (!areas.Contains(x.Desde)) Error($"{rp}.desde", $"El área \"{x.Desde}\" no existe.");
            if (!areas.Contains(x.Hacia)) Error($"{rp}.hacia", $"El área \"{x.Hacia}\" no existe.");
            if (x.Desde == x.Hacia) Error(rp, "Una puerta no puede conectar un área consigo misma.");

            foreach (var req in x.Requisitos?.Recorrer() ?? [])
            {
                switch (req)
                {
                    case RequisitoAreaDespejada d when !areas.Contains(d.Area):
                        Error($"{rp}.requisitos", $"El área \"{d.Area}\" no existe.");
                        break;
                    case RequisitoObjeto o when !ObjetoExiste(o.Objeto):
                        Error($"{rp}.requisitos", $"El objeto \"{o.Objeto}\" no está en el catálogo ni en objetosMision.");
                        break;
                    case RequisitoPuertaAbierta pa when !puertas.Contains(pa.Puerta):
                        Error($"{rp}.requisitos", $"La puerta \"{pa.Puerta}\" no existe.");
                        break;
                    case RequisitoPuertaAbierta pa when pa.Puerta == x.Id:
                        Error($"{rp}.requisitos", "Una puerta no puede requerir estar abierta ella misma.");
                        break;
                    case RequisitoEvento e when !eventos.Contains(e.Evento):
                        Error($"{rp}.requisitos", $"El evento \"{e.Evento}\" no existe.");
                        break;
                }
            }
        }

        return p;
    }

    private static void Duplicados(IEnumerable<(string Id, string Ruta)> items, string que, Action<string, string> error)
    {
        foreach (var g in items.GroupBy(x => x.Id).Where(g => g.Count() > 1))
            error(g.Skip(1).First().Ruta, $"Id de {que} repetido: \"{g.Key}\" (aparece {g.Count()} veces).");
    }
}
