using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RespaldosBak.Config;
using RespaldosBak.Core;

namespace RespaldosBak
{
    public sealed class MainForm : Form
    {
        private const string OrigenHabitual = @"C:\Compac\Empresas";
        private const string Titulo = "Respaldos BAK CONTPAQi";

        internal readonly TextBox Origen = CajaRuta();
        internal readonly TextBox Index = CajaRuta();
        internal readonly TextBox Destino = CajaRuta();
        internal readonly Label AvisoIndex = new Label { AutoSize = true, ForeColor = Color.FromArgb(160, 80, 0), Margin = new Padding(3, 0, 3, 6) };
        internal readonly ComboBox Anio = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70, Anchor = AnchorStyles.Left };
        internal readonly RadioButton Copiar = new RadioButton { Text = "Copiar", AutoSize = true, Anchor = AnchorStyles.Left };
        internal readonly RadioButton Mover = new RadioButton { Text = "Mover", AutoSize = true, Anchor = AnchorStyles.Left };
        internal readonly CheckBox Comprimir = new CheckBox { Text = "Comprimir .bak a ZIP", AutoSize = true, Anchor = AnchorStyles.Left };
        internal readonly CheckBox Simular = new CheckBox { Text = "Simular (no toca ningún archivo)", AutoSize = true, Anchor = AnchorStyles.Left };
        internal readonly Button Iniciar = Boton("Iniciar");
        internal readonly Button Cancelar = Boton("Cancelar");
        internal readonly Button AbrirLog = Boton("Abrir log");
        internal readonly Button AbrirDestino = Boton("Abrir destino");
        internal readonly ProgressBar Barra = new ProgressBar { Dock = DockStyle.Fill };
        internal readonly Label Estado = new Label { AutoSize = true, Text = "Listo." };
        internal readonly TextBox Registro = new TextBox
        {
            Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill,
            MaxLength = 0, // Sin el límite de 32 767 caracteres.
            BackColor = SystemColors.Window, Font = new Font(FontFamily.GenericMonospace, 9f)
        };

        private readonly string _carpetaApp;
        private readonly TableLayoutPanel _entradas = Tabla(3);
        private string _rutaLog;
        private string _ultimoOrigen;
        private bool _ocupado;

        /// <summary>Reemplazable en las pruebas para no abrir ventanas.</summary>
        internal Func<string, MessageBoxButtons, MessageBoxIcon, DialogResult> Mensaje { get; set; }

        public MainForm(string carpetaApp)
        {
            _carpetaApp = carpetaApp;
            Mensaje = (texto, botones, icono) => MessageBox.Show(this, texto, Titulo, botones, icono,
                botones == MessageBoxButtons.OK ? MessageBoxDefaultButton.Button1 : MessageBoxDefaultButton.Button2);

            Construir();
            Cargar();

            Origen.Leave += (s, e) => OrigenElegido();
            Index.TextChanged += (s, e) => RevisarIndex();
            Iniciar.Click += async (s, e) => await IniciarAsync();
            AbrirLog.Click += (s, e) => Abrir(_rutaLog);
            AbrirDestino.Click += (s, e) => Abrir(Destino.Text.Trim());
            FormClosing += AlCerrar;
        }

        internal Opciones LeerOpciones()
        {
            return new Opciones
            {
                Origen = Origen.Text.Trim(),
                Index = Index.Text.Trim(),
                Destino = Destino.Text.Trim(),
                Anio = (int)Anio.SelectedItem,
                Modo = Mover.Checked ? ModoOperacion.Mover : ModoOperacion.Copiar,
                Comprimir = Comprimir.Checked,
                Simular = Simular.Checked
            };
        }

        internal void OrigenElegido()
        {
            string origen = Origen.Text.Trim();
            if (origen == _ultimoOrigen) return;
            _ultimoOrigen = origen;
            try { Index.Text = Rutas.IndexPropuesto(origen) ?? ""; }
            catch (Exception ex) when (EsRutaInvalida(ex)) { Index.Text = ""; }
        }

