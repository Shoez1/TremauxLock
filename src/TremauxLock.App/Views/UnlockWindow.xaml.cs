using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

        ApplyModeUI();
    }

    // ── Input handlers ─────────────────────────────────────────────────────

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_vm.UseRecoveryKey)
        {
            _vm.Secret = PasswordBox.Password;
            UnlockButton.IsEnabled = _vm.CanUnlock;
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.CanUnlock)
            _ = RunUnlockAsync();
    }

    private void RecoveryBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_vm.UseRecoveryKey)
        {
            _vm.Secret = RecoveryBox.Text.Trim();
            UnlockButton.IsEnabled = _vm.CanUnlock;
        }
    }

    private void RecoveryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.CanUnlock)
            _ = RunUnlockAsync();
    }

    // ── Mode toggle ────────────────────────────────────────────────────────

    private void TabPassword_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.UseRecoveryKey) return;
        _vm.UseRecoveryKey = false;
        SwitchMode();
    }

    private void TabRecovery_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.UseRecoveryKey) return;
        _vm.UseRecoveryKey = true;
        SwitchMode();
    }

    private void SwitchMode()
    {
        _vm.Secret = string.Empty;
        PasswordBox.Password = string.Empty;
        RecoveryBox.Text = string.Empty;
        ErrorBorder.Visibility = Visibility.Collapsed;
        UnlockButton.IsEnabled = false;
        ApplyModeUI();
    }

    private void ApplyModeUI()
    {
        bool isRecovery = _vm.UseRecoveryKey;

        // Visual dos botões de aba
        var primaryBrush = FindResource("MaterialDesign.Brush.Primary") as Brush;
        var onPrimaryBrush = Brushes.White;
        var transparentBrush = Brushes.Transparent;
        var defaultForeground = FindResource("MaterialDesign.Brush.Foreground") as Brush;

        if (!isRecovery)
        {
            TabPasswordButton.Background = primaryBrush;
            TabPasswordButton.Foreground = onPrimaryBrush;
            TabRecoveryButton.Background = transparentBrush;
            TabRecoveryButton.Foreground = defaultForeground;

            PasswordPanel.Visibility = Visibility.Visible;
            RecoveryPanel.Visibility = Visibility.Collapsed;
            TitleBlock.Text = "Desbloquear com Senha";
            SubtitleBlock.Text = "Digite a senha definida no bloqueio do cofre.";
            PasswordBox.Focus();
        }
        else
        {
            TabRecoveryButton.Background = primaryBrush;
            TabRecoveryButton.Foreground = onPrimaryBrush;
            TabPasswordButton.Background = transparentBrush;
            TabPasswordButton.Foreground = defaultForeground;

            PasswordPanel.Visibility = Visibility.Collapsed;
            RecoveryPanel.Visibility = Visibility.Visible;
            TitleBlock.Text = "Recuperar Cofre";
            SubtitleBlock.Text = "Use a chave de 32 caracteres gerada quando o cofre foi bloqueado.";
            RecoveryBox.Focus();
        }
    }

    // ── Unlock ─────────────────────────────────────────────────────────────

    private async void Unlock_Click(object sender, RoutedEventArgs e) => await RunUnlockAsync();

    private async Task RunUnlockAsync()
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

            await Task.Delay(900);
            Close();
        }
    }

    // ── Actions ────────────────────────────────────────────────────────────

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
