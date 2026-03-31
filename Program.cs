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
                    $"Falha inesperada: {args.Exception.Message}",
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
                    $"Falha inesperada: {exception?.Message ?? "Erro desconhecido."}",
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
                    Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

                    {exception}
                    """);
            }
            catch
            {
            }
        }
    }
}
