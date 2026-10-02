using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Motor;

/// <summary>
/// Reglas de la partida: niebla de guerra, puertas, monstruos, despeje, recompensas,
/// inventario y eventos. No depende de la interfaz.
/// </summary>
public sealed class MotorJuego : IContextoRequisitos
{
    public Mapa Mapa { get; }
    public Catalogo Catalogo { get; }
    public EstadoPartida Estado { get; private set; }

    public MotorJuego(Mapa mapa, Catalogo catalogo, EstadoPartida? estado = null)
    {
        Mapa = mapa;
        Catalogo = catalogo;
        Estado = estado ?? new EstadoPartida();
    }

    /// <summary>Empieza la partida desde cero: briefing y area inicial revelada.</summary>
    /// <summary>Colores de marine por defecto segun cuantos marines juegan.</summary>
    public static List<ColorFigura> ColoresPorDefecto(int marines) =>
        [.. new[] { ColorFigura.Rojo, ColorFigura.Verde, ColorFigura.Azul }.Take(Math.Clamp(marines, 1, 3))];

    public ResultadoAccion Iniciar(IReadOnlyList<ColorFigura>? colores = null)
    {
        Estado = new EstadoPartida
        {
            ColoresMarines = colores is { Count: > 0 } ? [.. colores] : ColoresPorDefecto(Mapa.Escenario.Marines),
        };
        var textos = Mapa.Escenario.Textos;
        var mensajes = new List<Mensaje>
        {
            new(TipoMensaje.Briefing, Mapa.Escenario.Titulo, textos.Introduccion),
            new(TipoMensaje.Objetivo, "Objetivos", textos.Objetivos),
        };
        Revelar(Mapa.Escenario.AreaInicial, mensajes);
        return Registrar(ResultadoAccion.Ok(mensajes));
    }

    // ---------------------------------------------------------------- Consultas

    public bool AreaRevelada(string id) => Estado.AreasReveladas.Contains(id);
    public bool AreaConocida(string id) => Estado.AreasConocidas.Contains(id) && !AreaRevelada(id);
    public bool EstaPuertaAbierta(string id) => Estado.PuertasAbiertas.Contains(id);

    /// <summary>Puertas que tocan al menos un area revelada (de las demas no se sabe nada).</summary>
    public IEnumerable<Puerta> PuertasVisibles =>
        Mapa.Puertas.Where(p => AreaRevelada(p.Desde) || AreaRevelada(p.Hacia));

    public IReadOnlyList<MonstruoEnJuego> MonstruosDe(string areaId) =>
        Estado.Monstruos.TryGetValue(areaId, out var l) ? l : [];

    public bool PuedeAbrir(Puerta puerta) =>
        Estado.PuertasDesbloqueadas.Contains(puerta.Id) || EvaluadorRequisitos.Cumple(puerta.RequisitosEfectivos, this);

    public string NombreObjeto(string id) =>
        Catalogo.BuscarObjeto(id)?.Nombre ?? Mapa.ObjetosMision.FirstOrDefault(o => o.Id == id)?.Nombre ?? id;

    public CategoriaObjeto CategoriaObjeto(string id) =>
        Catalogo.BuscarObjeto(id)?.Categoria ?? Modelo.CategoriaObjeto.Mision;

    public string NombreMonstruo(string tipo) => Catalogo.BuscarMonstruo(tipo)?.Nombre ?? tipo;

    public string NombreArea(string id) => Mapa.BuscarArea(id)?.Nombre ?? id;

    // ---------------------------------------------------------------- Puertas

