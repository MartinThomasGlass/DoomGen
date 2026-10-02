using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DoomCompanion.App.ViewModels;
using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Motor;

namespace DoomCompanion.App.Controles;

/// <summary>
/// Plano del mapa con niebla de guerra: solo dibuja areas reveladas (y el contorno de las
/// conocidas) y las puertas visibles. Se escala solo para ocupar el espacio disponible.
/// </summary>
public sealed class MapaControl : Border
{
    private const double Celda = 40;
    private const double MinAncho = 22;
    private const double MinAlto = 13;

    private readonly Canvas _canvas = new();

    public MapaControl()
    {
        Child = new Viewbox { Stretch = Stretch.Uniform, Child = _canvas, Margin = new Thickness(8) };
        ClipToBounds = true;
    }

    public event Action<string>? AreaClick;
    public event Action<string>? PuertaClick;

    public static readonly DependencyProperty MotorProperty = DependencyProperty.Register(
        nameof(Motor), typeof(MotorJuego), typeof(MapaControl), new PropertyMetadata(null, (d, _) => ((MapaControl)d).Redibujar()));

    public static readonly DependencyProperty VersionProperty = DependencyProperty.Register(
        nameof(Version), typeof(int), typeof(MapaControl), new PropertyMetadata(0, (d, _) => ((MapaControl)d).Redibujar()));

    public static readonly DependencyProperty SeleccionIdProperty = DependencyProperty.Register(
        nameof(SeleccionId), typeof(string), typeof(MapaControl), new PropertyMetadata(null, (d, _) => ((MapaControl)d).Redibujar()));

    public MotorJuego? Motor { get => (MotorJuego?)GetValue(MotorProperty); set => SetValue(MotorProperty, value); }
    public int Version { get => (int)GetValue(VersionProperty); set => SetValue(VersionProperty, value); }
    public string? SeleccionId { get => (string?)GetValue(SeleccionIdProperty); set => SetValue(SeleccionIdProperty, value); }

