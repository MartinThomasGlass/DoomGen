using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using DoomCompanion.App.ViewModels;
using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Motor;

namespace DoomCompanion.App.Controles;

/// <summary>
/// Plano del mapa con niebla de guerra, dibujado con el estilo de las piezas del juego: placas
/// metalicas, bordes industriales, paredes biseladas y fichas de puerta. Solo dibuja areas
/// reveladas (y el contorno de las conocidas) y las puertas visibles.
/// </summary>
public sealed class MapaControl : Border
{
    private const double Celda = 40;
    private const int MinAncho = 22;
    private const int MinAlto = 13;

    private readonly Canvas _canvas = new();

    public MapaControl()
    {
        Background = Fondo;
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

    // ------------------------------------------------------------------ Dibujo

    private sealed record PiezaVisible(Area Area, ColocacionTile Tile, IReadOnlyList<Celda> Celdas);

    private void Redibujar()
    {
        _canvas.Children.Clear();
        var motor = Motor;
        if (motor is null) return;
        var catalogo = motor.Catalogo;

        var areasVisibles = motor.Mapa.Areas.Where(a => motor.AreaRevelada(a.Id) || motor.AreaConocida(a.Id)).ToList();
        var piezas = areasVisibles
            .SelectMany(a => a.Tiles.Select(t => new PiezaVisible(a, t, GeometriaTiles.Celdas(t, catalogo))))
            .ToList();
        var puertas = motor.PuertasVisibles.ToList();
        var rectsPuertas = UbicarPuertas(puertas, piezas, motor);

        // Limites solo de lo visible: el tamaño del lienzo no puede delatar areas ocultas.
        // El desplazamiento es entero para que las texturas queden alineadas con las casillas.
        var todos = piezas.SelectMany(p => p.Celdas).Select(c => new Rect(c.X, c.Y, 1, 1)).Concat(rectsPuertas.Values).ToList();
        if (todos.Count == 0) return;
        var minX = (int)Math.Floor(todos.Min(r => r.Left)) - 1;
        var minY = (int)Math.Floor(todos.Min(r => r.Top)) - 1;
        var maxX = (int)Math.Ceiling(todos.Max(r => r.Right)) + 1;
        var maxY = (int)Math.Ceiling(todos.Max(r => r.Bottom)) + 1;
        var ancho = Math.Max(maxX - minX, MinAncho);
        var alto = Math.Max(maxY - minY, MinAlto);
        minX -= (ancho - (maxX - minX)) / 2;
        minY -= (alto - (maxY - minY)) / 2;
        _canvas.Width = ancho * Celda;
        _canvas.Height = alto * Celda;

        Point P(double x, double y) => new((x - minX) * Celda, (y - minY) * Celda);

        DibujarFondo(ancho * Celda, alto * Celda);

        var reveladas = piezas.Where(p => motor.AreaRevelada(p.Area.Id)).ToList();
        var conocidas = piezas.Where(p => motor.AreaConocida(p.Area.Id)).ToList();

        // 1. Pisos.
        foreach (var pieza in reveladas)
        {
            var conexion = GeometriaTiles.CeldasDeConexion(pieza.Tile, catalogo).ToHashSet();
            var celdas = pieza.Celdas.ToHashSet();
            bool EsBorde(Celda c) =>
                !celdas.Contains(c with { X = c.X - 1 }) || !celdas.Contains(c with { X = c.X + 1 }) ||
                !celdas.Contains(c with { Y = c.Y - 1 }) || !celdas.Contains(c with { Y = c.Y + 1 });

            var interior = pieza.Celdas.Where(c => !conexion.Contains(c) && !EsBorde(c)).ToList();
            var borde = pieza.Celdas.Where(c => !conexion.Contains(c) && EsBorde(c)).ToList();
            Agregar(Union(interior, P), PlacaClara);
            Agregar(Union(borde, P), PlacaBorde);
            Agregar(Union(conexion.Where(celdas.Contains).ToList(), P), PlacaConexion);
        }

        // 2. Manchas: siempre las mismas para cada area.
        foreach (var grupo in reveladas.GroupBy(p => p.Area))
            DibujarManchas(grupo.Key.Id, grupo.SelectMany(p => p.Celdas).ToList(), P);

        // 3. Paredes biseladas (abiertas en las conexiones).
        var paredes = new GeometryGroup();
        foreach (var pieza in reveladas)
            foreach (var s in GeometriaTiles.Paredes(pieza.Tile, catalogo))
                paredes.Children.Add(new LineGeometry(P(s.X1, s.Y1), P(s.X2, s.Y2)));
        paredes.Freeze();
        _canvas.Children.Add(new Path { Data = paredes, Stroke = ParedOscura, StrokeThickness = 8, StrokeStartLineCap = PenLineCap.Square, StrokeEndLineCap = PenLineCap.Square, IsHitTestVisible = false });
        _canvas.Children.Add(new Path { Data = paredes, Stroke = ParedClara, StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Square, StrokeEndLineCap = PenLineCap.Square, IsHitTestVisible = false });

        // 4. Areas conocidas: solo un contorno punteado.
        foreach (var pieza in conocidas)
            _canvas.Children.Add(new Path
            {
                Data = Union(pieza.Celdas, P),
                Fill = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                Stroke = Colores.Conocida,
                StrokeThickness = 2,
                StrokeDashArray = [4, 3],
                IsHitTestVisible = false,
            });

        // 5. Zona de clic y seleccion de cada pieza.
        foreach (var pieza in piezas)
        {
            var seleccionada = pieza.Area.Id == SeleccionId;
            var zona = new Path
            {
                Data = Union(pieza.Celdas, P),
                Fill = Brushes.Transparent,
                Stroke = seleccionada ? BordeSeleccion : null,
                StrokeThickness = 4,
                Cursor = Cursors.Hand,
                Effect = seleccionada ? new DropShadowEffect { Color = Color.FromRgb(0xFF, 0xC1, 0x07), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 } : null,
            };
            var id = pieza.Area.Id;
            zona.MouseLeftButtonDown += (_, e) => { AreaClick?.Invoke(id); e.Handled = true; };
            _canvas.Children.Add(zona);
        }

        // 6. Puertas.
        foreach (var puerta in puertas)
            DibujarPuerta(puerta, rectsPuertas[puerta.Id], motor, P);

        // 7. Carteles con el nombre de cada area.
        foreach (var grupo in piezas.GroupBy(p => p.Area))
            DibujarCartel(grupo.Key, grupo.ToList(), motor, P);
    }

    private void Agregar(Geometry? geometria, Brush relleno)
    {
        if (geometria is null) return;
        _canvas.Children.Add(new Path { Data = geometria, Fill = relleno, IsHitTestVisible = false });
    }

    /// <summary>Union de casillas (las piezas pueden ser irregulares).</summary>
    private static Geometry? Union(IReadOnlyList<Celda> celdas, Func<double, double, Point> P)
    {
        Geometry? union = null;
        foreach (var c in celdas)
        {
            var r = new RectangleGeometry(new Rect(P(c.X, c.Y), P(c.X + 1, c.Y + 1)));
            union = union is null ? r : Geometry.Combine(union, r, GeometryCombineMode.Union, null);
        }
        union?.Freeze();
        return union;
    }

    private void DibujarFondo(double ancho, double alto)
    {
        _canvas.Children.Add(new Rectangle { Width = ancho, Height = alto, Fill = FondoVineta, IsHitTestVisible = false });

        // Pentagrama tenue, como en las piezas y la caja del juego.
        var cx = ancho / 2;
        var cy = alto / 2;
        var r = Math.Min(ancho, alto) * 0.44;
        var lapiz = new SolidColorBrush(Color.FromArgb(0x22, 0xE5, 0x39, 0x35));
        _canvas.Children.Add(new Ellipse { Width = r * 2, Height = r * 2, Stroke = lapiz, StrokeThickness = 4, IsHitTestVisible = false, Margin = new Thickness(cx - r, cy - r, 0, 0) });
        var puntas = Enumerable.Range(0, 5)
            .Select(i => new Point(cx + r * Math.Sin(i * 4 * Math.PI / 5), cy - r * Math.Cos(i * 4 * Math.PI / 5)))
            .ToList();
        var estrella = new PathFigure { StartPoint = puntas[0], IsClosed = true };
        foreach (var p in puntas.Skip(1)) estrella.Segments.Add(new LineSegment(p, true));
        _canvas.Children.Add(new Path { Data = new PathGeometry([estrella]), Stroke = lapiz, StrokeThickness = 4, IsHitTestVisible = false });
    }

    private void DibujarManchas(string areaId, IReadOnlyList<Celda> celdas, Func<double, double, Point> P)
    {
        var azar = new Random(HashEstable(areaId));
        var cantidad = 1 + azar.Next(3);
        for (var i = 0; i < cantidad; i++)
        {
            var c = celdas[azar.Next(celdas.Count)];
            var centro = P(c.X + 0.5, c.Y + 0.5);
            var tam = Celda * (0.6 + azar.NextDouble() * 0.9);
            var mancha = new Ellipse
            {
                Width = tam,
                Height = tam * (0.5 + azar.NextDouble() * 0.5),
                Fill = Sangre,
                Opacity = 0.45 + azar.NextDouble() * 0.3,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(azar.Next(180)),
            };
            Canvas.SetLeft(mancha, centro.X - mancha.Width / 2);
            Canvas.SetTop(mancha, centro.Y - mancha.Height / 2);
            _canvas.Children.Add(mancha);

            // Salpicaduras alrededor.
            for (var j = 0; j < 4; j++)
            {
                var gota = new Ellipse { Width = 3 + azar.Next(5), Height = 3 + azar.Next(5), Fill = Sangre, Opacity = mancha.Opacity, IsHitTestVisible = false };
                Canvas.SetLeft(gota, centro.X + (azar.NextDouble() - 0.5) * tam * 1.4);
                Canvas.SetTop(gota, centro.Y + (azar.NextDouble() - 0.5) * tam * 1.4);
                _canvas.Children.Add(gota);
            }
        }
    }

    private void DibujarPuerta(Puerta puerta, Rect r, MotorJuego motor, Func<double, double, Point> P)
    {
        var abierta = motor.EstaPuertaAbierta(puerta.Id);
        var color = ((SolidColorBrush)Colores.DePuerta(puerta.Tipo)).Color;
        var rect = new Rect(P(r.Left, r.Top), P(r.Right, r.Bottom));
        var horizontal = rect.Width >= rect.Height;
        var elementos = new List<FrameworkElement>();

        if (puerta.Tipo == TipoPuerta.Teleportador)
        {
            elementos.Add(new Ellipse
            {
                Width = rect.Width,
                Height = rect.Height,
                Fill = new RadialGradientBrush(Colors.White, color) { GradientOrigin = new Point(0.4, 0.35), Opacity = abierta ? 1 : 0.55 },
                Stroke = new SolidColorBrush(Color.FromRgb(0x10, 0x30, 0x34)),
                StrokeThickness = 2,
                Effect = abierta ? new DropShadowEffect { Color = color, BlurRadius = 22, ShadowDepth = 0, Opacity = 1 } : null,
            });
            Ubicar(elementos[0], rect.TopLeft);
        }
        else if (puerta.Tipo == TipoPuerta.Paso)
        {
            // Un paso abierto no tiene ficha: solo se ve la abertura. Cerrado, escombros.
            if (!abierta)
            {
                elementos.Add(new Rectangle
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Fill = Rayas(Color.FromRgb(0x6D, 0x4C, 0x41), Color.FromRgb(0x3E, 0x27, 0x23)),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5,
                    StrokeDashArray = [3, 2],
                    RadiusX = 3,
                    RadiusY = 3,
                });
                Ubicar(elementos[0], rect.TopLeft);
            }
            else
            {
                var hueco = new Rectangle { Width = rect.Width, Height = rect.Height, Fill = Brushes.Transparent };
                Ubicar(hueco, rect.TopLeft);
                elementos.Add(hueco);
            }
        }
        else
        {
            // Ficha de puerta: cuerpo + soportes del color de la llave en los extremos.
            var largoSoporte = (horizontal ? rect.Width : rect.Height) * 0.14;
            var soportes = horizontal
                ? new[] { new Rect(rect.Left, rect.Top, largoSoporte, rect.Height), new Rect(rect.Right - largoSoporte, rect.Top, largoSoporte, rect.Height) }
                : [new Rect(rect.Left, rect.Top, rect.Width, largoSoporte), new Rect(rect.Left, rect.Bottom - largoSoporte, rect.Width, largoSoporte)];

            if (!abierta)
            {
                var cuerpo = horizontal
                    ? new Rect(rect.Left + largoSoporte * 0.6, rect.Top + rect.Height * 0.12, rect.Width - largoSoporte * 1.2, rect.Height * 0.76)
                    : new Rect(rect.Left + rect.Width * 0.12, rect.Top + largoSoporte * 0.6, rect.Width * 0.76, rect.Height - largoSoporte * 1.2);
                Brush relleno = puerta.Tipo switch
                {
                    TipoPuerta.Normal => new LinearGradientBrush(Color.FromRgb(0xC9, 0xCD, 0xD2), Color.FromRgb(0x7E, 0x84, 0x8A), horizontal ? 90 : 0),
                    _ => Rayas(color, Color.FromRgb(0x14, 0x14, 0x14)),
                };
                var hoja = new Rectangle { Width = cuerpo.Width, Height = cuerpo.Height, Fill = relleno, Stroke = Brushes.Black, StrokeThickness = 1.5 };
                Ubicar(hoja, cuerpo.TopLeft);
                elementos.Add(hoja);

                // Junta central de la puerta.
                var junta = horizontal
                    ? new Line { X1 = cuerpo.Left + cuerpo.Width / 2, Y1 = cuerpo.Top, X2 = cuerpo.Left + cuerpo.Width / 2, Y2 = cuerpo.Bottom }
                    : new Line { X1 = cuerpo.Left, Y1 = cuerpo.Top + cuerpo.Height / 2, X2 = cuerpo.Right, Y2 = cuerpo.Top + cuerpo.Height / 2 };
                junta.Stroke = Brushes.Black;
                junta.StrokeThickness = 2;
                elementos.Add(junta);
            }
            else
            {
                // Abierta: la ficha se corre y quedan los soportes; un area transparente recibe el clic.
                var hueco = new Rectangle { Width = rect.Width, Height = rect.Height, Fill = Brushes.Transparent };
                Ubicar(hueco, rect.TopLeft);
                elementos.Add(hueco);
            }

            foreach (var s in soportes)
            {
                var soporte = new Rectangle
                {
                    Width = s.Width,
                    Height = s.Height,
                    Fill = new LinearGradientBrush(Mezclar(color, Colors.White, 0.35), Mezclar(color, Colors.Black, 0.35), 45),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5,
                    RadiusX = 2,
                    RadiusY = 2,
                };
                Ubicar(soporte, s.TopLeft);
                elementos.Add(soporte);
            }
        }

        var tooltip = $"{MotorJuego.NombrePuerta(puerta)} ({(abierta ? "abierta" : "cerrada")})" + (abierta ? "" : "\nClic para intentar abrirla");
        foreach (var e in elementos)
        {
            e.Cursor = Cursors.Hand;
            e.ToolTip = tooltip;
            var id = puerta.Id;
            e.MouseLeftButtonDown += (_, ev) => { PuertaClick?.Invoke(id); ev.Handled = true; };
            _canvas.Children.Add(e);
        }
    }