        internal async Task IniciarAsync()
        {
            if (_ocupado) return;
            OrigenElegido();
            Opciones opciones = LeerOpciones();

            // Antes de crear el log: un clic con rutas equivocadas no debe dejar archivos.
            Validacion validacion = Validador.ValidarRutas(opciones);
            if (validacion.Errores.Count > 0)
            {
                Mensaje(string.Join(Environment.NewLine, validacion.Errores), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Preferencias.Guardar(_carpetaApp, opciones);
            Registro.Clear();
            Ocupar(true);
            try
            {
                using (var bitacora = new Bitacora(_carpetaApp, opciones.Anio))
                {
                    _rutaLog = bitacora.RutaArchivo;
                    bitacora.Linea += AgregarLinea;
                    await ProcesarAsync(opciones, bitacora);
                }
            }
            catch (Exception ex)
            {
                Mensaje("No se pudo completar: " + ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Ocupar(false);
            }
        }

        private async Task ProcesarAsync(Opciones opciones, Bitacora bitacora)
        {
            var procesador = new Procesador(opciones, bitacora);
            Estado.Text = "Buscando respaldos…";
            PlanTrabajo plan = await Task.Run(() => procesador.Preparar(CancellationToken.None));
            Estado.Text = plan.Archivos.Count + " archivos .bak, " + Resultado.FormatoTamano(plan.TotalBytes);

            if (plan.Archivos.Count == 0)
            {
                bitacora.Escribir(Tipo.Info, "No se encontraron .bak de " + opciones.Anio);
                Mensaje("No se encontraron respaldos .bak de " + opciones.Anio + ".", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!Continuar(opciones, bitacora, plan))
            {
                bitacora.Escribir(Tipo.Info, "No se inició: el usuario no confirmó");
                return;
            }

            if (!opciones.Simular)
            {
                // La copia real llega en la etapa 3, junto con el progreso y la cancelación.
                bitacora.Escribir(Tipo.Info, "Copiar y mover todavía no están disponibles en esta versión; no se modificó ningún archivo");
                Mensaje("Copiar y mover todavía no están disponibles en esta versión. Por ahora usa Simular."
                        + Environment.NewLine + "No se modificó ningún archivo.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Resultado resultado = await Task.Run(() => procesador.Ejecutar(plan, null, CancellationToken.None));
            Mensaje(string.Join(Environment.NewLine, resultado.Resumen()), MessageBoxButtons.OK,
                    resultado.Errores > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        /// <summary>Advertencias de la unidad destino y confirmación de Mover.</summary>
        private bool Continuar(Opciones opciones, Bitacora bitacora, PlanTrabajo plan)
        {
            var advertencias = Validador.ValidarUnidadDestino(opciones, plan);
            foreach (string advertencia in advertencias) bitacora.Escribir(Tipo.Aviso, advertencia);
            if (opciones.Simular) return true;

            if (advertencias.Count > 0
                && Mensaje(string.Join(Environment.NewLine, advertencias) + Environment.NewLine + "¿Continuar de todos modos?",
                           MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return false;

            return opciones.Modo != ModoOperacion.Mover
                || Mensaje("Se moverán " + plan.Archivos.Count + " archivos (" + Resultado.FormatoTamano(plan.TotalBytes)
                           + "). Los originales se borrarán después de verificar la copia. ¿Continuar?",
                           MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private void Cargar()
        {
            Anio.Items.AddRange(Anios.Disponibles.Cast<object>().ToArray());
            int actual = DateTime.Now.Year;
            Opciones o = Preferencias.Cargar(_carpetaApp, Anios.Disponibles.Contains(actual) ? actual : Anios.Disponibles.Last());

            Origen.Text = o.Origen;
            Index.Text = o.Index;
            Destino.Text = o.Destino;
            Anio.SelectedItem = o.Anio;
            Mover.Checked = o.Modo == ModoOperacion.Mover;
            Copiar.Checked = !Mover.Checked;
            Comprimir.Checked = o.Comprimir;
            Simular.Checked = o.Simular;

            _ultimoOrigen = o.Origen;
            if (o.Origen.Length == 0 && Directory.Exists(OrigenHabitual))
            {
                Origen.Text = OrigenHabitual;
                OrigenElegido();
            }
            RevisarIndex();
            Ocupar(false);
        }

        private void RevisarIndex()
        {
            string index = Index.Text.Trim();
            bool existe = false;
            try { existe = index.Length > 0 && Directory.Exists(Path.Combine(index, Rutas.CarpetaRespaldos)); }
            catch (Exception ex) when (EsRutaInvalida(ex)) { }
            AvisoIndex.Text = existe || Origen.Text.Trim().Length == 0
                ? "" : "No existe Index\\respaldos; solo se procesarán los .bak.";
        }

        private void Ocupar(bool ocupado)
        {
            _ocupado = ocupado;
            _entradas.Enabled = !ocupado;
            Iniciar.Enabled = !ocupado;
            Cancelar.Enabled = false; // Se habilita en la etapa 3.
            AbrirLog.Enabled = !ocupado && _rutaLog != null;
            AbrirDestino.Enabled = !ocupado;
            if (!ocupado && Estado.Text.StartsWith("Buscando")) Estado.Text = "Listo.";
        }

        private void AgregarLinea(string linea)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => Registro.AppendText(linea + Environment.NewLine)));
        }

        private void AlCerrar(object sender, FormClosingEventArgs e)
        {
            if (_ocupado) e.Cancel = true;
            else Preferencias.Guardar(_carpetaApp, LeerOpciones());
        }

        private void Abrir(string ruta)
        {
            if (string.IsNullOrEmpty(ruta) || !(File.Exists(ruta) || Directory.Exists(ruta)))
            {
                Mensaje("No existe: " + ruta, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Process.Start(ruta); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is IOException)
            {
                Mensaje("No se pudo abrir: " + ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Examinar(TextBox caja, string descripcion, bool permiteCrear)
        {
            using (var dialogo = new FolderBrowserDialog { Description = descripcion, ShowNewFolderButton = permiteCrear })
            {
                if (Directory.Exists(caja.Text.Trim())) dialogo.SelectedPath = caja.Text.Trim();
                if (dialogo.ShowDialog(this) != DialogResult.OK) return;
                caja.Text = dialogo.SelectedPath;
                if (caja == Origen) OrigenElegido();
            }
        }

        private void Construir()
        {
            SuspendLayout();
            Font = new Font("Segoe UI", 9f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = Titulo + " " + Procesador.VersionApp;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 600);
            MinimumSize = new Size(680, 480);

            FilaRuta(0, "Origen (Empresas):", Origen, "Carpeta Empresas", false);
            FilaRuta(1, "Carpeta Index:", Index, "Carpeta Index (contiene respaldos)", false);
            _entradas.Controls.Add(AvisoIndex, 1, 2);
            FilaRuta(3, "Destino:", Destino, "Carpeta de destino", true);

            var operacion = Fila(Copiar, Mover);
            var opciones = Fila(Etiqueta("Año:"), Anio, Etiqueta("   Operación:"), operacion);
            _entradas.Controls.Add(opciones, 0, 4);
            _entradas.SetColumnSpan(opciones, 3);
            var casillas = Fila(Comprimir, Simular);
            _entradas.Controls.Add(casillas, 0, 5);
            _entradas.SetColumnSpan(casillas, 3);

            TableLayoutPanel principal = Tabla(1);
            principal.Padding = new Padding(10);
            principal.AutoSize = false;
            principal.Dock = DockStyle.Fill;
            principal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            principal.Controls.Add(_entradas, 0, 0);
            principal.Controls.Add(Fila(Iniciar, Cancelar, AbrirLog, AbrirDestino), 0, 1);
            principal.Controls.Add(Barra, 0, 2);
            principal.Controls.Add(Estado, 0, 3);
            principal.Controls.Add(Registro, 0, 4);
            for (int i = 0; i < 4; i++) principal.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            principal.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Controls.Add(principal);
            AcceptButton = Iniciar;
            ResumeLayout(true);
        }

        private void FilaRuta(int fila, string titulo, TextBox caja, string descripcion, bool permiteCrear)
        {
            if (fila == 0)
            {
                _entradas.Dock = DockStyle.Fill;
                _entradas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                _entradas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _entradas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }
            Button examinar = Boton("Examinar…");
            examinar.Click += (s, e) => Examinar(caja, descripcion, permiteCrear);
            _entradas.Controls.Add(Etiqueta(titulo), 0, fila);
            _entradas.Controls.Add(caja, 1, fila);
            _entradas.Controls.Add(examinar, 2, fila);
        }

        private static bool EsRutaInvalida(Exception ex)
        {
            return ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException;
        }

        private static TextBox CajaRuta() { return new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right }; }
        private static Label Etiqueta(string texto) { return new Label { Text = texto, AutoSize = true, Anchor = AnchorStyles.Left }; }

        private static Button Boton(string texto)
        {
            return new Button { Text = texto, AutoSize = true, Padding = new Padding(6, 1, 6, 1) };
        }

        private static FlowLayoutPanel Fila(params Control[] controles)
        {
            var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 3, 0, 3) };
            panel.Controls.AddRange(controles);
            return panel;
        }

        private static TableLayoutPanel Tabla(int columnas)
        {
            return new TableLayoutPanel { ColumnCount = columnas, AutoSize = true, Margin = new Padding(0) };
        }
    }
}
