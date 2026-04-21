using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TremauxLock
{
    /// <summary>
    /// Janela principal compacta (WinForms): tema cyber refinado, sem WebView2.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private readonly VaultService vaultService;
        private readonly System.Windows.Forms.Timer unlockCooldownTimer;

        private readonly Label lblEyebrow;
        private readonly Label lblTitle;
        private readonly Label lblSubtitle;
        private readonly StatusBadge statusBadge;
        private readonly Label lblMetrics;
        private readonly Label lblPathCaption;
        private readonly TextBox txtPath;
        private readonly ListBox lstFiles;
        private readonly Label lblProgress;
        private readonly ProgressBar progressBar;
        private readonly HackerButton btnFolder;
        private readonly HackerButton btnMain;
        private readonly HackerButton btnRecovery;
        private readonly Label lblFooter;
        private readonly Panel scrollHost;
        private readonly TableLayoutPanel layoutRoot;

        private VaultOverview? currentOverview;
        private bool isBusy;
        private int failedUnlockAttempts;
        private int lastProgressCurrent;
        private int lastProgressTotal = 1;
        private string progressText = string.Empty;
        private DateTime unlockCooldownUntilUtc = DateTime.MinValue;

        public MainForm(VaultService vaultService)
        {
            this.vaultService = vaultService;

            unlockCooldownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            unlockCooldownTimer.Tick += (_, _) => RefreshCooldownState();

            Text = "TremauxLock";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            DoubleBuffered = true;
            ClientSize = new Size(540, 620);
            MinimumSize = new Size(500, 520);
            BackColor = Color.FromArgb(6, 8, 14);
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.25f);

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            layoutRoot = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(20, 18, 20, 22),
                ColumnCount = 1,
                RowCount = 8,
                BackColor = Color.Transparent
            };
            layoutRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 120f));
            layoutRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // --- Cabeçalho
            var cardHeader = new VaultCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(18, 16, 18, 14),
                FillColor = Color.FromArgb(20, 24, 34),
                SecondaryFillColor = Color.FromArgb(8, 10, 18)
            };

            lblEyebrow = new Label
            {
                Text = "COFRE LOCAL  ·  AES-GCM  ·  PBKDF2",
                ForeColor = AppTheme.WithAlpha(AppTheme.HackerCyan, 180),
                Font = AppTheme.CreateCodeFont(7.5f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            lblTitle = new Label
            {
                Text = "TremauxLock",
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateDisplayFont(17f),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            lblSubtitle = new Label
            {
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(9f),
                AutoSize = true,
                MaximumSize = new Size(480, 0)
            };

            var headerStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            headerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerStack.Controls.Add(lblEyebrow, 0, 0);
            headerStack.Controls.Add(lblTitle, 0, 1);
            headerStack.Controls.Add(lblSubtitle, 0, 2);
            cardHeader.Controls.Add(headerStack);

            // --- Estado + métricas
            var cardStatus = new VaultCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(16, 12, 16, 12),
                FillColor = Color.FromArgb(14, 18, 26),
                SecondaryFillColor = Color.FromArgb(6, 8, 14)
            };

            statusBadge = new StatusBadge
            {
                Margin = new Padding(0, 0, 12, 0),
                Height = 30,
                ShowDot = true,
                Text = "—"
            };

            lblMetrics = new Label
            {
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(8.75f),
                AutoSize = true,
                Text = "—",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var statusFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            statusFlow.Controls.Add(statusBadge);
            statusFlow.Controls.Add(lblMetrics);
            cardStatus.Controls.Add(statusFlow);

            // --- Caminho
            var cardPath = new VaultCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(16, 12, 16, 12),
                FillColor = Color.FromArgb(16, 20, 28),
                SecondaryFillColor = Color.FromArgb(8, 10, 16)
            };

            lblPathCaption = new Label
            {
                Text = "Local",
                ForeColor = AppTheme.TextMuted,
                Font = AppTheme.CreateBodyFont(8f, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 6)
            };

            txtPath = new TextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(10, 14, 22),
                ForeColor = Color.FromArgb(180, 230, 255),
                Font = AppTheme.CreateCodeFont(8.75f),
                Dock = DockStyle.Fill,
                Multiline = false
            };

            var pathShell = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 14, 22),
                Padding = new Padding(10, 7, 10, 6)
            };
            pathShell.Paint += (_, e) =>
            {
                using var pen = new Pen(Color.FromArgb(60, 0, 255, 200), 1f);
                Rectangle r = new Rectangle(0, 0, pathShell.Width - 1, pathShell.Height - 1);
                e.Graphics.DrawRectangle(pen, r);
            };
            pathShell.Controls.Add(txtPath);
            txtPath.Dock = DockStyle.Fill;

            var pathStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            pathStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pathStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            pathStack.Controls.Add(lblPathCaption, 0, 0);
            pathStack.Controls.Add(pathShell, 0, 1);
            cardPath.Controls.Add(pathStack);

            // --- Lista de arquivos
            var cardFiles = new VaultCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(12, 10, 12, 10),
                FillColor = Color.FromArgb(12, 16, 24),
                SecondaryFillColor = Color.FromArgb(6, 8, 12)
            };

            lstFiles = new ListBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(8, 12, 18),
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(8.5f),
                IntegralHeight = false,
                Visible = false,
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 26
            };
            lstFiles.DrawItem += LstFiles_DrawItem;
            cardFiles.Controls.Add(lstFiles);

            lblProgress = new Label
            {
                ForeColor = AppTheme.HackerYellow,
                Font = AppTheme.CreateBodyFont(8.5f),
                AutoSize = true,
                Visible = false,
                Margin = new Padding(0, 0, 0, 6)
            };

            progressBar = new ProgressBar
            {
                Visible = false,
                Height = 6,
                Margin = new Padding(0, 0, 0, 8),
                Style = ProgressBarStyle.Continuous,
                Dock = DockStyle.Top
            };

            var btnRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 8, 0, 6),
                Padding = new Padding(0),
                BackColor = Color.Transparent,
                MinimumSize = new Size(200, 46)
            };

            btnFolder = new HackerButton
            {
                Text = "Abrir pasta",
                ButtonStyle = HackerButtonStyle.Accent,
                Width = 132,
                Height = 40,
                Margin = new Padding(0, 0, 10, 8)
            };
            btnFolder.Click += (_, _) => OpenRelevantFolder();

            btnMain = new HackerButton
            {
                Text = "—",
                ButtonStyle = HackerButtonStyle.Primary,
                Width = 158,
                Height = 40,
                Margin = new Padding(0, 0, 10, 8)
            };
            btnMain.Click += (_, _) => _ = HandlePrimaryActionAsync();

            btnRecovery = new HackerButton
            {
                Text = "Chave",
                ButtonStyle = HackerButtonStyle.Secondary,
                Width = 104,
                Height = 40,
                Visible = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            btnRecovery.Click += (_, _) => _ = UnlockVaultAsync(true);

            btnRow.Controls.Add(btnFolder);
            btnRow.Controls.Add(btnMain);
            btnRow.Controls.Add(btnRecovery);

            lblFooter = new Label
            {
                Text = "TremauxLock · arquivos protegidos com criptografia autenticada",
                ForeColor = AppTheme.WithAlpha(AppTheme.TextMuted, 140),
                Font = AppTheme.CreateCodeFont(7.5f),
                AutoSize = true,
                Margin = new Padding(2, 2, 0, 8)
            };

            int r = 0;
            layoutRoot.Controls.Add(cardHeader, 0, r++);
            layoutRoot.Controls.Add(cardStatus, 0, r++);
            layoutRoot.Controls.Add(cardPath, 0, r++);
            layoutRoot.Controls.Add(cardFiles, 0, r++);
            layoutRoot.Controls.Add(lblProgress, 0, r++);
            layoutRoot.Controls.Add(progressBar, 0, r++);
            layoutRoot.Controls.Add(btnRow, 0, r++);
            layoutRoot.Controls.Add(lblFooter, 0, r);

            scrollHost.Controls.Add(layoutRoot);
            Controls.Add(scrollHost);

            void SyncLayoutRootWidth()
            {
                int w = scrollHost.ClientSize.Width;
                if (w < 1)
                {
                    return;
                }

                layoutRoot.Width = w;
            }

            scrollHost.Resize += (_, _) => SyncLayoutRootWidth();
            Load += (_, _) =>
            {
                SyncLayoutRootWidth();
                RefreshOverview();
            };
            Activated += (_, _) => RefreshOverview();
            Paint += MainForm_Paint;
            Shown += (_, _) => SyncLayoutRootWidth();
        }

        private void MainForm_Paint(object? sender, PaintEventArgs e)
        {
            using var line = new LinearGradientBrush(
                new Rectangle(0, 0, Width, 3),
                AppTheme.WithAlpha(AppTheme.HackerCyan, 220),
                AppTheme.WithAlpha(AppTheme.HackerMagenta, 200),
                LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(line, 0, 0, Width, 3);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Rectangle bounds = ClientRectangle;
            using var brush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(255, 16, 20, 32),
                Color.FromArgb(255, 2, 3, 8),
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(brush, bounds);
        }

        private void LstFiles_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = (e.Index % 2 == 0)
                ? Color.FromArgb(26, 32, 44)
                : Color.FromArgb(20, 26, 36);
            if (selected)
            {
                back = Color.FromArgb(32, 48, 58);
            }

            using (var b = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }

            if (selected)
            {
                using var accent = new Pen(AppTheme.HackerCyan, 2f);
                e.Graphics.DrawLine(accent, e.Bounds.Left, e.Bounds.Top + 2, e.Bounds.Left, e.Bounds.Bottom - 2);
            }

            string text = lstFiles.Items[e.Index]?.ToString() ?? string.Empty;
            Rectangle textRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                lstFiles.Font,
                textRect,
                selected ? AppTheme.TextPrimary : AppTheme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
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
                RefreshOverview();
                return;
            }

            if (currentOverview.State == VaultState.Inconsistent)
            {
                RefreshOverview();
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

            SetBusy(true, "Criptografando…");
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

            SetBusy(true, useRecoveryKey ? "Validando chave…" : "Validando senha…");
            var progress = new Progress<VaultProgress>(UpdateProgress);

            try
            {
                UnlockResult result = useRecoveryKey
                    ? await vaultService.UnlockVaultWithRecoveryKeyAsync(dialog.Secret, progress)
                    : await vaultService.UnlockVaultWithPasswordAsync(dialog.Secret, progress);

                failedUnlockAttempts = 0;
                unlockCooldownUntilUtc = DateTime.MinValue;
                RefreshOverview();

                string message = $"Cofre restaurado.\n\nArquivos: {result.FileCount}\nVolume: {VaultCrypto.FormatSize(result.TotalBytes)}";
                if (!string.IsNullOrWhiteSpace(result.BackupWarning))
                {
                    message += $"\n\nAviso:\n{result.BackupWarning}";
                }

                MessageBox.Show(message, "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            if (!isBusy && DateTime.UtcNow >= unlockCooldownUntilUtc)
            {
                progressText = string.Empty;
                lastProgressCurrent = 0;
                lastProgressTotal = 1;
            }

            ApplyOverviewToUi();
        }

        private void ApplyOverviewToUi()
        {
            VaultOverview? o = currentOverview;
            if (o == null)
            {
                return;
            }

            bool cooldownActive = o.State == VaultState.Locked && DateTime.UtcNow < unlockCooldownUntilUtc;
            bool recoveryEnabled = !isBusy && o.State == VaultState.Locked && !cooldownActive;
            bool folderEnabled = !isBusy;

            btnMain.Enabled = !isBusy && !cooldownActive;
            btnRecovery.Enabled = recoveryEnabled;
            btnRecovery.Visible = o.State == VaultState.Locked;
            btnFolder.Enabled = folderEnabled;

            lblMetrics.Text = $"  {o.FileCount} arquivo(s)  ·  {VaultCrypto.FormatSize(o.TotalBytes)}";

            switch (o.State)
            {
                case VaultState.Empty:
                    lblTitle.Text = "Pronto para usar";
                    lblSubtitle.Text = "Adicione arquivos na pasta private e bloqueie o cofre quando quiser.";
                    ApplyStatusBadge("Aguardando arquivos", AppTheme.HackerCyan, Color.FromArgb(72, 0, 55, 70), Color.FromArgb(140, 0, 220, 200));
                    lblPathCaption.Text = "Pasta de trabalho (private)";
                    txtPath.Text = o.WorkingFolderPath;
                    btnFolder.Text = "Abrir private";
                    btnMain.Text = "Atualizar";
                    btnMain.ButtonStyle = HackerButtonStyle.Ghost;
                    lstFiles.Visible = false;
                    break;

                case VaultState.Unlocked:
                    lblTitle.Text = "Cofre aberto";
                    lblSubtitle.Text = "Arquivos visiveis. Bloqueie para encriptar e ocultar.";
                    ApplyStatusBadge("Desbloqueado", AppTheme.HackerGreen, Color.FromArgb(72, 0, 35, 75), Color.FromArgb(140, 0, 220, 120));
                    lblPathCaption.Text = "Pasta de trabalho (private)";
                    txtPath.Text = o.WorkingFolderPath;
                    btnFolder.Text = "Abrir private";
                    btnMain.Text = "Bloquear cofre";
                    btnMain.ButtonStyle = HackerButtonStyle.Danger;
                    FillFileList();
                    lstFiles.Visible = lstFiles.Items.Count > 0;
                    break;

                case VaultState.Locked:
                    lblTitle.Text = "Cofre bloqueado";
                    lblSubtitle.Text = "Conteudo encriptado. Use a senha ou a chave de recuperacao.";
                    if (cooldownActive)
                    {
                        ApplyStatusBadge(
                            $"Aguarde {Math.Max(1, (int)Math.Ceiling((unlockCooldownUntilUtc - DateTime.UtcNow).TotalSeconds))}s",
                            AppTheme.HackerYellow,
                            Color.FromArgb(72, 70, 55, 0),
                            Color.FromArgb(150, 255, 200, 0));
                    }
                    else
                    {
                        ApplyStatusBadge("Bloqueado", AppTheme.HackerYellow, Color.FromArgb(72, 65, 50, 0), Color.FromArgb(150, 255, 210, 0));
                    }

                    lblPathCaption.Text = "Pasta da aplicação";
                    txtPath.Text = vaultService.ApplicationDirectory;
                    btnFolder.Text = "Abrir pasta";
                    btnMain.Text = cooldownActive ? "Aguarde…" : "Desbloquear";
                    btnMain.ButtonStyle = HackerButtonStyle.Primary;
                    lstFiles.Visible = false;
                    break;

                default:
                    lblTitle.Text = "Estrutura inválida";
                    lblSubtitle.Text = "Pastas ou arquivos do cofre em conflito. Corrija manualmente.";
                    ApplyStatusBadge("Revisar pastas", AppTheme.HackerRed, Color.FromArgb(72, 90, 25, 35), Color.FromArgb(160, 255, 90, 110));
                    lblPathCaption.Text = "Diretório";
                    txtPath.Text = vaultService.ApplicationDirectory;
                    btnFolder.Text = "Abrir pasta";
                    btnMain.Text = "Atualizar";
                    btnMain.ButtonStyle = HackerButtonStyle.Ghost;
                    lstFiles.Visible = false;
                    break;
            }

            ResizeStatusBadge();
            UpdateProgressUi(o.State);
        }

        private void ApplyStatusBadge(string text, Color fore, Color fill, Color border)
        {
            statusBadge.Text = text;
            statusBadge.ForeColor = fore;
            statusBadge.FillColor = fill;
            statusBadge.BorderColor = border;
        }

        private void ResizeStatusBadge()
        {
            int w = TextRenderer.MeasureText(statusBadge.Text.ToUpperInvariant(), statusBadge.Font).Width + 52;
            statusBadge.Width = Math.Min(440, Math.Max(168, w));
        }

        private void FillFileList()
        {
            lstFiles.Items.Clear();
            if (!Directory.Exists(vaultService.WorkingFolderPath))
            {
                return;
            }

            string[] files = Directory.GetFiles(vaultService.WorkingFolderPath, "*", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string rel = Path.GetRelativePath(vaultService.WorkingFolderPath, file);
                long len = new FileInfo(file).Length;
                lstFiles.Items.Add($"{name}   ·   {VaultCrypto.FormatSize(len)}   ·   {rel}");
            }
        }

        private void UpdateProgressUi(VaultState state)
        {
            if (isBusy && !string.IsNullOrWhiteSpace(progressText))
            {
                lblProgress.Visible = true;
                progressBar.Visible = lastProgressTotal > 1;
                lblProgress.Text = progressText;
                if (lastProgressTotal > 1)
                {
                    progressBar.Maximum = lastProgressTotal;
                    progressBar.Value = Math.Min(lastProgressTotal, Math.Max(0, lastProgressCurrent));
                }
                else
                {
                    progressBar.Value = 0;
                }

                return;
            }

            if (state == VaultState.Locked && DateTime.UtcNow < unlockCooldownUntilUtc)
            {
                TimeSpan remaining = unlockCooldownUntilUtc - DateTime.UtcNow;
                int seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
                lblProgress.Visible = true;
                progressBar.Visible = false;
                lblProgress.Text = $"Tente novamente em {seconds}s";
                return;
            }

            lblProgress.Visible = false;
            progressBar.Visible = false;
        }

        private void SetBusy(bool busy, string statusText)
        {
            isBusy = busy;
            UseWaitCursor = busy;

            if (busy)
            {
                lastProgressCurrent = 0;
                lastProgressTotal = 1;
                progressText = statusText;
            }
            else if (DateTime.UtcNow >= unlockCooldownUntilUtc)
            {
                progressText = string.Empty;
                lastProgressCurrent = 0;
                lastProgressTotal = 1;
            }

            ApplyOverviewToUi();
        }

        private void UpdateProgress(VaultProgress progress)
        {
            lastProgressTotal = Math.Max(1, progress.Total);
            lastProgressCurrent = Math.Min(lastProgressTotal, Math.Max(0, progress.Current));
            progressText = progress.Total > 0
                ? $"{progress.Step} ({progress.Current}/{progress.Total})"
                : progress.Step;

            ApplyOverviewToUi();
        }

        private void RegisterUnlockFailure(string message)
        {
            failedUnlockAttempts++;

            if (failedUnlockAttempts >= 5)
            {
                failedUnlockAttempts = 0;
                unlockCooldownUntilUtc = DateTime.UtcNow.AddSeconds(15);
                unlockCooldownTimer.Start();
                ApplyOverviewToUi();

                MessageBox.Show(
                    $"{message}\n\nNovas tentativas foram pausadas por 15 segundos.",
                    "TremauxLock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ApplyOverviewToUi();
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
                progressText = string.Empty;
                ApplyOverviewToUi();
                return;
            }

            ApplyOverviewToUi();
        }

        private void OpenRelevantFolder()
        {
            string path = currentOverview?.State switch
            {
                VaultState.Unlocked => vaultService.WorkingFolderPath,
                VaultState.Empty => vaultService.WorkingFolderPath,
                _ => vaultService.ApplicationDirectory
            };

            OpenFolder(path);
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    MessageBox.Show($"O caminho nao existe: {path}", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Acesso negado.", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir: {ex.Message}", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
