using Velopack;

namespace DoomCompanion.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack tiene que correr antes que nada: maneja los ganchos de instalacion/desinstalacion.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
