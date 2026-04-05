using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace TremauxLock
{
    internal sealed class RecoveryKeyDialog : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 2;

        private readonly string recoveryKey;
        private readonly int fileCount;
        private readonly long totalBytes;
        private readonly string? backupWarning;
        private readonly WebView2 webView;
        private readonly Panel splashPanel;
        private readonly Label lblSplash;
        private bool webViewReady;

        public RecoveryKeyDialog(string recoveryKey, int fileCount, long totalBytes, string? backupWarning)
        {
            this.recoveryKey = recoveryKey;
            this.fileCount = fileCount;
            this.totalBytes = totalBytes;
            this.backupWarning = backupWarning;

            Text = "Chave de recuperacao";
            ClientSize = new Size(680, 460);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundPrimary;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.5f);
            KeyPreview = true;

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
                Text = "Carregando...",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = AppTheme.TextSecondary,
                BackColor = AppTheme.BackgroundPrimary,
                Font = AppTheme.CreateBodyFont(10f)
            };

            splashPanel.Controls.Add(lblSplash);
            Controls.Add(webView);
            Controls.Add(splashPanel);

            Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            };

            Shown += async (_, _) => await EnsureWebViewAsync();
            KeyDown += OnDialogKeyDown;
        }

        private async Task EnsureWebViewAsync()
        {
            if (webViewReady)
            {
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
                webView.NavigateToString(DialogHtmlBuilder.BuildRecovery(recoveryKey, fileCount, totalBytes, backupWarning));
                webViewReady = true;
                splashPanel.Visible = false;
                webView.Visible = true;
            }
            catch (Exception ex)
            {
                lblSplash.Text = "Falha ao carregar.";
                MessageBox.Show(
                    "Nao foi possivel abrir esta tela.\n\n" + ex.Message,
                    "TremauxLock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                    break;

                case "copy":
                    Clipboard.SetText(recoveryKey);
                    await ApplyNoticeAsync("Chave copiada para a area de transferencia.", false);
                    break;

                case "save":
                    await SaveRecoveryKeyAsync();
                    break;

                case "close":
                    DialogResult = DialogResult.OK;
                    Close();
                    break;
            }
        }

        private async Task SaveRecoveryKeyAsync()
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Texto (*.txt)|*.txt",
                FileName = "tremauxlock-vault-recovery.txt",
                Title = "Salvar chave de recuperacao"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                string content = $"""
                TremauxLock - Chave de recuperacao
                Gerada em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

                Chave:
                {recoveryKey}

                Guarde este arquivo em local seguro.
                """;

                File.WriteAllText(dialog.FileName, content);
                await ApplyNoticeAsync("Chave salva com sucesso.", false);
            }
            catch (Exception ex)
            {
                await ApplyNoticeAsync(ex.Message, true);
            }
        }

        private async Task ApplyNoticeAsync(string message, bool warning)
        {
            if (!webViewReady || webView.CoreWebView2 == null)
            {
                MessageBox.Show(message, "TremauxLock", MessageBoxButtons.OK, warning ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                return;
            }

            string script = $"window.applyRecoveryNotice({JsonSerializer.Serialize(message)}, {(warning ? "true" : "false")});";
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private void OnDialogKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape)
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
