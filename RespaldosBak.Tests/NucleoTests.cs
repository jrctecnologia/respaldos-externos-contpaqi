using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using RespaldosBak.Config;
using RespaldosBak.Core;
using Xunit;

namespace RespaldosBak.Tests
{
    public class NucleoTests : IDisposable
    {
        private readonly Escenario e = new Escenario();
        public void Dispose() { e.Dispose(); }

        private static readonly string[] Esperados2021 =
        {
            "Empresas/adEMP1/R_2021_A.bak", "Empresas/ctEMP2/sub/R_2021_C.bak", "Empresas/ctEMP3/R_2021_D.bak",
            "Index/respaldos/R_2021_A.zip", "Index/respaldos/R_2021_C.zip"
        };

        private static bool EsWindows { get { return RuntimeInformation.IsOSPlatform(OSPlatform.Windows); } }

        [Fact]
        public void Copiar_recrea_la_estructura_y_deja_los_originales_intactos()
        {
            string[] antes = e.Foto();
            Resultado r = e.Correr(e.Opciones());

            Assert.Equal(Esperados2021, e.ArchivosDestino());
            Assert.Equal(antes, e.Foto());
            Assert.Equal(File.ReadAllBytes(e.Bak("adEMP1/R_2021_A.bak")), File.ReadAllBytes(e.EnDestino(Esperados2021[0])));
            Assert.Equal(File.ReadAllBytes(e.Zip("R_2021_A.zip")), File.ReadAllBytes(e.EnDestino(Esperados2021[3])));
            Assert.Equal((3, 3, 0, 2, 1, 0),
                (r.Encontrados, r.Procesados, r.Omitidos, r.IndexProcesados, r.IndexNoEncontrados, r.Errores));
            Assert.Equal(3 * 5000 + 2 * 700, r.Bytes);
        }

        [Fact]
        public void Se_conserva_la_fecha_de_modificacion()
        {
            e.Correr(e.Opciones());
            Assert.Equal(File.GetLastWriteTimeUtc(e.Bak("adEMP1/R_2021_A.bak")), File.GetLastWriteTimeUtc(e.EnDestino(Esperados2021[0])));
            Assert.Equal(File.GetLastWriteTimeUtc(e.Zip("R_2021_A.zip")), File.GetLastWriteTimeUtc(e.EnDestino(Esperados2021[3])));
        }

        [Fact]
        public void Mover_borra_solo_lo_procesado()
        {
            Resultado r = e.Correr(e.Opciones(modo: ModoOperacion.Mover));

            Assert.Equal(Esperados2021, e.ArchivosDestino());
            Assert.False(File.Exists(e.Bak("adEMP1/R_2021_A.bak")));
            Assert.False(File.Exists(e.Bak("ctEMP2/sub/R_2021_C.bak")));
            Assert.False(File.Exists(e.Zip("R_2021_A.zip")));
            Assert.True(File.Exists(e.Bak("SISTEMA.bak")));
            Assert.True(File.Exists(e.Bak("adEMP1/R_2022_B.bak")));
            Assert.True(File.Exists(e.Zip("R_2022_B.zip")));
            Assert.Equal(0, r.Errores);
            Assert.True(e.LogContiene("MOVIDO", "R_2021_A.bak"));
        }

        [Theory]
        [InlineData(false, ModoOperacion.Copiar)]
        [InlineData(false, ModoOperacion.Mover)]
        [InlineData(true, ModoOperacion.Copiar)]
        [InlineData(true, ModoOperacion.Mover)]
        public void La_raiz_de_Empresas_nunca_se_toca(bool simular, ModoOperacion modo)
        {
            byte[] antes = File.ReadAllBytes(e.Bak("SISTEMA.bak"));
            e.Correr(e.Opciones(modo: modo, simular: simular));
            Assert.Equal(antes, File.ReadAllBytes(e.Bak("SISTEMA.bak")));
            Assert.DoesNotContain(e.ArchivosDestino(), a => a.Contains("SISTEMA"));
            Assert.DoesNotContain(e.Log, l => l.Contains("SISTEMA"));
        }