    public ResultadoAccion AbrirPuerta(string puertaId)
    {
        var puerta = Mapa.BuscarPuerta(puertaId);
        if (puerta is null || !PuertasVisibles.Contains(puerta))
            return ResultadoAccion.Falla(new Mensaje(TipoMensaje.Info, "Puerta desconocida", "Esa puerta todavía no es visible."));

        if (EstaPuertaAbierta(puerta.Id))
            return ResultadoAccion.Ok(new Mensaje(TipoMensaje.Info, NombrePuerta(puerta), "La puerta ya está abierta."));

        if (!PuedeAbrir(puerta))
        {
            var pista = puerta.Textos?.Bloqueada;
            return Registrar(ResultadoAccion.Falla(new Mensaje(TipoMensaje.PuertaBloqueada, NombrePuerta(puerta) + ": bloqueada",
                string.IsNullOrWhiteSpace(pista) ? TextoBloqueadaPorDefecto(puerta) : pista)));
        }

        Estado.PuertasAbiertas.Add(puerta.Id);
        var mensajes = new List<Mensaje>
        {
            new(TipoMensaje.PuertaAbierta, NombrePuerta(puerta) + ": abierta",
                string.IsNullOrWhiteSpace(puerta.Textos?.Abrir) ? "La puerta se abre." : puerta.Textos!.Abrir!),
        };
        Revelar(puerta.Desde, mensajes);
        Revelar(puerta.Hacia, mensajes);
        return Registrar(ResultadoAccion.Ok(mensajes));
    }

    public static string NombrePuerta(Puerta p) => p.Tipo switch
    {
        TipoPuerta.Normal => "Puerta",
        TipoPuerta.Roja => "Puerta de seguridad roja",
        TipoPuerta.Azul => "Puerta de seguridad azul",
        TipoPuerta.Amarilla => "Puerta de seguridad amarilla",
        TipoPuerta.Evento => "Compuerta sellada",
        TipoPuerta.Paso => "Paso",
        TipoPuerta.Teleportador => "Teleportador",
        _ => "Puerta",
    };

    private static string TextoBloqueadaPorDefecto(Puerta p) => p.Tipo switch
    {
        TipoPuerta.Roja or TipoPuerta.Azul or TipoPuerta.Amarilla => "La cerradura de seguridad no responde. Hace falta algo más para abrirla.",
        TipoPuerta.Teleportador => "El teleportador no responde.",
        TipoPuerta.Paso => "Todavía no se puede pasar por acá.",
        _ => "La puerta no se abre. Algo falta todavía.",
    };

    // ---------------------------------------------------------------- Areas y monstruos

    private void Revelar(string areaId, List<Mensaje> mensajes)
    {
        var area = Mapa.BuscarArea(areaId);
        if (area is null || !Estado.AreasReveladas.Add(areaId)) return;

        var lista = new List<MonstruoEnJuego>();
        Estado.Monstruos[areaId] = lista;
        foreach (var g in area.Monstruos)
            for (var i = 0; i < g.Cantidad; i++)
            {
                var pos = g.Posiciones?.ElementAtOrDefault(i);
                lista.Add(new MonstruoEnJuego
                {
                    Id = NuevoIdMonstruo(),
                    Tipo = g.Tipo,
                    Color = SiguienteColor(g.Tipo),
                    X = pos?.X,
                    Y = pos?.Y,
                    Rotacion = pos?.Rotacion ?? 0,
                });
            }

        mensajes.Add(new Mensaje(TipoMensaje.Entrada, area.Nombre, area.Textos.Entrar, DescribirPreparacion(area)));

        var cv = Mapa.Escenario.CondicionVictoria;
        if (cv.Tipo == TipoVictoria.LlegarAArea && cv.Area == areaId)
            mensajes.Add(new Mensaje(TipoMensaje.Objetivo, "Salida a la vista",
                $"Cuando un marine llegue a \"{area.Nombre}\", marcá Victoria."));

        if (lista.Count == 0) Despejar(area, mensajes);
    }

