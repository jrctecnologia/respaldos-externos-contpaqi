using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using RespaldosBak.Core;

namespace RespaldosBak.Tests
{
    /// <summary>Estructura falsa de CONTPAQi en una carpeta temporal (§10 del plan).</summary>
    public sealed class Escenario : IDisposable
    {
        public string Base { get; }
        public string Origen { get { return Path.Combine(Base, "Compac", "Empresas"); } }
        public string Index { get { return Path.Combine(Base, "Compac", "Index"); } }
        public string Destino { get { return Path.Combine(Base, "Destino"); } }
        public List<string> Log { get; } = new List<string>();
        public string RutaLog { get; private set; }

        public Escenario()
        {
            Base = Path.Combine(Path.GetTempPath(), "RespaldosBak_" + Guid.NewGuid().ToString("N"));
            CrearBak("SISTEMA.bak", 2021);
            CrearBak("adEMP1/R_2021_A.bak", 2021);
            CrearBak("adEMP1/R_2022_B.bak", 2022);
            CrearBak("ctEMP2/sub/R_2021_C.bak", 2021);
            CrearBak("ctEMP3/R_2021_D.bak", 2021);
            CrearZip("R_2021_A.zip");
            CrearZip("R_2021_C.zip");
            CrearZip("R_2022_B.zip");
        }

        public string CrearBak(string relativa, int anio, int tamano = 5000)
        {
            string ruta = Bak(relativa);
            Crear(ruta, tamano);
            File.SetLastWriteTime(ruta, new DateTime(anio, 6, 15, 10, 30, 0));
            return ruta;
        }

        public string CrearZip(string nombre, int tamano = 700)
        {
            string ruta = Zip(nombre);
            Crear(ruta, tamano);
            // Año distinto a propósito: los .zip no se filtran por su fecha.
            File.SetLastWriteTime(ruta, new DateTime(2019, 1, 2, 3, 4, 6));
            return ruta;
        }

        public string Bak(string relativa) { return Path.Combine(Origen, Nativa(relativa)); }
        public string Zip(string nombre) { return Path.Combine(Index, "respaldos", nombre); }
        public string EnDestino(string relativa) { return Path.Combine(Destino, Nativa(relativa)); }

        public Opciones Opciones(int anio = 2021, ModoOperacion modo = ModoOperacion.Copiar, bool comprimir = false, bool simular = false)
        {
            return new Opciones
            {
                Origen = Origen, Index = Index, Destino = Destino,
                Anio = anio, Modo = modo, Comprimir = comprimir, Simular = simular
            };
        }

        public Resultado Correr(Opciones opciones, Action<Progreso> alAvanzar = null, CancellationToken token = default)
        {
            string carpetaLog = Path.Combine(Base, "log");
            Directory.CreateDirectory(carpetaLog);
            using (var bitacora = new Bitacora(carpetaLog, opciones.Anio))
            {
                RutaLog = bitacora.RutaArchivo;
                bitacora.Linea += Log.Add;
                var procesador = new Procesador(opciones, bitacora);
                PlanTrabajo plan = procesador.Preparar(token);
                return procesador.Ejecutar(plan, alAvanzar == null ? null : new ProgresoDirecto(alAvanzar), token);
            }
        }

        /// <summary>Ruta, tamaño y fecha de todo lo que hay en Compac, para comprobar que no cambió.</summary>
        public string[] Foto()
        {
            return Directory.GetFiles(Path.Combine(Base, "Compac"), "*", SearchOption.AllDirectories)
                .Select(r => r + "|" + new FileInfo(r).Length + "|" + File.GetLastWriteTimeUtc(r).Ticks)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        public string[] ArchivosDestino()
        {
            if (!Directory.Exists(Destino)) return new string[0];
            return Directory.GetFiles(Destino, "*", SearchOption.AllDirectories)
                .Select(r => r.Substring(Destino.Length + 1).Replace('\\', '/'))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        public bool LogContiene(string tipo, string texto)
        {
            return Log.Any(l => l.Contains("  " + tipo + ": ") && l.Contains(texto));
        }

        public void Dispose()
        {
            foreach (string f in Directory.GetFiles(Base, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(Base, true);
        }

        private static string Nativa(string r) { return r.Replace('/', Path.DirectorySeparatorChar); }

        private static void Crear(string ruta, int tamano)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            var datos = new byte[tamano];
            new Random(ruta.Length).NextBytes(datos);
            File.WriteAllBytes(ruta, datos);
        }

        /// <summary>A diferencia de Progress&lt;T&gt;, avisa en el mismo hilo: la cancelación es determinista.</summary>
        private sealed class ProgresoDirecto : IProgress<Progreso>
        {
            private readonly Action<Progreso> _accion;
            public ProgresoDirecto(Action<Progreso> accion) { _accion = accion; }
            public void Report(Progreso valor) { _accion(valor); }
        }
    }
}
