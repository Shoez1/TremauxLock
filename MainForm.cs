using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace TremauxLock
{
    internal sealed class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 2;
        private const int WM_NCHITTEST = 0x84;
        private const int ResizeBorder = 6;

        private readonly VaultService vaultService;
        private readonly WebView2 webView;
        private readonly Panel splashPanel;
        private readonly Label lblSplash;
        private readonly System.Windows.Forms.Timer unlockCooldownTimer;

        private VaultOverview? currentOverview;
        private bool isBusy;
        private bool webViewReady;
        private bool firstRenderCompleted;
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

            Text = "TremauxLock Vault";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1060, 740);
            ClientSize = new Size(1180, 760);
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundPrimary;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.5f);

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            webView = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = false,
                AllowExternalDrop = false,
                DefaultBackgroundColor = AppTheme.BackgroundPrimary
            };

            splashPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.BackgroundPrimary
            };

            lblSplash = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Carregando interface...",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = AppTheme.TextSecondary,
                BackColor = AppTheme.BackgroundPrimary,
                Font = AppTheme.CreateBodyFont(10f)
            };

            splashPanel.Controls.Add(lblSplash);
            Controls.Add(webView);
            Controls.Add(splashPanel);

            Shown += async (_, _) => await EnsureWebViewAsync();
            Activated += (_, _) => RefreshOverview();
            Resize += (_, _) => Invalidate();

            Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            };
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(AppTheme.BackgroundPrimary);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                Point screenPoint = new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16);
                Point clientPoint = PointToClient(screenPoint);

                bool left = clientPoint.X < ResizeBorder;
                bool right = clientPoint.X >= Width - ResizeBorder;
                bool top = clientPoint.Y < ResizeBorder;
                bool bottom = clientPoint.Y >= Height - ResizeBorder;

                if (top && left) { m.Result = (IntPtr)13; return; }
                if (top && right) { m.Result = (IntPtr)14; return; }
                if (bottom && left) { m.Result = (IntPtr)16; return; }
                if (bottom && right) { m.Result = (IntPtr)17; return; }
                if (left) { m.Result = (IntPtr)10; return; }
                if (right) { m.Result = (IntPtr)11; return; }
                if (top) { m.Result = (IntPtr)12; return; }
                if (bottom) { m.Result = (IntPtr)15; return; }
            }

            base.WndProc(ref m);
        }

        private async Task EnsureWebViewAsync()
        {
            if (webViewReady)
            {
                RefreshOverview();
                return;
            }

            try
            {
                await webView.EnsureCoreWebView2Async();

                CoreWebView2Settings settings = webView.CoreWebView2.Settings;
                settings.AreDefaultContextMenusEnabled = false;
                settings.AreDevToolsEnabled = false;
                settings.AreBrowserAcceleratorKeysEnabled = true;
                settings.IsStatusBarEnabled = false;
                settings.IsZoomControlEnabled = false;

                webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                webViewReady = true;
                RefreshOverview();
            }
            catch (Exception ex)
            {
                lblSplash.Text = "Falha ao carregar a interface.";
                MessageBox.Show(
                    "Nao foi possivel carregar a interface moderna do TremauxLock.\n\n" + ex.Message,
                    "TremauxLock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string action;
            try
            {
                action = e.TryGetWebMessageAsString();
            }
            catch
            {
                return;
            }

            switch (action)
            {
                case "drag-window":
                    BeginWindowDrag();
                    break;
                case "minimize":
                    BeginInvoke(new Action(() => WindowState = FormWindowState.Minimized));
                    break;
                case "toggle-maximize":
                    BeginInvoke(new Action(ToggleWindowState));
                    break;
                case "close":
                    BeginInvoke(new Action(Close));
                    break;
                case "open-private":
                    BeginInvoke(new Action(OpenRelevantFolder));
                    break;
                case "open-app-folder":
                    BeginInvoke(new Action(() => OpenFolder(vaultService.ApplicationDirectory)));
                    break;
                case "refresh":
                    BeginInvoke(new Action(RefreshOverview));
                    break;
                case "primary":
                    BeginInvoke(new Action(() => _ = HandlePrimaryActionAsync()));
                    break;
                case "recovery":
                    BeginInvoke(new Action(() => _ = UnlockVaultAsync(true)));
                    break;
            }
        }

        private void BeginWindowDrag()
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
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

            if (currentOverview.State == VaultState.Inconsistent)
            {
                OpenFolder(vaultService.ApplicationDirectory);
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

            RenderOverview();
        }

        private void RenderOverview()
        {
            if (!webViewReady || currentOverview == null)
            {
                return;
            }

            VaultRenderState state = BuildRenderState(currentOverview);
            webView.NavigateToString(VaultHtmlBuilder.Build(state));

            if (!firstRenderCompleted)
            {
                firstRenderCompleted = true;
                splashPanel.Visible = false;
                webView.Visible = true;
            }
        }

        private VaultRenderState BuildRenderState(VaultOverview overview)
        {
            bool cooldownActive = overview.State == VaultState.Locked && DateTime.UtcNow < unlockCooldownUntilUtc;
            bool secondaryEnabled = !isBusy;
            bool primaryEnabled = !isBusy && overview.State != VaultState.Inconsistent && !cooldownActive;
            bool recoveryEnabled = !isBusy && overview.State == VaultState.Locked && !cooldownActive;

            string title;
            string subtitle;
            string statusText;
            string statusTone;
            string visibilityText;
            string visibilityTone;
            string filesValue;
            string sizeValue;
            string panelLabel;
            string panelTitle;
            string pathLabel;
            string pathValue;
            string pathHint;
            string secondaryText;
            string secondaryAction;
            string primaryText;
            string primaryAction;
            string primaryKind;
            string? tertiaryText = null;
            string? tertiaryAction = null;
            string contentHtml;
            string footerCenter;

            switch (overview.State)
            {
                case VaultState.Empty:
                    title = "Cofre pronto";
                    subtitle = "A pasta private esta pronta para receber arquivos antes do primeiro bloqueio.";
                    statusText = "Pronto";
                    statusTone = "info";
                    visibilityText = "Aguardando conteudo";
                    visibilityTone = "info";
                    filesValue = "0";
                    sizeValue = "\u2014 0 B";
                    panelLabel = "Destino";
                    panelTitle = "Pasta private";
                    pathLabel = "Pasta private";
                    pathValue = overview.WorkingFolderPath;
                    pathHint = "Abra a pasta para adicionar conteudo antes do primeiro bloqueio.";
                    secondaryText = "Abrir private";
                    secondaryAction = "open-private";
                    primaryText = "Atualizar";
                    primaryAction = "refresh";
                    primaryKind = "ghost";
                    footerCenter = "Proximo bloqueio · nao definido";
                    contentHtml = VaultHtmlBuilder.BuildEmptyState(
                        "Pasta vazia",
                        "Adicione arquivos na pasta private para preparar o proximo bloqueio.",
                        "info");
                    break;

                case VaultState.Unlocked:
                    title = "Cofre desbloqueado";
                    subtitle = "Arquivos visiveis e revisaveis antes do proximo bloqueio.";
                    statusText = "Desbloqueado";
                    statusTone = "success";
                    visibilityText = "Visivel no disco";
                    visibilityTone = "success";
                    filesValue = overview.FileCount.ToString();
                    sizeValue = "\u2014 " + VaultCrypto.FormatSize(overview.TotalBytes);
                    panelLabel = "Destino";
                    panelTitle = "Pasta private";
                    pathLabel = "Pasta private";
                    pathValue = overview.WorkingFolderPath;
                    pathHint = "Passe o mouse sobre o caminho para ver completo ou use o atalho de abertura.";
                    secondaryText = "Abrir private";
                    secondaryAction = "open-private";
                    primaryText = "Bloquear cofre";
                    primaryAction = "primary";
                    primaryKind = "danger";
                    footerCenter = "Proximo bloqueio · nao definido";
                    contentHtml = BuildUnlockedContent();
                    break;

                case VaultState.Locked:
                    title = "Cofre bloqueado";
                    subtitle = "Arquivos ocultos e protegidos por senha ou chave de recuperacao.";
                    statusText = "Bloqueado";
                    statusTone = "warning";
                    visibilityText = "Oculto e protegido";
                    visibilityTone = "warning";
                    filesValue = overview.FileCount.ToString();
                    sizeValue = "\u2014 " + VaultCrypto.FormatSize(overview.TotalBytes);
                    panelLabel = "Destino";
                    panelTitle = "Local do cofre";
                    pathLabel = "Diretorio do cofre";
                    pathValue = vaultService.ApplicationDirectory;
                    pathHint = "Use a senha principal ou a chave de recuperacao para restaurar o conteudo.";
                    secondaryText = "Abrir pasta";
                    secondaryAction = "open-app-folder";
                    primaryText = cooldownActive ? "Aguardando" : "Desbloquear";
                    primaryAction = "primary";
                    primaryKind = "primary";
                    tertiaryText = "Usar recuperacao";
                    tertiaryAction = "recovery";
                    footerCenter = "Protecao · ativa";
                    contentHtml = VaultHtmlBuilder.BuildEmptyState(
                        "Conteudo oculto",
                        "Desbloqueie o cofre para listar novamente os arquivos da pasta private.",
                        "warning");
                    break;

                default:
                    title = "Revisao necessaria";
                    subtitle = "Existe uma combinacao inesperada de artefatos neste diretorio.";
                    statusText = "Revisar";
                    statusTone = "danger";
                    visibilityText = "Estrutura inconsistente";
                    visibilityTone = "danger";
                    filesValue = "!";
                    sizeValue = "\u2014";
                    panelLabel = "Destino";
                    panelTitle = "Diretorio do app";
                    pathLabel = "Diretorio";
                    pathValue = vaultService.ApplicationDirectory;
                    pathHint = "Abra a pasta do aplicativo para revisar private, private.locked e private.vault.json.";
                    secondaryText = "Abrir pasta";
                    secondaryAction = "open-app-folder";
                    primaryText = "Indisponivel";
                    primaryAction = "refresh";
                    primaryKind = "ghost";
                    footerCenter = "Estrutura · requer revisao";
                    contentHtml = VaultHtmlBuilder.BuildEmptyState(
                        "Revisao necessaria",
                        "Existe uma combinacao inesperada de artefatos e a visualizacao foi pausada.",
                        "danger");
                    break;
            }

            (string? noticeText, int noticePercent, string noticeTone) = BuildNotice(overview.State);

            return new VaultRenderState
            {
                AppName = "TremauxLock Vault",
                Eyebrow = "TremauxLock · Vault",
                Title = title,
                Subtitle = subtitle,
                StatusText = statusText,
                StatusTone = statusTone,
                FilesValue = filesValue,
                SizeValue = sizeValue,
                VisibilityText = visibilityText,
                VisibilityTone = visibilityTone,
                PanelLabel = panelLabel,
                PanelTitle = panelTitle,
                PathLabel = pathLabel,
                PathValue = pathValue,
                PathHint = pathHint,
                SecondaryText = secondaryText,
                SecondaryAction = secondaryAction,
                SecondaryEnabled = secondaryEnabled,
                PrimaryText = primaryText,
                PrimaryAction = primaryAction,
                PrimaryKind = primaryKind,
                PrimaryEnabled = primaryEnabled,
                TertiaryText = tertiaryText,
                TertiaryAction = tertiaryAction,
                TertiaryEnabled = recoveryEnabled,
                NoticeText = noticeText,
                NoticePercent = noticePercent,
                NoticeTone = noticeTone,
                ContentHtml = contentHtml,
                FooterLeft = "Sessao · ativa",
                FooterCenter = footerCenter,
                FooterRight = "TremauxLock · v1.0"
            };
        }

        private string BuildUnlockedContent()
        {
            string[] files = Directory.Exists(vaultService.WorkingFolderPath)
                ? Directory.GetFiles(vaultService.WorkingFolderPath, "*", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToArray()
                : Array.Empty<string>();

            if (files.Length == 0)
            {
                return VaultHtmlBuilder.BuildEmptyState(
                    "Pasta vazia",
                    "Nenhum arquivo encontrado na pasta private no momento.",
                    "info");
            }

            return VaultHtmlBuilder.BuildFileRows(
                files.Select(file =>
                {
                    string fileName = Path.GetFileName(file);
                    string relativePath = Path.GetRelativePath(vaultService.WorkingFolderPath, file);
                    long size = new FileInfo(file).Length;

                    return new VaultFileRow
                    {
                        Name = fileName,
                        Meta = $"{VaultCrypto.FormatSize(size)}  {relativePath}",
                        IconText = string.IsNullOrWhiteSpace(fileName) ? "?" : fileName[..1].ToUpperInvariant()
                    };
                }));
        }

        private (string? NoticeText, int NoticePercent, string NoticeTone) BuildNotice(VaultState state)
        {
            if (isBusy && !string.IsNullOrWhiteSpace(progressText))
            {
                int percent = Math.Max(8, (int)Math.Round((lastProgressCurrent / (double)Math.Max(1, lastProgressTotal)) * 100d));
                return (progressText, Math.Min(100, percent), "info");
            }

            if (state == VaultState.Locked && DateTime.UtcNow < unlockCooldownUntilUtc)
            {
                TimeSpan remaining = unlockCooldownUntilUtc - DateTime.UtcNow;
                int seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
                return ($"Tente novamente em {seconds}s", 0, "warning");
            }

            return (null, 0, "info");
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

            RenderOverview();
        }

        private void UpdateProgress(VaultProgress progress)
        {
            lastProgressTotal = Math.Max(1, progress.Total);
            lastProgressCurrent = Math.Min(lastProgressTotal, Math.Max(0, progress.Current));
            progressText = progress.Total > 0
                ? $"{progress.Step} ({progress.Current}/{progress.Total})"
                : progress.Step;

            RenderOverview();
        }

        private void RegisterUnlockFailure(string message)
        {
            failedUnlockAttempts++;

            if (failedUnlockAttempts >= 5)
            {
                failedUnlockAttempts = 0;
                unlockCooldownUntilUtc = DateTime.UtcNow.AddSeconds(15);
                unlockCooldownTimer.Start();
                RenderOverview();

                MessageBox.Show(
                    $"{message}\n\nNovas tentativas foram pausadas por 15 segundos.",
                    "TremauxLock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            RenderOverview();
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
                RenderOverview();
                return;
            }

            RenderOverview();
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
    }
}
