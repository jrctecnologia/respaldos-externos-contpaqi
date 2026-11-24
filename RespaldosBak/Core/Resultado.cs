using System;
using System.Collections.Generic;
using System.Globalization;

namespace RespaldosBak.Core
{
    public sealed class Resultado
    {
        public int Encontrados { get; set; }
        public int Procesados { get; set; }
        public int Omitidos { get; set; }
        public int IndexProcesados { get; set; }
        public int IndexOmitidos { get; set; }
        public int IndexNoEncontrados { get; set; }
        public int Avisos { get; set; }
        public int Errores { get; set; }

        /// <summary>Bytes de origen (sin comprimir) de los archivos procesados.</summary>
        public long Bytes { get; set; }
        public bool Cancelado { get; set; }
        public bool Simulado { get; set; }

        public IEnumerable<string> Resumen()
        {
            if (Cancelado) yield return "Proceso CANCELADO: resumen parcial";
            if (Simulado) yield return "SIMULACIÓN: no se modificó ningún archivo";
            yield return ".bak encontrados: " + Encontrados;
            yield return (Simulado ? ".bak que se procesarían: " : ".bak procesados: ") + Procesados;
            yield return ".bak omitidos (ya existían): " + Omitidos;
            yield return (Simulado ? ".zip de Index que se procesarían: " : ".zip de Index procesados: ") + IndexProcesados;
            yield return ".zip de Index omitidos: " + IndexOmitidos;
            yield return ".zip de Index no encontrados: " + IndexNoEncontrados;
            yield return "Avisos: " + Avisos;
            yield return "Errores: " + Errores;
            yield return "Tamaño: " + FormatoTamano(Bytes);
        }

        public static string FormatoTamano(long bytes)
        {
            string[] unidades = { "B", "KB", "MB", "GB", "TB" };
            double valor = bytes;
            int i = 0;
            while (valor >= 1024 && i < unidades.Length - 1) { valor /= 1024; i++; }
            return valor.ToString(i == 0 ? "0" : "0.00", CultureInfo.InvariantCulture) + " " + unidades[i];
        }
    }

    public sealed class Progreso
    {
        public string Archivo { get; set; }
        public int Numero { get; set; }
        public int Total { get; set; }
        public long BytesProcesados { get; set; }
        public long BytesTotales { get; set; }
    }
}
