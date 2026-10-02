using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoomCompanion.App.Servicios;
using DoomCompanion.Core;
using DoomCompanion.Core.Guia;
using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Motor;
using DoomCompanion.Core.Persistencia;
using DoomCompanion.Core.Validacion;

namespace DoomCompanion.App.ViewModels;

/// <summary>Dialogos que el ViewModel le pide a la ventana.</summary>
public interface IDialogos
{
    string? ElegirArchivoParaAbrir(string titulo, string filtro);
    string? ElegirArchivoParaGuardar(string titulo, string filtro, string nombreSugerido);
    bool Confirmar(string titulo, string mensaje);
    void Avisar(string titulo, string mensaje, bool error = false);
    /// <summary>Muestra el resultado de la validacion; devuelve true si el usuario elige jugar.</summary>
    bool MostrarResultadoImportacion(ResultadoImportacion resultado, bool soloConsulta = false);
}

public sealed partial class MainViewModel : ObservableObject
{
    private const string FiltroMapa = "Mapa de Doom Companion (*.json)|*.json|Todos los archivos (*.*)|*.*";
    private const string FiltroPartida = "Partida de Doom Companion (*.doomsave)|*.doomsave";

    private readonly IDialogos _dialogos;
    private Catalogo _catalogo;
    private MotorJuego? _motor;
    private string? _mapaJson;
    private ResultadoImportacion? _importacion;
    private readonly Queue<Mensaje> _cola = new();

    public MainViewModel(IDialogos dialogos)
    {
        _dialogos = dialogos;
        (_catalogo, var error) = ServicioDatos.CargarCatalogo();
        if (error is not null) _dialogos.Avisar("Catálogo", error, error: true);
        HayAutoguardado = File.Exists(ServicioDatos.RutaAutoguardado);
        ActualizarOpciones();
    }

    public MotorJuego? Motor => _motor;

    // ------------------------------------------------------------------ Estado observable

