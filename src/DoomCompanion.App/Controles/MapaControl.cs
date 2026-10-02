using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DoomCompanion.App.Servicios;
using DoomCompanion.App.ViewModels;
using DoomCompanion.Core.Modelo;
using DoomCompanion.Core.Motor;

namespace DoomCompanion.App.Controles;

/// <summary>
/// Plano del mapa con niebla de guerra, al estilo DoomGen: piezas, puertas y fichas con las
/// imagenes de la carpeta de imagenes (si estan) y cada ficha en su casilla. Si falta una
/// imagen, la pieza o ficha se dibuja por codigo. Solo muestra areas reveladas (y el contorno de
/// las conocidas) y las puertas visibles.
/// </summary>
public sealed class MapaControl : Border
{
    private const double Celda = 40;
    private const int MinAncho = 22;
    private const int MinAlto = 13;

    private readonly Canvas _canvas = new();
    private readonly Brush _pentagrama;

    public MapaControl()
    {
        ClipToBounds = true;
        var capas = new Grid();
        // El pentagrama va como pincel para que no agrande el control; se mide a mano porque no
        // forma parte de la ventana.
        var pentagrama = CrearPentagrama();
        pentagrama.Measure(new Size(pentagrama.Width, pentagrama.Height));
        pentagrama.Arrange(new Rect(0, 0, pentagrama.Width, pentagrama.Height));
        _pentagrama = new VisualBrush(pentagrama) { Stretch = Stretch.UniformToFill };
        capas.Children.Add(new Rectangle { Fill = _pentagrama, IsHitTestVisible = false });
        capas.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = _canvas, Margin = new Thickness(34) });
        foreach (var borde in CrearMarcoDeFuego()) capas.Children.Add(borde);
        Child = capas;
        ActualizarFondo();
    }

    public event Action<string>? AreaClick;
    public event Action<string>? PuertaClick;
    /// <summary>Clic en un encuentro o un cadaver: (area, id de ficha).</summary>
    public event Action<string, string>? FichaClick;

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

    private sealed record PiezaVisible(Area Area, ColocacionTile Tile, IReadOnlyList<Celda> Celdas, BitmapSource? Imagen);

    private void ActualizarFondo()
    {
        // El fondo de DoomGen (pentagrama y marco de fuego) tiene transparencia: va estirado sobre blanco.
        var capas = ((Grid)Child).Children;
        var fondo = ServicioImagenes.Cargar("largebackground.png");
        Background = fondo is null ? FondoClaro : Brushes.White;
        ((Rectangle)capas[0]).Fill = fondo is null ? _pentagrama : new ImageBrush(fondo) { Stretch = Stretch.Fill };
        for (var i = 2; i < capas.Count; i++)
            capas[i].Visibility = fondo is null ? Visibility.Visible : Visibility.Collapsed; // marco de fuego propio
    }

    private void Redibujar()
    {
        ActualizarFondo();
        _canvas.Children.Clear();
        var motor = Motor;
        if (motor is null) return;
        var catalogo = motor.Catalogo;

        var areasVisibles = motor.Mapa.Areas.Where(a => motor.AreaRevelada(a.Id) || motor.AreaConocida(a.Id)).ToList();
        var piezas = areasVisibles
            .SelectMany(a => a.Tiles.Select(t =>
            {
                var cat = catalogo.BuscarTile(t.Tipo);
                var imagen = cat?.TieneForma == true ? ServicioImagenes.Cargar(cat.Imagen) : null;
                return new PiezaVisible(a, t, GeometriaTiles.Celdas(t, catalogo), imagen);
            }))
            .ToList();
        var reveladas = piezas.Where(p => motor.AreaRevelada(p.Area.Id)).ToList();
        var conocidas = piezas.Where(p => motor.AreaConocida(p.Area.Id)).ToList();
        var porArea = reveladas.GroupBy(p => p.Area).ToList();
        var fichas = porArea.ToDictionary(g => g.Key.Id, g => DistribucionFichas.Calcular(motor.Mapa, catalogo, motor.Estado, g.Key));

        var puertas = motor.PuertasVisibles.ToList();
        var rectsPuertas = UbicarPuertas(puertas, piezas, fichas, motor);

        // Limites solo de lo visible: el tamaño del lienzo no puede delatar areas ocultas.
        // Se deja lugar arriba para los rotulos y el desplazamiento es entero para que las
        // texturas queden alineadas con las casillas.
        // Los rotulos van al costado de las areas: se ubican antes para que el lienzo los incluya.
        var ocupadas = piezas.SelectMany(p => p.Celdas).ToHashSet();
        var rotulos = new List<(TextBlock Texto, Rect Lugar)>();
        foreach (var grupo in piezas.GroupBy(p => p.Area))
            rotulos.Add(UbicarRotulo(grupo.Key, grupo.ToList(), motor, ocupadas, rotulos.Select(r => r.Lugar).ToList()));

        var todos = piezas.SelectMany(p => p.Celdas).Select(c => new Rect(c.X, c.Y, 1, 1))
            .Concat(rectsPuertas.Values).Concat(rotulos.Select(r => r.Lugar)).ToList();
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

        // 1. Halo del color de cada area (por fuera de las paredes).
        foreach (var grupo in porArea)
        {
            var color = Colores.DeArea(motor.Mapa, grupo.Key.Id);
            _canvas.Children.Add(new Path
            {
                Data = Union(grupo.SelectMany(p => p.Celdas).ToList(), P)!,
                Stroke = new SolidColorBrush(Mezclar(color, Colors.White, 0.35)),
                StrokeThickness = 20,
                StrokeLineJoin = PenLineJoin.Round,
                Effect = new DropShadowEffect { Color = color, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.9 },
                IsHitTestVisible = false,
            });
        }

        // 2. Piezas: imagen de DoomGen o piso dibujado.
        foreach (var pieza in reveladas)
        {
            if (pieza.Imagen is { } img)
            {
                var forma = catalogo.BuscarTile(pieza.Tile.Tipo)!.Forma!;
                AgregarImagen(img, pieza.Tile.X - 1, pieza.Tile.Y - 1, forma[0].Length + 2, forma.Count + 2, pieza.Tile.Rotacion, P);
                continue;
            }
            var conexion = GeometriaTiles.CeldasDeConexion(pieza.Tile, catalogo).ToHashSet();
            var celdas = pieza.Celdas.ToHashSet();
            bool EsBorde(Celda c) =>
                !celdas.Contains(c with { X = c.X - 1 }) || !celdas.Contains(c with { X = c.X + 1 }) ||
                !celdas.Contains(c with { Y = c.Y - 1 }) || !celdas.Contains(c with { Y = c.Y + 1 });

            Agregar(Union(pieza.Celdas.Where(c => !conexion.Contains(c) && !EsBorde(c)).ToList(), P), PlacaClara);
            Agregar(Union(pieza.Celdas.Where(c => !conexion.Contains(c) && EsBorde(c)).ToList(), P), PlacaBorde);
            Agregar(Union(conexion.Where(celdas.Contains).ToList(), P), PlacaConexion);
        }

        // 3. Tinte de cada area (la inicial queda gris, sin tinte).
        foreach (var grupo in porArea.Where(g => g.Key.Id != motor.Mapa.Escenario.AreaInicial))
        {
            var color = Colores.DeArea(motor.Mapa, grupo.Key.Id);
            Agregar(Union(grupo.SelectMany(p => p.Celdas).ToList(), P), new SolidColorBrush(Color.FromArgb(0x55, color.R, color.G, color.B)));
        }

        // 4-6. Solo para las piezas dibujadas: costuras, manchas y paredes.
        var dibujadas = reveladas.Where(p => p.Imagen is null).ToList();
        DibujarCosturas(dibujadas, catalogo, P);
        foreach (var grupo in dibujadas.GroupBy(p => p.Area))
            DibujarManchas(grupo.Key.Id, grupo.SelectMany(p => p.Celdas).ToList(), P);
        var paredes = new GeometryGroup();
        foreach (var pieza in dibujadas)
            foreach (var s in GeometriaTiles.Paredes(pieza.Tile, catalogo))
                paredes.Children.Add(new LineGeometry(P(s.X1, s.Y1), P(s.X2, s.Y2)));
        paredes.Freeze();
        _canvas.Children.Add(new Path { Data = paredes, Stroke = ParedClara, StrokeThickness = 11, StrokeStartLineCap = PenLineCap.Square, StrokeEndLineCap = PenLineCap.Square, IsHitTestVisible = false });
        _canvas.Children.Add(new Path { Data = paredes, Stroke = ParedOscura, StrokeThickness = 6, StrokeStartLineCap = PenLineCap.Square, StrokeEndLineCap = PenLineCap.Square, IsHitTestVisible = false });
        _canvas.Children.Add(new Path { Data = paredes, Stroke = ParedMedia, StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Square, StrokeEndLineCap = PenLineCap.Square, IsHitTestVisible = false });

        // 7. Areas conocidas: solo un contorno punteado.
        foreach (var pieza in conocidas)
            _canvas.Children.Add(new Path
            {
                Data = Union(pieza.Celdas, P),
                Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x60, 0x60, 0x70)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x55, 0x58, 0x66)),
                StrokeThickness = 2.5,
                StrokeDashArray = [4, 3],
                IsHitTestVisible = false,
            });

        // 8. Zona de clic y seleccion de cada pieza.
        foreach (var pieza in piezas)
        {
            var seleccionada = pieza.Area.Id == SeleccionId;
            var zona = new Path
            {
                Data = Union(pieza.Celdas, P),
                Fill = Brushes.Transparent,
                Stroke = seleccionada ? BordeSeleccion : null,
                StrokeThickness = 3,
                StrokeDashArray = seleccionada ? [3, 2] : null,
                Cursor = Cursors.Hand,
            };
            var id = pieza.Area.Id;
            zona.MouseLeftButtonDown += (_, e) => { AreaClick?.Invoke(id); e.Handled = true; };
            _canvas.Children.Add(zona);
        }

        // 9. Fichas (escenografia, objetos, marines y monstruos) en su casilla.
        foreach (var (areaId, lista) in fichas)
            foreach (var ficha in lista)
                DibujarFicha(areaId, ficha, motor, P);

        // 10. Puertas.
        foreach (var puerta in puertas)
            DibujarPuerta(puerta, rectsPuertas[puerta.Id], motor, P);

        // 11. Rotulos al costado de cada area, como en DoomGen.
        foreach (var (texto, lugar) in rotulos)
        {
            Ubicar(texto, P(lugar.Left, lugar.Top));
            _canvas.Children.Add(texto);
        }
    }

    /// <summary>
    /// Agrega una imagen cuyo tamaño sin rotar es <paramref name="anchoCeldas"/> × <paramref name="altoCeldas"/>,
    /// girada en sentido horario, con la esquina superior izquierda ya girada en (x, y).
    /// </summary>
    private Image AgregarImagen(BitmapSource img, double x, double y, double anchoCeldas, double altoCeldas, int rotacion, Func<double, double, Point> P, double opacidad = 1)
    {
        var imagen = new Image
        {
            Source = img,
            Width = anchoCeldas * Celda,
            Height = altoCeldas * Celda,
            Stretch = Stretch.Fill,
            Opacity = opacidad,
            IsHitTestVisible = false,
            LayoutTransform = rotacion % 360 == 0 ? Transform.Identity : new RotateTransform(rotacion),
        };
        RenderOptions.SetBitmapScalingMode(imagen, BitmapScalingMode.HighQuality);
        Ubicar(imagen, P(x, y));
        _canvas.Children.Add(imagen);
        return imagen;
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

    // ------------------------------------------------------------------ Fichas

    private static string Sufijo(ColorFigura c) => c switch
    {
        ColorFigura.Rojo => "red",
        ColorFigura.Verde => "green",
        _ => "blue",
    };

    private static Color ColorDe(ColorFigura c) => c switch
    {
        ColorFigura.Rojo => Color.FromRgb(0xE5, 0x39, 0x35),
        ColorFigura.Verde => Color.FromRgb(0x43, 0xA0, 0x47),
        _ => Color.FromRgb(0x1E, 0x88, 0xE5),
    };

    private void DibujarFicha(string areaId, FichaEnPlano ficha, MotorJuego motor, Func<double, double, Point> P)
    {
        var catalogo = motor.Catalogo;
        string archivo;
        string nombre;
        string sigla;
        Color fondo;
        var redonda = true;
        switch (ficha.Clase)
        {
            case ClaseFicha.Monstruo:
            {
                var cat = catalogo.BuscarMonstruo(ficha.Tipo);
                archivo = $"{cat?.Imagen ?? ficha.Tipo}_{Sufijo(ficha.Color!.Value)}.png";
                nombre = $"{cat?.Nombre ?? ficha.Tipo} ({MotorJuego.NombreColor(ficha.Color.Value)})";
                sigla = cat?.Sigla ?? ficha.Tipo[..Math.Min(3, ficha.Tipo.Length)].ToUpperInvariant();
                fondo = ColorDe(ficha.Color.Value);
                break;
            }
            case ClaseFicha.Marine:
                archivo = $"marine_{Sufijo(ficha.Color!.Value)}.png";
                nombre = $"Marine {MotorJuego.NombreColor(ficha.Color.Value)} (inicio)";
                sigla = "M";
                fondo = ColorDe(ficha.Color.Value);
                break;
            case ClaseFicha.Objeto:
            {
                var cat = catalogo.BuscarObjeto(ficha.Tipo);
                archivo = cat?.Imagen ?? catalogo.BuscarFicha("encuentro")?.Imagen ?? "";
                nombre = motor.NombreObjeto(ficha.Tipo);
                sigla = cat?.Sigla ?? "?";
                fondo = (cat?.Categoria ?? CategoriaObjeto.Mision) switch
                {
                    CategoriaObjeto.Arma => Color.FromRgb(0x2E, 0x7D, 0x32),
                    CategoriaObjeto.Municion => Color.FromRgb(0x43, 0xA0, 0x47),
                    CategoriaObjeto.Salud => Color.FromRgb(0xC6, 0x28, 0x28),
                    CategoriaObjeto.Armadura => Color.FromRgb(0xFB, 0x8C, 0x00),
                    CategoriaObjeto.Llave => ((SolidColorBrush)Colores.DePuerta(ficha.Tipo switch
                    {
                        "llave-roja" => TipoPuerta.Roja,
                        "llave-azul" => TipoPuerta.Azul,
                        _ => TipoPuerta.Amarilla,
                    })).Color,
                    CategoriaObjeto.Mision => Color.FromRgb(0xF9, 0xA8, 0x25),
                    _ => Color.FromRgb(0x00, 0x89, 0x7B),
                };
                redonda = false;
                break;
            }
            default:
            {
                var cat = catalogo.BuscarFicha(ficha.Tipo);
                archivo = cat?.Imagen ?? "";
                nombre = (cat?.Nombre ?? ficha.Tipo) + (ficha.Interactiva ? (ficha.Revisada ? "\n(ya revisado: clic para volver a leer)" : "\nClic para revisar") : "");
                sigla = ficha.Tipo == "encuentro" ? "?" : "";
                fondo = Color.FromRgb(0x30, 0x30, 0x34);
                redonda = false;
                break;
            }
        }

        FrameworkElement elemento;
        if (ServicioImagenes.Cargar(archivo) is { } img)
        {
            // Si la ficha esta girada respecto de la imagen (por ejemplo un demon vertical), se rota 90°.
            var (an, al) = ServicioImagenes.Casillas(img);
            var rotar = an != al && (an, al) == (ficha.Alto, ficha.Ancho);
            elemento = AgregarImagen(img, ficha.X, ficha.Y, rotar ? ficha.Alto : ficha.Ancho, rotar ? ficha.Ancho : ficha.Alto, rotar ? 90 : 0, P);
        }
        else
        {
            elemento = DibujarFichaPropia(ficha, sigla, fondo, redonda, P);
        }

        // El plano es de referencia: solo los encuentros y cadaveres se tocan (para leerlos).
        elemento.IsHitTestVisible = true;
        elemento.ToolTip = nombre;
        if (ficha.Interactiva)
        {
            if (ficha.Revisada) elemento.Opacity = 0.45;
            else elemento.Effect = new DropShadowEffect { Color = Color.FromRgb(0xFF, 0xD5, 0x00), BlurRadius = 14, ShadowDepth = 0, Opacity = 1 };
            elemento.Cursor = Cursors.Hand;
            elemento.MouseLeftButtonDown += (_, e) =>
            {
                FichaClick?.Invoke(areaId, ficha.Id);
                e.Handled = true;
            };
        }
    }

    /// <summary>Ficha dibujada cuando no hay imagen: redonda (figuras) u octogonal (objetos).</summary>
    private FrameworkElement DibujarFichaPropia(FichaEnPlano ficha, string sigla, Color color, bool redonda, Func<double, double, Point> P)
    {
        var ancho = ficha.Ancho * Celda - 6;
        var alto = ficha.Alto * Celda - 6;
        var grupo = new Grid { Width = ancho, Height = alto };

        if (ficha.Clase == ClaseFicha.Escenografia)
        {
            grupo.Children.Add(new Rectangle
            {
                Fill = ficha.Tipo.StartsWith("teleportador") ? new RadialGradientBrush(Colors.White, Color.FromRgb(0x26, 0xC6, 0xDA))
                     : ficha.Tipo == "residuos" ? new SolidColorBrush(Color.FromRgb(0x7C, 0xB3, 0x42))
                     : ficha.Tipo == "barril" ? new SolidColorBrush(Color.FromRgb(0xBF, 0x36, 0x0C))
                     : Rayas(Color.FromRgb(0x55, 0x58, 0x5E), Color.FromRgb(0x22, 0x23, 0x26)),
                Stroke = Brushes.Black,
                StrokeThickness = 3,
                RadiusX = 3,
                RadiusY = 3,
            });
        }
        else
        {
            Shape forma = redonda
                ? new Ellipse()
                : new Polygon
                {
                    Points = [new(0.3, 0), new(0.7, 0), new(1, 0.3), new(1, 0.7), new(0.7, 1), new(0.3, 1), new(0, 0.7), new(0, 0.3)],
                    Stretch = Stretch.Fill,
                };
            var oscura = ficha.Clase == ClaseFicha.Monstruo;
            forma.Fill = oscura
                ? new RadialGradientBrush(Color.FromRgb(0x4A, 0x10, 0x10), Color.FromRgb(0x14, 0x04, 0x04))
                : new RadialGradientBrush(Mezclar(color, Colors.White, 0.35), color);
            forma.Stroke = oscura ? new SolidColorBrush(color) : Brushes.Black;
            forma.StrokeThickness = oscura ? 5 : 2;
            grupo.Children.Add(forma);
            grupo.Children.Add(new TextBlock
            {
                Text = sigla,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Black,
                FontSize = Math.Min(ancho, alto) * (sigla.Length > 2 ? 0.34 : 0.45),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 1 },
            });
        }
        Ubicar(grupo, new Point(P(ficha.X, ficha.Y).X + 3, P(ficha.X, ficha.Y).Y + 3));
        _canvas.Children.Add(grupo);
        return grupo;
    }

    // ------------------------------------------------------------------ Puertas

    private void DibujarPuerta(Puerta puerta, Rect r, MotorJuego motor, Func<double, double, Point> P)
    {
        var abierta = motor.EstaPuertaAbierta(puerta.Id);
        var color = ((SolidColorBrush)Colores.DePuerta(puerta.Tipo)).Color;
        var rect = new Rect(P(r.Left, r.Top), P(r.Right, r.Bottom));
        var horizontal = rect.Width >= rect.Height;
        var elementos = new List<FrameworkElement>();

        // Zona de clic generosa alrededor de la puerta.
        var zona = new Rectangle { Width = rect.Width, Height = rect.Height, Fill = Brushes.Transparent };
        Ubicar(zona, rect.TopLeft);
        elementos.Add(zona);

        var imagenPuerta = puerta.Tipo switch
        {
            TipoPuerta.Normal or TipoPuerta.Evento => motor.Catalogo.Puertas.FirstOrDefault(p => p.Tipo == TipoPuerta.Normal)?.Imagen,
            TipoPuerta.Roja or TipoPuerta.Azul or TipoPuerta.Amarilla => motor.Catalogo.Puertas.FirstOrDefault(p => p.Tipo == puerta.Tipo)?.Imagen,
            _ => null,
        };

        if (puerta.Tipo == TipoPuerta.Teleportador)
        {
            // La ficha de teleportador ya esta en el plano: aca solo el brillo cuando esta activo.
            var disco = new Ellipse
            {
                Width = rect.Width,
                Height = rect.Height,
                Fill = abierta ? Brushes.Transparent : new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 3,
                Effect = abierta ? new DropShadowEffect { Color = color, BlurRadius = 22, ShadowDepth = 0, Opacity = 1 } : null,
            };
            Ubicar(disco, rect.TopLeft);
            elementos.Add(disco);
        }
        else if (puerta.Posicion is { } pos && ServicioImagenes.Cargar(imagenPuerta) is { } img)
        {
            // Imagen de DoomGen: 2×4 casillas, vertical, centrada en la linea de la puerta.
            var x = pos.Orientacion == Orientacion.Vertical ? pos.X - 1 : pos.X - 1;
            var y = pos.Y - 1;
            var imagen = AgregarImagen(img, x, y, 2, 4, pos.Orientacion == Orientacion.Horizontal ? 90 : 0, P, abierta ? 0.3 : 1);
            if (puerta.Tipo == TipoPuerta.Evento && !abierta)
                imagen.Effect = new DropShadowEffect { Color = color, BlurRadius = 14, ShadowDepth = 0, Opacity = 1 };
        }
        else
        {
            // Barra fina sobre la linea de la puerta, con soportes del color de la llave.
            var grosor = Celda * 0.2;
            var barra = horizontal
                ? new Rect(rect.Left, rect.Top + rect.Height / 2 - grosor / 2, rect.Width, grosor)
                : new Rect(rect.Left + rect.Width / 2 - grosor / 2, rect.Top, grosor, rect.Height);
            if (!abierta)
            {
                Brush relleno = puerta.Tipo switch
                {
                    TipoPuerta.Normal => Rayas(Color.FromRgb(0xF2, 0xF2, 0xF2), Color.FromRgb(0x22, 0x22, 0x22)),
                    TipoPuerta.Azul => new SolidColorBrush(color),
                    TipoPuerta.Paso => Rayas(Color.FromRgb(0x8D, 0x6E, 0x63), Color.FromRgb(0x3E, 0x27, 0x23)),
                    _ => Rayas(color, Color.FromRgb(0x14, 0x14, 0x14)),
                };
                var hoja = new Rectangle { Width = barra.Width, Height = barra.Height, Fill = relleno, Stroke = Brushes.Black, StrokeThickness = 1 };
                if (puerta.Tipo == TipoPuerta.Paso) hoja.StrokeDashArray = [2, 2];
                Ubicar(hoja, barra.TopLeft);
                elementos.Add(hoja);
            }
            if (puerta.Tipo != TipoPuerta.Paso)
            {
                var lado = Celda * 0.3;
                var centros = horizontal
                    ? new[] { new Point(rect.Left, rect.Top + rect.Height / 2), new Point(rect.Right, rect.Top + rect.Height / 2) }
                    : [new Point(rect.Left + rect.Width / 2, rect.Top), new Point(rect.Left + rect.Width / 2, rect.Bottom)];
                var colorSoporte = puerta.Tipo == TipoPuerta.Normal ? Colors.White : color;
                foreach (var c in centros)
                {
                    var soporte = new Rectangle { Width = lado, Height = lado, Fill = new SolidColorBrush(colorSoporte), Stroke = Brushes.Black, StrokeThickness = 1.2 };
                    Ubicar(soporte, new Point(c.X - lado / 2, c.Y - lado / 2));
                    elementos.Add(soporte);
                }
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

    private static Dictionary<string, Rect> UbicarPuertas(List<Puerta> puertas, List<PiezaVisible> piezas,
        Dictionary<string, IReadOnlyList<FichaEnPlano>> fichas, MotorJuego motor)
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
            // Sin posicion (teleportadores): sobre la ficha de teleportador del area revelada.
            var lado = motor.AreaRevelada(p.Desde) ? p.Desde : p.Hacia;
            var n = sinPosicionPorArea.GetValueOrDefault(lado);
            sinPosicionPorArea[lado] = n + 1;
            var teleportadores = (fichas.GetValueOrDefault(lado) ?? []).Where(f => f.Tipo.StartsWith("teleportador")).ToList();
            if (teleportadores.Count > n)
            {
                resultado[p.Id] = new Rect(teleportadores[n].X + 0.05, teleportadores[n].Y + 0.05, 0.9, 0.9);
                continue;
            }
            var primera = piezas.FirstOrDefault(x => x.Area.Id == lado)?.Celdas.MinBy(c => (c.Y, c.X)) ?? new Celda(0, 0);
            resultado[p.Id] = new Rect(primera.X + 0.15 + n * 1.1, primera.Y + 0.15, 0.85, 0.85);
        }
        return resultado;
    }

    // ------------------------------------------------------------------ Detalles de las piezas dibujadas

    private void DibujarCosturas(List<PiezaVisible> piezas, Catalogo catalogo, Func<double, double, Point> P)
    {
        var lineas = piezas
            .SelectMany(p => GeometriaTiles.Conexiones(p.Tile, catalogo).Select(c => (c.Linea, p.Area.Id)))
            .GroupBy(x => x.Linea)
            .Where(g => g.Count() == 2 && g.Select(x => x.Id).Distinct().Count() == 1)
            .Select(g => g.Key);

        var costura = new PathGeometry();
        foreach (var l in lineas)
        {
            var horizontal = l.Orientacion == Orientacion.Horizontal;
            Point Q(double a, double b) => horizontal ? P(l.X + a, l.Y + b) : P(l.X + b, l.Y + a);
            var figura = new PathFigure { StartPoint = Q(0, 0) };
            figura.Segments.Add(new LineSegment(Q(0.75, 0), true));
            figura.Segments.Add(new BezierSegment(Q(0.7, 0.35), Q(1.3, 0.35), Q(1.25, 0), true));
            figura.Segments.Add(new LineSegment(Q(2, 0), true));
            costura.Figures.Add(figura);
        }
        costura.Freeze();
        _canvas.Children.Add(new Path { Data = costura, Stroke = new SolidColorBrush(Color.FromArgb(0x90, 0x2A, 0x2C, 0x30)), StrokeThickness = 2, IsHitTestVisible = false });
    }

    private void DibujarManchas(string areaId, IReadOnlyList<Celda> celdas, Func<double, double, Point> P)
    {
        var azar = new Random(HashEstable(areaId));
        var cantidad = 1 + azar.Next(3);
        for (var i = 0; i < cantidad; i++)
        {
            var c = celdas[azar.Next(celdas.Count)];
            var centro = P(c.X + 0.5, c.Y + 0.5);
            var tam = Celda * (0.8 + azar.NextDouble() * 1.2);
            var mancha = new Ellipse
            {
                Width = tam,
                Height = tam * (0.5 + azar.NextDouble() * 0.5),
                Fill = Sangre,
                Opacity = 0.4 + azar.NextDouble() * 0.3,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(azar.Next(180)),
            };
            Canvas.SetLeft(mancha, centro.X - mancha.Width / 2);
            Canvas.SetTop(mancha, centro.Y - mancha.Height / 2);
            _canvas.Children.Add(mancha);
            for (var j = 0; j < 7; j++)
            {
                var gota = new Ellipse { Width = 2 + azar.Next(6), Height = 2 + azar.Next(6), Fill = Sangre, Opacity = mancha.Opacity, IsHitTestVisible = false };
                Canvas.SetLeft(gota, centro.X + (azar.NextDouble() - 0.5) * tam * 1.6);
                Canvas.SetTop(gota, centro.Y + (azar.NextDouble() - 0.5) * tam * 1.6);
                _canvas.Children.Add(gota);
            }
        }
    }

    /// <summary>
    /// Rotulo del area en su color, al costado del area (izquierda, derecha, arriba o abajo, el
    /// primer lugar libre), como en DoomGen. Sin contadores: solo el nombre.
    /// </summary>
    private static (TextBlock Texto, Rect Lugar) UbicarRotulo(Area area, List<PiezaVisible> piezas, MotorJuego motor, HashSet<Celda> ocupadas, List<Rect> rotulos)
    {
        var conocida = motor.AreaConocida(area.Id);
        var inicial = area.Id == motor.Mapa.Escenario.AreaInicial;
        var color = conocida ? Color.FromRgb(0x77, 0x7A, 0x88) : Colores.DeArea(motor.Mapa, area.Id);
        if (inicial) color = Color.FromRgb(0x55, 0x58, 0x60);

        var texto = new TextBlock
        {
            Text = (conocida ? "? " : "") + area.Nombre.ToUpperInvariant() + (inicial ? "\nSTART" : ""),
            Foreground = new SolidColorBrush(color),
            FontSize = 22,
            FontWeight = FontWeights.Black,
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 5, ShadowDepth = 0, Opacity = 1 },
        };
        texto.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var ancho = texto.DesiredSize.Width / Celda;
        var alto = texto.DesiredSize.Height / Celda;

        var celdas = piezas.SelectMany(p => p.Celdas).ToList();
        var izq = celdas.Min(c => c.X);
        var der = celdas.Max(c => c.X) + 1;
        var arriba = celdas.Min(c => c.Y);
        var abajo = celdas.Max(c => c.Y) + 1;
        var medioY = (arriba + abajo) / 2.0 - alto / 2;
        var medioX = (izq + der) / 2.0 - ancho / 2;
        const double separacion = 0.6;

        var candidatos = new[]
        {
            new Rect(izq - separacion - ancho, medioY, ancho, alto),   // izquierda
            new Rect(der + separacion, medioY, ancho, alto),           // derecha
            new Rect(medioX, arriba - separacion - alto, ancho, alto), // arriba
            new Rect(medioX, abajo + separacion, ancho, alto),         // abajo
            new Rect(izq - separacion - ancho, arriba, ancho, alto),   // arriba a la izquierda
            new Rect(der + separacion, arriba, ancho, alto),           // arriba a la derecha
        };
        bool Libre(Rect r)
        {
            for (var y = (int)Math.Floor(r.Top); y < Math.Ceiling(r.Bottom); y++)
                for (var x = (int)Math.Floor(r.Left); x < Math.Ceiling(r.Right); x++)
                    if (ocupadas.Contains(new Celda(x, y))) return false;
            return !rotulos.Any(o => o.IntersectsWith(r));
        }
        var lugar = candidatos.FirstOrDefault(Libre);
        if (lugar == default) lugar = candidatos[0];
        return (texto, lugar);
    }

    // ------------------------------------------------------------------ Auxiliares

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

    /// <summary>Pentagrama blanco con circulos y rayones (cuando no hay imagen de fondo).</summary>
    private static Canvas CrearPentagrama()
    {
        const double t = 1000;
        var lienzo = new Canvas { Width = t, Height = t };
        var trazo = new SolidColorBrush(Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF));
        var sombra = new DropShadowEffect { Color = Color.FromRgb(0xA8, 0xAC, 0xC4), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8 };
        foreach (var radio in new[] { 470.0, 430.0 })
        {
            var circulo = new Ellipse { Width = radio * 2, Height = radio * 2, Stroke = trazo, StrokeThickness = 6, Effect = sombra };
            Canvas.SetLeft(circulo, t / 2 - radio);
            Canvas.SetTop(circulo, t / 2 - radio);
            lienzo.Children.Add(circulo);
        }
        var puntas = Enumerable.Range(0, 5)
            .Select(i => new Point(t / 2 + 430 * Math.Sin(i * 4 * Math.PI / 5), t / 2 - 430 * Math.Cos(i * 4 * Math.PI / 5)))
            .ToList();
        var estrella = new PathFigure { StartPoint = puntas[0], IsClosed = true };
        foreach (var p in puntas.Skip(1)) estrella.Segments.Add(new LineSegment(p, true));
        lienzo.Children.Add(new Path { Data = new PathGeometry([estrella]), Stroke = trazo, StrokeThickness = 8, Effect = sombra });
        var azar = new Random(666);
        var rayon = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));
        for (var i = 0; i < 90; i++)
        {
            var x = azar.NextDouble() * t;
            var y = azar.NextDouble() * t;
            var ang = azar.NextDouble() * Math.PI;
            var largo = 60 + azar.NextDouble() * 220;
            lienzo.Children.Add(new Line { X1 = x, Y1 = y, X2 = x + Math.Cos(ang) * largo, Y2 = y + Math.Sin(ang) * largo, Stroke = rayon, StrokeThickness = 1 + azar.NextDouble() * 1.5 });
        }
        lienzo.Background = FondoClaro;
        return lienzo;
    }

    /// <summary>Marco de fuego: negro en el borde, rojo y naranja hacia adentro.</summary>
    private static IEnumerable<FrameworkElement> CrearMarcoDeFuego()
    {
        const double grosor = 34;
        GradientStopCollection Paradas() =>
        [
            new GradientStop(Color.FromRgb(0x0A, 0x02, 0x00), 0),
            new GradientStop(Color.FromRgb(0x5A, 0x0C, 0x02), 0.35),
            new GradientStop(Color.FromArgb(0xD0, 0xE0, 0x4A, 0x10), 0.7),
            new GradientStop(Color.FromArgb(0x00, 0xFF, 0x8A, 0x30), 1),
        ];
        yield return new Rectangle { Height = grosor, VerticalAlignment = VerticalAlignment.Top, Fill = new LinearGradientBrush(Paradas(), 90), IsHitTestVisible = false };
        yield return new Rectangle { Height = grosor, VerticalAlignment = VerticalAlignment.Bottom, Fill = new LinearGradientBrush(Paradas(), 270), IsHitTestVisible = false };
        yield return new Rectangle { Width = grosor, HorizontalAlignment = HorizontalAlignment.Left, Fill = new LinearGradientBrush(Paradas(), 0), IsHitTestVisible = false };
        yield return new Rectangle { Width = grosor, HorizontalAlignment = HorizontalAlignment.Right, Fill = new LinearGradientBrush(Paradas(), 180), IsHitTestVisible = false };
    }

    // ------------------------------------------------------------------ Texturas (piezas sin imagen)

    private static readonly Brush FondoClaro = Congelar(new RadialGradientBrush(Color.FromRgb(0xF2, 0xF2, 0xF8), Color.FromRgb(0xD9, 0xDB, 0xE8)));
    private static readonly Brush BordeSeleccion = Congelar(new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x00)));
    private static readonly Brush ParedClara = Congelar(new SolidColorBrush(Color.FromRgb(0xD8, 0xDC, 0xE6)));
    private static readonly Brush ParedOscura = Congelar(new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x31)));
    private static readonly Brush ParedMedia = Congelar(new SolidColorBrush(Color.FromRgb(0x8C, 0x91, 0x9A)));
    private static readonly Brush Sangre = Congelar(new RadialGradientBrush(Color.FromRgb(0x7A, 0x10, 0x10), Color.FromArgb(0x00, 0x9A, 0x20, 0x20)));

    private static readonly Brush PlacaClara = CrearPlaca(Color.FromRgb(0xC4, 0xC8, 0xCE), Color.FromRgb(0xA0, 0xA5, 0xAC), estrias: true, rejilla: false, recuadro: false);
    private static readonly Brush PlacaBorde = CrearPlaca(Color.FromRgb(0x9A, 0x9D, 0xA3), Color.FromRgb(0x7A, 0x7D, 0x83), estrias: false, rejilla: true, recuadro: false);
    private static readonly Brush PlacaConexion = CrearPlaca(Color.FromRgb(0xA9, 0xA6, 0xA0), Color.FromRgb(0x86, 0x83, 0x7D), estrias: false, rejilla: false, recuadro: true);

    private static Brush CrearPlaca(Color claro, Color oscuro, bool estrias, bool rejilla, bool recuadro)
    {
        const double c = Celda;
        var grupo = new DrawingGroup();
        using (var dc = grupo.Open())
        {
            dc.DrawRectangle(new LinearGradientBrush(claro, oscuro, 45), null, new Rect(0, 0, c, c));
            if (estrias)
            {
                var estria = new Pen(new SolidColorBrush(Color.FromArgb(0x28, 0x00, 0x00, 0x00)), 1);
                for (var y = 10.0; y < c - 6; y += 5) dc.DrawLine(estria, new Point(9, y), new Point(c - 9, y));
            }
            if (rejilla)
            {
                var hueco = new SolidColorBrush(Color.FromArgb(0x60, 0x20, 0x22, 0x26));
                for (var y = 8.0; y < c - 7; y += 5)
                    for (var x = 8.0; x < c - 7; x += 5)
                        dc.DrawRectangle(hueco, null, new Rect(x, y, 3, 3));
            }
            if (recuadro)
            {
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0x30, 0x2C, 0x26)), 2), new Rect(11, 11, c - 22, c - 22));
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)), 1), new Rect(13, 13, c - 26, c - 26));
            }
            var luz = new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)), 2);
            var sombra = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0x00, 0x00, 0x00)), 2);
            dc.DrawLine(luz, new Point(1, 1), new Point(c - 1, 1));
            dc.DrawLine(luz, new Point(1, 1), new Point(1, c - 1));
            dc.DrawLine(sombra, new Point(c - 1, 1), new Point(c - 1, c - 1));
            dc.DrawLine(sombra, new Point(1, c - 1), new Point(c - 1, c - 1));
            var remache = new SolidColorBrush(Color.FromRgb(0x4A, 0x4E, 0x54));
            var brillo = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
            foreach (var (x, y) in new[] { (5.0, 5.0), (c - 5, 5.0), (5.0, c - 5), (c - 5, c - 5) })
            {
                dc.DrawEllipse(remache, null, new Point(x, y), 1.7, 1.7);
                dc.DrawEllipse(brillo, null, new Point(x - 0.6, y - 0.6), 0.6, 0.6);
            }
        }
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

    private static Brush Rayas(Color a, Color b)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(6, 6),
            SpreadMethod = GradientSpreadMethod.Repeat,
            GradientStops = { new GradientStop(a, 0), new GradientStop(a, 0.5), new GradientStop(b, 0.5), new GradientStop(b, 1) },
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
