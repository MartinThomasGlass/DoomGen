using System.Text.Json;
using DoomCompanion.Core.Modelo;

namespace DoomCompanion.Core.Validacion;

/// <summary>Esquema → referencias → logica e inventario.</summary>
public static class ImportadorMapa
{
    public static ResultadoImportacion ImportarArchivo(string ruta, Catalogo catalogo)
    {
        string json;
        try
        {
            json = File.ReadAllText(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ResultadoImportacion
            {
                Problemas = [new Problema(Severidad.Error, CategoriaProblema.Esquema, "(archivo)", $"No se pudo leer el archivo: {ex.Message}")],
            };
        }
        return Importar(json, catalogo);
    }

    public static ResultadoImportacion Importar(string json, Catalogo catalogo)
    {
        var problemas = ValidadorEsquema.Validar(json);
        if (problemas.Count > 0) return new ResultadoImportacion { Problemas = problemas };

        Mapa mapa;
        try
        {
            mapa = Json.Leer<Mapa>(json);
        }
        catch (JsonException ex)
        {
            return new ResultadoImportacion
            {
                Problemas = [new Problema(Severidad.Error, CategoriaProblema.Esquema, ex.Path ?? "(archivo)", $"No se pudo interpretar el mapa: {ex.Message}")],
            };
        }

        problemas = ValidadorReferencias.Validar(mapa, catalogo);
        if (problemas.Count > 0) return new ResultadoImportacion { Problemas = problemas };

        problemas = [.. ValidadorLogico.Validar(mapa, catalogo), .. ValidadorInventario.Validar(mapa, catalogo)];
        return new ResultadoImportacion { Mapa = mapa, JsonOriginal = json, Problemas = problemas };
    }
}
