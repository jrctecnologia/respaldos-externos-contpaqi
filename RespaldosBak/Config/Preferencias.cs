using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using RespaldosBak.Core;

namespace RespaldosBak.Config
{
    /// <summary>
    /// Últimas rutas, año y compresión, en RespaldosBak.ini junto al .exe. Nunca lanza errores de archivo.
    /// Operación y Simular no se guardan a propósito: siempre se abre en Copiar y Simular.
    /// </summary>
    public static class Preferencias
    {
        public const string NombreArchivo = "RespaldosBak.ini";

        /// <summary>Lo que falte o no sea válido queda con su valor por defecto.</summary>
        public static Opciones Cargar(string carpeta, int anioPorDefecto)
        {
            var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string linea in File.ReadAllLines(Path.Combine(carpeta, NombreArchivo), Encoding.UTF8))
                {
                    int igual = linea.IndexOf('=');
                    if (igual > 0) valores[linea.Substring(0, igual).Trim()] = linea.Substring(igual + 1).Trim();
                }
            }
            catch (Exception ex) when (EsDeArchivo(ex)) { }

            int anio;
            bool anioValido = int.TryParse(Leer(valores, "Anio"), NumberStyles.None, CultureInfo.InvariantCulture, out anio)
                              && Anios.Disponibles.Contains(anio);
            return new Opciones
            {
                Origen = Leer(valores, "Origen"),
                Index = Leer(valores, "Index"),
                Destino = Leer(valores, "Destino"),
                Anio = anioValido ? anio : anioPorDefecto,
                Comprimir = Es(valores, "Comprimir", "Si")
            };
        }

        public static void Guardar(string carpeta, Opciones o)
        {
            string[] lineas =
            {
                "[RespaldosBak]",
                "Origen=" + o.Origen,
                "Index=" + o.Index,
                "Destino=" + o.Destino,
                "Anio=" + o.Anio.ToString(CultureInfo.InvariantCulture),
                "Comprimir=" + (o.Comprimir ? "Si" : "No")
            };
            try { File.WriteAllLines(Path.Combine(carpeta, NombreArchivo), lineas, new UTF8Encoding(true)); }
            catch (Exception ex) when (EsDeArchivo(ex)) { }
        }

        private static string Leer(Dictionary<string, string> valores, string clave)
        {
            string valor;
            return valores.TryGetValue(clave, out valor) ? valor : "";
        }

        private static bool Es(Dictionary<string, string> valores, string clave, string esperado)
        {
            return string.Equals(Leer(valores, clave), esperado, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsDeArchivo(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException
                   || ex is NotSupportedException;
        }
    }
}
