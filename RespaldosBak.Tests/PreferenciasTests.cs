using System;
using System.IO;
using RespaldosBak.Config;
using RespaldosBak.Core;
using Xunit;

namespace RespaldosBak.Tests
{
    public class PreferenciasTests : IDisposable
    {
        private readonly Escenario e = new Escenario();
        public void Dispose() { e.Dispose(); }

        [Theory]
        [InlineData(ModoOperacion.Mover, true, false)]
        [InlineData(ModoOperacion.Copiar, false, true)]
        public void Guarda_y_recupera_todo_con_acentos(ModoOperacion modo, bool comprimir, bool simular)
        {
            var o = new Opciones
            {
                Origen = @"C:\Compañía\Empresas=1", Index = @"C:\Índice", Destino = @"E:\Año nuevo",
                Anio = 2022, Modo = modo, Comprimir = comprimir, Simular = simular
            };
            Preferencias.Guardar(e.Base, o);
            Opciones r = Preferencias.Cargar(e.Base, 2025);
            Assert.Equal((o.Origen, o.Index, o.Destino, o.Anio), (r.Origen, r.Index, r.Destino, r.Anio));
            Assert.Equal((modo, comprimir, simular), (r.Modo, r.Comprimir, r.Simular));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("basura\n=\n\nAnio=1999\nOperacion=x\nSimular=\nComprimir=quizá")]
        [InlineData("Anio=99999999999999")]
        public void Ini_ausente_o_danado_usa_los_valores_seguros(string contenido)
        {
            if (contenido != null) File.WriteAllText(Path.Combine(e.Base, Preferencias.NombreArchivo), contenido);
            Opciones r = Preferencias.Cargar(e.Base, 2025);
            Assert.Equal((2025, ModoOperacion.Copiar, false, true), (r.Anio, r.Modo, r.Comprimir, r.Simular));
            Assert.Equal(("", "", ""), (r.Origen, r.Index, r.Destino));
        }

        [Fact]
        public void Si_no_se_puede_escribir_se_ignora()
        {
            Preferencias.Guardar(Path.Combine(e.Base, "no", "existe"), e.Opciones());
            Assert.Equal("", Preferencias.Cargar(Path.Combine(e.Base, "no", "existe"), 2025).Origen);
        }
    }
}
