using System;
using System.Windows;
using System.Windows.Media;
using TremauxLock.App.Services;
using TremauxLock.App.ViewModels;

namespace TremauxLock.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly VaultBridgeService _vault;

    public MainWindow()
    {
        InitializeComponent();
        _vault = new VaultBridgeService();
        _vm = new MainViewModel(_vault);
        DataContext = _vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.State)
                                or nameof(MainViewModel.StatusKind))
            {
                UpdateStatusDot();
            }
        };

        UpdateStatusDot();
    }

    private void UpdateStatusDot()
    {
        var (brush, icon) = _vm.State switch
        {
            VaultStateKind.Unlocked => (FindResource("UnlockedBrush") as Brush, "🔓"),
            VaultStateKind.Locked => (FindResource("LockedBrush") as Brush, "🔒"),
            VaultStateKind.Inconsistent => (FindResource("InconsistentBrush") as Brush, "⚠️"),
            _ => (FindResource("EmptyBrush") as Brush, "📂")
        };

        StatusDotEllipse.Fill = brush;
        Title = $"{icon} TremauxLock";
    }

    // ── Button event handlers ──────────────────────────────────────────────

    private async void LockButton_Click(object sender, RoutedEventArgs e)
    {
        var lockWindow = new LockWindow(_vault);
        lockWindow.Owner = this;
        lockWindow.ShowDialog();

        if (lockWindow.LockSucceeded)
            await _vm.OnVaultOperationCompletedAsync();
    }

    private async void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        var unlockWindow = new UnlockWindow(_vault, useRecoveryKey: false);
        unlockWindow.Owner = this;
        unlockWindow.ShowDialog();

        if (unlockWindow.UnlockSucceeded)
            await _vm.OnVaultOperationCompletedAsync();
    }

    private async void RecoveryButton_Click(object sender, RoutedEventArgs e)
    {
        var unlockWindow = new UnlockWindow(_vault, useRecoveryKey: true);
        unlockWindow.Owner = this;
        unlockWindow.ShowDialog();

        if (unlockWindow.UnlockSucceeded)
            await _vm.OnVaultOperationCompletedAsync();
    }

    private void OpenFolderEmpty_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", _vm.WorkingFolderPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