    /// <summary>Que colocar en la mesa al revelar el area.</summary>
    public string DescribirPreparacion(Area area)
    {
        var lineas = new List<string>();
        foreach (var t in area.Tiles)
        {
            var cat = Catalogo.BuscarTile(t.Tipo);
            var nombre = cat?.Nombre ?? t.Tipo;
            // Con forma en el catalogo el nombre ya trae las medidas; sin forma se usan las del mapa.
            var tam = cat?.TieneForma != true && t.Ancho is int an && t.Alto is int al ? $" de {an}×{al}" : "";
            lineas.Add($"Pieza: {nombre}{tam} en ({t.X}, {t.Y}), rotación {t.Rotacion}°{(t.Nota is { } n ? $" — {n}" : "")}");
        }

        // Fichas con su casilla, tal como aparecen en el plano.
        var fichas = DistribucionFichas.Calcular(Mapa, Catalogo, Estado, area);
        string En(FichaEnPlano f) => $"({f.X}, {f.Y})";

        var marines = fichas.Where(f => f.Clase == ClaseFicha.Marine).ToList();
        if (marines.Count > 0)
            lineas.Add("Marines: " + string.Join(", ", marines.Select(f => $"{NombreColor(f.Color!.Value)} en {En(f)}")));
        foreach (var g in fichas.Where(f => f.Clase == ClaseFicha.Monstruo).GroupBy(f => f.Tipo))
            lineas.Add($"Monstruo: {g.Count()} × {NombreMonstruo(g.Key)} — " +
                       string.Join(", ", g.Select(f => $"{NombreColor(f.Color!.Value)} en {En(f)}")));
        foreach (var g in fichas.Where(f => f.Clase == ClaseFicha.Objeto).GroupBy(f => f.Tipo))
            lineas.Add($"Objeto: {g.Count()} × {NombreObjeto(g.Key)} en " + string.Join(", ", g.Select(En)));
        foreach (var g in fichas.Where(f => f.Clase == ClaseFicha.Escenografia).GroupBy(f => f.Tipo))
        {
            var nota = area.Fichas.FirstOrDefault(x => x.Tipo == g.Key)?.Nota;
            lineas.Add($"Ficha: {g.Count()} × {Catalogo.BuscarFicha(g.Key)?.Nombre ?? g.Key} en " +
                       string.Join(", ", g.Select(En)) + (nota is null ? "" : $" — {nota}"));
        }
        return string.Join("\n", lineas);
    }

    public static string NombreColor(ColorFigura c) => c switch
    {
        ColorFigura.Rojo => "rojo",
        ColorFigura.Verde => "verde",
        _ => "azul",
    };

    /// <summary>
    /// Reparte los colores de los marines en juego entre las figuras de cada tipo de monstruo,
    /// de a uno por vez, para no agotar las figuras de un solo color.
    /// </summary>
    private ColorFigura SiguienteColor(string tipo)
    {
        var colores = Estado.ColoresMarines.Count > 0 ? Estado.ColoresMarines : ColoresPorDefecto(Mapa.Escenario.Marines);
        var yaHay = Estado.Monstruos.Values.SelectMany(l => l).Count(m => m.Tipo == tipo);
        return colores[yaHay % colores.Count];
    }

    /// <summary>Cambia los colores de los marines en juego y vuelve a repartir los de los monstruos.</summary>
    public void CambiarColoresMarines(IReadOnlyList<ColorFigura> colores)
    {
        if (colores.Count == 0) return;
        Estado.ColoresMarines = [.. colores.Distinct().OrderBy(c => c)];
        foreach (var grupo in Estado.Monstruos.Values.SelectMany(l => l).GroupBy(m => m.Tipo))
        {
            var k = 0;
            foreach (var m in grupo.OrderBy(m => int.TryParse(m.Id.AsSpan(1), out var n) ? n : 0))
                m.Color = Estado.ColoresMarines[k++ % Estado.ColoresMarines.Count];
        }
    }

    public ResultadoAccion MarcarMonstruo(string areaId, string monstruoId, bool muerto)
    {
        var m = MonstruosDe(areaId).FirstOrDefault(x => x.Id == monstruoId);
        if (m is null) return ResultadoAccion.Falla();
        m.Muerto = muerto;
        return Registrar(ResultadoAccion.Ok(VerificarDespeje(areaId)));
    }

    public ResultadoAccion AgregarMonstruo(string areaId, string tipo)
    {
        if (!AreaRevelada(areaId) || Catalogo.BuscarMonstruo(tipo) is null) return ResultadoAccion.Falla();
        Estado.Monstruos[areaId].Add(new MonstruoEnJuego { Id = NuevoIdMonstruo(), Tipo = tipo, Agregado = true, Color = SiguienteColor(tipo) });
        return Registrar(ResultadoAccion.Ok(new Mensaje(TipoMensaje.Info, "Aparición",
            $"Aparece {NombreMonstruo(tipo)} en {NombreArea(areaId)}.")));
    }

