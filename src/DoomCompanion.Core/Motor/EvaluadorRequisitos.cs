using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Motor;

/// <summary>Lo que hace falta saber del estado para evaluar un requisito.</summary>
public interface IContextoRequisitos
{
    bool AreaDespejada(string areaId);
    int CantidadEnInventario(string objetoId);
    bool PuertaAbierta(string puertaId);
    bool EventoActivo(string eventoId);
}

public static class EvaluadorRequisitos
{
    /// <summary>Un requisito nulo siempre se cumple.</summary>
    public static bool Cumple(Requisito? requisito, IContextoRequisitos ctx) => requisito switch
    {
        null => true,
        RequisitoTodas t => t.Condiciones.All(c => Cumple(c, ctx)),
        RequisitoAlguna a => a.Condiciones.Any(c => Cumple(c, ctx)),
        RequisitoAreaDespejada d => ctx.AreaDespejada(d.Area),
        RequisitoObjeto o => ctx.CantidadEnInventario(o.Objeto) >= o.Cantidad,
        RequisitoPuertaAbierta p => ctx.PuertaAbierta(p.Puerta),
        RequisitoEvento e => ctx.EventoActivo(e.Evento),
        _ => throw new NotSupportedException(requisito.GetType().Name),
    };

    /// <summary>
    /// Condiciones simples que no se cumplen. Para un "alguna" sin ninguna rama cumplida
    /// devuelve las hojas faltantes de todas sus ramas.
    /// </summary>
    public static List<Requisito> Faltantes(Requisito? requisito, IContextoRequisitos ctx)
    {
        var lista = new List<Requisito>();
        Juntar(requisito, ctx, lista);
        return lista;
    }

    private static void Juntar(Requisito? r, IContextoRequisitos ctx, List<Requisito> lista)
    {
        if (r is null || Cumple(r, ctx)) return;
        switch (r)
        {
            case RequisitoTodas t:
                foreach (var c in t.Condiciones) Juntar(c, ctx, lista);
                break;
            case RequisitoAlguna a:
                foreach (var c in a.Condiciones) Juntar(c, ctx, lista);
                break;
            default:
                lista.Add(r);
                break;
        }
    }
}
