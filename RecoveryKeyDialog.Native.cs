using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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
        private readonly Panel titleBar;
        private readonly Label lblBrand;
        private readonly Button btnClose;
        private readonly VaultCard card;
        private readonly Label lblEyebrow;
        private readonly Label lblTitle;
        private readonly Label lblSummary;
        private readonly Label lblNotice;
        private readonly Label lblHint;
        private readonly SurfacePanel keyPanel;
        private readonly Label lblKeyLabel;
        private readonly TextBox txtRecoveryKey;
        private readonly AccentButton btnCopy;
        private readonly AccentButton btnSave;
        private readonly AccentButton btnDone;

        public RecoveryKeyDialog(string recoveryKey, int fileCount, long totalBytes, string? backupWarning)
        {
            this.recoveryKey = recoveryKey;

            Text = "Chave de recuperacao";
            ClientSize = new Size(700, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundPrimary;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.5f);

            titleBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = AppTheme.Surface };
            titleBar.MouseDown += TitleBarMouseDown;

            lblBrand = new Label
            {
                AutoSize = false,
                Text = "TremauxLock Vault",
                Font = AppTheme.CreateTitleFont(9f),
                ForeColor = AppTheme.AccentBlue,
                BackColor = AppTheme.Surface,
                TextAlign = ContentAlignment.MiddleLeft
            };
            lblBrand.MouseDown += TitleBarMouseDown;

            btnClose = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Text = "X",
                Cursor = Cursors.Hand,
                TabStop = false,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextSecondary
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, AppTheme.AccentRed);
            btnClose.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            titleBar.Controls.AddRange(new Control[] { lblBrand, btnClose });

            card = new VaultCard
            {
                FillColor = AppTheme.Surface,
                SecondaryFillColor = AppTheme.Surface,
                BorderColor = AppTheme.Border,
                InnerStrokeColor = Color.FromArgb(0, 0, 0, 0),
                CornerRadius = 14
            };

            lblEyebrow = CreateCodeLabel("RECOVERY KEY", 8.25f, FontStyle.Bold, AppTheme.AccentBlue);
            lblTitle = CreateTextLabel("Guarde esta chave antes de fechar", 21f, FontStyle.Regular, AppTheme.TextPrimary);
            lblTitle.Font = AppTheme.CreateTitleFont(21f);
            lblSummary = CreateTextLabel($"{fileCount} arquivo(s) foram protegidos, totalizando {VaultCrypto.FormatSize(totalBytes)}.", 9.25f, FontStyle.Regular, AppTheme.TextSecondary);
            lblNotice = CreateTextLabel(string.Empty, 8.75f, FontStyle.Regular, Color.FromArgb(141, 223, 154));
            lblNotice.Visible = false;
            lblNotice.BackColor = Color.FromArgb(20, 63, 185, 80);
            lblHint = CreateTextLabel(
                backupWarning ?? "Salve a chave fora desta pasta para nao depender do executavel atual.",
                8.5f,
                FontStyle.Regular,
                backupWarning == null ? AppTheme.TextSoft : AppTheme.Warning);

            keyPanel = new SurfacePanel
            {
                FillColor = AppTheme.SurfaceInset,
                SecondaryFillColor = AppTheme.SurfaceInset,
                BorderColor = AppTheme.Border,
                InnerStrokeColor = Color.FromArgb(0, 0, 0, 0),
                CornerRadius = 10
            };

            lblKeyLabel = CreateCodeLabel("CHAVE DE RECUPERACAO", 8f, FontStyle.Bold, AppTheme.TextSoft);
            txtRecoveryKey = new TextBox
            {
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = AppTheme.SurfaceInset,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateCodeFont(10f),
                Text = recoveryKey
            };
            keyPanel.Controls.AddRange(new Control[] { lblKeyLabel, txtRecoveryKey });

            btnCopy = new AccentButton { Text = "Copiar chave", Width = 132, ButtonStyle = AccentButtonStyle.Primary };
            btnCopy.Click += (_, _) => CopyRecoveryKey();
            btnSave = new AccentButton { Text = "Salvar arquivo", Width = 138, ButtonStyle = AccentButtonStyle.Secondary };
            btnSave.Click += (_, _) => SaveRecoveryKey();
            btnDone = new AccentButton { Text = "Fechar", Width = 112, ButtonStyle = AccentButtonStyle.Secondary };
            btnDone.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };

            card.Controls.AddRange(new Control[] { lblEyebrow, lblTitle, lblSummary, lblNotice, keyPanel, lblHint, btnCopy, btnSave, btnDone });

            Controls.Add(card);
            Controls.Add(titleBar);

            Resize += (_, _) => LayoutControls();
            Shown += (_, _) =>
            {
                txtRecoveryKey.Focus();
                txtRecoveryKey.SelectAll();
            };

            LayoutControls();
        }

        private void LayoutControls()
        {
            lblBrand.SetBounds(18, 11, 240, 18);
            btnClose.SetBounds(Width - 48, 7, 28, 28);
            card.SetBounds(28, 64, ClientSize.Width - 56, ClientSize.Height - 92);

            int inset = 24;
            int y = 24;

            lblEyebrow.SetBounds(inset, y, card.Width - (inset * 2), 14);
            y += 20;
            lblTitle.SetBounds(inset, y, card.Width - (inset * 2), 28);
            y += 34;
            lblSummary.SetBounds(inset, y, card.Width - (inset * 2), 34);
            y += 44;

            if (lblNotice.Visible)
            {
                lblNotice.SetBounds(inset, y, card.Width - (inset * 2), 32);
                y += 44;
            }

            keyPanel.SetBounds(inset, y, card.Width - (inset * 2), 170);
            lblKeyLabel.SetBounds(14, 14, keyPanel.Width - 28, 14);
            txtRecoveryKey.SetBounds(14, 38, keyPanel.Width - 28, keyPanel.Height - 52);
            y += keyPanel.Height + 16;

            lblHint.SetBounds(inset, y, card.Width - (inset * 2), 36);

            btnDone.SetBounds(card.Width - inset - btnDone.Width, card.Height - 58, btnDone.Width, 34);
            btnSave.SetBounds(btnDone.Left - 10 - btnSave.Width, btnDone.Top, btnSave.Width, 34);
            btnCopy.SetBounds(btnSave.Left - 10 - btnCopy.Width, btnDone.Top, btnCopy.Width, 34);
        }

        private void CopyRecoveryKey()
        {
            Clipboard.SetText(recoveryKey);
            ShowNotice("Chave copiada para a area de transferencia.", false);
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
                ShowNotice("Chave salva com sucesso.", false);
            }
            catch (Exception ex)
            {
                ShowNotice(ex.Message, true);
            }
        }

        private void ShowNotice(string message, bool warning)
        {
            lblNotice.Text = "  " + message;
            lblNotice.BackColor = warning ? Color.FromArgb(22, 225, 178, 94) : Color.FromArgb(20, 63, 185, 80);
            lblNotice.ForeColor = warning ? AppTheme.Warning : Color.FromArgb(141, 223, 154);
            lblNotice.Visible = true;
            LayoutControls();
        }

        private void TitleBarMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        private static Label CreateTextLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label { AutoSize = false, Text = text, Font = AppTheme.CreateBodyFont(size, style), ForeColor = color, BackColor = AppTheme.Surface };
        }

        private static Label CreateCodeLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label { AutoSize = false, Text = text, Font = AppTheme.CreateCodeFont(size, style), ForeColor = color, BackColor = AppTheme.Surface };
        }
    }
}
