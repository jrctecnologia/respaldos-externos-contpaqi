using System;
using System.Collections.Generic;

namespace RespaldosBak.Core
{
    public sealed class ArchivoBak
    {
        public string RutaCompleta { get; set; }
        public string RutaRelativa { get; set; }
        public long Tamano { get; set; }
        public DateTime FechaModificacion { get; set; }

        /// <summary>Ruta del .zip asociado en Index\respaldos; null si no existe.</summary>
        public string ZipIndex { get; set; }
        public long TamanoZipIndex { get; set; }
    }

    /// <summary>Lo que se va a procesar. Permite a la interfaz pedir confirmación antes de ejecutar.</summary>
    public sealed class PlanTrabajo
    {
        public List<ArchivoBak> Archivos { get; } = new List<ArchivoBak>();

        /// <summary>False si Index\respaldos no existe o no se pudo leer: solo se procesan los .bak.</summary>
        public bool IndexDisponible { get; set; }

        /// <summary>.bak más .zip de Index, cada .zip contado una sola vez.</summary>
        public long TotalBytes { get; set; }
        public long ArchivoMasGrande { get; set; }
    }
}