        [Fact]
        public void El_filtro_funciona_en_cada_anio_del_selector()
        {
            Assert.Equal(Enumerable.Range(2020, 8), Anios.Disponibles);
            foreach (int anio in Anios.Disponibles)
            {
                e.CrearBak("todos/T" + anio + ".bak", anio);
                // Límites del año.
                File.SetLastWriteTime(e.CrearBak("todos/Ini" + anio + ".bak", anio), new DateTime(anio, 1, 1, 0, 0, 0));
                File.SetLastWriteTime(e.CrearBak("todos/Fin" + anio + ".bak", anio), new DateTime(anio, 12, 31, 23, 59, 58));
            }
            foreach (int anio in Anios.Disponibles)
            {
                var nombres = BuscadorBak.Buscar(e.Origen, anio, (a, b) => Assert.Fail(a + b), CancellationToken.None)
                    .Where(b => b.RutaRelativa.StartsWith("todos")).Select(b => Path.GetFileName(b.RutaRelativa));
                Assert.Equal(new[] { "Fin" + anio + ".bak", "Ini" + anio + ".bak", "T" + anio + ".bak" }, nombres);
            }
        }

        [Fact]
        public void Solo_la_extension_bak_exacta_sin_importar_mayusculas()
        {
            e.CrearBak("ext/Uno.BAK", 2021);
            e.CrearBak("ext/Dos.bak2", 2021);
            e.CrearBak("ext/Tres.bakx", 2021);
            e.CrearBak("ext/Cuatro.bak.zip", 2021);
            var r = BuscadorBak.Buscar(e.Origen, 2021, (a, b) => { }, CancellationToken.None);
            Assert.Equal(new[] { "Uno.BAK" },
                r.Where(b => b.RutaRelativa.StartsWith("ext")).Select(b => Path.GetFileName(b.RutaRelativa)));
        }

        [Theory]
        [InlineData(ModoOperacion.Copiar, "COPIARÍA")]
        [InlineData(ModoOperacion.Mover, "MOVERÍA")]
        public void Simular_no_crea_ni_borra_nada(ModoOperacion modo, string tipo)
        {
            string[] antes = e.Foto();
            Resultado r = e.Correr(e.Opciones(modo: modo, comprimir: true, simular: true));
            Assert.Equal(antes, e.Foto());
            Assert.False(Directory.Exists(e.Destino));
            Assert.Equal((3, 2, 1), (r.Procesados, r.IndexProcesados, r.IndexNoEncontrados));
            Assert.True(e.LogContiene(tipo, "(y comprimiría)"));
        }

        [Theory]
        [InlineData(ModoOperacion.Copiar)]
        [InlineData(ModoOperacion.Mover)]
        public void Comprimir_genera_bak_zip_valido_y_no_recomprime_Index(ModoOperacion modo)
        {
            byte[] bak = File.ReadAllBytes(e.Bak("adEMP1/R_2021_A.bak"));
            byte[] zipIndex = File.ReadAllBytes(e.Zip("R_2021_A.zip"));
            e.Correr(e.Opciones(modo: modo, comprimir: true));

            Assert.Equal(new[]
            {
                "Empresas/adEMP1/R_2021_A.bak.zip", "Empresas/ctEMP2/sub/R_2021_C.bak.zip", "Empresas/ctEMP3/R_2021_D.bak.zip",
                "Index/respaldos/R_2021_A.zip", "Index/respaldos/R_2021_C.zip"
            }, e.ArchivosDestino());
            Assert.Equal(zipIndex, File.ReadAllBytes(e.EnDestino("Index/respaldos/R_2021_A.zip")));
            Assert.Equal(modo == ModoOperacion.Copiar, File.Exists(e.Bak("adEMP1/R_2021_A.bak")));

            using (var zip = new ZipArchive(File.OpenRead(e.EnDestino("Empresas/adEMP1/R_2021_A.bak.zip"))))
            {
                ZipArchiveEntry entrada = Assert.Single(zip.Entries);
                Assert.Equal("R_2021_A.bak", entrada.FullName);
                Assert.Equal(2021, entrada.LastWriteTime.Year);
                using (var ms = new MemoryStream())
                using (Stream s = entrada.Open())
                {
                    s.CopyTo(ms);
                    Assert.Equal(bak, ms.ToArray());
                }
            }
        }

        [Fact]
        public void Zip_de_Index_faltante_es_aviso_no_error()
        {
            Resultado r = e.Correr(e.Opciones());
            Assert.Equal((1, 0), (r.IndexNoEncontrados, r.Errores));
            Assert.True(e.LogContiene("AVISO", "R_2021_D.bak"));
        }

        [Fact]
        public void El_zip_de_Index_se_encuentra_sin_distinguir_mayusculas()
        {
            e.CrearBak("adEMP1/Mixto.bak", 2021);
            e.CrearZip("MIXTO.ZIP");
            e.Correr(e.Opciones());
            Assert.Contains("Index/respaldos/MIXTO.ZIP", e.ArchivosDestino());
        }

