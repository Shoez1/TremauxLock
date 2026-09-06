using System;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
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
                UpdateStatusAppearance();
            }
        };

        UpdateStatusAppearance();
    }

    private void UpdateStatusAppearance()
    {
        var (dotBrush, pillBgBrush, icon, heroPackIcon) = _vm.State switch
        {
            VaultStateKind.Unlocked => (
                FindResource("UnlockedBrush") as Brush ?? Brushes.Green,
                FindResource("UnlockedSoftBrush") as Brush ?? Brushes.LightGreen,
                "🔓",
                PackIconKind.LockOpenVariantOutline
            ),
            VaultStateKind.Locked => (
                FindResource("LockedBrush") as Brush ?? Brushes.Red,
                FindResource("LockedSoftBrush") as Brush ?? Brushes.MistyRose,
                "🛡️",
                PackIconKind.ShieldBugOutline
            ),
            VaultStateKind.Inconsistent => (
                FindResource("InconsistentBrush") as Brush ?? Brushes.Orange,
                FindResource("InconsistentSoftBrush") as Brush ?? Brushes.Wheat,
                "⚠️",
                PackIconKind.AlertDecagramOutline
            ),
            _ => (
                FindResource("EmptyBrush") as Brush ?? Brushes.Gray,
                FindResource("EmptySoftBrush") as Brush ?? Brushes.LightGray,
                "⚡",
                PackIconKind.CodeJson
            )
        };

        StatusDotEllipse.Fill = dotBrush;
        StatusTextBlock.Foreground = dotBrush;
        StatusPillBorder.Background = pillBgBrush;
        HeroStatusIcon.Kind = heroPackIcon;
        HeroStatusIcon.Foreground = dotBrush;
        Title = $"{icon} TremauxLock // Ofuscador Criptográfico";
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
            if (!string.IsNullOrEmpty(_vm.WorkingFolderPath))
            {
                if (!System.IO.Directory.Exists(_vm.WorkingFolderPath))
                {
                    System.IO.Directory.CreateDirectory(_vm.WorkingFolderPath);
                    VaultSecurity.TryHardenPath(_vm.WorkingFolderPath);
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _vm.WorkingFolderPath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Erro ao abrir pasta", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
