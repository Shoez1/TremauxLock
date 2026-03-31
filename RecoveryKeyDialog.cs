using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed class RecoveryKeyDialog : Form
    {
        private readonly Panel shellPanel;
        private readonly Panel keyPanel;
        private readonly TextBox txtRecoveryKey;
        private readonly Label lblHint;
        private readonly AccentButton btnCopy;
        private readonly AccentButton btnSave;
        private readonly AccentButton btnClose;

        public RecoveryKeyDialog(string recoveryKey, int fileCount, long totalBytes, string? backupWarning)
        {
            Text = "Chave de recuperacao";
            Width = 560;
            Height = 340;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = Color.FromArgb(11, 16, 27);
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.5f);

            shellPanel = CreateBorderPanel(Color.FromArgb(15, 22, 35), Color.FromArgb(38, 52, 72));

            Label lblEyebrow = CreateLabel("RECOVERY KEY", 8.25f, FontStyle.Bold, AppTheme.AccentSoft, shellPanel.BackColor);
            Label lblTitle = CreateLabel("Guarde esta chave antes de fechar", 14.5f, FontStyle.Regular, AppTheme.TextPrimary, shellPanel.BackColor);
            Label lblSummary = CreateLabel(
                $"{fileCount} arquivo(s) foram protegidos, totalizando {VaultCrypto.FormatSize(totalBytes)}.",
                9f,
                FontStyle.Regular,
                AppTheme.TextSecondary,
                shellPanel.BackColor);

            lblTitle.Font = AppTheme.CreateTitleFont(14.5f);

            keyPanel = CreateBorderPanel(Color.FromArgb(12, 19, 30), Color.FromArgb(42, 56, 77));

            txtRecoveryKey = new TextBox
            {
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Multiline = false,
                BackColor = keyPanel.BackColor,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateCodeFont(11f, FontStyle.Bold),
                Text = recoveryKey
            };

            keyPanel.Controls.Add(txtRecoveryKey);

            lblHint = CreateLabel(
                backupWarning ?? "Salve a chave fora desta pasta para nao depender do executavel atual.",
                8.75f,
                FontStyle.Regular,
                backupWarning == null ? AppTheme.TextSoft : AppTheme.Warning,
                shellPanel.BackColor);
            lblHint.AutoEllipsis = true;

            btnCopy = new AccentButton
            {
                Text = "Copiar chave",
                Width = 132,
                Height = 36,
                ButtonStyle = AccentButtonStyle.Primary
            };
            btnCopy.Click += (_, _) =>
            {
                Clipboard.SetText(txtRecoveryKey.Text);
                MessageBox.Show("Chave copiada para a area de transferencia.", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnSave = new AccentButton
            {
                Text = "Salvar arquivo",
                Width = 138,
                Height = 36,
                ButtonStyle = AccentButtonStyle.Secondary
            };
            btnSave.Click += (_, _) => SaveRecoveryKey();

            btnClose = new AccentButton
            {
                Text = "Fechar",
                Width = 112,
                Height = 36,
                ButtonStyle = AccentButtonStyle.Secondary,
                DialogResult = DialogResult.OK
            };

            shellPanel.Controls.Add(lblEyebrow);
            shellPanel.Controls.Add(lblTitle);
            shellPanel.Controls.Add(lblSummary);
            shellPanel.Controls.Add(keyPanel);
            shellPanel.Controls.Add(lblHint);
            shellPanel.Controls.Add(btnCopy);
            shellPanel.Controls.Add(btnSave);
            shellPanel.Controls.Add(btnClose);

            Controls.Add(shellPanel);

            Resize += (_, _) =>
            {
                shellPanel.SetBounds(18, 18, ClientSize.Width - 36, ClientSize.Height - 36);

                int inset = 18;
                int contentWidth = shellPanel.Width - (inset * 2);

                lblEyebrow.SetBounds(inset, 16, contentWidth, 14);
                lblTitle.SetBounds(inset, 34, contentWidth, 24);
                lblSummary.SetBounds(inset, 62, contentWidth, 18);
                keyPanel.SetBounds(inset, 92, contentWidth, 54);
                txtRecoveryKey.SetBounds(12, 18, keyPanel.Width - 24, 18);
                lblHint.SetBounds(inset, 156, contentWidth, 34);

                int buttonTop = shellPanel.Height - 54;
                btnClose.SetBounds(shellPanel.Width - inset - btnClose.Width, buttonTop, btnClose.Width, btnClose.Height);
                btnSave.SetBounds(btnClose.Left - 8 - btnSave.Width, buttonTop, btnSave.Width, btnSave.Height);
                btnCopy.SetBounds(btnSave.Left - 8 - btnCopy.Width, buttonTop, btnCopy.Width, btnCopy.Height);
            };

            Shown += (_, _) =>
            {
                txtRecoveryKey.Focus();
                txtRecoveryKey.SelectAll();
            };

            OnResize(EventArgs.Empty);
        }

        private void SaveRecoveryKey()
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

            string content = $"""
            TremauxLock - Chave de recuperacao
            Gerada em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

            Chave:
            {txtRecoveryKey.Text}

            Guarde este arquivo em local seguro.
            """;

            File.WriteAllText(dialog.FileName, content);
            MessageBox.Show("Chave salva com sucesso.", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static Panel CreateBorderPanel(Color backColor, Color borderColor)
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

        private static Label CreateLabel(string text, float size, FontStyle style, Color color, Color backColor)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = backColor,
                Text = text,
                ForeColor = color,
                Font = AppTheme.CreateBodyFont(size, style)
            };
        }
    }
}
