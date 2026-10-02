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
        json = LimpiarRespuestaIA(json);
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

    /// <summary>
    /// Tolera respuestas de IA pegadas tal cual: saca el bloque ```json ... ``` o el texto
    /// antes de la primera llave y despues de la ultima.
    /// </summary>
    internal static string LimpiarRespuestaIA(string texto)
    {
        var t = texto.Trim().TrimStart('﻿');
        var inicioBloque = t.IndexOf("```", StringComparison.Ordinal);
        if (inicioBloque >= 0)
        {
            var finLinea = t.IndexOf('\n', inicioBloque);
            var cierre = finLinea < 0 ? -1 : t.IndexOf("```", finLinea, StringComparison.Ordinal);
            if (cierre > finLinea) t = t[(finLinea + 1)..cierre].Trim();
        }
        var primera = t.IndexOf('{');
        var ultima = t.LastIndexOf('}');
        return primera > 0 && ultima > primera ? t[primera..(ultima + 1)] : t;
    }
}