    /// <summary>Solo se pueden quitar monstruos agregados a mano.</summary>
    public ResultadoAccion QuitarMonstruo(string areaId, string monstruoId)
    {
        var lista = MonstruosDe(areaId) as List<MonstruoEnJuego>;
        var m = lista?.FirstOrDefault(x => x.Id == monstruoId);
        if (m is null || !m.Agregado) return ResultadoAccion.Falla();
        lista!.Remove(m);
        return Registrar(ResultadoAccion.Ok(VerificarDespeje(areaId)));
    }

    private List<Mensaje> VerificarDespeje(string areaId)
    {
        var mensajes = new List<Mensaje>();
        if (AreaRevelada(areaId) && !Estado.AreasDespejadas.Contains(areaId)
            && MonstruosDe(areaId).All(x => x.Muerto) && Mapa.BuscarArea(areaId) is { } area)
            Despejar(area, mensajes);
        return mensajes;
    }

    private void Despejar(Area area, List<Mensaje> mensajes)
    {
        if (!Estado.AreasDespejadas.Add(area.Id)) return;
        mensajes.Add(new Mensaje(TipoMensaje.Despeje, area.Nombre + ": despejada", area.Textos.Despejar));

        foreach (var r in area.Recompensas)
            mensajes.Add(AplicarRecompensa(r));

        var cv = Mapa.Escenario.CondicionVictoria;
        if (cv.Tipo == TipoVictoria.DespejarArea && cv.Area == area.Id)
            mensajes.Add(DeclararResultadoInterno(ResultadoPartida.Victoria));
        VerificarVictoriaPorEvento(mensajes);
    }

    /// <summary>
    /// Revisar un encuentro o un cadaver: se lee su texto y, la primera vez, se otorgan sus
    /// recompensas.
    /// </summary>
    public ResultadoAccion RevisarFicha(string areaId, string fichaId)
    {
        var area = Mapa.BuscarArea(areaId);
        if (area is null || !AreaRevelada(areaId)) return ResultadoAccion.Falla();
        var ficha = area.Fichas.Select((f, i) => (f, i))
            .SelectMany(x => Enumerable.Range(0, x.f.Cantidad).Select(k => (x.f, Id: DistribucionFichas.IdFicha(areaId, x.i, k))))
            .FirstOrDefault(x => x.Id == fichaId).f;
        if (ficha is null || !ficha.EsInteractiva) return ResultadoAccion.Falla();

        var nombre = Catalogo.BuscarFicha(ficha.Tipo)?.Nombre ?? ficha.Tipo;
        var texto = ficha.Texto ?? (ficha.Tipo == "cadaver" ? "No encuentran nada útil." : "No pasa nada.");
        if (!Estado.FichasRevisadas.Add(fichaId))
            return ResultadoAccion.Ok(new Mensaje(TipoMensaje.Encuentro, nombre + " (ya revisado)", texto));

        var mensajes = new List<Mensaje> { new(TipoMensaje.Encuentro, nombre, texto) };
        foreach (var r in ficha.Recompensas) mensajes.Add(AplicarRecompensa(r));
        VerificarVictoriaPorEvento(mensajes);
        return Registrar(ResultadoAccion.Ok(mensajes));
    }

    private Mensaje AplicarRecompensa(Recompensa r)
    {
        {
            string? detalle = null;
            switch (r.Tipo)
            {
                case TipoRecompensa.OtorgarObjeto when r.Objeto is not null:
                    var cant = r.Cantidad ?? 1;
                    SumarInventario(r.Objeto, cant);
                    detalle = $"+{cant} × {NombreObjeto(r.Objeto)} al inventario del equipo";
                    break;
                case TipoRecompensa.DesbloquearPuerta when r.Puerta is not null:
                    Estado.PuertasDesbloqueadas.Add(r.Puerta);
                    break;
                case TipoRecompensa.ActivarEvento when r.Evento is not null:
                    Estado.EventosActivos.Add(r.Evento);
                    break;
                case TipoRecompensa.RevelarInfo when r.Area is not null:
                    Estado.AreasConocidas.Add(r.Area);
                    break;
            }
            return new Mensaje(TipoMensaje.Recompensa, "Recompensa", r.Texto, detalle);
        }
    }

    // ---------------------------------------------------------------- Objetos e inventario