        [Fact]
        public void Sin_carpeta_Index_se_procesan_solo_los_bak()
        {
            Directory.Delete(e.Index, true);
            Resultado r = e.Correr(e.Opciones());
            Assert.Equal((3, 0, 0, 0), (r.Procesados, r.IndexProcesados, r.IndexNoEncontrados, r.Errores));
            Assert.True(e.LogContiene("AVISO", "Index"));
        }

        [Fact]
        public void Destino_existente_se_omite_y_su_zip_no_se_procesa()
        {
            string existente = e.EnDestino(Esperados2021[0]);
            Directory.CreateDirectory(Path.GetDirectoryName(existente));
            File.WriteAllText(existente, "previo");
            Resultado r = e.Correr(e.Opciones(modo: ModoOperacion.Mover));

            Assert.Equal("previo", File.ReadAllText(existente));
            Assert.Equal((1, 2, 0), (r.Omitidos, r.Procesados, r.Errores));
            Assert.True(File.Exists(e.Bak("adEMP1/R_2021_A.bak")));
            Assert.True(File.Exists(e.Zip("R_2021_A.zip")));
            Assert.False(File.Exists(e.EnDestino("Index/respaldos/R_2021_A.zip")));
        }

        [Fact]
        public void Nombre_duplicado_procesa_el_zip_una_sola_vez()
        {
            e.CrearBak("zzOTRA/R_2021_A.bak", 2021);
            Resultado r = e.Correr(e.Opciones(modo: ModoOperacion.Mover));
            Assert.Equal((4, 2, 1, 0), (r.Procesados, r.IndexProcesados, r.IndexNoEncontrados, r.Errores));
            Assert.True(e.LogContiene("AVISO", "nombre duplicado"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Cancelar_a_la_mitad_no_deja_tmp_ni_borra_originales(bool comprimir)
        {
            string grande = e.CrearBak("bbGRANDE/Grande.bak", 2021, 20 * 1024 * 1024);
            string[] antes = e.Foto();
            using (var fuente = new CancellationTokenSource())
            {
                // Se cancela tras el primer bloque del archivo grande; adEMP1 ya se movió.
                Resultado r = e.Correr(e.Opciones(modo: ModoOperacion.Mover, comprimir: comprimir), p =>
                {
                    if (p.Archivo.EndsWith("Grande.bak") && p.BytesProcesados > 4 * 1024 * 1024) fuente.Cancel();
                }, fuente.Token);

                Assert.True(r.Cancelado);
                Assert.Equal(1, r.Procesados);
            }
            Assert.DoesNotContain(e.ArchivosDestino(), a => a.EndsWith(".tmp") || a.Contains("Grande"));
            Assert.Equal(20 * 1024 * 1024, new FileInfo(grande).Length);
            // Solo faltan los dos originales del archivo ya movido.
            Assert.Equal(antes.Where(f => !f.Contains("R_2021_A.")), e.Foto());
        }

        [Fact]
        public void Cancelar_durante_el_zip_de_Index_avisa_que_quedo_pendiente()
        {
            e.CrearZip("R_2021_A.zip", 9 * 1024 * 1024);
            using (var fuente = new CancellationTokenSource())
            {
                Resultado r = e.Correr(e.Opciones(), p =>
                {
                    if (p.Archivo.EndsWith("R_2021_A.zip")) fuente.Cancel();
                }, fuente.Token);
                Assert.True(r.Cancelado);
            }
            Assert.Equal(new[] { Esperados2021[0] }, e.ArchivosDestino());
            Assert.True(e.LogContiene("AVISO", "R_2021_A.zip"));
        }

        [Fact]
        public void Un_tmp_de_una_ejecucion_interrumpida_se_reemplaza()
        {
            string tmp = e.EnDestino(Esperados2021[0]) + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(tmp));
            File.WriteAllBytes(tmp, new byte[90000]);
            Assert.Equal(0, e.Correr(e.Opciones()).Errores);
            Assert.Equal(Esperados2021, e.ArchivosDestino());
        }

        [Fact]
        public void Un_bak_en_uso_da_error_no_se_borra_y_el_resto_continua()
        {
            Resultado r;
            using (new FileStream(e.Bak("adEMP1/R_2021_A.bak"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                r = e.Correr(e.Opciones(modo: ModoOperacion.Mover));

            Assert.Equal((1, 2), (r.Errores, r.Procesados));
            Assert.True(File.Exists(e.Bak("adEMP1/R_2021_A.bak")));
            Assert.True(File.Exists(e.Zip("R_2021_A.zip")));
            Assert.Equal(Esperados2021.Where(a => !a.Contains("R_2021_A")), e.ArchivosDestino());
        }

        [Fact]
        public void Si_no_se_puede_escribir_en_el_destino_el_original_se_queda()
        {
            // Un archivo ocupa el lugar de la carpeta destino de adEMP1.
            Directory.CreateDirectory(e.EnDestino("Empresas"));
            File.WriteAllText(e.EnDestino("Empresas/adEMP1"), "");
            Resultado r = e.Correr(e.Opciones(modo: ModoOperacion.Mover));
            Assert.Equal((1, 2), (r.Errores, r.Procesados));
            Assert.True(File.Exists(e.Bak("adEMP1/R_2021_A.bak")));
            Assert.True(File.Exists(e.Zip("R_2021_A.zip")));
        }

        [Fact]
        public void Si_no_se_puede_borrar_el_original_se_registra_error()
        {
            string bak = e.Bak("adEMP1/R_2021_A.bak");
            if (EsWindows) File.SetAttributes(bak, FileAttributes.ReadOnly);
#if NET
            else File.SetUnixFileMode(Path.GetDirectoryName(bak), UnixFileMode.UserRead | UnixFileMode.UserExecute);
#endif
            Resultado r;
            try { r = e.Correr(e.Opciones(modo: ModoOperacion.Mover)); }
            finally
            {
#if NET
                if (!EsWindows) File.SetUnixFileMode(Path.GetDirectoryName(bak), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#endif
            }
            Assert.Equal((1, 2), (r.Errores, r.Procesados));
            Assert.True(File.Exists(bak));
            Assert.True(e.LogContiene("ERROR", "no se pudo borrar el original"));
        }

        [Fact]
        public void Carpeta_sin_permiso_da_aviso_y_no_aborta()
        {
#if NET
            if (EsWindows) return;
            e.CrearBak("aaPRIVADA/X.bak", 2021);
            string privada = Path.Combine(e.Origen, "aaPRIVADA");
            File.SetUnixFileMode(privada, UnixFileMode.None);
            try
            {
                Resultado r = e.Correr(e.Opciones());
                Assert.Equal((3, 0), (r.Procesados, r.Errores));
                Assert.True(e.LogContiene("AVISO", "aaPRIVADA"));
            }
            finally { File.SetUnixFileMode(privada, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
#endif
        }

        [Fact]
        public void Los_enlaces_a_carpetas_no_se_recorren()
        {
#if NET
            if (EsWindows) return; // Crear enlaces en Windows requiere privilegios.
            Directory.CreateSymbolicLink(Path.Combine(e.Origen, "ctEMP2", "ciclo"), e.Origen);
            Resultado r = e.Correr(e.Opciones());
            Assert.Equal((3, 3), (r.Encontrados, r.Procesados));
            Assert.True(e.LogContiene("AVISO", "enlace"));
#endif
        }

        [Fact]
        public void Log_en_utf8_con_BOM_y_acentos()
        {
            e.CrearBak("ctMUÑOZ/Respaldo_Nómina.bak", 2021);
            e.Correr(e.Opciones(simular: true));
            byte[] datos = File.ReadAllBytes(e.RutaLog);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, datos.Take(3));
            string texto = File.ReadAllText(e.RutaLog);
            Assert.Contains("COPIARÍA: ", texto);
            Assert.Contains("ctMUÑOZ", texto);
            Assert.Contains("Operación: Copiar", texto);
            Assert.Contains(".bak encontrados: 4", texto);
            Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d  INFO: ", texto.TrimStart('\uFEFF'));
        }

        [Fact]
        public void Si_no_se_puede_escribir_junto_al_exe_el_log_va_a_temp()
        {
            using (var b = new Bitacora(Path.Combine(e.Base, "no", "existe"), 2021))
            {
                Assert.StartsWith(Path.GetTempPath(), b.RutaArchivo);
                b.Dispose();
                File.Delete(b.RutaArchivo);
            }
        }

        [Fact]
        public void Destino_traslapado_se_bloquea()
        {
            Action<string, bool> probar = (destino, bloquea) =>
            {
                Opciones o = e.Opciones();
                o.Destino = destino;
                Assert.Equal(bloquea, Validador.ValidarRutas(o).Errores.Count > 0);
            };
            probar(e.Origen, true);
            probar(Path.Combine(e.Origen, "adEMP1", "x"), true);
            probar(Path.Combine(e.Index, "respaldos"), true);
            probar(Path.Combine(e.Base, "Compac"), true); // Destino\Empresas sería el propio origen.
            probar(e.Origen.ToUpperInvariant() + Path.DirectorySeparatorChar, true);
            probar("", true);
            probar(e.Origen + "2", false); // Mismo prefijo, otra carpeta.
            probar(e.Destino, false);
        }

        [Fact]
        public void Preparar_rechaza_rutas_invalidas_sin_tocar_nada()
        {
            string[] antes = e.Foto();
            Opciones o = e.Opciones(modo: ModoOperacion.Mover);
            o.Destino = Path.Combine(e.Origen, "adEMP1");
            Assert.Throws<InvalidOperationException>(() => e.Correr(o));
            Assert.Equal(antes, e.Foto());
        }

        [Fact]
        public void Origen_inexistente_o_sin_subcarpetas_se_bloquea()
        {
            Opciones o = e.Opciones();
            o.Origen = Path.Combine(e.Base, "nada");
            Assert.NotEmpty(Validador.ValidarRutas(o).Errores);
            Directory.CreateDirectory(o.Origen);
            Assert.NotEmpty(Validador.ValidarRutas(o).Errores);
        }

        [Fact]
        public void Espacio_insuficiente_y_FAT32_se_advierten()
        {
            var plan = new PlanTrabajo { TotalBytes = 5L << 30, ArchivoMasGrande = 5L << 30 };
            Assert.Empty(Validador.EvaluarUnidad(6L << 30, "NTFS", plan));
            Assert.Empty(Validador.EvaluarUnidad(6L << 30, "exFAT", plan));
            Assert.Single(Validador.EvaluarUnidad(4L << 30, "NTFS", plan));
            Assert.Single(Validador.EvaluarUnidad(6L << 30, "FAT32", plan));
            Assert.Empty(Validador.ValidarUnidadDestino(e.Opciones(), new PlanTrabajo { TotalBytes = 1 }));
        }

        [Fact]
        public void El_progreso_termina_en_el_total()
        {
            Progreso ultimo = null;
            long anterior = 0;
            e.Correr(e.Opciones(), p => { Assert.True(p.BytesProcesados >= anterior); anterior = p.BytesProcesados; ultimo = p; });
            Assert.Equal((3, 3), (ultimo.Numero, ultimo.Total));
            Assert.Equal(ultimo.BytesTotales, ultimo.BytesProcesados);
        }

        [Fact]
        public void El_progreso_llega_al_total_aunque_se_omita_o_falle()
        {
            string existente = e.EnDestino(Esperados2021[0]);
            Directory.CreateDirectory(Path.GetDirectoryName(existente));
            File.WriteAllText(existente, "previo");
            File.WriteAllText(e.EnDestino("Empresas/ctEMP2"), ""); // Ocupa el lugar de la carpeta: ctEMP2 falla.
            Progreso ultimo = null;
            Resultado r = e.Correr(e.Opciones(), p => ultimo = p);
            Assert.Equal((1, 1, 1), (r.Omitidos, r.Errores, r.Procesados));
            Assert.Equal(ultimo.BytesTotales, ultimo.BytesProcesados);
        }

        [Fact]
        public void Index_se_propone_junto_al_origen()
        {
            Assert.Equal(e.Index, Rutas.IndexPropuesto(e.Origen + Path.DirectorySeparatorChar));
        }

        /// <summary>Lenta: solo corre con RESPALDOS_PRUEBA_4GB=1.</summary>
        [Fact]
        public void Archivo_mayor_a_4GB_comprimido_ZIP64()
        {
            if (Environment.GetEnvironmentVariable("RESPALDOS_PRUEBA_4GB") != "1") return;
            const long tamano = (4L << 30) + 12345;
            string bak = e.CrearBak("ggENORME/Enorme.bak", 2024);
            using (var f = new FileStream(bak, FileMode.Open)) f.SetLength(tamano);
            File.SetLastWriteTime(bak, new DateTime(2024, 5, 5));

            Resultado r = e.Correr(e.Opciones(2024, ModoOperacion.Mover, true));
            Assert.Equal((1, 0), (r.Procesados, r.Errores));
            Assert.False(File.Exists(bak));
            using (var zip = new ZipArchive(File.OpenRead(e.EnDestino("Empresas/ggENORME/Enorme.bak.zip"))))
            {
                long leidos = 0;
                var bufer = new byte[1 << 20];
                int n;
                using (Stream s = zip.Entries.Single().Open())
                    while ((n = s.Read(bufer, 0, bufer.Length)) > 0) leidos += n;
                Assert.Equal(tamano, leidos);
            }
        }
    }
}