    private static readonly Brush FondoRevelada = Congelar(new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x45)));
    private static readonly Brush FondoDespejada = Congelar(new SolidColorBrush(Color.FromRgb(0x2E, 0x45, 0x34)));
    private static readonly Brush BordeArea = Congelar(new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x99)));
    private static readonly Brush BordeSeleccion = Congelar(new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)));
    private static readonly Brush Cuadricula = CrearCuadricula();

    private void Redibujar()
    {
        _canvas.Children.Clear();
        var motor = Motor;
        if (motor is null) return;

        var areasVisibles = motor.Mapa.Areas.Where(a => motor.AreaRevelada(a.Id) || motor.AreaConocida(a.Id)).ToList();
        var rects = areasVisibles.ToDictionary(a => a.Id, a => a.Tiles.Select(t => RectTile(t, motor.Catalogo)).ToList());
        var puertas = motor.PuertasVisibles.ToList();
        var rectsPuertas = UbicarPuertas(puertas, rects, motor);

        // Limites solo de lo visible: el tamaño del lienzo no puede delatar areas ocultas.
        var todos = rects.Values.SelectMany(r => r).Concat(rectsPuertas.Values).ToList();
        if (todos.Count == 0) return;
        var minX = todos.Min(r => r.Left) - 1;
        var minY = todos.Min(r => r.Top) - 1;
        var maxX = todos.Max(r => r.Right) + 1;
        var maxY = todos.Max(r => r.Bottom) + 1;
        var ancho = Math.Max(maxX - minX, MinAncho);
        var alto = Math.Max(maxY - minY, MinAlto);
        minX -= (ancho - (maxX - minX)) / 2;
        minY -= (alto - (maxY - minY)) / 2;
        _canvas.Width = ancho * Celda;
        _canvas.Height = alto * Celda;

        Point P(double x, double y) => new((x - minX) * Celda, (y - minY) * Celda);

        foreach (var area in areasVisibles)
        {
            var conocida = motor.AreaConocida(area.Id);
            var despejada = motor.Estado.AreasDespejadas.Contains(area.Id);
            var seleccionada = area.Id == SeleccionId;

            foreach (var r in rects[area.Id])
            {
                var forma = new Rectangle
                {
                    Width = r.Width * Celda,
                    Height = r.Height * Celda,
                    Fill = conocida ? Brushes.Transparent : despejada ? FondoDespejada : FondoRevelada,
                    Stroke = seleccionada ? BordeSeleccion : BordeArea,
                    StrokeThickness = seleccionada ? 4 : 2,
                    RadiusX = 3,
                    RadiusY = 3,
                    Cursor = Cursors.Hand,
                    Tag = area.Id,
                };
                if (conocida) forma.StrokeDashArray = [4, 3];
                forma.MouseLeftButtonDown += (_, e) => { AreaClick?.Invoke(area.Id); e.Handled = true; };
                Ubicar(forma, P(r.Left, r.Top));
                _canvas.Children.Add(forma);

                if (!conocida)
                {
                    var grilla = new Rectangle { Width = forma.Width, Height = forma.Height, Fill = Cuadricula, IsHitTestVisible = false };
                    Ubicar(grilla, P(r.Left, r.Top));
                    _canvas.Children.Add(grilla);
                }
            }

            // Etiqueta en el tile mas grande del area.
            var principal = rects[area.Id].OrderByDescending(r => r.Width * r.Height).First();
            var vivos = motor.MonstruosDe(area.Id).Count(m => !m.Muerto);
            var etiqueta = new TextBlock
            {
                Text = conocida ? $"? {area.Nombre}" : area.Nombre + (vivos > 0 ? $"\n☠ {vivos}" : despejada ? "\n✔" : ""),
                Foreground = conocida ? Colores.Conocida : Brushes.White,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(principal.Width * Celda - 8, 60),
                IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 0, Opacity = 1 },
            };
            etiqueta.Measure(new Size(etiqueta.Width, double.PositiveInfinity));
            var centro = P(principal.Left + principal.Width / 2, principal.Top + principal.Height / 2);
            Ubicar(etiqueta, new Point(centro.X - etiqueta.Width / 2, centro.Y - etiqueta.DesiredSize.Height / 2));
            _canvas.Children.Add(etiqueta);
        }

        foreach (var puerta in puertas)
        {
            var r = rectsPuertas[puerta.Id];
            var abierta = motor.EstaPuertaAbierta(puerta.Id);
            var color = Colores.DePuerta(puerta.Tipo);
            Shape forma = puerta.Tipo == TipoPuerta.Teleportador ? new Ellipse() : new Rectangle { RadiusX = 3, RadiusY = 3 };
            forma.Width = r.Width * Celda;
            forma.Height = r.Height * Celda;
            forma.Fill = abierta ? Brushes.Transparent : color;
            forma.Stroke = abierta ? color : Brushes.Black;
            forma.StrokeThickness = abierta ? 3 : 2;
            forma.Cursor = Cursors.Hand;
            if (puerta.Tipo == TipoPuerta.Paso) forma.StrokeDashArray = [2, 2];
            forma.ToolTip = $"{MotorJuego.NombrePuerta(puerta)} ({(abierta ? "abierta" : "cerrada")})" +
                            (abierta ? "" : "\nClic para intentar abrirla");
            var id = puerta.Id;
            forma.MouseLeftButtonDown += (_, e) => { PuertaClick?.Invoke(id); e.Handled = true; };
            Ubicar(forma, P(r.Left, r.Top));
            _canvas.Children.Add(forma);
        }
    }

    /// <summary>Rectangulo en casillas que ocupa un tile, segun el catalogo o lo que diga el mapa.</summary>
    private static Rect RectTile(ColocacionTile t, Catalogo catalogo)
    {
        double ancho, alto;
        if (catalogo.BuscarTile(t.Tipo) is { Ancho: int an, Alto: int al })
            (ancho, alto) = t.Rotacion is 90 or 270 ? (al, an) : (an, al);
        else
            (ancho, alto) = (t.Ancho ?? 4, t.Alto ?? 4);
        return new Rect(t.X, t.Y, ancho, alto);
    }

    private static Dictionary<string, Rect> UbicarPuertas(List<Puerta> puertas, Dictionary<string, List<Rect>> rects, MotorJuego motor)
    {
        var resultado = new Dictionary<string, Rect>();
        var sinPosicionPorArea = new Dictionary<string, int>();
        foreach (var p in puertas)
        {
            if (p.Posicion is { } pos)
            {
                resultado[p.Id] = pos.Orientacion == Orientacion.Horizontal
                    ? new Rect(pos.X, pos.Y - 0.3, 2, 0.6)
                    : new Rect(pos.X - 0.3, pos.Y, 0.6, 2);
                continue;
            }
            // Sin posicion (p.ej. teleportadores): icono dentro del area revelada de ese lado.
            var lado = motor.AreaRevelada(p.Desde) ? p.Desde : p.Hacia;
            var baseRect = rects.TryGetValue(lado, out var rs) ? rs[0] : new Rect(0, 0, 1, 1);
            var n = sinPosicionPorArea.GetValueOrDefault(lado);
            sinPosicionPorArea[lado] = n + 1;
            resultado[p.Id] = new Rect(baseRect.Left + 0.25 + n * 1.1, baseRect.Top + 0.25, 0.9, 0.9);
        }
        return resultado;
    }

    private static void Ubicar(UIElement e, Point p)
    {
        Canvas.SetLeft(e, p.X);
        Canvas.SetTop(e, p.Y);
    }

    private static Brush CrearCuadricula()
    {
        var linea = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), 1);
        var dibujo = new GeometryDrawing(null, linea, new RectangleGeometry(new Rect(0, 0, Celda, Celda)));
        var brush = new DrawingBrush(dibujo)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, Celda, Celda),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, Celda, Celda),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }

    private static Brush Congelar(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
