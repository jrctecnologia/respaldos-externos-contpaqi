#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RespaldosBak.Config;
using RespaldosBak.Core;
using Xunit;

namespace RespaldosBak.Tests
{
    public class VentanaTests : IDisposable
    {
        private readonly Escenario e = new Escenario();
        private readonly string _app;
        private readonly List<string> _mensajes = new List<string>();
        private DialogResult _respuesta = DialogResult.Yes;

        public VentanaTests()
        {
            _app = Path.Combine(e.Base, "app");
            Directory.CreateDirectory(_app);
        }

        public void Dispose() { e.Dispose(); }

        [Fact]
        public void Abre_con_valores_seguros()
        {
            Con(f =>
            {
                Assert.True(f.Copiar.Checked);
                Assert.False(f.Mover.Checked);
                Assert.True(f.Simular.Checked);
                Assert.False(f.Comprimir.Checked);
                Assert.False(f.Cancelar.Enabled);
                Assert.False(f.AbrirLog.Enabled);
                Assert.Equal(Anios.Disponibles, f.Anio.Items.Cast<int>());
                Assert.NotNull(f.Anio.SelectedItem);
            });
        }

        [Fact]
        public void Copiar_y_Mover_son_excluyentes()
        {
            Con(f =>
            {
                f.Mover.Checked = true;
                Assert.False(f.Copiar.Checked);
                Assert.Equal(ModoOperacion.Mover, f.LeerOpciones().Modo);
            });
        }

        [Fact]
        public void Elegir_origen_autollena_Index_y_respeta_lo_editado()
        {
            Con(f =>
            {
                f.Origen.Text = e.Origen;
                f.OrigenElegido();
                Assert.Equal(e.Index, f.Index.Text);
                Assert.Equal("", f.AvisoIndex.Text);

                f.Index.Text = Path.Combine(e.Base, "otro");
                Assert.Contains("No existe", f.AvisoIndex.Text);
                f.OrigenElegido();
                Assert.Equal(Path.Combine(e.Base, "otro"), f.Index.Text);
            });
        }

        [Fact]
        public void No_recuerda_nada_al_cerrar()
        {
            Con(f =>
            {
                Llenar(f);
                f.Mover.Checked = true;
                f.Simular.Checked = false;
                f.Close();
            });
            Con(f =>
            {
                Assert.Equal("", f.Destino.Text);
                Assert.True(f.Copiar.Checked && f.Simular.Checked);
            });
            Assert.Empty(Directory.GetFiles(_app));
        }

        [Fact]
        public void Rutas_invalidas_avisan_y_no_crean_log()
        {
            Con(f =>
            {
                f.Origen.Text = e.Origen;
                f.Destino.Text = Path.Combine(e.Origen, "adEMP1");
                Esperar(f.IniciarAsync());
            });
            Assert.Contains("El destino y el origen", Assert.Single(_mensajes));
            Assert.Empty(Directory.GetFiles(_app, "*.log"));
        }

        [Fact]
        public void Simular_corre_completo_sin_tocar_nada()
        {
            string[] antes = e.Foto();
            Con(f =>
            {
                Llenar(f);
                Esperar(f.IniciarAsync());
                Assert.Contains("COPIARÍA: ", f.Registro.Text);
                Assert.True(f.Iniciar.Enabled);
                Assert.True(f.AbrirLog.Enabled);
                Capturar(f);
            });
            Assert.Contains(".bak encontrados: 3", Assert.Single(_mensajes));
            Assert.Equal(antes, e.Foto());
            Assert.False(Directory.Exists(e.Destino));
            Assert.Single(Directory.GetFiles(_app, "*.log"));
        }

        [Fact]
        public void Copiar_procesa_y_llena_la_barra()
        {
            string[] antes = e.Foto();
            Con(f =>
            {
                Llenar(f);
                f.Simular.Checked = false;
                Esperar(f.IniciarAsync());
                Assert.Equal(f.Barra.Maximum, f.Barra.Value);
                Assert.Equal("Terminado.", f.Estado.Text);
                Assert.Contains("COPIADO: ", f.Registro.Text);
                Assert.False(f.Cancelar.Enabled);
                Assert.True(f.Origen.Enabled);
            });
            Assert.Contains(".bak procesados: 3", Assert.Single(_mensajes));
            Assert.Equal(antes, e.Foto());
            Assert.Equal(5, e.ArchivosDestino().Length);
        }

