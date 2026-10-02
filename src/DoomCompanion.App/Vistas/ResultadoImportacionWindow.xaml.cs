using System.Windows;
using System.Windows.Media;
using DoomCompanion.Core.Validacion;

namespace DoomCompanion.App.Vistas;

/// <summary>
/// Resultado de importar un mapa. Los errores de formato se muestran directo (hacen falta para
/// corregir el archivo); los de logica y las advertencias quedan detras de "Ver detalle (spoiler)".
/// </summary>
public partial class ResultadoImportacionWindow : Window
{
    private readonly ResultadoImportacion _resultado;

    public ResultadoImportacionWindow(ResultadoImportacion resultado, bool soloConsulta)
    {
        InitializeComponent();
        _resultado = resultado;
        Resumen.Text = resultado.Resumen;

        if (resultado.TieneErroresDeFormato)
        {
            Resumen.Foreground = Brushes.OrangeRed;
            Explicacion.Text = "El archivo no se puede jugar. Corregilo o pegale estos errores a la IA para que lo corrija.";
            BotonJugar.Visibility = Visibility.Collapsed;
            BotonDetalle.Visibility = Visibility.Collapsed;
            BotonCerrar.Content = "Cerrar";
            MostrarDetalle();
            return;
        }

        if (resultado.TieneProblemaLogico)
        {
            Resumen.Foreground = Brushes.Orange;
            Explicacion.Text = "Puede que el mapa no se pueda terminar. El detalle revela partes del mapa: miralo solo si sos el invasor o no te importa.";
            BotonJugar.Content = "Jugar de todos modos";
            BotonJugar.Style = (Style)FindResource(typeof(System.Windows.Controls.Button));
        }
        else
        {
            Resumen.Foreground = (Brush)FindResource("Exito");
            Explicacion.Text = resultado.CantidadAdvertencias > 0
                ? "Se puede jugar. Las advertencias suelen ser de balance o de piezas físicas; el detalle puede revelar partes del mapa."
                : "Todas las áreas son alcanzables, la salida también, no hay softlocks y cada área da una recompensa.";
            BotonDetalle.Visibility = resultado.CantidadAdvertencias > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (soloConsulta)
        {
            BotonJugar.Visibility = Visibility.Collapsed;
            BotonCerrar.Content = "Cerrar";
        }
    }

    private void VerDetalle(object sender, RoutedEventArgs e)
    {
        var ok = MessageBox.Show(this,
            "El detalle puede revelar áreas, puertas, llaves y recompensas del mapa.\n\n¿Ver el detalle igual?",
            "Spoiler", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok == MessageBoxResult.Yes) MostrarDetalle();
    }

    private void MostrarDetalle()
    {
        Lista.ItemsSource = _resultado.Problemas
            .OrderBy(p => p.Severidad)
            .Select(p => new
            {
                Encabezado = $"{(p.Severidad == Severidad.Error ? "ERROR" : "ADVERTENCIA")} · {NombreCategoria(p.Categoria)}" +
                             (string.IsNullOrEmpty(p.Ruta) ? "" : $" · {p.Ruta}"),
                p.Mensaje,
                Color = p.Severidad == Severidad.Error ? Brushes.OrangeRed : Brushes.Gold,
            })
            .ToList();
        PanelDetalle.Visibility = Visibility.Visible;
        BotonDetalle.Visibility = Visibility.Collapsed;
        BotonCopiar.Visibility = Visibility.Visible;
    }

    private void Copiar(object sender, RoutedEventArgs e)
    {
        var texto = "El mapa tiene estos problemas, corregilos y devolvé solo el JSON corregido:\n" +
                    string.Join("\n", _resultado.Problemas.Select(p => "- " + p));
        Clipboard.SetText(texto);
        BotonCopiar.Content = "✔ Copiado";
    }

    private static string NombreCategoria(CategoriaProblema c) => c switch
    {
        CategoriaProblema.Esquema => "Formato",
        CategoriaProblema.Referencia => "Referencias",
        CategoriaProblema.Logica => "Lógica",
        _ => "Piezas y balance",
    };

    private void Jugar(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cerrar(object sender, RoutedEventArgs e) => DialogResult = false;
}
