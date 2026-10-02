using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Validacion;

public enum Severidad { Error, Advertencia }

public enum CategoriaProblema
{
    /// <summary>El archivo no respeta el JSON Schema.</summary>
    Esquema,
    /// <summary>Ids repetidos o referencias a cosas que no existen.</summary>
    Referencia,
    /// <summary>Alcanzabilidad, softlocks, dependencias circulares, recompensas.</summary>
    Logica,
    /// <summary>Uso de piezas por encima de lo que hay en la caja.</summary>
    Inventario,
}

public sealed record Problema(Severidad Severidad, CategoriaProblema Categoria, string Ruta, string Mensaje)
{
    public override string ToString() =>
        string.IsNullOrEmpty(Ruta) ? Mensaje : $"{Ruta}: {Mensaje}";
}

/// <summary>Resultado de importar un archivo de mapa.</summary>
public sealed class ResultadoImportacion
{
    public Mapa? Mapa { get; init; }
    public string? JsonOriginal { get; init; }
    public IReadOnlyList<Problema> Problemas { get; init; } = [];

    /// <summary>Errores de esquema o referencias: el mapa no se puede jugar.</summary>
    public IEnumerable<Problema> ErroresDeFormato => Problemas.Where(p =>
        p.Severidad == Severidad.Error && p.Categoria is CategoriaProblema.Esquema or CategoriaProblema.Referencia);

    public bool TieneErroresDeFormato => ErroresDeFormato.Any();

    public bool TieneProblemaLogico => Problemas.Any(p => p.Severidad == Severidad.Error && p.Categoria == CategoriaProblema.Logica);

    public int CantidadAdvertencias => Problemas.Count(p => p.Severidad == Severidad.Advertencia);

    public bool SePuedeJugar => Mapa is not null && !TieneErroresDeFormato;

    /// <summary>Resumen sin spoilers.</summary>
    public string Resumen
    {
        get
        {
            if (TieneErroresDeFormato) return "El archivo tiene errores de formato.";
            if (TieneProblemaLogico) return "Se detectó un problema de lógica.";
            return CantidadAdvertencias == 0
                ? "Mapa válido."
                : $"Mapa válido ({CantidadAdvertencias} {(CantidadAdvertencias == 1 ? "advertencia" : "advertencias")}).";
        }
    }
}
