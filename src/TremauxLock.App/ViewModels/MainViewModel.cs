using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TremauxLock.App.Services;

namespace TremauxLock.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly VaultBridgeService _vault;
    private CancellationTokenSource? _refreshCts;

    // ── State ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnlocked))]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsInconsistent))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusSubtext))]
    [NotifyPropertyChangedFor(nameof(StatusKind))]
    private VaultStateKind _state = VaultStateKind.Empty;

    [ObservableProperty] private int _fileCount;
    [ObservableProperty] private string _totalSize = string.Empty;
    [ObservableProperty] private string _lockedAt = string.Empty;
    [ObservableProperty] private string _workingFolderPath = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    // ── Computed ───────────────────────────────────────────────────────────

    public bool IsUnlocked => State == VaultStateKind.Unlocked;
    public bool IsLocked => State == VaultStateKind.Locked;
    public bool IsEmpty => State == VaultStateKind.Empty;
    public bool IsInconsistent => State == VaultStateKind.Inconsistent;

    public string StatusText => State switch
    {
        VaultStateKind.Unlocked => "Cofre Aberto",
        VaultStateKind.Locked => "Cofre Bloqueado",
        VaultStateKind.Empty => "Cofre Vazio",
        VaultStateKind.Inconsistent => "Estado Inconsistente",
        _ => "Desconhecido"
    };

    public string StatusSubtext => State switch
    {
        VaultStateKind.Unlocked => "Seus arquivos estão acessíveis na pasta private",
        VaultStateKind.Locked => "Seus arquivos estão criptografados e protegidos",
        VaultStateKind.Empty => "Adicione arquivos à pasta private e bloqueie",
        VaultStateKind.Inconsistent => "Verifique se há arquivos pendentes",
        _ => string.Empty
    };

    public string StatusKind => State.ToString();

    // ── Constructor ────────────────────────────────────────────────────────

    public MainViewModel(VaultBridgeService vault)
    {
        _vault = vault;
        _ = RefreshAsync();
    }

    // ── Commands ───────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", WorkingFolderPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Não foi possível abrir a pasta: {ex.Message}";
        }
    }

    private bool CanOpenFolder() => IsUnlocked;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();

        try
        {
            var overview = await _vault.GetOverviewAsync(_refreshCts.Token);
            State = overview.State;
            FileCount = overview.FileCount;
            TotalSize = overview.TotalSize;
            LockedAt = overview.LockedAt;
            WorkingFolderPath = overview.WorkingFolderPath;
            ErrorMessage = null;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    // Called by the Lock/Unlock windows after they complete their operation
    public async Task OnVaultOperationCompletedAsync()
    {
        await RefreshAsync();
    }
}

public enum VaultStateKind { Empty, Unlocked, Locked, Inconsistent }
