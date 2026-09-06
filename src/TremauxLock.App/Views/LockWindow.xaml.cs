using System;
using System.Windows;
using System.Windows.Media;
using TremauxLock.App.Services;
using TremauxLock.App.ViewModels;

namespace TremauxLock.App.Views;

public partial class LockWindow : Window
{
    private readonly LockViewModel _vm;
    public bool LockSucceeded { get; private set; }

    public LockWindow(VaultBridgeService vault)
    {
        InitializeComponent();
        _vm = new LockViewModel(vault);
        DataContext = _vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LockViewModel.LockSucceeded) && _vm.LockSucceeded)
            {
                LockSucceeded = true;
            }
        };
    }

    // ── Password handlers ──────────────────────────────────────────────────

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.Password = PasswordBox.Password;
        UpdateStrengthBars();
        UpdateLockButton();
    }

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.PasswordConfirm = ConfirmBox.Password;
        MismatchLabel.Visibility = _vm.PasswordsMatch || _vm.PasswordConfirm.Length == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        UpdateLockButton();
    }

    private void UpdateStrengthBars()
    {
        double strength = _vm.PasswordStrength;

        // Get colors from resources
        var weakBrush = FindResource("StrengthWeak") as Brush ?? Brushes.Red;
        var fairBrush = FindResource("StrengthFair") as Brush ?? Brushes.Orange;
        var strongBrush = FindResource("StrengthStrong") as Brush ?? Brushes.Green;
        var emptyBrush = FindResource("MaterialDesign.Brush.Divider") as Brush ?? Brushes.LightGray;

        Brush activeBrush = strength < 0.35 ? weakBrush : strength < 0.65 ? fairBrush : strongBrush;

        StrengthBar1.Fill = strength >= 0.1 ? activeBrush : emptyBrush;
        StrengthBar2.Fill = strength >= 0.35 ? activeBrush : emptyBrush;
        StrengthBar3.Fill = strength >= 0.65 ? activeBrush : emptyBrush;

        StrengthLabel.Text = _vm.Password.Length == 0 ? string.Empty : _vm.PasswordStrengthText;
    }

    private void UpdateLockButton()
    {
        LockConfirmButton.IsEnabled = _vm.CanLock;
    }

    // ── Actions ────────────────────────────────────────────────────────────

    private async void LockConfirm_Click(object sender, RoutedEventArgs e)
    {
        ErrorBorder.Visibility = Visibility.Collapsed;
        BusyOverlay.Visibility = Visibility.Visible;
        LockConfirmButton.IsEnabled = false;

        await _vm.LockVaultCommand.ExecuteAsync(null);

        BusyOverlay.Visibility = Visibility.Collapsed;

        if (_vm.ErrorMessage != null)
        {
            ErrorText.Text = _vm.ErrorMessage;
            ErrorBorder.Visibility = Visibility.Visible;
            LockConfirmButton.IsEnabled = _vm.CanLock;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void CopyKey_Click(object sender, RoutedEventArgs e)
    {
        _vm.CopyRecoveryKeyCommand.Execute(null);
        CopyKeyLabel.Text = "Copiado!";
        // Reset label after 3s
        _ = System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() => CopyKeyLabel.Text = "Copiar Chave");
        });
    }
}