    public ResultadoAccion RecogerObjeto(string objetoId, bool recogido)
    {
        var (area, obj) = Mapa.TodosLosObjetos().FirstOrDefault(x => x.Objeto.Id == objetoId);
        if (obj is null || !AreaRevelada(area.Id)) return ResultadoAccion.Falla();

        if (recogido && Estado.ObjetosRecogidos.Add(objetoId))
        {
            SumarInventario(obj.Tipo, obj.Cantidad);
            return Registrar(ResultadoAccion.Ok(new Mensaje(TipoMensaje.Info, "Objeto recogido",
                $"{obj.Cantidad} × {NombreObjeto(obj.Tipo)} ({area.Nombre})")));
        }
        if (!recogido && Estado.ObjetosRecogidos.Remove(objetoId))
            SumarInventario(obj.Tipo, -obj.Cantidad);
        return ResultadoAccion.Ok();
    }

    public void AjustarInventario(string objetoId, int delta) => SumarInventario(objetoId, delta);

    private void SumarInventario(string objetoId, int delta)
    {
        var nuevo = Math.Max(0, Estado.Inventario.GetValueOrDefault(objetoId) + delta);
        if (nuevo == 0) Estado.Inventario.Remove(objetoId);
        else Estado.Inventario[objetoId] = nuevo;
    }

    // ---------------------------------------------------------------- Eventos y resultado

    public ResultadoAccion CambiarEvento(string eventoId, bool activo)
    {
        var ev = Mapa.BuscarEvento(eventoId);
        if (ev is null || !ev.Manual) return ResultadoAccion.Falla();

        var mensajes = new List<Mensaje>();
        if (activo && Estado.EventosActivos.Add(eventoId))
        {
            mensajes.Add(new Mensaje(TipoMensaje.Evento, "Evento: " + ev.Nombre, ev.Descripcion ?? "Evento activado."));
            VerificarVictoriaPorEvento(mensajes);
        }
        else if (!activo)
        {
            Estado.EventosActivos.Remove(eventoId);
        }
        return Registrar(ResultadoAccion.Ok(mensajes));
    }

    private void VerificarVictoriaPorEvento(List<Mensaje> mensajes)
    {
        var cv = Mapa.Escenario.CondicionVictoria;
        if (cv.Tipo == TipoVictoria.Evento && cv.Evento is { } e && Estado.EventosActivos.Contains(e) && Estado.Resultado is null)
            mensajes.Add(DeclararResultadoInterno(ResultadoPartida.Victoria));
    }

    public ResultadoAccion DeclararResultado(ResultadoPartida resultado) =>
        Registrar(ResultadoAccion.Ok(DeclararResultadoInterno(resultado)));

    private Mensaje DeclararResultadoInterno(ResultadoPartida resultado)
    {
        Estado.Resultado = resultado;
        var textos = Mapa.Escenario.Textos;
        return resultado == ResultadoPartida.Victoria
            ? new Mensaje(TipoMensaje.Victoria, "¡Victoria!", textos.Victoria)
            : new Mensaje(TipoMensaje.Derrota, "Derrota", textos.Derrota);
    }

    // ---------------------------------------------------------------- Auxiliares

    private string NuevoIdMonstruo() => "m" + Estado.SiguienteIdMonstruo++;

    private ResultadoAccion Registrar(ResultadoAccion r)
    {
        foreach (var m in r.Mensajes.Where(m => m.Tipo != TipoMensaje.Info || m.Titulo is "Aparición" or "Objeto recogido"))
            Estado.Historial.Add(new EntradaHistorial
            {
                Momento = DateTime.Now, Tipo = m.Tipo, Titulo = m.Titulo, Texto = m.Texto, Detalle = m.Detalle,
            });
        return r;
    }

    bool IContextoRequisitos.AreaDespejada(string areaId) => Estado.AreasDespejadas.Contains(areaId);
    int IContextoRequisitos.CantidadEnInventario(string objetoId) => Estado.Inventario.GetValueOrDefault(objetoId);
    bool IContextoRequisitos.PuertaAbierta(string puertaId) => Estado.PuertasAbiertas.Contains(puertaId);
    bool IContextoRequisitos.EventoActivo(string eventoId) => Estado.EventosActivos.Contains(eventoId);
}
