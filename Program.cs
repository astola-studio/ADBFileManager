using System;
using System.Windows.Forms;

namespace ADBFileManager
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the ADB File Manager application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
