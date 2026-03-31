using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed class MainForm : Form
    {
        private readonly VaultService vaultService;
        private readonly Panel headerPanel;
        private readonly Panel statePanel;
        private readonly Panel summaryPanel;
        private readonly Panel contentPanel;
        private readonly Panel pathPanel;
        private readonly FlowLayoutPanel actionRow;
        private readonly Label lblBrand;
        private readonly Label lblTitle;
        private readonly Label lblSubtitle;
        private readonly Label lblState;
        private readonly Label lblSummaryTitle;
        private readonly Label lblFilesLabel;
        private readonly Label lblFilesValue;
        private readonly Label lblSizeLabel;
        private readonly Label lblSizeValue;
        private readonly Label lblAccessLabel;
        private readonly Label lblAccessValue;
        private readonly Label lblContentTitle;
        private readonly Label lblPathLabel;
        private readonly Label lblPathValue;
        private readonly Label lblPathHint;
        private readonly Label lblProgress;
        private readonly Panel progressTrack;
        private readonly Panel progressFill;
        private readonly AccentButton btnPrimary;
        private readonly AccentButton btnSecondary;
        private readonly AccentButton btnRecovery;
        private readonly ToolTip pathToolTip;
        private readonly System.Windows.Forms.Timer unlockCooldownTimer;

        private VaultOverview? currentOverview;
        private bool isBusy;
        private int failedUnlockAttempts;
        private int lastProgressCurrent;
        private int lastProgressTotal = 1;
        private DateTime unlockCooldownUntilUtc = DateTime.MinValue;
        private Color statePanelBorderColor = AppTheme.Accent;

        public MainForm(VaultService vaultService)
        {
            this.vaultService = vaultService;

            Text = "TremauxLock Vault";
            MinimumSize = new Size(920, 560);
            ClientSize = new Size(1040, 620);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundTop;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.5f);

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            headerPanel = CreateBorderPanel(Color.FromArgb(15, 22, 35), Color.FromArgb(38, 52, 72));
            statePanel = CreateBorderPanel(Color.FromArgb(21, 31, 42), () => statePanelBorderColor);
            summaryPanel = CreateBorderPanel(Color.FromArgb(15, 22, 35), Color.FromArgb(38, 52, 72));
            contentPanel = CreateBorderPanel(Color.FromArgb(15, 22, 35), Color.FromArgb(38, 52, 72));
            pathPanel = CreateBorderPanel(Color.FromArgb(12, 19, 30), Color.FromArgb(42, 56, 77));

            lblBrand = CreateLabel("TremauxLock Vault", 9f, FontStyle.Bold, AppTheme.AccentSoft, headerPanel.BackColor);
            lblTitle = CreateLabel(string.Empty, 24f, FontStyle.Regular, AppTheme.TextPrimary, headerPanel.BackColor);
            lblSubtitle = CreateLabel(string.Empty, 9.5f, FontStyle.Regular, AppTheme.TextSecondary, headerPanel.BackColor);
            lblState = CreateLabel(string.Empty, 9f, FontStyle.Bold, AppTheme.TextPrimary, statePanel.BackColor);
            lblSummaryTitle = CreateLabel("Resumo atual", 10f, FontStyle.Bold, AppTheme.TextPrimary, summaryPanel.BackColor);

            lblTitle.Font = AppTheme.CreateTitleFont(24f);
            lblSubtitle.AutoEllipsis = true;

            lblFilesLabel = CreateMetricLabel("Arquivos");
            lblFilesValue = CreateMetricValue();
            lblSizeLabel = CreateMetricLabel("Tamanho");
            lblSizeValue = CreateMetricValue();
            lblAccessLabel = CreateMetricLabel("Visibilidade");
            lblAccessValue = CreateMetricValue();

            lblContentTitle = CreateLabel("Pasta private", 10f, FontStyle.Bold, AppTheme.TextPrimary, contentPanel.BackColor);
            lblPathLabel = CreateLabel("CAMINHO ATUAL", 8.5f, FontStyle.Bold, AppTheme.TextSoft, pathPanel.BackColor);
            lblPathValue = CreateLabel(string.Empty, 9.75f, FontStyle.Regular, AppTheme.TextPrimary, pathPanel.BackColor);
            lblPathHint = CreateLabel(string.Empty, 8.75f, FontStyle.Regular, AppTheme.TextSoft, contentPanel.BackColor);
            lblProgress = CreateLabel(string.Empty, 8.75f, FontStyle.Regular, AppTheme.TextSoft, contentPanel.BackColor);

            lblPathValue.Font = AppTheme.CreateCodeFont(9.25f);
            lblPathValue.AutoEllipsis = true;
            lblPathValue.UseMnemonic = false;
            lblPathHint.AutoEllipsis = true;
            lblProgress.AutoEllipsis = true;
            lblProgress.Visible = false;

            progressTrack = new Panel
            {
                BackColor = Color.FromArgb(25, 35, 49),
                Visible = false
            };

            progressFill = new Panel
            {
                BackColor = AppTheme.Accent
            };
            progressTrack.Controls.Add(progressFill);

            btnPrimary = new AccentButton
            {
                Text = "Bloquear cofre",
                Width = 150,
                Height = 36,
                ButtonStyle = AccentButtonStyle.Primary
            };

            btnSecondary = new AccentButton
            {
                Text = "Abrir private",
                Width = 136,
                Height = 36,
                ButtonStyle = AccentButtonStyle.Secondary
            };

            btnRecovery = new AccentButton
            {
                Text = "Usar chave de recuperacao",
                Width = 198,
                Height = 34,
                ButtonStyle = AccentButtonStyle.Ghost
            };

            actionRow = new FlowLayoutPanel
            {
                AutoSize = false,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = contentPanel.BackColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            actionRow.Controls.Add(btnSecondary);
            actionRow.Controls.Add(btnPrimary);

            btnPrimary.Click += async (_, _) => await HandlePrimaryActionAsync();
            btnSecondary.Click += (_, _) => OpenRelevantFolder();
            btnRecovery.Click += async (_, _) => await UnlockVaultAsync(true);

            pathToolTip = new ToolTip();

            unlockCooldownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            unlockCooldownTimer.Tick += (_, _) => RefreshCooldownState();

            BuildUi();

            Resize += (_, _) => LayoutControls();
            Shown += (_, _) => RefreshOverview();
            Activated += (_, _) => RefreshOverview();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(11, 16, 27), Color.FromArgb(15, 22, 35), 90f);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        private void BuildUi()
        {
            headerPanel.Controls.Add(lblBrand);
            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(statePanel);

            statePanel.Controls.Add(lblState);

            summaryPanel.Controls.Add(lblSummaryTitle);
            summaryPanel.Controls.Add(lblFilesLabel);
            summaryPanel.Controls.Add(lblFilesValue);
            summaryPanel.Controls.Add(lblSizeLabel);
            summaryPanel.Controls.Add(lblSizeValue);
            summaryPanel.Controls.Add(lblAccessLabel);
            summaryPanel.Controls.Add(lblAccessValue);

            contentPanel.Controls.Add(lblContentTitle);
            contentPanel.Controls.Add(actionRow);
            contentPanel.Controls.Add(pathPanel);
            contentPanel.Controls.Add(lblPathHint);
            contentPanel.Controls.Add(btnRecovery);
            contentPanel.Controls.Add(lblProgress);
            contentPanel.Controls.Add(progressTrack);

            pathPanel.Controls.Add(lblPathLabel);
            pathPanel.Controls.Add(lblPathValue);

            Controls.Add(headerPanel);
            Controls.Add(summaryPanel);
            Controls.Add(contentPanel);

            LayoutControls();
        }

        private void LayoutControls()
        {
            int outer = 24;
            int gap = 16;
            int contentWidth = ClientSize.Width - (outer * 2);

            headerPanel.SetBounds(outer, outer, contentWidth, 102);

            int bodyTop = headerPanel.Bottom + gap;
            int bodyHeight = ClientSize.Height - bodyTop - outer;
            int panelHeight = Math.Max(280, Math.Min(340, bodyHeight));
            int summaryWidth = Math.Max(228, Math.Min(260, (int)(contentWidth * 0.26)));

            summaryPanel.SetBounds(outer, bodyTop, summaryWidth, panelHeight);
            contentPanel.SetBounds(summaryPanel.Right + gap, bodyTop, contentWidth - summaryWidth - gap, panelHeight);

            int headerInset = 20;
            lblBrand.SetBounds(headerInset, 16, headerPanel.Width - 220, 16);
            lblTitle.SetBounds(headerInset, 34, headerPanel.Width - 240, 32);
            lblSubtitle.SetBounds(headerInset, 68, headerPanel.Width - 240, 18);
            statePanel.SetBounds(headerPanel.Width - 172, 30, 148, 34);
            lblState.SetBounds(12, 8, statePanel.Width - 24, 18);

            int summaryInset = 18;
            lblSummaryTitle.SetBounds(summaryInset, 18, summaryPanel.Width - (summaryInset * 2), 18);
            lblFilesLabel.SetBounds(summaryInset, 62, summaryPanel.Width - (summaryInset * 2), 16);
            lblFilesValue.SetBounds(summaryInset, 82, summaryPanel.Width - (summaryInset * 2), 28);
            lblSizeLabel.SetBounds(summaryInset, 132, summaryPanel.Width - (summaryInset * 2), 16);
            lblSizeValue.SetBounds(summaryInset, 152, summaryPanel.Width - (summaryInset * 2), 28);
            lblAccessLabel.SetBounds(summaryInset, 202, summaryPanel.Width - (summaryInset * 2), 16);
            lblAccessValue.SetBounds(summaryInset, 222, summaryPanel.Width - (summaryInset * 2), 28);

            int contentInset = 20;
            lblContentTitle.SetBounds(contentInset, 18, 180, 18);

            int primaryWidth = btnPrimary.Width;
            int secondaryWidth = btnSecondary.Width;
            actionRow.SetBounds(
                contentPanel.Width - contentInset - primaryWidth - secondaryWidth - 8,
                14,
                primaryWidth + secondaryWidth + 8,
                40);

            btnSecondary.Margin = new Padding(0, 0, 8, 0);
            btnPrimary.Margin = Padding.Empty;

            pathPanel.SetBounds(contentInset, 64, contentPanel.Width - (contentInset * 2), 72);
            lblPathLabel.SetBounds(14, 12, pathPanel.Width - 28, 16);
            lblPathValue.SetBounds(14, 34, pathPanel.Width - 28, 22);

            lblPathHint.SetBounds(contentInset, pathPanel.Bottom + 12, contentPanel.Width - (contentInset * 2), 18);

            if (btnRecovery.Visible)
            {
                btnRecovery.SetBounds(contentInset, lblPathHint.Bottom + 12, btnRecovery.Width, btnRecovery.Height);
            }
            else
            {
                btnRecovery.SetBounds(-200, -200, 0, 0);
            }

            bool showProgress = lblProgress.Visible || progressTrack.Visible;
            int progressTop = btnRecovery.Visible ? btnRecovery.Bottom + 18 : lblPathHint.Bottom + 18;
            if (showProgress)
            {
                lblProgress.SetBounds(contentInset, progressTop, contentPanel.Width - (contentInset * 2), 16);
                progressTrack.SetBounds(contentInset, lblProgress.Bottom + 8, contentPanel.Width - (contentInset * 2), progressTrack.Visible ? 6 : 0);
            }
            else
            {
                lblProgress.SetBounds(0, 0, 0, 0);
                progressTrack.SetBounds(0, 0, 0, 0);
            }

            UpdateProgressFill();
            Invalidate();
        }

        private async Task HandlePrimaryActionAsync()
        {
            if (isBusy || currentOverview == null)
            {
                return;
            }

            if (currentOverview.State == VaultState.Locked)
            {
                await UnlockVaultAsync(false);
                return;
            }

            if (currentOverview.State == VaultState.Empty)
            {
                OpenWorkingFolder();
                return;
            }

            await LockVaultAsync();
        }

        private async Task LockVaultAsync()
        {
            using var dialog = new CredentialDialog(CredentialDialogMode.CreatePassword);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            SetBusy(true, "Protegendo arquivos...");
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
            if (DateTime.UtcNow < unlockCooldownUntilUtc)
            {
                RefreshCooldownState();
                return;
            }

            using var dialog = new CredentialDialog(
                useRecoveryKey ? CredentialDialogMode.UnlockWithRecoveryKey : CredentialDialogMode.UnlockWithPassword);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            SetBusy(true, useRecoveryKey ? "Validando chave..." : "Validando senha...");
            var progress = new Progress<VaultProgress>(UpdateProgress);

            try
            {
                UnlockResult result = useRecoveryKey
                    ? await vaultService.UnlockVaultWithRecoveryKeyAsync(dialog.Secret, progress)
                    : await vaultService.UnlockVaultWithPasswordAsync(dialog.Secret, progress);

                failedUnlockAttempts = 0;
                unlockCooldownUntilUtc = DateTime.MinValue;
                RefreshOverview();

                string successMessage = $"Cofre restaurado.\n\nArquivos: {result.FileCount}\nVolume: {VaultCrypto.FormatSize(result.TotalBytes)}";
                if (!string.IsNullOrWhiteSpace(result.BackupWarning))
                {
                    successMessage += $"\n\nAviso:\n{result.BackupWarning}";
                }

                MessageBox.Show(successMessage, "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (VaultAuthenticationException ex)
            {
                RegisterUnlockFailure(ex.Message);
            }
            catch (VaultIntegrityException ex)
            {
                MessageBox.Show(ex.Message, "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void RefreshOverview()
        {
            currentOverview = vaultService.GetOverview();

            switch (currentOverview.State)
            {
                case VaultState.Empty:
                    ApplyStateVisual("Pronto", AppTheme.Accent, Color.FromArgb(18, 32, 39));
                    lblTitle.Text = "Cofre pronto";
                    lblSubtitle.Text = "A pasta private esta pronta para receber os arquivos que voce deseja proteger.";
                    lblFilesValue.Text = "Nenhum arquivo";
                    lblSizeValue.Text = "0 B";
                    lblAccessValue.Text = "Aguardando arquivos";
                    lblPathLabel.Text = "PASTA PRIVATE";
                    lblPathValue.Text = currentOverview.WorkingFolderPath;
                    lblPathHint.Text = "Abra a pasta para adicionar conteudo antes do primeiro bloqueio.";
                    btnPrimary.Text = "Abrir private";
                    btnSecondary.Text = "Abrir pasta";
                    btnRecovery.Visible = false;
                    break;

                case VaultState.Unlocked:
                    ApplyStateVisual("Desbloqueado", AppTheme.Accent, Color.FromArgb(18, 32, 39));
                    lblTitle.Text = "Cofre desbloqueado";
                    lblSubtitle.Text = "Os arquivos estao visiveis e podem ser revisados antes do proximo bloqueio.";
                    lblFilesValue.Text = $"{currentOverview.FileCount} arquivo(s)";
                    lblSizeValue.Text = VaultCrypto.FormatSize(currentOverview.TotalBytes);
                    lblAccessValue.Text = "Visivel no disco";
                    lblPathLabel.Text = "PASTA PRIVATE";
                    lblPathValue.Text = currentOverview.WorkingFolderPath;
                    lblPathHint.Text = "Passe o mouse para ver o caminho completo ou use o atalho de abertura.";
                    btnPrimary.Text = "Bloquear cofre";
                    btnSecondary.Text = "Abrir private";
                    btnRecovery.Visible = false;
                    break;

                case VaultState.Locked:
                    ApplyStateVisual("Bloqueado", AppTheme.Warning, Color.FromArgb(38, 31, 20));
                    lblTitle.Text = "Cofre bloqueado";
                    lblSubtitle.Text = "Os artefatos do cofre estao ocultos e so podem ser restaurados com senha ou chave.";
                    lblFilesValue.Text = $"{currentOverview.FileCount} arquivo(s)";
                    lblSizeValue.Text = VaultCrypto.FormatSize(currentOverview.TotalBytes);
                    lblAccessValue.Text = "Oculto e protegido";
                    lblPathLabel.Text = "LOCAL DO COFRE";
                    lblPathValue.Text = vaultService.ApplicationDirectory;
                    lblPathHint.Text = "Use a senha principal ou a chave de recuperacao para restaurar o conteudo.";
                    btnPrimary.Text = "Desbloquear";
                    btnSecondary.Text = "Abrir pasta";
                    btnRecovery.Visible = true;
                    break;

                default:
                    ApplyStateVisual("Revisar", AppTheme.Danger, Color.FromArgb(42, 25, 25));
                    lblTitle.Text = "Estado do cofre exige revisao";
                    lblSubtitle.Text = "Existe uma combinacao inesperada de artefatos neste diretorio.";
                    lblFilesValue.Text = "Estado inconsistente";
                    lblSizeValue.Text = "-";
                    lblAccessValue.Text = "Requer verificacao";
                    lblPathLabel.Text = "LOCAL";
                    lblPathValue.Text = vaultService.ApplicationDirectory;
                    lblPathHint.Text = "Abra a pasta do aplicativo para revisar os artefatos manualmente.";
                    btnPrimary.Text = "Indisponivel";
                    btnSecondary.Text = "Abrir pasta";
                    btnRecovery.Visible = false;
                    break;
            }

            pathToolTip.SetToolTip(lblPathValue, lblPathValue.Text);
            pathToolTip.SetToolTip(pathPanel, lblPathValue.Text);

            if (!isBusy && DateTime.UtcNow >= unlockCooldownUntilUtc)
            {
                lblProgress.Visible = false;
                progressTrack.Visible = false;
                lblProgress.Text = string.Empty;
                lastProgressCurrent = 0;
                lastProgressTotal = 1;
            }

            ApplyInteractiveState();
            LayoutControls();
            RefreshCooldownState();
        }

        private void ApplyStateVisual(string text, Color textColor, Color panelFill)
        {
            lblState.Text = text;
            lblState.ForeColor = textColor;
            statePanel.BackColor = panelFill;
            statePanelBorderColor = textColor;
            statePanel.Invalidate();
        }

        private void ApplyInteractiveState()
        {
            bool isLocked = currentOverview?.State == VaultState.Locked;
            bool cooldownActive = isLocked && DateTime.UtcNow < unlockCooldownUntilUtc;

            btnPrimary.Enabled = !isBusy;
            btnSecondary.Enabled = !isBusy;
            btnRecovery.Enabled = !isBusy && btnRecovery.Visible;

            if (currentOverview?.State == VaultState.Inconsistent)
            {
                btnPrimary.Enabled = false;
            }

            if (cooldownActive)
            {
                btnPrimary.Enabled = false;
                btnRecovery.Enabled = false;
            }
        }

        private void SetBusy(bool busy, string statusText)
        {
            isBusy = busy;
            UseWaitCursor = busy;

            if (busy)
            {
                lastProgressCurrent = 0;
                lastProgressTotal = 1;
                lblProgress.Text = statusText;
                lblProgress.Visible = true;
                progressTrack.Visible = true;
            }
            else if (DateTime.UtcNow >= unlockCooldownUntilUtc)
            {
                progressTrack.Visible = false;
                lblProgress.Visible = false;
                lblProgress.Text = string.Empty;
            }

            ApplyInteractiveState();
            LayoutControls();
        }

        private void UpdateProgress(VaultProgress progress)
        {
            lastProgressTotal = Math.Max(1, progress.Total);
            lastProgressCurrent = Math.Min(lastProgressTotal, Math.Max(0, progress.Current));
            lblProgress.Text = progress.Total > 0
                ? $"{progress.Step} ({progress.Current}/{progress.Total})"
                : progress.Step;
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
            fillWidth = Math.Min(progressTrack.Width, fillWidth);
            progressFill.SetBounds(0, 0, fillWidth, progressTrack.Height);
        }

        private void RegisterUnlockFailure(string message)
        {
            failedUnlockAttempts++;

            if (failedUnlockAttempts >= 5)
            {
                failedUnlockAttempts = 0;
                unlockCooldownUntilUtc = DateTime.UtcNow.AddSeconds(15);
                unlockCooldownTimer.Start();

                lblProgress.Text = $"{message} Aguarde 15 segundos para tentar novamente.";
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
            if (isBusy)
            {
                return;
            }

            if (DateTime.UtcNow >= unlockCooldownUntilUtc)
            {
                unlockCooldownTimer.Stop();
                ApplyInteractiveState();

                if (!progressTrack.Visible)
                {
                    lblProgress.Visible = false;
                    lblProgress.Text = string.Empty;
                    LayoutControls();
                }

                return;
            }

            TimeSpan remaining = unlockCooldownUntilUtc - DateTime.UtcNow;
            lblProgress.Text = $"Aguarde {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))} segundo(s) para tentar novamente.";
            lblProgress.Visible = true;
            progressTrack.Visible = false;
            ApplyInteractiveState();
            LayoutControls();
        }

        private void OpenRelevantFolder()
        {
            string path = currentOverview?.State switch
            {
                VaultState.Unlocked => vaultService.WorkingFolderPath,
                _ => vaultService.ApplicationDirectory
            };

            OpenFolder(path);
        }

        private void OpenWorkingFolder()
        {
            OpenFolder(vaultService.WorkingFolderPath);
        }

        private void OpenFolder(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Panel CreateBorderPanel(Color backColor, Color borderColor)
        {
            Panel panel = new Panel
            {
                BackColor = backColor
            };

            panel.Paint += (_, e) =>
            {
                using var pen = new Pen(borderColor, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, panel.Width - 1), Math.Max(0, panel.Height - 1));
            };

            return panel;
        }

        private Panel CreateBorderPanel(Color backColor, Func<Color> borderColorProvider)
        {
            Panel panel = new Panel
            {
                BackColor = backColor
            };

            panel.Paint += (_, e) =>
            {
                using var pen = new Pen(borderColorProvider(), 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, panel.Width - 1), Math.Max(0, panel.Height - 1));
            };

            return panel;
        }

        private static Label CreateLabel(string text, float size, FontStyle style, Color foreColor, Color backColor)
        {
            return new Label
            {
                AutoSize = false,
                Text = text,
                ForeColor = foreColor,
                BackColor = backColor,
                Font = AppTheme.CreateBodyFont(size, style)
            };
        }

        private Label CreateMetricLabel(string text)
        {
            return CreateLabel(text, 8.5f, FontStyle.Bold, AppTheme.TextSoft, summaryPanel.BackColor);
        }

        private Label CreateMetricValue()
        {
            Label label = CreateLabel(string.Empty, 15f, FontStyle.Regular, AppTheme.TextPrimary, summaryPanel.BackColor);
            label.Font = AppTheme.CreateTitleFont(15f);
            return label;
        }
    }
}