        [Theory]
        [InlineData(DialogResult.No)]
        [InlineData(DialogResult.Yes)]
        public void Mover_pide_confirmacion(DialogResult respuesta)
        {
            _respuesta = respuesta;
            string[] antes = e.Foto();
            Con(f =>
            {
                Llenar(f);
                f.Mover.Checked = true;
                f.Simular.Checked = false;
                Esperar(f.IniciarAsync());
            });
            Assert.StartsWith("Se moverán 3 archivos (", _mensajes[0]);
            Assert.EndsWith("Los originales se borrarán después de verificar la copia. ¿Continuar?", _mensajes[0]);
            if (respuesta == DialogResult.Yes)
            {
                Assert.Equal(2, _mensajes.Count);
                Assert.Equal(5, e.ArchivosDestino().Length);
                Assert.Equal(antes.Length - 5, e.Foto().Length);
            }
            else
            {
                Assert.Single(_mensajes);
                Assert.Equal(antes, e.Foto());
                Assert.False(Directory.Exists(e.Destino));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Cancelar_a_la_mitad_bloquea_controles_y_no_deja_tmp(bool cerrandoLaVentana)
        {
            // Datos aleatorios y con compresión: tarda lo suficiente para cancelar a la mitad.
            string grande = e.CrearBak("bbGRANDE/Grande.bak", 2021, 150 * 1024 * 1024);
            Con(f =>
            {
                Llenar(f);
                f.Mover.Checked = true;
                f.Comprimir.Checked = true;
                f.Simular.Checked = false;
                Task tarea = f.IniciarAsync();
                DateTime limite = DateTime.Now.AddSeconds(30);
                while (!f.Estado.Text.Contains("Grande.bak") && DateTime.Now < limite)
                {
                    Application.DoEvents();
                    Thread.Sleep(1);
                }
                Assert.False(f.Origen.Enabled);
                Assert.False(f.Iniciar.Enabled);
                Assert.True(f.Cancelar.Enabled);

                if (cerrandoLaVentana)
                {
                    f.Close();
                    Assert.False(f.IsDisposed);
                }
                else
                {
                    f.Cancelar.PerformClick();
                }
                Esperar(tarea);
                Assert.Equal("Cancelado.", f.Estado.Text);
                Assert.True(f.Iniciar.Enabled);
            });
            Assert.Contains("CANCELADO", _mensajes.Last());
            Assert.Equal(cerrandoLaVentana, _mensajes.Any(m => m.Contains("Hay un proceso en curso")));
            Assert.Equal(150 * 1024 * 1024, new FileInfo(grande).Length);
            Assert.DoesNotContain(e.ArchivosDestino(), a => a.EndsWith(".tmp") || a.Contains("Grande"));
        }

        [Fact]
        public void Sin_resultados_avisa()
        {
            Con(f =>
            {
                Llenar(f);
                f.Anio.SelectedItem = 2027;
                Esperar(f.IniciarAsync());
            });
            Assert.Contains("No se encontraron", Assert.Single(_mensajes));
        }

        [Fact]
        public void Nada_se_encima_ni_se_corta()
        {
            Con(f =>
            {
                f.Origen.Text = e.Origen;
                f.Index.Text = Path.Combine(e.Base, "otro"); // Con el aviso de Index visible.
                foreach (Size tamano in new[] { f.Size, f.MinimumSize })
                {
                    f.Size = tamano;
                    f.PerformLayout();
                    Revisar(f);
                }
            });
        }

        private static void Revisar(Control padre)
        {
            var hijos = padre.Controls.Cast<Control>().Where(c => c.Width > 0 && c.Height > 0).ToList();
            foreach (Control c in hijos)
            {
                Assert.True(padre.ClientRectangle.Contains(c.Bounds), "Se sale: " + c + " " + c.Bounds + " de " + padre.ClientRectangle);
                if (c is Button || c is Label || c is CheckBox || c is RadioButton)
                    Assert.True(c.Width >= TextRenderer.MeasureText(c.Text, c.Font).Width - 8, "Texto cortado: " + c.Text);
                foreach (Control otro in hijos)
                    if (c != otro) Assert.False(c.Bounds.IntersectsWith(otro.Bounds), "Se enciman: " + c + " y " + otro);
                if (!(c is TextBox) && !(c is ComboBox)) Revisar(c);
            }
        }

        private void Llenar(MainForm f)
        {
            f.Origen.Text = e.Origen;
            f.OrigenElegido();
            f.Destino.Text = e.Destino;
            f.Anio.SelectedItem = 2021;
        }

        /// <summary>Imagen de la ventana para revisarla a ojo en los artefactos de GitHub Actions.</summary>
        private static void Capturar(Form f)
        {
            string ruta = Environment.GetEnvironmentVariable("RESPALDOS_CAPTURA");
            if (string.IsNullOrEmpty(ruta)) return;
            using (var imagen = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(imagen, new Rectangle(Point.Empty, f.Size));
                imagen.Save(ruta, ImageFormat.Png);
            }
        }

        private static void Esperar(Task tarea)
        {
            DateTime limite = DateTime.Now.AddSeconds(30);
            while (!tarea.IsCompleted)
            {
                if (DateTime.Now > limite) throw new TimeoutException();
                Application.DoEvents();
                Thread.Sleep(5);
            }
            Application.DoEvents(); // Líneas de la bitácora pendientes.
            tarea.GetAwaiter().GetResult();
        }

        private void Con(Action<MainForm> accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    using (var f = new MainForm(_app))
                    {
                        f.Mensaje = (texto, botones, icono) =>
                        {
                            _mensajes.Add(texto);
                            return botones == MessageBoxButtons.OK ? DialogResult.OK : _respuesta;
                        };
                        f.Show();
                        Application.DoEvents();
                        accion(f);
                    }
                }
                catch (Exception ex) { error = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
#endif
