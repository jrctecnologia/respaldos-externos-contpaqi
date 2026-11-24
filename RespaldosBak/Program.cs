using System;
using System.Windows.Forms;
using RespaldosBak.Core;

namespace RespaldosBak
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Provisional: la ventana principal llega en la etapa 2.
            MessageBox.Show(
                "Respaldos BAK CONTPAQi " + Procesador.VersionApp + "\n\nLa interfaz todavía está en desarrollo.",
                "Respaldos BAK CONTPAQi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
