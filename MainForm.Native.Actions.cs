using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed partial class MainForm
    {
        private async Task HandlePrimaryActionAsync()
        {
            if (isBusy || currentOverview == null) return;
            if (currentOverview.State == VaultState.Locked) { await UnlockVaultAsync(false); return; }
            if (currentOverview.State == VaultState.Empty) { OpenWorkingFolder(); return; }
            if (currentOverview.State == VaultState.Inconsistent) { OpenFolder(vaultService.ApplicationDirectory); return; }
            await LockVaultAsync();
        }

        private void HandleSecondaryAction()
        {
            if (isBusy || currentOverview == null) return;

            if (currentOverview.State == VaultState.Unlocked)
            {
                OpenWorkingFolder();
                return;
            }

            OpenFolder(vaultService.ApplicationDirectory);
        }

        private async Task LockVaultAsync()
        {
            using var dialog = new CredentialDialog(CredentialDialogMode.CreatePassword);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            SetBusy(true, "PROTEGENDO ARQUIVOS");
            var progress = new Progress<VaultProgress>(UpdateProgress);

            try
            {
                LockResult result = await vaultService.LockVaultAsync(dialog.Secret, progress);
                RefreshOverview();
                using var recoveryDialog = new RecoveryKeyDialog(result.RecoveryKey, result.FileCount, result.TotalBytes, result.BackupWarning);
                recoveryDialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, string.Empty);
                RefreshOverview();
            }
        }

        private async Task UnlockVaultAsync(bool useRecoveryKey)
        {
            if (DateTime.UtcNow < unlockCooldownUntilUtc) { RefreshCooldownState(); return; }

            using var dialog = new CredentialDialog(useRecoveryKey ? CredentialDialogMode.UnlockWithRecoveryKey : CredentialDialogMode.UnlockWithPassword);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            SetBusy(true, useRecoveryKey ? "VALIDANDO CHAVE" : "VALIDANDO SENHA");
            var progress = new Progress<VaultProgress>(UpdateProgress);

            try
            {
                UnlockResult result = useRecoveryKey
                    ? await vaultService.UnlockVaultWithRecoveryKeyAsync(dialog.Secret, progress)
                    : await vaultService.UnlockVaultWithPasswordAsync(dialog.Secret, progress);

                failedUnlockAttempts = 0;
                unlockCooldownUntilUtc = DateTime.MinValue;
                RefreshOverview();
                MessageBox.Show($"Cofre restaurado.\n\nArquivos: {result.FileCount}\nVolume: {VaultCrypto.FormatSize(result.TotalBytes)}", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (VaultAuthenticationException ex) { RegisterUnlockFailure(ex.Message); }
            catch (VaultIntegrityException ex) { MessageBox.Show(ex.Message, "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally
            {
                SetBusy(false, string.Empty);
                RefreshOverview();
            }
        }

        private void OpenWorkingFolder() => OpenFolder(vaultService.WorkingFolderPath);

        private void OpenFolder(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetBusy(bool busy, string text)
        {
            isBusy = busy;
            if (busy)
            {
                lblProgress.Text = text;
                lblProgress.Visible = true;
                progressTrack.Visible = true;
                lastProgressCurrent = 0;
                lastProgressTotal = 1;
            }
            else if (DateTime.UtcNow >= unlockCooldownUntilUtc)
            {
                lblProgress.Visible = false;
                progressTrack.Visible = false;
            }

            ApplyInteractiveState();
            LayoutControls();
        }

        private void UpdateProgress(VaultProgress progress)
        {
            lastProgressTotal = Math.Max(1, progress.Total);
            lastProgressCurrent = Math.Min(lastProgressTotal, Math.Max(0, progress.Current));
            lblProgress.Text = progress.Total > 0 ? $"{progress.Step.ToUpperInvariant()} ({progress.Current}/{progress.Total})" : progress.Step.ToUpperInvariant();
            lblProgress.Visible = true;
            progressTrack.Visible = true;
            UpdateProgressFill();
            LayoutControls();
        }

        private void UpdateProgressFill()
        {
            if (!progressTrack.Visible || progressTrack.Width <= 0)
            {
                progressFill.SetBounds(0, 0, 0, progressTrack.Height);
                return;
            }

            int fillWidth = Math.Max(8, (int)Math.Round(progressTrack.Width * (lastProgressCurrent / (double)Math.Max(1, lastProgressTotal))));
            progressFill.SetBounds(0, 0, Math.Min(progressTrack.Width, fillWidth), progressTrack.Height);
        }

        private void RegisterUnlockFailure(string message)
        {
            failedUnlockAttempts++;
            if (failedUnlockAttempts >= 5)
            {
                failedUnlockAttempts = 0;
                unlockCooldownUntilUtc = DateTime.UtcNow.AddSeconds(15);
                unlockCooldownTimer.Start();
                lblProgress.Text = $"{message.ToUpperInvariant()}  AGUARDE 15 SEGUNDOS.";
                lblProgress.Visible = true;
                progressTrack.Visible = false;
                ApplyInteractiveState();
                LayoutControls();
                MessageBox.Show($"{message}\n\nNovas tentativas foram pausadas por 15 segundos.", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(message, "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void RefreshCooldownState()
        {
            if (isBusy) return;
            if (DateTime.UtcNow >= unlockCooldownUntilUtc)
            {
                unlockCooldownTimer.Stop();
                if (!progressTrack.Visible) { lblProgress.Visible = false; lblProgress.Text = string.Empty; }
                ApplyInteractiveState();
                LayoutControls();
                return;
            }

            TimeSpan remaining = unlockCooldownUntilUtc - DateTime.UtcNow;
            lblProgress.Text = $"TENTE NOVAMENTE EM {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))}S";
            lblProgress.Visible = true;
            progressTrack.Visible = false;
            ApplyInteractiveState();
            LayoutControls();
        }

        private void ApplyInteractiveState()
        {
            bool cooldownActive = currentOverview?.State == VaultState.Locked && DateTime.UtcNow < unlockCooldownUntilUtc;
            btnPrimary.Enabled = !isBusy && currentOverview?.State != VaultState.Inconsistent && !cooldownActive;
            btnSecondary.Enabled = !isBusy;
            btnRecovery.Enabled = !isBusy && btnRecovery.Visible && !cooldownActive;
        }
    }
}
