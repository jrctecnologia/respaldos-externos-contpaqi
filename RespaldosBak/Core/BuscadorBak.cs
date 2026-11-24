using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace RespaldosBak.Core
{
    /// <summary>Busca los .bak de un año dentro de las subcarpetas del origen. Nunca incluye los de la raíz.</summary>
    public static class BuscadorBak
    {
        /// <param name="aviso">Recibe (ruta, motivo) por cada carpeta o archivo que no se pudo leer.</param>
        public static List<ArchivoBak> Buscar(string origen, int anio, Action<string, string> aviso, CancellationToken token)
        {
            string raiz = Rutas.Normalizar(origen);
            var encontrados = new List<ArchivoBak>();
            var pendientes = new Stack<string>();
            pendientes.Push(raiz);

            // Recorrido manual: SearchOption.AllDirectories aborta todo ante la primera carpeta sin permiso.
            while (pendientes.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string carpeta = pendientes.Pop();

                try
                {
                    foreach (string sub in Directory.GetDirectories(carpeta))
                    {
                        // Los enlaces y uniones pueden formar ciclos o sacar el recorrido del origen.
                        if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0)
                            aviso(sub, "es un enlace a otra carpeta; no se recorre");
                        else
                            pendientes.Push(sub);
                    }

                    if (Rutas.SonIguales(carpeta, raiz)) continue;

                    foreach (string archivo in Directory.GetFiles(carpeta))
                        Agregar(archivo, raiz, anio, encontrados, aviso);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    aviso(carpeta, "no se pudo leer la carpeta: " + ex.Message);
                }
            }

            encontrados.Sort((a, b) => string.Compare(a.RutaRelativa, b.RutaRelativa, StringComparison.OrdinalIgnoreCase));
            return encontrados;
        }

        private static void Agregar(string archivo, string raiz, int anio, List<ArchivoBak> lista, Action<string, string> aviso)
        {
            // Comparación explícita: en .NET Framework el patrón "*.bak" también devuelve ".bak2", ".bakx", etc.
            if (!string.Equals(Path.GetExtension(archivo), ".bak", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                var info = new FileInfo(archivo);
                if (info.LastWriteTime.Year != anio) return;
                lista.Add(new ArchivoBak
                {
                    RutaCompleta = archivo,
                    RutaRelativa = Rutas.Relativa(raiz, archivo),
                    Tamano = info.Length,
                    FechaModificacion = info.LastWriteTime
                });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                aviso(archivo, "no se pudo leer el archivo: " + ex.Message);
            }
        }
    }
}
