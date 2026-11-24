using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace RespaldosBak.Core
{
    /// <summary>Copia o comprime un archivo a un temporal, lo verifica y solo entonces le da su nombre final.</summary>
    internal static class Transferencia
    {
        private const int TamanoBufer = 4 * 1024 * 1024;

        /// <param name="avance">Recibe los bytes leídos en cada bloque.</param>
        public static void Copiar(string origen, string destino, Action<int> avance, CancellationToken token)
        {
            Ejecutar(origen, destino, token, (entrada, temporal) =>
            {
                using (FileStream salida = CrearTemporal(temporal))
                {
                    CopiarFlujo(entrada, salida, avance, token);
                    salida.Flush(true); // Hasta el disco físico: en modo Mover después se borra el original.
                }
                Verificar(new FileInfo(temporal).Length, entrada.Length);
            });
        }

        public static void Comprimir(string origen, string destino, Action<int> avance, CancellationToken token)
        {
            string nombreEntrada = Path.GetFileName(origen);
            Ejecutar(origen, destino, token, (entrada, temporal) =>
            {
                using (FileStream salida = CrearTemporal(temporal))
                {
                    using (var zip = new ZipArchive(salida, ZipArchiveMode.Create, true))
                    {
                        ZipArchiveEntry item = zip.CreateEntry(nombreEntrada, CompressionLevel.Optimal);
                        item.LastWriteTime = File.GetLastWriteTime(origen);
                        using (Stream flujo = item.Open())
                            CopiarFlujo(entrada, flujo, avance, token);
                    }
                    salida.Flush(true);
                }

                using (var lectura = new FileStream(temporal, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(lectura, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry item = zip.GetEntry(nombreEntrada);
                    if (item == null) throw new InvalidDataException("la verificación falló: el ZIP no contiene " + nombreEntrada);
                    Verificar(item.Length, entrada.Length);
                }
            });
        }

        private static void Ejecutar(string origen, string destino, CancellationToken token, Action<FileStream, string> escribir)
        {
            string temporal = destino + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(destino));
            try
            {
                // FileShare.Read: si otro proceso está escribiendo el respaldo, se registra error en vez de copiarlo a medias.
                using (var entrada = new FileStream(origen, FileMode.Open, FileAccess.Read, FileShare.Read, TamanoBufer,
                                                    FileOptions.SequentialScan))
                    escribir(entrada, temporal);

                token.ThrowIfCancellationRequested();

                // Se conserva la fecha original: es el criterio con el que se filtra por año.
                File.SetLastWriteTimeUtc(temporal, File.GetLastWriteTimeUtc(origen));
                File.Move(temporal, destino); // Falla si el destino ya existe; nunca sobrescribe.
            }
            catch
            {
                BorrarTemporal(temporal);
                throw;
            }
        }

        private static FileStream CrearTemporal(string ruta)
        {
            // Create reemplaza un .tmp que haya quedado de una ejecución interrumpida.
            return new FileStream(ruta, FileMode.Create, FileAccess.ReadWrite, FileShare.None, TamanoBufer);
        }

        private static void CopiarFlujo(Stream entrada, Stream salida, Action<int> avance, CancellationToken token)
        {
            var bufer = new byte[TamanoBufer];
            int leidos;
            token.ThrowIfCancellationRequested();
            while ((leidos = entrada.Read(bufer, 0, bufer.Length)) > 0)
            {
                salida.Write(bufer, 0, leidos);
                avance(leidos);
                token.ThrowIfCancellationRequested();
            }
        }

        private static void Verificar(long escrito, long esperado)
        {
            if (escrito != esperado)
                throw new InvalidDataException("la verificación falló: se esperaban " + esperado + " bytes y hay " + escrito);
        }

        private static void BorrarTemporal(string temporal)
        {
            try { File.Delete(temporal); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }
    }
}
