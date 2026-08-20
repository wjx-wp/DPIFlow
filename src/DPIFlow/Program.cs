using System;
using System.Threading;
using System.Windows.Forms;

namespace DPIFlow
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\DPIFlow.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("DPIFlow is already running in the system tray.", "DPIFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApplicationContext());
                GC.KeepAlive(mutex);
            }
        }
    }
}
