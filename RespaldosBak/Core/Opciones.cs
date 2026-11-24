namespace RespaldosBak.Core
{
    public enum ModoOperacion { Copiar, Mover }

    public sealed class Opciones
    {
        /// <summary>Carpeta Empresas.</summary>
        public string Origen { get; set; }

        /// <summary>Carpeta Index (la que contiene "respaldos"). Puede no existir.</summary>
        public string Index { get; set; }

        public string Destino { get; set; }
        public int Anio { get; set; }
        public ModoOperacion Modo { get; set; } = ModoOperacion.Copiar;
        public bool Comprimir { get; set; }
        public bool Simular { get; set; } = true;
    }
}
