using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Validacion;

/// <summary>
/// Advertencias de uso de piezas fisicas por encima de lo que trae la caja, y de balance de
/// armas sin municion. Nunca bloquean la importacion.
/// </summary>
public static class ValidadorInventario
{
    public static List<Problema> Validar(Mapa mapa, Catalogo catalogo)
    {
        var p = new List<Problema>();
        void Aviso(string ruta, string msg) => p.Add(new(Severidad.Advertencia, CategoriaProblema.Inventario, ruta, msg));

        var marines = mapa.Escenario.Marines;

        // Monstruos por area contra figuras disponibles segun colores en juego.
        for (var i = 0; i < mapa.Areas.Count; i++)
        {
            var a = mapa.Areas[i];
            foreach (var g in a.Monstruos.GroupBy(m => m.Tipo))
            {
                var cat = catalogo.BuscarMonstruo(g.Key);
                if (cat is null) continue;
                var pedidos = g.Sum(m => m.Cantidad);
                var disponibles = cat.Disponibles(marines);
                if (pedidos > disponibles)
                    Aviso($"areas[{i}] ({a.Id}).monstruos",
                        $"Pide {pedidos} {cat.Nombre} pero con {marines} marine(s) solo hay {disponibles} figuras disponibles.");
            }
        }

        // Puertas fisicas.
        var puertasNormales = mapa.Puertas.Count(x => x.Tipo is TipoPuerta.Normal or TipoPuerta.Evento);
        var normalesCaja = catalogo.Puertas.FirstOrDefault(c => c.Tipo == TipoPuerta.Normal)?.Cantidad;
        if (normalesCaja is int n && puertasNormales > n)
            Aviso("puertas", $"El mapa usa {puertasNormales} puertas normales/de evento y la caja trae {n}.");
        foreach (var color in new[] { TipoPuerta.Roja, TipoPuerta.Azul, TipoPuerta.Amarilla })
        {
            var usadas = mapa.Puertas.Count(x => x.Tipo == color);
            var caja = catalogo.Puertas.FirstOrDefault(c => c.Tipo == color)?.Cantidad;
            if (caja is int c && usadas > c)
                Aviso("puertas", $"El mapa usa {usadas} puertas de seguridad {color.ToString().ToLowerInvariant()} y la caja trae {c}.");
        }

        // Tiles.
        foreach (var g in mapa.Areas.SelectMany(a => a.Tiles).GroupBy(t => t.Tipo))
        {
            var cat = catalogo.BuscarTile(g.Key);
            if (cat?.Cantidad is int c && g.Count() > c)
                Aviso("areas", $"El mapa usa {g.Count()} piezas de tipo {cat.Nombre} y la caja trae {c}.");
        }

        // Fichas (incluye los teleportadores que implica cada conexion de tipo teleportador).
        var fichas = mapa.Areas.SelectMany(a => a.Fichas).GroupBy(f => f.Tipo)
            .ToDictionary(g => g.Key, g => g.Sum(f => f.Cantidad));
        foreach (var (tipo, usadas) in fichas)
        {
            var cat = catalogo.BuscarFicha(tipo);
            if (cat?.Cantidad is int c && usadas > c)
                Aviso("areas", $"El mapa usa {usadas} fichas de {cat.Nombre} y la caja trae {c}.");
        }
        var teleportadores = mapa.Puertas.Count(x => x.Tipo == TipoPuerta.Teleportador) * 2;
        var tc = catalogo.Fichas.Where(f => f.Id.StartsWith("teleportador", StringComparison.Ordinal)).Sum(f => f.Cantidad ?? 0);
        if (tc > 0 && teleportadores > tc)
            Aviso("puertas", $"Las conexiones por teleportador necesitan {teleportadores} fichas y la caja trae {tc}.");

        // Equipo: objetos colocados + objetos otorgados por recompensas.
        var equipo = new Dictionary<string, int>();
        foreach (var (_, o) in mapa.TodosLosObjetos())
            equipo[o.Tipo] = equipo.GetValueOrDefault(o.Tipo) + o.Cantidad;
        foreach (var r in mapa.Areas.SelectMany(ValidadorLogico.TodasLasRecompensas).Where(r => r.Tipo == TipoRecompensa.OtorgarObjeto && r.Objeto is not null))
            equipo[r.Objeto!] = equipo.GetValueOrDefault(r.Objeto!) + (r.Cantidad ?? 1);

        foreach (var (tipo, usados) in equipo)
        {
            var cat = catalogo.BuscarObjeto(tipo);
            if (cat?.Cantidad is int c && usados > c)
                Aviso("areas", $"El mapa usa {usados} fichas de {cat.Nombre} y el catálogo indica {c}{(cat.Estimado ? " (cantidad estimada)" : "")}.");
        }
        foreach (var grupo in catalogo.GruposEquipo)
        {
            var usados = equipo.Where(kv => catalogo.BuscarObjeto(kv.Key)?.Grupo == grupo.Id).Sum(kv => kv.Value);
            if (usados > grupo.Total)
                Aviso("areas", $"El mapa usa {usados} {grupo.Nombre.ToLowerInvariant()} y la caja trae {grupo.Total} en total.");
        }

        // Balance: armas cuya municion no aparece en ningun lado.
        foreach (var tipo in equipo.Keys)
        {
            var arma = catalogo.BuscarObjeto(tipo);
            if (arma is not { Categoria: CategoriaObjeto.Arma, Municion: { } mun }) continue;
            if (!equipo.ContainsKey(mun) && mun != "municion-balas")
                Aviso("areas", $"Hay {arma.Nombre} en el mapa pero ninguna ficha de {catalogo.BuscarObjeto(mun)?.Nombre ?? mun}.");
        }

        return p;
    }
}
