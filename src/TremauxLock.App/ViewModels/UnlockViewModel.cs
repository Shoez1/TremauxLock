using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TremauxLock.App.Services;

namespace TremauxLock.App.ViewModels;

public partial class UnlockViewModel : ObservableObject
{
    private readonly VaultBridgeService _vault;

    // ── Fields ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUnlock))]
    private string _secret = string.Empty;

    [ObservableProperty] private bool _useRecoveryKey;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _unlockSucceeded;
    [ObservableProperty] private string? _backupWarning;

    // ── Computed ───────────────────────────────────────────────────────────
    public bool CanUnlock => !string.IsNullOrWhiteSpace(Secret) && !IsBusy;

    public string PromptText => UseRecoveryKey
        ? "Chave Mestra de Bypass"
        : "Senha de Acesso Criptográfico";

    public string PlaceholderText => UseRecoveryKey
        ? "XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX"
        : "Digite sua senha de acesso...";

    // ── Constructor ────────────────────────────────────────────────────────
    public UnlockViewModel(VaultBridgeService vault)
    {
        _vault = vault;
    }

    // ── Commands ───────────────────────────────────────────────────────────
    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockVaultAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        BusyMessage = "Revertendo ofuscação e descriptografando payload...";

        try
        {
            UnlockResultDto result;
            if (UseRecoveryKey)
                result = await _vault.UnlockWithRecoveryKeyAsync(Secret, CancellationToken.None);
            else
                result = await _vault.UnlockAsync(Secret, CancellationToken.None);

            BackupWarning = result.BackupWarning;
            UnlockSucceeded = true;
        }
        catch (VaultAuthenticationException)
        {
            ErrorMessage = UseRecoveryKey
                ? "Chave Mestra de Bypass incorreta ou payload corrompido."
                : "Credencial incorreta. Acesso negado.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
            Secret = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        UseRecoveryKey = !UseRecoveryKey;
        Secret = string.Empty;
        ErrorMessage = null;
        OnPropertyChanged(nameof(PromptText));
        OnPropertyChanged(nameof(PlaceholderText));
    }
}
