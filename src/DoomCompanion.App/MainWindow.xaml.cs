using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DoomCompanion.App.Servicios;
using DoomCompanion.App.ViewModels;
using DoomCompanion.App.Vistas;
using DoomCompanion.Core.Motor;
using DoomCompanion.Core.Validacion;
using Microsoft.Win32;

namespace DoomCompanion.App;

public partial class MainWindow : Window, IDialogos
{
    private readonly MainViewModel _vm;
    private WindowState _estadoPrevio;
    private bool _pantallaCompleta;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(this);
        DataContext = _vm;
        Mapa.AreaClick += id => _vm.SeleccionarAreaCommand.Execute(id);
        Mapa.PuertaClick += id => _vm.AbrirPuertaCommand.Execute(id);
        PreviewKeyDown += AlPresionarTecla;
    }

    // ------------------------------------------------------------------ Teclado y pantalla completa

    private void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            AlternarPantallaCompleta(this, e);
            e.Handled = true;
        }
        else if (_vm.LectorVisible && e.Key is Key.Enter or Key.Space or Key.Right)
        {
            _vm.SiguienteMensajeCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_vm.LectorVisible) _vm.CerrarLectorCommand.Execute(null);
            else if (_pantallaCompleta) AlternarPantallaCompleta(this, e);
            e.Handled = true;
        }
    }

    private void AlternarPantallaCompleta(object sender, RoutedEventArgs e)
    {
        if (!_pantallaCompleta)
        {
            _estadoPrevio = WindowState;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            // Pasar por Normal fuerza a Windows a recalcular el maximizado sin bordes ni barra de tareas.
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            BotonPantallaCompleta.Content = "🗗 Salir de pantalla completa";
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = _estadoPrevio;
            BotonPantallaCompleta.Content = "⛶ Pantalla completa";
        }
        _pantallaCompleta = !_pantallaCompleta;
    }

    // ------------------------------------------------------------------ Eventos de la vista

    private void AbrirMenuMas(object sender, RoutedEventArgs e)
    {
        MenuMas.PlacementTarget = (Button)sender;
        MenuMas.DataContext = _vm;
        MenuMas.IsOpen = true;
    }

    private void AbrirCarpetaDatos(object sender, RoutedEventArgs e) => ServicioDatos.AbrirCarpeta(ServicioDatos.Carpeta);

    private void HistorialDobleClic(object sender, MouseButtonEventArgs e)
    {
        if (ListaHistorial.SelectedItem is EntradaHistorial entrada)
            _vm.VerEntradaHistorialCommand.Execute(entrada);
    }

    private void FondoLectorClic(object sender, MouseButtonEventArgs e) => _vm.SiguienteMensajeCommand.Execute(null);

    // Los clics dentro de la tarjeta (por ejemplo, para hacer scroll) no avanzan el texto.
    private void TarjetaLectorClic(object sender, MouseButtonEventArgs e) => e.Handled = true;

    // ------------------------------------------------------------------ IDialogos

    public string? ElegirArchivoParaAbrir(string titulo, string filtro)
    {
        var dlg = new OpenFileDialog { Title = titulo, Filter = filtro, CheckFileExists = true };
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    }

    public string? ElegirArchivoParaGuardar(string titulo, string filtro, string nombreSugerido)
    {
        var dlg = new SaveFileDialog { Title = titulo, Filter = filtro, FileName = nombreSugerido, OverwritePrompt = true };
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    }

    public bool Confirmar(string titulo, string mensaje) =>
        MessageBox.Show(this, mensaje, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Avisar(string titulo, string mensaje, bool error = false) =>
        MessageBox.Show(this, mensaje, titulo, MessageBoxButton.OK, error ? MessageBoxImage.Warning : MessageBoxImage.Information);

    public bool MostrarResultadoImportacion(ResultadoImportacion resultado, bool soloConsulta = false)
    {
        var ventana = new ResultadoImportacionWindow(resultado, soloConsulta) { Owner = this };
        return ventana.ShowDialog() == true;
    }
}
