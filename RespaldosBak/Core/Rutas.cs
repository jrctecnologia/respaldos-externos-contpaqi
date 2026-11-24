using System;
using System.IO;

namespace RespaldosBak.Core
{
    public static class Rutas
    {
        public const string CarpetaIndex = "Index";
        public const string CarpetaRespaldos = "respaldos";

        /// <summary>Ruta absoluta sin separador final (salvo que sea la raíz de la unidad).</summary>
        public static string Normalizar(string ruta)
        {
            string completa = Path.GetFullPath(ruta.Trim());
            string raiz = Path.GetPathRoot(completa);
            while (completa.Length > raiz.Length && EsSeparador(completa[completa.Length - 1]))
                completa = completa.Substring(0, completa.Length - 1);
            return completa;
        }

        public static bool SonIguales(string a, string b)
        {
            return string.Equals(Normalizar(a), Normalizar(b), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True si <paramref name="ruta"/> es <paramref name="carpeta"/> o está dentro de ella.</summary>
        public static bool EstaDentroOEsIgual(string ruta, string carpeta)
        {
            return ConSeparador(Normalizar(ruta))
                .StartsWith(ConSeparador(Normalizar(carpeta)), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parte de <paramref name="ruta"/> posterior a <paramref name="carpetaBase"/> (debe estar dentro).</summary>
        public static string Relativa(string carpetaBase, string ruta)
        {
            string prefijo = ConSeparador(Normalizar(carpetaBase));
            if (!ruta.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("La ruta no está dentro de la carpeta base: " + ruta);
            return ruta.Substring(prefijo.Length);
        }

        /// <summary>Nombre de la carpeta de origen que se recrea en el destino.</summary>
        public static string NombreCarpeta(string carpeta)
        {
            string nombre = Path.GetFileName(Normalizar(carpeta));
            return string.IsNullOrEmpty(nombre) ? "Empresas" : nombre;
        }

        /// <summary>&lt;padre de Origen&gt;\Index, o null si Origen no tiene carpeta padre.</summary>
        public static string IndexPropuesto(string origen)
        {
            if (string.IsNullOrWhiteSpace(origen)) return null;
            string padre = Path.GetDirectoryName(Normalizar(origen));
            return padre == null ? null : Path.Combine(padre, CarpetaIndex);
        }

        private static string ConSeparador(string ruta)
        {
            return EsSeparador(ruta[ruta.Length - 1]) ? ruta : ruta + Path.DirectorySeparatorChar;
        }

        private static bool EsSeparador(char c)
        {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }
    }
}
