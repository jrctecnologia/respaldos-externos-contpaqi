using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace RespaldosBak.Core
{
    public static class Tipo
    {
        public const string Info = "INFO";
        public const string Copiado = "COPIADO";
        public const string Movido = "MOVIDO";
        public const string Comprimido = "COMPRIMIDO";
        public const string Copiaria = "COPIARÍA";
        public const string Moveria = "MOVERÍA";
        public const string Omitido = "OMITIDO";
        public const string Aviso = "AVISO";
        public const string Error = "ERROR";
    }

    /// <summary>Log a archivo (UTF-8 con BOM) más un evento por línea para la interfaz.</summary>
    public sealed class Bitacora : IDisposable
    {
        private readonly object _candado = new object();
        private StreamWriter _escritor;

        /// <summary>Ruta del log, o null si no se pudo crear en ninguna carpeta.</summary>
        public string RutaArchivo { get; }

        /// <summary>Se dispara en el hilo que escribe; la interfaz debe pasar a su propio hilo.</summary>
        public event Action<string> Linea;

        /// <param name="carpeta">Carpeta preferida (la del .exe); si no se puede escribir se usa %TEMP%.</param>
        public Bitacora(string carpeta, int anio)
        {
            string nombre = string.Format(CultureInfo.InvariantCulture,
                "respaldos_bak_{0}_{1:yyyyMMdd_HHmmss}.log", anio, DateTime.Now);

            foreach (string candidata in new[] { carpeta, Path.GetTempPath() })
            {
                if (string.IsNullOrEmpty(candidata)) continue;
                try
                {
                    string ruta = Path.Combine(candidata, nombre);
                    var flujo = new FileStream(ruta, FileMode.Append, FileAccess.Write, FileShare.Read);
                    _escritor = new StreamWriter(flujo, new UTF8Encoding(true)) { AutoFlush = true };
                    RutaArchivo = ruta;
                    break;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                                           || ex is NotSupportedException || ex is System.Security.SecurityException)
                {
                    // Se intenta con la siguiente carpeta.
                }
            }
        }

        public void Escribir(string tipo, string texto, string detalle = null)
        {
            string linea = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                           + "  " + tipo + ": " + texto + (detalle == null ? "" : " -> " + detalle);
            lock (_candado)
            {
                // Que falle el log (p. ej. disco lleno) no debe interrumpir el respaldo.
                try { _escritor?.WriteLine(linea); }
                catch (IOException) { _escritor = null; }
            }
            Linea?.Invoke(linea);
        }

        public void Dispose()
        {
            lock (_candado)
            {
                try { _escritor?.Dispose(); }
                catch (IOException) { }
                _escritor = null;
            }
        }
    }
}
