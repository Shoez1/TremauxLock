using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TremauxLock.App.Services;

/// <summary>
/// Ponte entre os ViewModels do WPF e o VaultService do TremauxLock.Core.
/// Garante que o diretório de payload 'private' seja sempre resolvido e criado
/// exatamente no mesmo diretorio onde o arquivo .exe esta sendo executado.
/// </summary>
public sealed class VaultBridgeService
{
    private readonly VaultService _inner;

    public string ApplicationDirectory => _inner.ApplicationDirectory;
    public string WorkingFolderPath => _inner.WorkingFolderPath;
    public string LockedFolderPath => _inner.LockedFolderPath;
    public string MetadataPath => _inner.MetadataPath;

    public VaultBridgeService()
    {
        // Obtem o diretorio real onde o executavel (.exe) esta no disco
        string appDir = ResolveExecutableDirectory();
        
        _inner = new VaultService(appDir);
        _inner.EnsureWorkspace();
        
        // Garante que a pasta 'private' exista se o payload nao estiver ofuscado
        EnsureWorkingFolderExists();
    }

    /// <summary>
    /// Resolve com precisao o caminho do diretorio onde o executavel .exe esta no Windows,
    /// mesmo em publicacoes Single-File (.NET bundle).
    /// </summary>
    public static string ResolveExecutableDirectory()
    {
        // 1. Environment.ProcessPath aponta com precisao para o .exe real no disco
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            string? dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                return dir;
            }
        }

        // 2. Process.GetCurrentProcess().MainModule?.FileName
        try
        {
            using var proc = Process.GetCurrentProcess();
            string? modulePath = proc.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(modulePath))
            {
                string? dir = Path.GetDirectoryName(modulePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    return dir;
                }
            }
        }
        catch { }

        // 3. AppContext.BaseDirectory como fallback
        return AppContext.BaseDirectory;
    }

    public void EnsureWorkingFolderExists()
    {
        try
        {
            // Se o payload nao esta ofuscado e nem existe metadados de bloqueio,
            // garante a criacao da pasta 'private' ao lado do exe
            if (!Directory.Exists(_inner.LockedFolderPath) && !File.Exists(_inner.MetadataPath))
            {
                if (!Directory.Exists(_inner.WorkingFolderPath))
                {
                    Directory.CreateDirectory(_inner.WorkingFolderPath);
                    VaultSecurity.TryHardenPath(_inner.WorkingFolderPath);
                }
            }
        }
        catch { }
    }

    // ── Overview ───────────────────────────────────────────────────────────

    public Task<VaultOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            
            // Garante a existencia da pasta de trabalho se aplicavel
            EnsureWorkingFolderExists();

            var ov = _inner.GetOverview();
            return new VaultOverviewDto
            {
                State = MapState(ov.State),
                FileCount = ov.FileCount,
                TotalSize = VaultCrypto.FormatSize(ov.TotalBytes),
                LockedAt = ov.LockedAtUtc.HasValue
                    ? ov.LockedAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                    : string.Empty,
                WorkingFolderPath = ov.WorkingFolderPath,
                PasswordHint = ov.PasswordHint
            };
        }, ct);
    }

    // ── Lock ───────────────────────────────────────────────────────────────

    public Task<LockResultDto> LockAsync(string password, CancellationToken ct = default)
    {
        return LockAsync(password, null, ct);
    }

    public Task<LockResultDto> LockAsync(string password, string? passwordHint, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var result = string.IsNullOrWhiteSpace(passwordHint)
                ? await _inner.LockVaultAsync(password, progress: null, ct)
                : await _inner.LockVaultWithHintAsync(password, passwordHint, progress: null, ct);
            return new LockResultDto
            {
                RecoveryKey = result.RecoveryKey,
                BackupWarning = result.BackupWarning
            };
        }, ct);
    }

    public Task<string?> GetPasswordHintAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return _inner.TryGetPasswordHint();
        }, ct);
    }

    // ── Unlock ─────────────────────────────────────────────────────────────

    public Task<UnlockResultDto> UnlockAsync(string password, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var result = await _inner.UnlockVaultWithPasswordAsync(password, progress: null, ct);
            return new UnlockResultDto { BackupWarning = result.BackupWarning };
        }, ct);
    }

    public Task<UnlockResultDto> UnlockWithRecoveryKeyAsync(string recoveryKey, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var result = await _inner.UnlockVaultWithRecoveryKeyAsync(recoveryKey, progress: null, ct);
            return new UnlockResultDto { BackupWarning = result.BackupWarning };
        }, ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ViewModels.VaultStateKind MapState(VaultState state) => state switch
    {
        VaultState.Empty => ViewModels.VaultStateKind.Empty,
        VaultState.Unlocked => ViewModels.VaultStateKind.Unlocked,
        VaultState.Locked => ViewModels.VaultStateKind.Locked,
        VaultState.Inconsistent => ViewModels.VaultStateKind.Inconsistent,
        _ => ViewModels.VaultStateKind.Inconsistent
    };
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public sealed class VaultOverviewDto
{
    public ViewModels.VaultStateKind State { get; init; }
    public int FileCount { get; init; }
    public string TotalSize { get; init; } = string.Empty;
    public string LockedAt { get; init; } = string.Empty;
    public string WorkingFolderPath { get; init; } = string.Empty;
    public string? PasswordHint { get; init; }
}

public sealed class LockResultDto
{
    public string RecoveryKey { get; init; } = string.Empty;
    public string? BackupWarning { get; init; }
}

public sealed class UnlockResultDto
{
    public string? BackupWarning { get; init; }
}
