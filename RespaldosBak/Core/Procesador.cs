using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace RespaldosBak.Core
{
    /// <summary>
    /// Copia o mueve los .bak y sus .zip de Index. Uso: <see cref="Preparar"/> y después <see cref="Ejecutar"/>.
    /// </summary>
    public sealed class Procesador
    {
        private enum Estado { Procesado, Omitido, Error }

        private readonly Opciones _op;
        private readonly Bitacora _log;
        private int _avisos;

        public Procesador(Opciones opciones, Bitacora bitacora)
        {
            _op = opciones;
            _log = bitacora;
        }

        public static string VersionApp
        {
            get
            {
                var atributo = typeof(Procesador).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return atributo == null ? "" : atributo.InformationalVersion;
            }
        }

        private bool Mover { get { return _op.Modo == ModoOperacion.Mover; } }

        /// <summary>Valida las rutas y arma la lista de trabajo. No modifica nada.</summary>
        /// <exception cref="InvalidOperationException">Las rutas no son válidas.</exception>
        public PlanTrabajo Preparar(CancellationToken token)
        {
            EscribirEncabezado();

            Validacion v = Validador.ValidarRutas(_op);
            if (v.Errores.Count > 0)
            {
                foreach (string error in v.Errores) _log.Escribir(Tipo.Error, error);
                throw new InvalidOperationException(string.Join(Environment.NewLine, v.Errores));
            }
            foreach (string advertencia in v.Advertencias) Aviso(advertencia);

            var plan = new PlanTrabajo();
            plan.Archivos.AddRange(BuscadorBak.Buscar(_op.Origen, _op.Anio, (ruta, motivo) => Aviso(ruta, motivo), token));

            Dictionary<string, FileInfo> index = LeerIndex();
            plan.IndexDisponible = index != null;

            var contados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ArchivoBak bak in plan.Archivos)
            {
                plan.TotalBytes += bak.Tamano;
                plan.ArchivoMasGrande = Math.Max(plan.ArchivoMasGrande, bak.Tamano);

                FileInfo zip;
                if (index == null || !index.TryGetValue(Path.GetFileNameWithoutExtension(bak.RutaCompleta), out zip)) continue;
                bak.ZipIndex = zip.FullName;
                bak.TamanoZipIndex = zip.Length;
                if (!contados.Add(zip.FullName)) continue;
                plan.TotalBytes += zip.Length;
                plan.ArchivoMasGrande = Math.Max(plan.ArchivoMasGrande, zip.Length);
            }
            return plan;
        }

        public Resultado Ejecutar(PlanTrabajo plan, IProgress<Progreso> progreso, CancellationToken token)
        {
            var r = new Resultado { Encontrados = plan.Archivos.Count, Simulado = _op.Simular };
            var zipsAtendidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var zipsContados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string destinoBak = Path.Combine(Rutas.Normalizar(_op.Destino), Rutas.NombreCarpeta(_op.Origen));
            string destinoIndex = Path.Combine(Rutas.Normalizar(_op.Destino), Rutas.CarpetaIndex, Rutas.CarpetaRespaldos);
            long acumulado = 0;

            try
            {
                for (int i = 0; i < plan.Archivos.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    ArchivoBak bak = plan.Archivos[i];
                    int numero = i + 1;

                    Action<string, long> informar = (nombre, bytes) => progreso?.Report(new Progreso
                    {
                        Archivo = nombre,
                        Numero = numero,
                        Total = plan.Archivos.Count,
                        BytesProcesados = Math.Min(bytes, plan.TotalBytes),
                        BytesTotales = plan.TotalBytes
                    });

                    long baseBak = acumulado;
                    string destino = Path.Combine(destinoBak, bak.RutaRelativa) + (_op.Comprimir ? ".zip" : "");
                    informar(bak.RutaRelativa, baseBak);
                    Estado estado = Transferir(bak.RutaCompleta, bak.RutaRelativa, destino, _op.Comprimir, r,
                                               leidos => informar(bak.RutaRelativa, baseBak + leidos), token);
                    acumulado = baseBak + bak.Tamano;

                    if (estado == Estado.Omitido) r.Omitidos++;
                    if (estado == Estado.Procesado)
                    {
                        r.Procesados++;
                        r.Bytes += bak.Tamano;
                    }

                    // El .zip de Index sigue al .bak: si este se omitió o falló, no se toca.
                    if (estado == Estado.Procesado && plan.IndexDisponible)
                        ProcesarIndex(bak, destinoIndex, zipsAtendidos, r, acumulado, informar, token);

                    // Cuenta aunque se haya omitido o fallado, para que el progreso llegue al total.
                    if (bak.ZipIndex != null && zipsContados.Add(bak.ZipIndex)) acumulado += bak.TamanoZipIndex;
                    informar(bak.RutaRelativa, acumulado);
                }
            }
            catch (OperationCanceledException)
            {
                r.Cancelado = true;
                Aviso("Proceso cancelado por el usuario");
            }

            r.Avisos = _avisos;
            foreach (string linea in r.Resumen()) _log.Escribir(Tipo.Info, linea);
            return r;
        }

        private void ProcesarIndex(ArchivoBak bak, string destinoIndex, HashSet<string> atendidos, Resultado r,
                                   long acumulado, Action<string, long> informar, CancellationToken token)
        {
            if (bak.ZipIndex == null)
            {
                r.IndexNoEncontrados++;
                Aviso(bak.RutaRelativa, "no tiene .zip en Index\\respaldos");
                return;
            }

            string nombre = Path.GetFileName(bak.ZipIndex);
            if (!atendidos.Add(bak.ZipIndex))
            {
                Aviso(bak.RutaRelativa, "nombre duplicado; " + nombre + " ya se atendió con otro .bak");
                return;
            }

            string relativa = Path.Combine(Rutas.CarpetaIndex, Rutas.CarpetaRespaldos, nombre);
            Estado estado;
            try
            {
                informar(relativa, acumulado);
                // Nunca se recomprime: ya es .zip.
                estado = Transferir(bak.ZipIndex, relativa, Path.Combine(destinoIndex, nombre), false, r,
                                    leidos => informar(relativa, acumulado + leidos), token);
            }
            catch (OperationCanceledException)
            {
                // Su .bak ya quedó en el destino, así que otra ejecución ya no tomaría este .zip.
                Aviso(relativa, "cancelado; su .bak ya se procesó, cópialo a mano si lo necesitas");
                throw;
            }

            if (estado == Estado.Omitido) r.IndexOmitidos++;
            if (estado == Estado.Procesado)
            {
                r.IndexProcesados++;
                r.Bytes += bak.TamanoZipIndex;
            }
        }

        private Estado Transferir(string origen, string relativa, string destino, bool comprimir, Resultado r,
                                  Action<long> avance, CancellationToken token)
        {
            try
            {
                string destinoRelativo = Rutas.Relativa(_op.Destino, destino);
                if (File.Exists(destino) || Directory.Exists(destino))
                {
                    _log.Escribir(Tipo.Omitido, relativa, "ya existe en el destino: " + destinoRelativo);
                    return Estado.Omitido;
                }

                if (_op.Simular)
                {
                    _log.Escribir(Mover ? Tipo.Moveria : Tipo.Copiaria, relativa,
                                  destinoRelativo + (comprimir ? " (y comprimiría)" : ""));
                    return Estado.Procesado;
                }

                long leidos = 0;
                Action<int> alLeer = n => { leidos += n; avance(leidos); };
                if (comprimir) Transferencia.Comprimir(origen, destino, alLeer, token);
                else Transferencia.Copiar(origen, destino, alLeer, token);

                if (Mover) BorrarOriginal(origen);

                string tipo = Mover ? Tipo.Movido : comprimir ? Tipo.Comprimido : Tipo.Copiado;
                _log.Escribir(tipo, relativa, destinoRelativo + (Mover && comprimir ? " (comprimido)" : ""));
                return Estado.Procesado;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                r.Errores++;
                _log.Escribir(Tipo.Error, relativa, ex.Message);
                return Estado.Error;
            }
        }

        private static void BorrarOriginal(string origen)
        {
            try
            {
                File.Delete(origen);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new IOException("se copió y verificó, pero no se pudo borrar el original: " + ex.Message, ex);
            }
        }

        /// <summary>Nombre sin extensión -> .zip. Null si la carpeta no existe o no se puede leer.</summary>
        private Dictionary<string, FileInfo> LeerIndex()
        {
            if (string.IsNullOrWhiteSpace(_op.Index)) return null;
            string carpeta = Path.Combine(Rutas.Normalizar(_op.Index), Rutas.CarpetaRespaldos);
            if (!Directory.Exists(carpeta)) return null; // La advertencia ya la registró el validador.

            try
            {
                var zips = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (FileInfo archivo in new DirectoryInfo(carpeta).GetFiles())
                {
                    if (string.Equals(archivo.Extension, ".zip", StringComparison.OrdinalIgnoreCase))
                        zips[Path.GetFileNameWithoutExtension(archivo.Name)] = archivo;
                }
                return zips;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Aviso(carpeta, "no se pudo leer; solo se procesarán los .bak: " + ex.Message);
                return null;
            }
        }

        private void Aviso(string texto, string detalle = null)
        {
            _avisos++;
            _log.Escribir(Tipo.Aviso, texto, detalle);
        }

        private void EscribirEncabezado()
        {
            _log.Escribir(Tipo.Info, "Respaldos BAK CONTPAQi " + VersionApp + " - JRC Tecnología");
            _log.Escribir(Tipo.Info, "Usuario: " + Environment.UserName + "  Equipo: " + Environment.MachineName);
            _log.Escribir(Tipo.Info, "Origen: " + _op.Origen);
            _log.Escribir(Tipo.Info, "Index: " + _op.Index);
            _log.Escribir(Tipo.Info, "Destino: " + _op.Destino);
            _log.Escribir(Tipo.Info, "Año: " + _op.Anio);
            _log.Escribir(Tipo.Info, "Operación: " + (Mover ? "Mover" : "Copiar"));
            _log.Escribir(Tipo.Info, "Comprimir: " + (_op.Comprimir ? "Sí" : "No"));
            _log.Escribir(Tipo.Info, "Simular: " + (_op.Simular ? "Sí" : "No"));
        }
    }
}
