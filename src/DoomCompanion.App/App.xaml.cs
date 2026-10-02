using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using DoomCompanion.Core.Motor;

namespace DoomCompanion.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += AlFallar;
    }

    private static void AlFallar(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Ocurrió un error inesperado:\n\n{e.Exception.Message}\n\nLa partida se autoguarda después de cada acción.",
            "Doom Companion", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

public sealed class BoolInvertidoAVisibilidad : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NuloAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class CeroAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is 0 or null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TipoMensajeATexto : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        TipoMensaje.Briefing => "BRIEFING",
        TipoMensaje.Entrada => "NUEVA ÁREA",
        TipoMensaje.Despeje => "ÁREA DESPEJADA",
        TipoMensaje.Recompensa => "RECOMPENSA",
        TipoMensaje.PuertaAbierta => "PUERTA",
        TipoMensaje.PuertaBloqueada => "PUERTA BLOQUEADA",
        TipoMensaje.Evento => "EVENTO",
        TipoMensaje.Objetivo => "OBJETIVO",
        TipoMensaje.Victoria or TipoMensaje.Derrota => "FIN DE LA PARTIDA",
        _ => "",
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TextoVacioAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
