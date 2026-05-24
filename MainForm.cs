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
    internal sealed class MainForm : Form
    {
        private readonly VaultService vaultService;
        private readonly System.Windows.Forms.Timer unlockCooldownTimer;

        private readonly Panel scrollHost;
        private readonly TableLayoutPanel layoutRoot;
        private readonly Label lblEyebrow;
        private readonly Label lblTitle;
        private readonly Label lblSubtitle;
        private readonly StatusBadge statusBadge;
        private readonly Label lblStateMetric;
        private readonly Label lblCountMetric;
        private readonly Label lblSizeMetric;
        private readonly Label lblPathCaption;
        private readonly TextBox txtPath;
        private readonly ListBox lstFiles;
        private readonly Label lblFilesTitle;
        private readonly Label lblFilesHint;
        private readonly Label lblProgress;
        private readonly ProgressBar progressBar;
        private readonly HackerButton btnFolder;
        private readonly HackerButton btnMain;
        private readonly HackerButton btnRecovery;
        private readonly Label lblFooter;

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
            ClientSize = new Size(760, 660);
            MinimumSize = new Size(620, 540);
            BackColor = AppTheme.BackgroundPrimary;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9f);

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
                BackColor = Color.Transparent
            };

            layoutRoot = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(24, 22, 24, 20),
                ColumnCount = 1,
                RowCount = 7,
                BackColor = Color.Transparent
            };

            for (int index = 0; index < layoutRoot.RowCount; index++)
            {
                layoutRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            var headerCard = CreateCard(150, new Padding(20, 18, 20, 18));
            headerCard.Margin = new Padding(0, 0, 0, 12);

            var headerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));

            var titleStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            titleStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            titleStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            lblEyebrow = new Label
            {
                Text = "LOCAL VAULT / WINDOWS / AES-GCM",
                AutoSize = true,
                ForeColor = AppTheme.HackerCyan,
                Font = AppTheme.CreateCodeFont(8.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 6)
            };

            lblTitle = new Label
            {
                Text = "TremauxLock",
                AutoSize = true,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateDisplayFont(21f),
                Margin = new Padding(0, 0, 0, 5)
            };

            lblSubtitle = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(10f),
                TextAlign = ContentAlignment.TopLeft
            };

            titleStack.Controls.Add(lblEyebrow, 0, 0);
            titleStack.Controls.Add(lblTitle, 0, 1);
            titleStack.Controls.Add(lblSubtitle, 0, 2);

            var statusStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Padding = new Padding(10, 2, 0, 0)
            };
            statusStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            statusStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            statusStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            statusStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

            statusBadge = new StatusBadge
            {
                Dock = DockStyle.Top,
                Height = 30,
                Width = 220,
                ShowDot = true,
                Text = "-"
            };

            lblStateMetric = CreateMetricLabel();
            lblCountMetric = CreateMetricLabel();
            lblSizeMetric = CreateMetricLabel();

            statusStack.Controls.Add(statusBadge, 0, 0);
            statusStack.Controls.Add(lblStateMetric, 0, 1);
            statusStack.Controls.Add(lblCountMetric, 0, 2);
            statusStack.Controls.Add(lblSizeMetric, 0, 3);

            headerGrid.Controls.Add(titleStack, 0, 0);
            headerGrid.Controls.Add(statusStack, 1, 0);
            headerCard.Controls.Add(headerGrid);

            var pathCard = CreateCard(92, new Padding(18, 14, 18, 14));
            pathCard.Margin = new Padding(0, 0, 0, 12);

            lblPathCaption = new Label
            {
                Text = "Local",
                AutoSize = true,
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(8.75f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 8)
            };

            txtPath = new TextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(10, 15, 24),
                ForeColor = Color.FromArgb(135, 245, 204),
                Font = AppTheme.CreateCodeFont(10f),
                Dock = DockStyle.Fill,
                Multiline = false
            };

            var pathShell = new SurfacePanel
            {
                Dock = DockStyle.Fill,
                Height = 36,
                Padding = new Padding(12, 8, 12, 6),
                FillColor = Color.FromArgb(7, 12, 20),
                SecondaryFillColor = Color.FromArgb(7, 12, 20),
                BorderColor = Color.FromArgb(60, 75, 96),
                CornerRadius = 8
            };
            pathShell.Controls.Add(txtPath);

            var pathStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            pathStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pathStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            pathStack.Controls.Add(lblPathCaption, 0, 0);
            pathStack.Controls.Add(pathShell, 0, 1);
            pathCard.Controls.Add(pathStack);

            var filesCard = CreateCard(236, new Padding(18, 14, 18, 18));
            filesCard.Margin = new Padding(0, 0, 0, 12);

            lblFilesTitle = new Label
            {
                Text = "Arquivos",
                AutoSize = false,
                Height = 28,
                Dock = DockStyle.Top,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateTitleFont(12f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var listHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(7, 12, 20),
                Padding = new Padding(1)
            };

            lstFiles = new ListBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(7, 12, 20),
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateCodeFont(9.25f),
                IntegralHeight = false,
                Visible = false,
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 28
            };
            lstFiles.DrawItem += LstFiles_DrawItem;

            lblFilesHint = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                BackColor = Color.FromArgb(7, 12, 20),
                ForeColor = AppTheme.TextMuted,
                Font = AppTheme.CreateBodyFont(9.5f),
                TextAlign = ContentAlignment.MiddleCenter
            };

            listHost.Controls.Add(lstFiles);
            listHost.Controls.Add(lblFilesHint);

            var fileStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            fileStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            fileStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            fileStack.Controls.Add(lblFilesTitle, 0, 0);
            fileStack.Controls.Add(listHost, 0, 1);
            filesCard.Controls.Add(fileStack);

            lblProgress = new Label
            {
                AutoSize = false,
                Height = 22,
                Dock = DockStyle.Top,
                Visible = false,
                ForeColor = AppTheme.HackerYellow,
                Font = AppTheme.CreateBodyFont(9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(2, 0, 2, 4)
            };

            progressBar = new ProgressBar
            {
                Visible = false,
                Height = 8,
                Style = ProgressBarStyle.Continuous,
                Dock = DockStyle.Top,
                Margin = new Padding(2, 0, 2, 12)
            };

            var buttonRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 4, 0, 8),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };

            btnFolder = new HackerButton
            {
                Text = "Abrir pasta",
                ButtonStyle = HackerButtonStyle.Secondary,
                Width = 148,
                Height = 42,
                Margin = new Padding(0, 0, 10, 8)
            };
            btnFolder.Click += (_, _) => OpenRelevantFolder();

            btnMain = new HackerButton
            {
                Text = "-",
                ButtonStyle = HackerButtonStyle.Primary,
                Width = 184,
                Height = 42,
                Margin = new Padding(0, 0, 10, 8)
            };
            btnMain.Click += (_, _) => _ = HandlePrimaryActionAsync();

            btnRecovery = new HackerButton
            {
                Text = "Chave",
                ButtonStyle = HackerButtonStyle.Accent,
                Width = 128,
                Height = 42,
                Visible = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            btnRecovery.Click += (_, _) => _ = UnlockVaultAsync(true);

            buttonRow.Controls.Add(btnFolder);
            buttonRow.Controls.Add(btnMain);
            buttonRow.Controls.Add(btnRecovery);

            lblFooter = new Label
            {
                Text = "TremauxLock protege apenas a pasta private escolhida pelo usuario.",
                ForeColor = AppTheme.TextMuted,
                Font = AppTheme.CreateBodyFont(8.25f),
                AutoSize = true,
                Margin = new Padding(2, 0, 0, 0)
            };

            int row = 0;
            layoutRoot.Controls.Add(headerCard, 0, row++);
            layoutRoot.Controls.Add(pathCard, 0, row++);
            layoutRoot.Controls.Add(filesCard, 0, row++);
            layoutRoot.Controls.Add(lblProgress, 0, row++);
            layoutRoot.Controls.Add(progressBar, 0, row++);
            layoutRoot.Controls.Add(buttonRow, 0, row++);
            layoutRoot.Controls.Add(lblFooter, 0, row);

            scrollHost.Controls.Add(layoutRoot);
            Controls.Add(scrollHost);

            void SyncLayoutRootWidth()
            {
                int width = scrollHost.ClientSize.Width;
                if (width > 0)
                {
                    layoutRoot.Width = width;
                }
            }

            scrollHost.Resize += (_, _) => SyncLayoutRootWidth();
            Load += (_, _) =>
            {
                SyncLayoutRootWidth();
                RefreshOverview();
            };
            Activated += (_, _) => RefreshOverview();
            Shown += (_, _) => SyncLayoutRootWidth();
            Paint += MainForm_Paint;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Rectangle bounds = ClientRectangle;
            using var brush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(12, 17, 26),
                AppTheme.BackgroundBottom,
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(brush, bounds);

            using var glow = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height / 2)),
                Color.FromArgb(18, AppTheme.HackerBlue),
                Color.FromArgb(0, AppTheme.HackerBlue),
                LinearGradientMode.ForwardDiagonal);
            e.Graphics.FillRectangle(glow, 0, 0, Width, Height / 2);

            using var gridPen = new Pen(Color.FromArgb(7, 255, 255, 255), 1f);
            for (int x = 0; x < Width; x += 56)
            {
                e.Graphics.DrawLine(gridPen, x, 0, x, Height);
            }

            for (int y = 0; y < Height; y += 56)
            {
                e.Graphics.DrawLine(gridPen, 0, y, Width, y);
            }
        }

        private void MainForm_Paint(object? sender, PaintEventArgs e)
        {
            using var line = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, Width), 3),
                AppTheme.HackerCyan,
                AppTheme.HackerBlue,
                LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(line, 0, 0, Width, 3);
        }

        private static VaultCard CreateCard(int height, Padding padding)
        {
            return new VaultCard
            {
                Dock = DockStyle.Top,
                Height = height,
                Padding = padding,
                FillColor = Color.FromArgb(17, 23, 34),
                SecondaryFillColor = Color.FromArgb(17, 23, 34),
                BorderColor = Color.FromArgb(74, 88, 108),
                InnerStrokeColor = Color.FromArgb(0, Color.White)
            };
        }

        private static Label CreateMetricLabel()
        {
            return new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateBodyFont(9.25f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
        }

        private void LstFiles_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = selected
                ? Color.FromArgb(28, 45, 64)
                : e.Index % 2 == 0
                    ? Color.FromArgb(10, 17, 27)
                    : Color.FromArgb(7, 13, 22);

            using (var b = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }

            if (selected)
            {
                using var accent = new Pen(AppTheme.HackerCyan, 2f);
                e.Graphics.DrawLine(accent, e.Bounds.Left, e.Bounds.Top + 4, e.Bounds.Left, e.Bounds.Bottom - 4);
            }

            string text = lstFiles.Items[e.Index]?.ToString() ?? string.Empty;
            Rectangle textRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 18, e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                lstFiles.Font,
                textRect,
                selected ? AppTheme.TextPrimary : AppTheme.TextSecondary,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding);
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

            if (currentOverview.State == VaultState.Empty || currentOverview.State == VaultState.Inconsistent)
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

            SetBusy(true, "Criptografando arquivos...");
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
                dialog.ClearSecret();
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
                dialog.ClearSecret();
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
            VaultOverview? overview = currentOverview;
            if (overview == null)
            {
                return;
            }

            bool cooldownActive = overview.State == VaultState.Locked && DateTime.UtcNow < unlockCooldownUntilUtc;
            bool recoveryEnabled = !isBusy && overview.State == VaultState.Locked && !cooldownActive;

            btnMain.Enabled = !isBusy && !cooldownActive;
            btnRecovery.Enabled = recoveryEnabled;
            btnRecovery.Visible = overview.State == VaultState.Locked;
            btnFolder.Enabled = !isBusy;

            lblCountMetric.Text = $"Arquivos: {overview.FileCount}";
            lblSizeMetric.Text = $"Volume: {VaultCrypto.FormatSize(overview.TotalBytes)}";

            switch (overview.State)
            {
                case VaultState.Empty:
                    lblTitle.Text = "Cofre pronto";
                    lblSubtitle.Text = "Adicione arquivos na pasta private. Depois, bloqueie para guardar tudo em formato criptografado.";
                    ApplyStatusBadge("Pronto", AppTheme.HackerCyan, Color.FromArgb(11, 42, 50), Color.FromArgb(68, 184, 210));
                    lblStateMetric.Text = "Estado: aguardando arquivos";
                    lblPathCaption.Text = "Pasta de trabalho";
                    txtPath.Text = overview.WorkingFolderPath;
                    btnFolder.Text = "Abrir private";
                    btnMain.Text = "Atualizar";
                    btnMain.ButtonStyle = HackerButtonStyle.Secondary;
                    lblFilesHint.Text = "Coloque os arquivos na pasta private para iniciar.";
                    lstFiles.Visible = false;
                    lblFilesHint.Visible = true;
                    break;

                case VaultState.Unlocked:
                    lblTitle.Text = "Cofre aberto";
                    lblSubtitle.Text = "Os arquivos estao visiveis no Windows. Bloqueie quando terminar para proteger o conteudo.";
                    ApplyStatusBadge("Aberto", AppTheme.HackerGreen, Color.FromArgb(13, 48, 34), Color.FromArgb(67, 190, 128));
                    lblStateMetric.Text = "Estado: acesso liberado";
                    lblPathCaption.Text = "Pasta de trabalho";
                    txtPath.Text = overview.WorkingFolderPath;
                    btnFolder.Text = "Abrir private";
                    btnMain.Text = "Bloquear cofre";
                    btnMain.ButtonStyle = HackerButtonStyle.Primary;
                    FillFileList();
                    break;

                case VaultState.Locked:
                    lblTitle.Text = "Cofre protegido";
                    lblSubtitle.Text = "O conteudo esta criptografado. Use a senha ou a chave de recuperacao para restaurar.";
                    if (cooldownActive)
                    {
                        int seconds = Math.Max(1, (int)Math.Ceiling((unlockCooldownUntilUtc - DateTime.UtcNow).TotalSeconds));
                        ApplyStatusBadge($"Aguarde {seconds}s", AppTheme.HackerYellow, Color.FromArgb(52, 40, 13), Color.FromArgb(190, 150, 48));
                        lblStateMetric.Text = "Estado: pausa temporaria";
                    }
                    else
                    {
                        ApplyStatusBadge("Protegido", AppTheme.HackerYellow, Color.FromArgb(52, 40, 13), Color.FromArgb(190, 150, 48));
                        lblStateMetric.Text = "Estado: criptografado";
                    }

                    lblPathCaption.Text = "Pasta da aplicacao";
                    txtPath.Text = vaultService.ApplicationDirectory;
                    btnFolder.Text = "Abrir pasta";
                    btnMain.Text = cooldownActive ? "Aguarde..." : "Desbloquear";
                    btnMain.ButtonStyle = HackerButtonStyle.Primary;
                    lblFilesHint.Text = "Lista protegida. Desbloqueie o cofre para ver os nomes dos arquivos.";
                    lstFiles.Visible = false;
                    lblFilesHint.Visible = true;
                    break;

                default:
                    lblTitle.Text = "Revisao necessaria";
                    lblSubtitle.Text = "As pastas do cofre estao em conflito. Confira private, private.locked e private.vault.json.";
                    ApplyStatusBadge("Revisar", AppTheme.HackerRed, Color.FromArgb(58, 24, 32), Color.FromArgb(205, 80, 96));
                    lblStateMetric.Text = "Estado: estrutura inconsistente";
                    lblPathCaption.Text = "Diretorio";
                    txtPath.Text = vaultService.ApplicationDirectory;
                    btnFolder.Text = "Abrir pasta";
                    btnMain.Text = "Atualizar";
                    btnMain.ButtonStyle = HackerButtonStyle.Warning;
                    lblFilesHint.Text = "Corrija a estrutura antes de continuar.";
                    lstFiles.Visible = false;
                    lblFilesHint.Visible = true;
                    break;
            }

            ResizeStatusBadge();
            UpdateProgressUi(overview.State);
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
            int width = TextRenderer.MeasureText(statusBadge.Text.ToUpperInvariant(), statusBadge.Font).Width + 54;
            statusBadge.Width = Math.Min(260, Math.Max(160, width));
        }

        private void FillFileList()
        {
            lstFiles.Items.Clear();
            string[] allFiles = vaultService.GetWorkingFiles();
            string[] files = allFiles.Take(40).ToArray();

            foreach (string file in files)
            {
                string relativePath = Path.GetRelativePath(vaultService.WorkingFolderPath, file);
                long size = new FileInfo(file).Length;
                lstFiles.Items.Add($"{relativePath}   |   {VaultCrypto.FormatSize(size)}");
            }

            if (allFiles.Length > files.Length)
            {
                lstFiles.Items.Add($"... e mais {allFiles.Length - files.Length} arquivo(s)");
            }

            bool hasFiles = lstFiles.Items.Count > 0;
            lstFiles.Visible = hasFiles;
            lblFilesHint.Visible = !hasFiles;
            if (!hasFiles)
            {
                lblFilesHint.Text = "Nenhum arquivo encontrado na pasta private.";
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
