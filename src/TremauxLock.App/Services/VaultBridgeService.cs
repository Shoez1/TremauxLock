using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TremauxLock.App.Services;

/// <summary>
/// Thin bridge between the WPF ViewModels and the TremauxLock.Core VaultService.
/// Resolves the application directory and wraps VaultService calls with
/// progress and cancellation support.
/// </summary>
public sealed class VaultBridgeService
{
    private readonly VaultService _inner;

    public VaultBridgeService()
    {
        // The vault lives next to the executable, same as the original app
        string appDir = AppContext.BaseDirectory;
        _inner = new VaultService(appDir);
        _inner.EnsureWorkspace();
    }

    // ── Overview ───────────────────────────────────────────────────────────

    public Task<VaultOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var ov = _inner.GetOverview();
            return new VaultOverviewDto
            {
                State = MapState(ov.State),
                FileCount = ov.FileCount,
                TotalSize = VaultCrypto.FormatSize(ov.TotalBytes),
                LockedAt = ov.LockedAtUtc.HasValue
                    ? ov.LockedAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                    : string.Empty,
                WorkingFolderPath = ov.WorkingFolderPath
            };
        }, ct);
    }

    // ── Lock ───────────────────────────────────────────────────────────────

    public Task<LockResultDto> LockAsync(string password, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var result = await _inner.LockVaultAsync(password, progress: null, ct);
            return new LockResultDto
            {
                RecoveryKey = result.RecoveryKey,
                BackupWarning = result.BackupWarning
            };
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
