using System;
using System.Windows;
using System.Windows.Input;
using TremauxLock.App.Services;
using TremauxLock.App.ViewModels;

namespace TremauxLock.App.Views;

public partial class UnlockWindow : Window
{
    private readonly UnlockViewModel _vm;
    public bool UnlockSucceeded { get; private set; }

    public UnlockWindow(VaultBridgeService vault, bool useRecoveryKey = false)
    {
        InitializeComponent();
        _vm = new UnlockViewModel(vault);
        _vm.UseRecoveryKey = useRecoveryKey;
        DataContext = _vm;

        ApplyMode();
    }

    // ── Input handlers ─────────────────────────────────────────────────────

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.Secret = PasswordBox.Password;
        UnlockButton.IsEnabled = _vm.CanUnlock;
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.CanUnlock)
            _ = RunUnlockAsync();
    }

    private void RecoveryBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _vm.Secret = RecoveryBox.Text;
        UnlockButton.IsEnabled = _vm.CanUnlock;
    }

    private void RecoveryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.CanUnlock)
            _ = RunUnlockAsync();
    }

    // ── Mode toggle ────────────────────────────────────────────────────────

    private void ToggleMode_Click(object sender, RoutedEventArgs e)
    {
        _vm.UseRecoveryKey = !_vm.UseRecoveryKey;
        _vm.Secret = string.Empty;
        PasswordBox.Password = string.Empty;
        RecoveryBox.Text = string.Empty;
        ErrorBorder.Visibility = Visibility.Collapsed;
        UnlockButton.IsEnabled = false;
        ApplyMode();
    }

    private void ApplyMode()
    {
        bool isRecovery = _vm.UseRecoveryKey;

        PasswordBox.Visibility = isRecovery ? Visibility.Collapsed : Visibility.Visible;
        RecoveryBox.Visibility = isRecovery ? Visibility.Visible : Visibility.Collapsed;
        ToggleModeButton.Content = isRecovery
            ? "Usar senha em vez disso"
            : "Usar chave de recuperação";
        TitleBlock.Text = isRecovery ? "Recuperar Cofre" : "Desbloquear Cofre";
        SubtitleBlock.Text = isRecovery
            ? "Digite a chave de recuperação gerada no momento do bloqueio."
            : "Digite sua senha para acessar os arquivos.";

        if (isRecovery)
            RecoveryBox.Focus();
        else
            PasswordBox.Focus();
    }

    // ── Unlock ─────────────────────────────────────────────────────────────

    private async void Unlock_Click(object sender, RoutedEventArgs e) => await RunUnlockAsync();

    private async System.Threading.Tasks.Task RunUnlockAsync()
    {
        ErrorBorder.Visibility = Visibility.Collapsed;
        BusyPanel.Visibility = Visibility.Visible;
        UnlockButton.IsEnabled = false;

        await _vm.UnlockVaultCommand.ExecuteAsync(null);

        BusyPanel.Visibility = Visibility.Collapsed;

        if (_vm.ErrorMessage != null)
        {
            ErrorText.Text = _vm.ErrorMessage;
            ErrorBorder.Visibility = Visibility.Visible;
            UnlockButton.IsEnabled = _vm.CanUnlock;
        }
        else if (_vm.UnlockSucceeded)
        {
            UnlockSucceeded = true;
            SuccessBorder.Visibility = Visibility.Visible;

            // Auto-close after brief success display
            await System.Threading.Tasks.Task.Delay(800);
            Close();
        }
    }

    // ── Actions ────────────────────────────────────────────────────────────

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
