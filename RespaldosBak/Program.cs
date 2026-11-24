using System;
using System.Windows.Forms;

namespace RespaldosBak
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(AppDomain.CurrentDomain.BaseDirectory));
        }
    }
}
