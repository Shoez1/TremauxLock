using System;
using System.IO;
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
    public bool HasFiles => !IsEmpty;
    public bool CanLockState => IsUnlocked || IsEmpty;

    public string StatusText => State switch
    {
        VaultStateKind.Unlocked => "DESOFUSCADO // CLEAR",
        VaultStateKind.Locked => "OFUSCAÇÃO ATIVA [AES-GCM]",
        VaultStateKind.Empty => "STANDBY // SEM DADOS",
        VaultStateKind.Inconsistent => "ANOMALIA // INCONSISTENTE",
        _ => "DESCONHECIDO"
    };

    public string StatusSubtext => State switch
    {
        VaultStateKind.Unlocked => "Payload em texto claro acessível na pasta private",
        VaultStateKind.Locked => "Payload criptografado e ofuscado em private.locked com AES-256 GCM",
        VaultStateKind.Empty => "Adicione arquivos na pasta private para executar a ofuscação",
        VaultStateKind.Inconsistent => "Inconsistência detectada entre payload e metadados",
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
            if (!string.IsNullOrEmpty(WorkingFolderPath))
            {
                if (!Directory.Exists(WorkingFolderPath))
                {
                    Directory.CreateDirectory(WorkingFolderPath);
                    VaultSecurity.TryHardenPath(WorkingFolderPath);
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = WorkingFolderPath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Não foi possível abrir a pasta: {ex.Message}";
        }
    }

    private bool CanOpenFolder() => !IsLocked;

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
