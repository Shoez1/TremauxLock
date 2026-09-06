using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
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
        UpdateValidationUI();
    }

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.PasswordConfirm = ConfirmBox.Password;
        UpdateValidationUI();
    }

    private void UpdateValidationUI()
    {
        bool hasConfirm = !string.IsNullOrEmpty(ConfirmBox.Password);
        bool match = _vm.PasswordsMatch;

        MismatchBorder.Visibility = (hasConfirm && !match)
            ? Visibility.Visible
            : Visibility.Collapsed;

        LockConfirmButton.IsEnabled = _vm.CanLock;
    }

    private void UpdateStrengthBars()
    {
        double strength = _vm.PasswordStrength;

        var weakBrush = FindResource("StrengthWeak") as Brush ?? Brushes.Red;
        var fairBrush = FindResource("StrengthFair") as Brush ?? Brushes.Orange;
        var strongBrush = FindResource("StrengthStrong") as Brush ?? Brushes.Green;
        var emptyBrush = FindResource("MaterialDesign.Brush.Divider") as Brush ?? Brushes.LightGray;

        Brush activeBrush = strength < 0.35 ? weakBrush : strength < 0.65 ? fairBrush : strongBrush;

        StrengthBar1.Fill = strength >= 0.1 ? activeBrush : emptyBrush;
        StrengthBar2.Fill = strength >= 0.35 ? activeBrush : emptyBrush;
        StrengthBar3.Fill = strength >= 0.65 ? activeBrush : emptyBrush;

        StrengthLabel.Text = _vm.Password.Length == 0 ? string.Empty : _vm.PasswordStrengthText;
        StrengthLabel.Foreground = activeBrush;
    }

    private void GeneratePassword_Click(object sender, RoutedEventArgs e)
    {
        _vm.GenerateSecurePassword();
        PasswordBox.Password = _vm.Password;
        ConfirmBox.Password = _vm.Password;
        UpdateStrengthBars();
        UpdateValidationUI();
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
        CopyKeyLabel.Text = "Chave copiada!";
        CopyKeyIcon.Kind = PackIconKind.Check;

        _ = Task.Delay(3000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                CopyKeyLabel.Text = "Copiar Chave para Área de Transferência";
                CopyKeyIcon.Kind = PackIconKind.ContentCopy;
            });
        });
    }
}
