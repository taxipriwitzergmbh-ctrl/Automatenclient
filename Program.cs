using System;
using System.IO;
using System.Windows.Forms;

namespace TaMi_Kassenclient
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => HandleException(e.Exception, "ThreadException");
                AppDomain.CurrentDomain.UnhandledException += (s, e) => HandleException(e.ExceptionObject as Exception, "UnhandledException");

                if (AppSettings.AutomatenNamen.Count == 0)
                    AppSettings.LoadAutomatenNamenFromIni();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MenueForm());
            }
            catch (Exception ex)
            {
                HandleException(ex, "MainCatch");
            }
        }

        private static void HandleException(Exception ex, string source)
        {
            try
            {
                if (ex == null) return;
                var time = DateTime.Now;
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SuE-Software", "TaMi-Kassenclient", "logs");
                Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, $"error_{time:yyyyMMdd_HHmmss}_{source}.log");
                File.WriteAllText(logFile, ex.ToString());
                MessageBox.Show($"Fehler beim Start ({source}):\n\n{ex.Message}\n\nDetails: {logFile}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Fallback, wenn Logging selbst scheitert
                MessageBox.Show($"Fehler: {ex}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
