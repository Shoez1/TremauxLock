using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace TremauxLock.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Global crash handlers — write to crash.log next to the exe
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            WriteCrashLog(ex.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, ex) =>
        {
            WriteCrashLog(ex.Exception);
            ex.Handled = true;
            MessageBox.Show(
                $"Erro inesperado:\n\n{ex.Exception.Message}\n\nDetalhes em crash.log",
                "TremauxLockPro — Erro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        };

        base.OnStartup(e);

        try
        {
            ApplySystemTheme();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
        }
    }

    private static void WriteCrashLog(Exception? ex)
    {
        try
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
        }
        catch { }
    }

    private static void ApplySystemTheme()
    {
        // Follow Windows dark/light mode setting
        bool isDark = IsWindowsDarkMode();
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(theme);
    }

    private static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intVal && intVal == 0;
        }
        catch
        {
            return false; // Default to light
        }
    }
}
