using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TremauxLock.App.Services;

namespace TremauxLock.App.ViewModels;

public partial class LockViewModel : ObservableObject
{
    private readonly VaultBridgeService _vault;

    // ── Password fields ────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordStrength))]
    [NotifyPropertyChangedFor(nameof(PasswordStrengthText))]
    [NotifyPropertyChangedFor(nameof(PasswordStrengthColor))]
    [NotifyPropertyChangedFor(nameof(CanLock))]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLock))]
    [NotifyPropertyChangedFor(nameof(PasswordsMatch))]
    private string _passwordConfirm = string.Empty;

    // ── State ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _lockSucceeded;
    [ObservableProperty] private string _recoveryKey = string.Empty;
    [ObservableProperty] private string? _backupWarning;
    [ObservableProperty] private bool _recoveryCopied;

    // ── Computed ───────────────────────────────────────────────────────────
    public bool PasswordsMatch =>
        Password.Length == 0 || Password == PasswordConfirm;

    public bool CanLock =>
        Password.Length >= VaultCrypto.MinimumPasswordLength
        && Password == PasswordConfirm
        && !IsBusy;

    public double PasswordStrength => ComputeStrength(Password);

    public string PasswordStrengthText => PasswordStrength switch
    {
        < 0.35 => "Fraca",
        < 0.65 => "Razoável",
        _ => "Forte"
    };

    public string PasswordStrengthColor => PasswordStrength switch
    {
        < 0.35 => "StrengthWeak",
        < 0.65 => "StrengthFair",
        _ => "StrengthStrong"
    };

    public int MinPasswordLength => VaultCrypto.MinimumPasswordLength;

    // ── Constructor ────────────────────────────────────────────────────────
    public LockViewModel(VaultBridgeService vault)
    {
        _vault = vault;
    }

    // ── Commands ───────────────────────────────────────────────────────────
    [RelayCommand(CanExecute = nameof(CanLock))]
    private async Task LockVaultAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        BusyMessage = "Criptografando arquivos...";

        try
        {
            var result = await _vault.LockAsync(Password, CancellationToken.None);
            RecoveryKey = result.RecoveryKey;
            BackupWarning = result.BackupWarning;
            LockSucceeded = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
            Password = string.Empty;
            PasswordConfirm = string.Empty;
        }
    }

    [RelayCommand]
    private void CopyRecoveryKey()
    {
        if (string.IsNullOrEmpty(RecoveryKey)) return;
        Clipboard.SetText(RecoveryKey);
        RecoveryCopied = true;

        // Auto-clear clipboard after 60s
        _ = Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (Clipboard.GetText() == RecoveryKey)
                        Clipboard.Clear();
                }
                catch { }
            });
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static double ComputeStrength(string password)
    {
        if (password.Length < VaultCrypto.MinimumPasswordLength) return 0.15;

        double score = 0;
        if (password.Length >= 12) score += 0.2;
        if (password.Length >= 16) score += 0.2;
        if (System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]")) score += 0.15;
        if (System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]")) score += 0.1;
        if (System.Text.RegularExpressions.Regex.IsMatch(password, @"\d")) score += 0.15;
        if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[^A-Za-z0-9]")) score += 0.2;
        return Math.Min(score, 1.0);
    }
}
