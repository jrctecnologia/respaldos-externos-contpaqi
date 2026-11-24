using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RespaldosBak.Core
{
    public sealed class Validacion
    {
        /// <summary>Impiden iniciar.</summary>
        public List<string> Errores { get; } = new List<string>();

        /// <summary>Se muestran, pero se puede continuar.</summary>
        public List<string> Advertencias { get; } = new List<string>();
    }

    public static class Validador
    {
        private const long LimiteFat32 = 4L * 1024 * 1024 * 1024 - 1;

        public static Validacion ValidarRutas(Opciones o)
        {
            var v = new Validacion();
            if (string.IsNullOrWhiteSpace(o.Origen)) v.Errores.Add("Selecciona la carpeta de origen.");
            if (string.IsNullOrWhiteSpace(o.Destino)) v.Errores.Add("Selecciona la carpeta de destino.");
            if (v.Errores.Count > 0) return v;

            bool hayIndex = !string.IsNullOrWhiteSpace(o.Index);
            try
            {
                Rutas.Normalizar(o.Origen);
                Rutas.Normalizar(o.Destino);
                if (hayIndex) Rutas.Normalizar(o.Index);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException
                                       || ex is PathTooLongException || ex is System.Security.SecurityException)
            {
                v.Errores.Add("Una de las rutas no es válida: " + ex.Message);
                return v;
            }

            if (!Directory.Exists(o.Origen))
            {
                v.Errores.Add("La carpeta de origen no existe: " + o.Origen);
            }
            else
            {
                try
                {
                    if (!Directory.EnumerateDirectories(o.Origen).Any())
                        v.Errores.Add("La carpeta de origen no contiene subcarpetas.");
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    v.Errores.Add("No se puede leer la carpeta de origen: " + ex.Message);
                }
            }

            if (SeTraslapan(o.Origen, o.Destino))
                v.Errores.Add("El destino y el origen no pueden ser la misma carpeta ni estar uno dentro del otro.");
            if (hayIndex && SeTraslapan(o.Index, o.Destino))
                v.Errores.Add("El destino y la carpeta Index no pueden ser la misma carpeta ni estar uno dentro del otro.");

            if (!hayIndex || !Directory.Exists(Path.Combine(o.Index, Rutas.CarpetaRespaldos)))
                v.Advertencias.Add("No existe la carpeta Index\\respaldos; solo se procesarán los .bak.");

            return v;
        }

        /// <summary>Espacio libre (cálculo conservador, sin comprimir) y límite de 4 GB por archivo de FAT32.</summary>
        public static List<string> ValidarUnidadDestino(Opciones o, PlanTrabajo plan)
        {
            var advertencias = new List<string>();
            try
            {
                var unidad = new DriveInfo(Path.GetPathRoot(Rutas.Normalizar(o.Destino)));
                advertencias.AddRange(EvaluarUnidad(unidad.AvailableFreeSpace, unidad.DriveFormat, plan));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // Rutas de red (\\servidor\carpeta): DriveInfo no las admite y no se puede consultar el espacio.
                advertencias.Add("No se pudo consultar el espacio libre del destino.");
            }
            return advertencias;
        }

        internal static IEnumerable<string> EvaluarUnidad(long libres, string formato, PlanTrabajo plan)
        {
            if (libres < plan.TotalBytes)
                yield return "Espacio insuficiente en el destino: se necesitan hasta "
                             + Resultado.FormatoTamano(plan.TotalBytes) + " y hay "
                             + Resultado.FormatoTamano(libres) + " libres.";

            if (string.Equals(formato, "FAT32", StringComparison.OrdinalIgnoreCase) && plan.ArchivoMasGrande > LimiteFat32)
                yield return "El destino tiene formato FAT32, que no admite archivos de más de 4 GB, y hay al menos uno de "
                             + Resultado.FormatoTamano(plan.ArchivoMasGrande) + ". Usa una unidad NTFS o exFAT.";
        }

        private static bool SeTraslapan(string a, string b)
        {
            return Rutas.EstaDentroOEsIgual(a, b) || Rutas.EstaDentroOEsIgual(b, a);
        }
    }
}
