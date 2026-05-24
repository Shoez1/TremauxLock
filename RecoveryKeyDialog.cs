using System;
using System.Drawing;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed class RecoveryKeyDialog : Form
    {
        private const int ClipboardClearDelayMs = 60000;

        public RecoveryKeyDialog(string recoveryKey, int fileCount, long totalBytes, string? backupWarning)
        {
            Text = "Chave de recuperacao";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.BackgroundPrimary;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.5f);
            ClientSize = new Size(620, 460);

            var lblHead = new Label
            {
                Bounds = new Rectangle(24, 20, ClientSize.Width - 48, 36),
                ForeColor = AppTheme.HackerCyan,
                Font = AppTheme.CreateTitleFont(15f),
                Text = "Guarde esta chave fora deste computador",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblSub = new Label
            {
                Bounds = new Rectangle(24, 56, ClientSize.Width - 48, 40),
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(9.5f),
                Text = "Sem esta chave, apenas a senha principal desbloqueia o cofre. Copie e armazene em local seguro."
            };

            var txtKey = new TextBox
            {
                Bounds = new Rectangle(24, 108, ClientSize.Width - 48, 120),
                ReadOnly = true,
                Multiline = true,
                WordWrap = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = AppTheme.BackgroundPanel,
                ForeColor = AppTheme.TextCode,
                Font = AppTheme.CreateCodeFont(11f),
                Text = recoveryKey
            };

            string stats = $"Arquivos: {fileCount}   |   Volume: {VaultCrypto.FormatSize(totalBytes)}";
            var lblStats = new Label
            {
                Bounds = new Rectangle(24, 238, ClientSize.Width - 48, 22),
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(9f),
                Text = stats
            };

            Label? lblWarn = null;
            if (!string.IsNullOrWhiteSpace(backupWarning))
            {
                lblWarn = new Label
                {
                    Bounds = new Rectangle(24, 266, ClientSize.Width - 48, 72),
                    ForeColor = AppTheme.TextWarning,
                    Font = AppTheme.CreateBodyFont(9f),
                    Text = backupWarning
                };
            }

            var clipboardTimer = new System.Windows.Forms.Timer
            {
                Interval = ClipboardClearDelayMs
            };
            clipboardTimer.Tick += (_, _) =>
            {
                clipboardTimer.Stop();
                TryClearClipboardIfUnchanged(recoveryKey);
            };
            Disposed += (_, _) => clipboardTimer.Dispose();

            var btnCopy = new HackerButton
            {
                Text = "Copiar chave",
                ButtonStyle = HackerButtonStyle.Accent,
                Location = new Point(24, ClientSize.Height - 24 - 44),
                Width = 180,
                Height = 44
            };
            btnCopy.Click += (_, _) =>
            {
                try
                {
                    Clipboard.SetText(recoveryKey);
                    clipboardTimer.Stop();
                    clipboardTimer.Start();
                    MessageBox.Show("Chave copiada. A area de transferencia sera limpa em 60 segundos se continuar igual.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nao foi possivel copiar: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            var btnOk = new HackerButton
            {
                Text = "Entendi",
                ButtonStyle = HackerButtonStyle.Primary,
                Location = new Point(ClientSize.Width - 24 - 180, ClientSize.Height - 24 - 44),
                Width = 180,
                Height = 44,
                DialogResult = DialogResult.OK
            };

            AcceptButton = btnOk;

            Controls.Add(lblHead);
            Controls.Add(lblSub);
            Controls.Add(txtKey);
            Controls.Add(lblStats);
            if (lblWarn != null)
            {
                Controls.Add(lblWarn);
            }

            Controls.Add(btnCopy);
            Controls.Add(btnOk);

            Shown += (_, _) => txtKey.SelectAll();
        }

        private static void TryClearClipboardIfUnchanged(string expectedText)
        {
            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText() == expectedText)
                {
                    Clipboard.Clear();
                }
            }
            catch
            {
            }
        }
    }
}