    [ObservableProperty, NotifyPropertyChangedFor(nameof(TituloVentana))] private bool _hayPartida;
    [ObservableProperty] private bool _hayAutoguardado;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(TituloVentana))] private string _titulo = "Doom Companion";
    [ObservableProperty] private string _subtitulo = "";
    [ObservableProperty] private IReadOnlyList<AreaVM> _areas = [];
    [ObservableProperty] private AreaVM? _areaSeleccionada;
    [ObservableProperty] private IReadOnlyList<GrupoInventarioVM> _inventario = [];
    [ObservableProperty] private IReadOnlyList<EventoVM> _eventosManuales = [];
    [ObservableProperty] private IReadOnlyList<string> _eventosAutomaticos = [];
    [ObservableProperty] private IReadOnlyList<EntradaHistorial> _historial = [];
    [ObservableProperty] private IReadOnlyList<OpcionVM> _tiposMonstruo = [];
    [ObservableProperty] private IReadOnlyList<OpcionVM> _tiposObjeto = [];
    [ObservableProperty] private OpcionVM? _monstruoAAgregar;
    [ObservableProperty] private OpcionVM? _objetoAAgregar;
    [ObservableProperty] private int _versionMapa;
    [ObservableProperty] private string? _seleccionId;
    [ObservableProperty] private Mensaje? _mensajeActual;
    [ObservableProperty] private string _indiceLector = "";
    [ObservableProperty] private bool _notasInvasorVisibles;
    [ObservableProperty] private string? _notasInvasorEscenario;
    [ObservableProperty] private double _escala = 1.0;
    [ObservableProperty] private string _estado = "Importá un mapa o cargá el escenario de ejemplo para empezar.";
    [ObservableProperty] private string? _resultadoTexto;

    public bool LectorVisible => MensajeActual is not null;

    public string TituloVentana => HayPartida ? $"{Titulo} — Doom Companion" : "Doom Companion";

    partial void OnMensajeActualChanged(Mensaje? value) => OnPropertyChanged(nameof(LectorVisible));

    // ------------------------------------------------------------------ Escenario: importar / cargar

    [RelayCommand]
    private void ImportarMapa()
    {
        var ruta = _dialogos.ElegirArchivoParaAbrir("Importar mapa", FiltroMapa);
        if (ruta is null) return;
        ProcesarImportacion(ImportadorMapa.ImportarArchivo(ruta, _catalogo));
    }

    [RelayCommand]
    private void CargarEjemplo() =>
        ProcesarImportacion(ImportadorMapa.Importar(Recursos.LeerTexto(Recursos.Ejemplo), _catalogo));

    private void ProcesarImportacion(ResultadoImportacion r)
    {
        if (!ConfirmarAbandonarPartida()) return;
        if (!_dialogos.MostrarResultadoImportacion(r) || !r.SePuedeJugar) return;
        EmpezarPartida(r.Mapa!, r.JsonOriginal!, r);
    }

    private bool ConfirmarAbandonarPartida() =>
        !HayPartida || _dialogos.Confirmar("Nueva partida",
            "Hay una partida en curso. Está autoguardada, pero se va a reemplazar por la nueva. ¿Continuar?");

    private void EmpezarPartida(Mapa mapa, string json, ResultadoImportacion importacion)
    {
        _motor = new MotorJuego(mapa, _catalogo);
        _mapaJson = json;
        _importacion = importacion;
        NotasInvasorVisibles = false;
        var r = _motor.Iniciar();
        HayPartida = true;
        Estado = $"Partida iniciada: {mapa.Escenario.Titulo}. Se autoguarda después de cada acción.";
        Refrescar(seleccionar: mapa.Escenario.AreaInicial);
        Mostrar(r.Mensajes);
        Autoguardar();
    }

    [RelayCommand]
    private void GuardarPartida()
    {
        if (_motor is null) return;
        var ruta = _dialogos.ElegirArchivoParaGuardar("Guardar partida", FiltroPartida,
            $"{_motor.Mapa.Escenario.Id}-{DateTime.Now:yyyyMMdd-HHmm}{ArchivoPartida.Extension}");
        if (ruta is null) return;
        try
        {
            ArchivoPartida.Guardar(ruta, _motor.Mapa, _mapaJson!, _motor.Estado);
            Estado = $"Partida guardada en {Path.GetFileName(ruta)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogos.Avisar("Guardar partida", $"No se pudo guardar: {ex.Message}", error: true);
        }
    }

    [RelayCommand]
    private void CargarPartida()
    {
        var ruta = _dialogos.ElegirArchivoParaAbrir("Cargar partida", FiltroPartida);
        if (ruta is not null) CargarDesde(ruta);
    }

    [RelayCommand]
    private void ContinuarAutoguardado() => CargarDesde(ServicioDatos.RutaAutoguardado);

    private void CargarDesde(string ruta)
    {
        if (!ConfirmarAbandonarPartida()) return;
        var carga = ArchivoPartida.Cargar(ruta, _catalogo);
        if (!carga.Exito)
        {
            _dialogos.Avisar("Cargar partida", carga.Mensaje!, error: true);
            return;
        }
        _motor = carga.Motor;
        _mapaJson = carga.MapaJson;
        _importacion = carga.Importacion;
        NotasInvasorVisibles = false;
        HayPartida = true;
        Refrescar(seleccionar: _motor!.Mapa.Escenario.AreaInicial);
        Estado = $"Partida cargada: {_motor.Mapa.Escenario.Titulo}.";
        Autoguardar();
    }

    [RelayCommand]
    private void Reiniciar()
    {
        if (_motor is null) return;
        if (!_dialogos.Confirmar("Reiniciar escenario", "Se pierde todo el progreso de esta partida. ¿Reiniciar el escenario desde el principio?"))
            return;
        var r = _motor.Iniciar();
        Refrescar(seleccionar: _motor.Mapa.Escenario.AreaInicial);
        Mostrar(r.Mensajes);
        Autoguardar();
    }

    [RelayCommand]
    private void VerValidacion()
    {
        if (_importacion is not null) _dialogos.MostrarResultadoImportacion(_importacion, soloConsulta: true);
    }

    // ------------------------------------------------------------------ Guia y catalogo

    [RelayCommand]
    private void ExportarGuia()
    {
        var ruta = _dialogos.ElegirArchivoParaGuardar("Exportar guía para IA", "Markdown (*.md)|*.md", "guia-mapas-doom-companion.md");
        if (ruta is null) return;
        try
        {
            File.WriteAllText(ruta, GeneradorGuiaIA.Generar(_catalogo));
            _dialogos.Avisar("Guía para IA",
                $"Guía exportada en:\n{ruta}\n\nPegásela a Claude y pedile, por ejemplo:\n\"Generá un mapa de dificultad media para 2 marines con ambientación laboratorio inundado. Respondé solo con el JSON.\"\n\nDespués guardá la respuesta como .json e importala.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogos.Avisar("Guía para IA", $"No se pudo exportar: {ex.Message}", error: true);
        }
    }

    [RelayCommand]
    private void EditarCatalogo()
    {
        ServicioDatos.AbrirCatalogoParaEditar();
        _dialogos.Avisar("Catálogo",
            $"Se abrió tu catálogo para editar:\n{ServicioDatos.RutaCatalogo}\n\nCuando termines de editarlo, guardalo y usá \"Recargar catálogo\".");
    }

    [RelayCommand]
    private void RecargarCatalogo()
    {
        var (catalogo, error) = ServicioDatos.CargarCatalogo();
        if (error is not null)
        {
            _dialogos.Avisar("Catálogo", error, error: true);
            return;
        }
        _catalogo = catalogo;
        ActualizarOpciones();
        Estado = "Catálogo recargado. Los cambios se aplican a los próximos mapas que importes.";
    }

    // ------------------------------------------------------------------ Acciones de juego

    [RelayCommand]
    private void SeleccionarArea(string? id)
    {
        if (id is null) return;
        AreaSeleccionada = Areas.FirstOrDefault(a => a.Id == id);
        SeleccionId = AreaSeleccionada?.Id;
    }

    [RelayCommand]
    private void AbrirPuerta(string? puertaId)
    {
        if (_motor is null || puertaId is null) return;
        var antes = _motor.Estado.AreasReveladas.ToHashSet();
        var r = _motor.AbrirPuerta(puertaId);
        var nueva = _motor.Estado.AreasReveladas.FirstOrDefault(a => !antes.Contains(a));
        Aplicar(r, seleccionar: nueva);
    }

    [RelayCommand]
    private void AlternarMonstruo(MonstruoVM? m)
    {
        if (_motor is null || m is null) return;
        Aplicar(_motor.MarcarMonstruo(m.AreaId, m.Id, !m.Muerto));
    }

    [RelayCommand]
    private void QuitarMonstruo(MonstruoVM? m)
    {
        if (_motor is null || m is null) return;
        Aplicar(_motor.QuitarMonstruo(m.AreaId, m.Id));
    }

    [RelayCommand]
    private void AgregarMonstruo()
    {
        if (_motor is null || AreaSeleccionada is null || MonstruoAAgregar is null || AreaSeleccionada.EsConocida) return;
        Aplicar(_motor.AgregarMonstruo(AreaSeleccionada.Id, MonstruoAAgregar.Id));
    }

    [RelayCommand]
    private void AlternarObjeto(ObjetoVM? o)
    {
        if (_motor is null || o is null) return;
        Aplicar(_motor.RecogerObjeto(o.Id, !o.Recogido));
    }

    [RelayCommand]
    private void SumarInventario(ItemInventarioVM? item) => AjustarInventario(item?.Id, +1);

    [RelayCommand]
    private void RestarInventario(ItemInventarioVM? item) => AjustarInventario(item?.Id, -1);

    [RelayCommand]
    private void AgregarAlInventario() => AjustarInventario(ObjetoAAgregar?.Id, +1);

    private void AjustarInventario(string? id, int delta)
    {
        if (_motor is null || id is null) return;
        _motor.AjustarInventario(id, delta);
        Aplicar(ResultadoAccion.Ok());
    }

    [RelayCommand]
    private void AlternarEvento(EventoVM? e)
    {
        if (_motor is null || e is null) return;
        if (!e.Activo && !_dialogos.Confirmar("Evento", $"¿Marcar el evento \"{e.Nombre}\" como ocurrido?\n\n{e.Descripcion}"))
        {
            Refrescar();
            return;
        }
        Aplicar(_motor.CambiarEvento(e.Id, !e.Activo));
    }

    [RelayCommand]
    private void DeclararVictoria() => DeclararResultado(ResultadoPartida.Victoria, "¿Los marines cumplieron el objetivo?");

    [RelayCommand]
    private void DeclararDerrota() => DeclararResultado(ResultadoPartida.Derrota, "¿Los marines fueron derrotados?");

    private void DeclararResultado(ResultadoPartida resultado, string pregunta)
    {
        if (_motor is null || !_dialogos.Confirmar(resultado == ResultadoPartida.Victoria ? "Victoria" : "Derrota", pregunta)) return;
        Aplicar(_motor.DeclararResultado(resultado));
    }

    private void Aplicar(ResultadoAccion r, string? seleccionar = null)
    {
        Refrescar(seleccionar);
        Mostrar(r.Mensajes);
        Autoguardar();
    }

    // ------------------------------------------------------------------ Textos y lector

    [RelayCommand]
    private void VerBriefing()
    {
        if (_motor is null) return;
        var t = _motor.Mapa.Escenario.Textos;
        Mostrar([
            new Mensaje(TipoMensaje.Briefing, _motor.Mapa.Escenario.Titulo, t.Introduccion),
            new Mensaje(TipoMensaje.Objetivo, "Objetivos", t.Objetivos),
        ]);
    }

    [RelayCommand]
    private void AlternarNotasInvasor()
    {
        if (_motor is null) return;
        if (!NotasInvasorVisibles && !_dialogos.Confirmar("Notas del invasor",
                "Las notas del invasor son privadas: pueden revelar lo que hay en el mapa.\n\n¿Mostrarlas? (que los marines no miren)"))
            return;
        NotasInvasorVisibles = !NotasInvasorVisibles;
        Refrescar();
    }

    [RelayCommand]
    private void ReleerEntrada()
    {
        if (_motor is null || AreaSeleccionada is null || AreaSeleccionada.EsConocida) return;
        var area = _motor.Mapa.BuscarArea(AreaSeleccionada.Id)!;
        Mostrar([new Mensaje(TipoMensaje.Entrada, area.Nombre, area.Textos.Entrar, _motor.DescribirPreparacion(area))]);
    }

    [RelayCommand]
    private void VerEntradaHistorial(EntradaHistorial? e)
    {
        if (e is not null) Mostrar([new Mensaje(e.Tipo, e.Titulo, e.Texto, e.Detalle)]);
    }

    private void Mostrar(IEnumerable<Mensaje> mensajes)
    {
        foreach (var m in mensajes)
        {
            if (m.Tipo == TipoMensaje.Info)
                Estado = $"{m.Titulo}: {m.Texto}";
            else
                _cola.Enqueue(m);
        }
        if (MensajeActual is null) SiguienteMensaje();
        else ActualizarIndice();
    }

    [RelayCommand]
    private void SiguienteMensaje()
    {
        MensajeActual = _cola.Count > 0 ? _cola.Dequeue() : null;
        ActualizarIndice();
    }

    [RelayCommand]
    private void CerrarLector()
    {
        _cola.Clear();
        MensajeActual = null;
    }

    private void ActualizarIndice() =>
        IndiceLector = _cola.Count > 0 ? $"Siguiente ({_cola.Count} más)" : "Continuar";

    // ------------------------------------------------------------------ Pantalla

    [RelayCommand]
    private void AumentarEscala() => Escala = Math.Min(2.0, Math.Round(Escala + 0.1, 1));

    [RelayCommand]
    private void ReducirEscala() => Escala = Math.Max(0.7, Math.Round(Escala - 0.1, 1));

    // ------------------------------------------------------------------ Proyeccion del estado

    private void Refrescar(string? seleccionar = null)
    {
        if (_motor is null) return;
        var motor = _motor;
        var mapa = motor.Mapa;
        var esc = mapa.Escenario;

        Titulo = esc.Titulo;
        Subtitulo = $"Dificultad {esc.Dificultad.ToString().ToLowerInvariant()} · {esc.Marines} marine(s)" +
                    (string.IsNullOrWhiteSpace(esc.Ambientacion) ? "" : $" · {esc.Ambientacion}");
        ResultadoTexto = motor.Estado.Resultado switch
        {
            ResultadoPartida.Victoria => "VICTORIA",
            ResultadoPartida.Derrota => "DERROTA",
            _ => null,
        };
        NotasInvasorEscenario = NotasInvasorVisibles ? esc.Textos.NotasInvasor : null;

        var areas = new List<AreaVM>();
        foreach (var area in mapa.Areas)
        {
            if (motor.AreaRevelada(area.Id)) areas.Add(ProyectarArea(area));
            else if (motor.AreaConocida(area.Id))
                areas.Add(new AreaVM { Id = area.Id, Nombre = area.Nombre, Estado = EstadoArea.Conocida });
        }
        Areas = areas;

        var id = seleccionar ?? AreaSeleccionada?.Id ?? esc.AreaInicial;
        AreaSeleccionada = areas.FirstOrDefault(a => a.Id == id) ?? areas.FirstOrDefault();
        SeleccionId = AreaSeleccionada?.Id;

        Inventario = motor.Estado.Inventario
            .Select(kv => (Id: kv.Key, Cant: kv.Value, Cat: motor.CategoriaObjeto(kv.Key)))
            .GroupBy(x => x.Cat)
            .OrderBy(g => g.Key)
            .Select(g => new GrupoInventarioVM
            {
                Nombre = NombreCategoria(g.Key),
                Items = g.Select(x => new ItemInventarioVM { Id = x.Id, Nombre = motor.NombreObjeto(x.Id), Cantidad = x.Cant })
                    .OrderBy(i => i.Nombre).ToList(),
            })
            .ToList();

        EventosManuales = mapa.Eventos.Where(e => e.Manual)
            .Select(e => new EventoVM { Id = e.Id, Nombre = e.Nombre, Descripcion = e.Descripcion, Activo = motor.Estado.EventosActivos.Contains(e.Id) })
            .ToList();
        EventosAutomaticos = mapa.Eventos.Where(e => !e.Manual && motor.Estado.EventosActivos.Contains(e.Id))
            .Select(e => e.Nombre).ToList();

        Historial = motor.Estado.Historial.AsEnumerable().Reverse().ToList();
        ActualizarOpciones();
        OnPropertyChanged(nameof(Motor));
        VersionMapa++;
    }

    private AreaVM ProyectarArea(Area area)
    {
        var motor = _motor!;
        var despejada = motor.Estado.AreasDespejadas.Contains(area.Id);
        return new AreaVM
        {
            Id = area.Id,
            Nombre = area.Nombre,
            Estado = despejada ? EstadoArea.Despejada : EstadoArea.Revelada,
            Preparacion = motor.DescribirPreparacion(area),
            TextoEntrar = area.Textos.Entrar,
            TextoDespejar = despejada ? area.Textos.Despejar : "",
            NotasInvasor = NotasInvasorVisibles ? area.NotasInvasor : null,
            Monstruos = motor.MonstruosDe(area.Id)
                .Select(m => new MonstruoVM { AreaId = area.Id, Id = m.Id, Nombre = motor.NombreMonstruo(m.Tipo), Muerto = m.Muerto, Agregado = m.Agregado })
                .ToList(),
            Objetos = area.Objetos
                .Select(o => new ObjetoVM { Id = o.Id, Nombre = motor.NombreObjeto(o.Tipo), Cantidad = o.Cantidad, Texto = o.Texto, Recogido = motor.Estado.ObjetosRecogidos.Contains(o.Id) })
                .ToList(),
            Puertas = motor.Mapa.PuertasDe(area.Id)
                .Select(p =>
                {
                    var otra = p.Otra(area.Id);
                    var destino = motor.AreaRevelada(otra) || motor.AreaConocida(otra) ? motor.NombreArea(otra) : "Sin explorar";
                    return new PuertaVM { Id = p.Id, Tipo = p.Tipo, NombreTipo = MotorJuego.NombrePuerta(p), Destino = destino, Abierta = motor.EstaPuertaAbierta(p.Id) };
                })
                .ToList(),
            Recompensas = despejada ? area.Recompensas.Select(r => r.Texto).ToList() : [],
        };
    }

    private void ActualizarOpciones()
    {
        TiposMonstruo = _catalogo.Monstruos.Select(m => new OpcionVM { Id = m.Id, Nombre = m.Nombre }).ToList();
        var objetos = _catalogo.Objetos.Select(o => new OpcionVM { Id = o.Id, Nombre = o.Nombre });
        if (_motor is not null)
            objetos = objetos.Concat(_motor.Mapa.ObjetosMision.Select(o => new OpcionVM { Id = o.Id, Nombre = o.Nombre }));
        TiposObjeto = objetos.ToList();
        MonstruoAAgregar = TiposMonstruo.FirstOrDefault(m => m.Id == MonstruoAAgregar?.Id) ?? TiposMonstruo.FirstOrDefault();
        ObjetoAAgregar = TiposObjeto.FirstOrDefault(o => o.Id == ObjetoAAgregar?.Id) ?? TiposObjeto.FirstOrDefault();
    }

    private static string NombreCategoria(CategoriaObjeto c) => c switch
    {
        CategoriaObjeto.Llave => "Llaves",
        CategoriaObjeto.Arma => "Armas",
        CategoriaObjeto.Municion => "Munición",
        CategoriaObjeto.Salud => "Salud",
        CategoriaObjeto.Armadura => "Armadura",
        CategoriaObjeto.Mision => "Objetos de misión",
        _ => "Otros",
    };

    private void Autoguardar()
    {
        if (_motor is null || _mapaJson is null) return;
        try
        {
            ArchivoPartida.Guardar(ServicioDatos.RutaAutoguardado, _motor.Mapa, _mapaJson, _motor.Estado);
            HayAutoguardado = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Estado = $"No se pudo autoguardar: {ex.Message}";
        }
    }
}