    private void DibujarCartel(Area area, List<PiezaVisible> piezas, MotorJuego motor, Func<double, double, Point> P)
    {
        var conocida = motor.AreaConocida(area.Id);
        var despejada = motor.Estado.AreasDespejadas.Contains(area.Id);
        var vivos = motor.MonstruosDe(area.Id).Count(m => !m.Muerto);

        var principal = piezas.OrderByDescending(p => p.Celdas.Count).First().Celdas;
        var centro = P((principal.Min(c => c.X) + principal.Max(c => c.X) + 1) / 2.0,
                       (principal.Min(c => c.Y) + principal.Max(c => c.Y) + 1) / 2.0);

        var contenido = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        contenido.Children.Add(new TextBlock
        {
            Text = conocida ? "? " + area.Nombre : area.Nombre,
            Foreground = conocida ? Colores.Conocida : Brushes.White,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 220,
        });
        if (!conocida)
        {
            var (texto, fondo) = vivos > 0 ? ($"☠ {vivos}", Colores.ConEnemigos) : despejada ? ("✔ despejada", Colores.Despejada) : ("", null);
            if (fondo is not null)
                contenido.Children.Add(new Border
                {
                    Background = fondo,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 0, 8, 1),
                    Margin = new Thickness(0, 3, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new TextBlock { Text = texto, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold },
                });
        }

        var cartel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x12, 0x12, 0x14)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x90, 0x00, 0x00, 0x00)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 4, 10, 6),
            Child = contenido,
            IsHitTestVisible = false,
        };
        cartel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Ubicar(cartel, new Point(centro.X - cartel.DesiredSize.Width / 2, centro.Y - cartel.DesiredSize.Height / 2));
        _canvas.Children.Add(cartel);
    }

    private static Dictionary<string, Rect> UbicarPuertas(List<Puerta> puertas, List<PiezaVisible> piezas, MotorJuego motor)
    {
        var resultado = new Dictionary<string, Rect>();
        var sinPosicionPorArea = new Dictionary<string, int>();
        foreach (var p in puertas)
        {
            if (p.Posicion is { } pos)
            {
                resultado[p.Id] = pos.Orientacion == Orientacion.Horizontal
                    ? new Rect(pos.X, pos.Y - 0.32, 2, 0.64)
                    : new Rect(pos.X - 0.32, pos.Y, 0.64, 2);
                continue;
            }
            // Sin posicion (p.ej. teleportadores): ficha dentro del area revelada de ese lado.
            var lado = motor.AreaRevelada(p.Desde) ? p.Desde : p.Hacia;
            var primera = piezas.FirstOrDefault(x => x.Area.Id == lado)?.Celdas.MinBy(c => (c.Y, c.X)) ?? new Celda(0, 0);
            var n = sinPosicionPorArea.GetValueOrDefault(lado);
            sinPosicionPorArea[lado] = n + 1;
            resultado[p.Id] = new Rect(primera.X + 0.15 + n * 1.1, primera.Y + 0.15, 0.85, 0.85);
        }
        return resultado;
    }

    private static void Ubicar(UIElement e, Point p)
    {
        Canvas.SetLeft(e, p.X);
        Canvas.SetTop(e, p.Y);
    }

    private static int HashEstable(string s)
    {
        unchecked
        {
            var h = (int)2166136261;
            foreach (var c in s) h = (h ^ c) * 16777619;
            return h;
        }
    }

    private static Color Mezclar(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    // ------------------------------------------------------------------ Texturas

    private static readonly Brush Fondo = Congelar(new SolidColorBrush(Color.FromRgb(0x0C, 0x0B, 0x0B)));
    private static readonly Brush BordeSeleccion = Congelar(new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)));
    private static readonly Brush ParedOscura = Congelar(new SolidColorBrush(Color.FromRgb(0x1A, 0x1B, 0x1E)));
    private static readonly Brush ParedClara = Congelar(new SolidColorBrush(Color.FromRgb(0xB8, 0xBE, 0xC6)));
    private static readonly Brush Sangre = Congelar(new RadialGradientBrush(Color.FromRgb(0x5A, 0x08, 0x08), Color.FromArgb(0x00, 0x7A, 0x0E, 0x0E)));

    private static readonly Brush FondoVineta = Congelar(new RadialGradientBrush(Color.FromRgb(0x24, 0x10, 0x10), Color.FromRgb(0x0C, 0x0B, 0x0B))
    {
        RadiusX = 0.5,
        RadiusY = 0.5,
    });

    /// <summary>Placa metalica clara con remaches: interior de las salas.</summary>
    private static readonly Brush PlacaClara = CrearPlaca(
        Color.FromRgb(0xA7, 0xAD, 0xB3), Color.FromRgb(0x86, 0x8C, 0x93), estrias: true, rejilla: false, rayas: false);

    /// <summary>Placa industrial oscura: bordes de las salas y pasillos.</summary>
    private static readonly Brush PlacaBorde = CrearPlaca(
        Color.FromRgb(0x7C, 0x78, 0x70), Color.FromRgb(0x5C, 0x58, 0x51), estrias: false, rejilla: true, rayas: false);

    /// <summary>Casillas de conexion: chapa gastada con marcas diagonales.</summary>
    private static readonly Brush PlacaConexion = CrearPlaca(
        Color.FromRgb(0x84, 0x6E, 0x55), Color.FromRgb(0x5E, 0x4C, 0x3A), estrias: false, rejilla: false, rayas: true);

    private static Brush CrearPlaca(Color claro, Color oscuro, bool estrias, bool rejilla, bool rayas)
    {
        const double c = Celda;
        var grupo = new DrawingGroup();
        using (var dc = grupo.Open())
        {
            dc.DrawRectangle(new LinearGradientBrush(claro, oscuro, 45), null, new Rect(0, 0, c, c));

            if (estrias)
            {
                var estria = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x00, 0x00)), 1);
                for (var y = 10.0; y < c - 6; y += 6) dc.DrawLine(estria, new Point(9, y), new Point(c - 9, y));
            }
            if (rejilla)
            {
                var hueco = new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0x00, 0x00));
                for (var y = 9.0; y < c - 8; y += 6)
                    for (var x = 9.0; x < c - 8; x += 6)
                        dc.DrawRectangle(hueco, null, new Rect(x, y, 3, 3));
            }
            if (rayas)
            {
                var raya = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x20, 0x10, 0x00)), 3);
                for (var d = -c; d < c; d += 10) dc.DrawLine(raya, new Point(d, c), new Point(d + c, 0));
            }

            // Bisel: luz arriba/izquierda, sombra abajo/derecha.
            var luz = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)), 2);
            var sombra = new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00)), 2);
            dc.DrawLine(luz, new Point(1, 1), new Point(c - 1, 1));
            dc.DrawLine(luz, new Point(1, 1), new Point(1, c - 1));
            dc.DrawLine(sombra, new Point(c - 1, 1), new Point(c - 1, c - 1));
            dc.DrawLine(sombra, new Point(1, c - 1), new Point(c - 1, c - 1));

            // Remaches.
            var remache = new SolidColorBrush(Color.FromRgb(0x3E, 0x42, 0x47));
            var brillo = new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF));
            foreach (var (x, y) in new[] { (5.0, 5.0), (c - 5, 5.0), (5.0, c - 5), (c - 5, c - 5) })
            {
                dc.DrawEllipse(remache, null, new Point(x, y), 1.8, 1.8);
                dc.DrawEllipse(brillo, null, new Point(x - 0.6, y - 0.6), 0.6, 0.6);
            }
        }
        // Recorte exacto a la casilla para que el mosaico no se corra.
        grupo.ClipGeometry = new RectangleGeometry(new Rect(0, 0, c, c));
        var brush = new DrawingBrush(grupo)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, c, c),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, c, c),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>Franjas diagonales de peligro de dos colores.</summary>
    private static Brush Rayas(Color a, Color b)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(9, 9),
            SpreadMethod = GradientSpreadMethod.Repeat,
            GradientStops =
            {
                new GradientStop(a, 0), new GradientStop(a, 0.5),
                new GradientStop(b, 0.5), new GradientStop(b, 1),
            },
        };
        brush.Freeze();
        return brush;
    }

    private static Brush Congelar(Brush b)
    {
        b.Freeze();
        return b;
    }
}
