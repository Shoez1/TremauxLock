using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace TremauxLock
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
            string appDirectory = Directory.Exists(executablePath)
                ? executablePath
                : Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
            string errorLogPath = Path.Combine(appDirectory, "tremauxlock-error.log");

            Application.ThreadException += (_, args) =>
            {
                TryWriteErrorLog(errorLogPath, args.Exception);
                MessageBox.Show(
                    "Falha inesperada. Um registro tecnico foi salvo sem detalhes sensiveis.",
                    "TremauxLock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                if (exception != null)
                {
                    TryWriteErrorLog(errorLogPath, exception);
                }

                MessageBox.Show(
                    "Falha inesperada. Um registro tecnico foi salvo sem detalhes sensiveis.",
                    "TremauxLock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            var vaultService = new VaultService(appDirectory);
            vaultService.EnsureWorkspace();

            Application.Run(new MainForm(vaultService));
        }

        private static void TryWriteErrorLog(string errorLogPath, Exception exception)
        {
            try
            {
                File.WriteAllText(
                    errorLogPath,
                    $"""
                    TremauxLock error log
                    TimestampUtc: {DateTime.UtcNow:O}

                    ExceptionType: {exception.GetType().FullName}
                    HResult: 0x{exception.HResult:X8}
                    Details: omitted to avoid persisting vault paths or secrets.
                    """);
            }
            catch
            {
            }
        }
    }
}
