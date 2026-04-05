using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TremauxLock
{
    internal enum CredentialDialogMode
    {
        CreatePassword,
        UnlockWithPassword,
        UnlockWithRecoveryKey
    }

    internal sealed class CredentialDialog : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 2;
        private const int WM_NCHITTEST = 0x84;
        private const int ResizeBorder = 6;

        private readonly CredentialDialogMode mode;
        private readonly Panel titleBar;
        private readonly Label lblWindowTitle;
        private readonly Button btnClose;
        private readonly Panel shellPanel;
        private readonly Panel headerPanel;
        private readonly Panel contentPanel;
        private readonly Panel footerPanel;
        private readonly Label lblEyebrow;
        private readonly Label lblTitle;
        private readonly Label lblDescription;
        private readonly SurfacePanel errorPanel;
        private readonly Label lblError;
        private readonly Label lblPrimary;
        private readonly Panel primaryShell;
        private readonly TextBox txtPrimary;
        private readonly Label lblConfirm;
        private readonly Panel confirmShell;
        private readonly TextBox txtConfirm;
        private readonly Label lblHint;
        private readonly SurfacePanel infoPanel;
        private readonly Label lblInfoTitle;
        private readonly Label lblInfoLineOne;
        private readonly Label lblInfoLineTwo;
        private readonly Label lblInfoLineThree;
        private readonly AccentButton btnCancel;
        private readonly AccentButton btnSubmit;

        public CredentialDialog(CredentialDialogMode mode)
        {
            this.mode = mode;

            Text = mode switch
            {
                CredentialDialogMode.CreatePassword => "Proteger cofre",
                CredentialDialogMode.UnlockWithPassword => "Desbloquear com senha",
                _ => "Usar chave de recuperacao"
            };

            ClientSize = mode == CredentialDialogMode.CreatePassword
                ? new Size(680, 454)
                : new Size(680, 378);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            ResizeRedraw = true;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundPrimary;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.25f);

            titleBar = new Panel { Height = 38, BackColor = AppTheme.Surface };
            titleBar.MouseDown += TitleBarMouseDown;

            lblWindowTitle = new Label
            {
                AutoSize = false,
                Text = Text,
                Font = AppTheme.CreateBodyFont(8.5f, FontStyle.Bold),
                ForeColor = AppTheme.AccentBlue,
                BackColor = AppTheme.Surface,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            lblWindowTitle.MouseDown += TitleBarMouseDown;

            btnClose = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Text = "X",
                Width = 32,
                Cursor = Cursors.Hand,
                TabStop = false,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(9f, FontStyle.Bold)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(18, 248, 81, 73);
            btnClose.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            titleBar.Controls.AddRange(new Control[] { lblWindowTitle, btnClose });

            shellPanel = CreateRoundedShellPanel(AppTheme.Surface);

            headerPanel = new Panel { BackColor = AppTheme.Surface };
            headerPanel.Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            lblEyebrow = CreateCodeLabel(string.Empty, 7.5f, FontStyle.Bold, AppTheme.AccentBlue, AppTheme.Surface);
            lblTitle = CreateTextLabel(string.Empty, 14f, FontStyle.Bold, AppTheme.TextPrimary, AppTheme.Surface);
            lblTitle.Font = AppTheme.CreateTitleFont(14f);
            lblDescription = CreateTextLabel(string.Empty, 9f, FontStyle.Regular, AppTheme.TextSecondary, AppTheme.Surface);
            headerPanel.Controls.AddRange(new Control[] { lblEyebrow, lblTitle, lblDescription });

            contentPanel = new Panel { BackColor = AppTheme.Surface };

            errorPanel = new SurfacePanel
            {
                FillColor = Color.FromArgb(34, 64, 28, 31),
                SecondaryFillColor = Color.FromArgb(34, 64, 28, 31),
                BorderColor = Color.FromArgb(88, 248, 81, 73),
                InnerStrokeColor = Color.FromArgb(0, 0, 0, 0),
                CornerRadius = 8,
                Visible = false
            };
            lblError = CreateTextLabel(string.Empty, 8.75f, FontStyle.Regular, Color.FromArgb(255, 187, 182), errorPanel.FillColor);
            lblError.Visible = false;
            errorPanel.Controls.Add(lblError);

            lblPrimary = CreateTextLabel(string.Empty, 8.75f, FontStyle.Bold, AppTheme.TextSecondary, AppTheme.Surface);
            primaryShell = CreateInputShell(out txtPrimary);

            lblConfirm = CreateTextLabel("Confirmar senha", 8.75f, FontStyle.Bold, AppTheme.TextSecondary, AppTheme.Surface);
            confirmShell = CreateInputShell(out txtConfirm);

            lblHint = CreateTextLabel(string.Empty, 8.5f, FontStyle.Regular, AppTheme.TextSoft, AppTheme.Surface);
            infoPanel = new SurfacePanel
            {
                FillColor = Color.FromArgb(20, 18, 28, 43),
                SecondaryFillColor = Color.FromArgb(20, 18, 28, 43),
                BorderColor = Color.FromArgb(52, 88, 166, 255),
                InnerStrokeColor = Color.FromArgb(0, 0, 0, 0),
                CornerRadius = 8
            };
            lblInfoTitle = CreateCodeLabel("ORIENTACAO", 7.75f, FontStyle.Bold, AppTheme.AccentBlue, infoPanel.FillColor);
            lblInfoLineOne = CreateTextLabel(string.Empty, 8.5f, FontStyle.Regular, AppTheme.TextSecondary, infoPanel.FillColor);
            lblInfoLineTwo = CreateTextLabel(string.Empty, 8.5f, FontStyle.Regular, AppTheme.TextSecondary, infoPanel.FillColor);
            lblInfoLineThree = CreateTextLabel(string.Empty, 8.5f, FontStyle.Regular, AppTheme.TextSecondary, infoPanel.FillColor);
            infoPanel.Controls.AddRange(new Control[] { lblInfoTitle, lblInfoLineOne, lblInfoLineTwo, lblInfoLineThree });
            contentPanel.Controls.AddRange(new Control[]
            {
                errorPanel, lblPrimary, primaryShell, lblConfirm, confirmShell, lblHint, infoPanel
            });

            footerPanel = new Panel { BackColor = AppTheme.Surface };
            footerPanel.Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
            };

            btnCancel = new AccentButton { Text = "Cancelar", Width = 118, ButtonStyle = AccentButtonStyle.Ghost };
            btnSubmit = new AccentButton { Text = "Continuar", Width = 148, ButtonStyle = AccentButtonStyle.Primary };
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            btnSubmit.Click += (_, _) => ValidateAndClose();
            footerPanel.Controls.AddRange(new Control[] { btnCancel, btnSubmit });

            shellPanel.Controls.AddRange(new Control[] { contentPanel, footerPanel, headerPanel });
            Controls.AddRange(new Control[] { shellPanel, titleBar });

            Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            Resize += (_, _) => LayoutControls();
            Shown += (_, _) => txtPrimary.Focus();
            KeyDown += OnDialogKeyDown;
            txtPrimary.TextChanged += (_, _) =>
            {
                ClearError();
                UpdateInfoPanel();
            };
            txtConfirm.TextChanged += (_, _) =>
            {
                ClearError();
                UpdateInfoPanel();
            };

            ApplyMode();
            LayoutControls();
        }

        public string Secret { get; private set; } = string.Empty;

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

        private void ApplyMode()
        {
            lblError.Visible = false;
            errorPanel.Visible = false;
            lblError.Text = string.Empty;
            txtPrimary.Text = string.Empty;
            txtConfirm.Text = string.Empty;
            txtPrimary.Multiline = false;
            txtPrimary.AcceptsReturn = false;
            txtPrimary.ScrollBars = ScrollBars.None;
            txtPrimary.UseSystemPasswordChar = false;
            txtConfirm.UseSystemPasswordChar = false;
            txtPrimary.Font = AppTheme.CreateBodyFont(10f);
            txtConfirm.Font = AppTheme.CreateBodyFont(10f);

            if (mode == CredentialDialogMode.CreatePassword)
            {
                lblEyebrow.Text = "CONFIGURACAO DE SENHA";
                lblTitle.Text = "Proteja o cofre com uma senha";
                lblDescription.Text = "Defina a senha que sera exigida para restaurar os arquivos ocultos do cofre.";
                lblPrimary.Text = "Senha";
                lblConfirm.Visible = true;
                confirmShell.Visible = true;
                lblHint.Text = $"Use pelo menos {VaultCrypto.MinimumPasswordLength} caracteres. A chave de recuperacao sera exibida ao final.";
                txtPrimary.PlaceholderText = "Digite uma senha forte";
                txtConfirm.PlaceholderText = "Repita a mesma senha";
                txtPrimary.UseSystemPasswordChar = true;
                txtConfirm.UseSystemPasswordChar = true;
                btnSubmit.Text = "Bloquear cofre";
                btnSubmit.ButtonStyle = AccentButtonStyle.Danger;
                infoPanel.Visible = true;
            }
            else if (mode == CredentialDialogMode.UnlockWithPassword)
            {
                lblEyebrow.Text = "ACESSO POR SENHA";
                lblTitle.Text = "Desbloqueie com sua senha";
                lblDescription.Text = "Digite a senha definida no ultimo bloqueio para restaurar a pasta private.";
                lblPrimary.Text = "Senha do cofre";
                lblConfirm.Visible = false;
                confirmShell.Visible = false;
                lblHint.Text = "Se a senha nao estiver disponivel, voce ainda pode usar a chave de recuperacao.";
                txtPrimary.PlaceholderText = "Digite sua senha";
                txtPrimary.UseSystemPasswordChar = true;
                btnSubmit.Text = "Desbloquear";
                btnSubmit.ButtonStyle = AccentButtonStyle.Primary;
                infoPanel.Visible = true;
            }
            else
            {
                lblEyebrow.Text = "CHAVE DE RECUPERACAO";
                lblTitle.Text = "Restaure com a chave";
                lblDescription.Text = "Cole a chave de recuperacao gerada no ultimo bloqueio para restaurar o cofre.";
                lblPrimary.Text = "Chave de recuperacao";
                lblConfirm.Visible = false;
                confirmShell.Visible = false;
                lblHint.Text = "A chave deve ser informada por completo.";
                txtPrimary.PlaceholderText = "Cole a chave completa";
                txtPrimary.Multiline = true;
                txtPrimary.AcceptsReturn = true;
                txtPrimary.ScrollBars = ScrollBars.Vertical;
                txtPrimary.Font = AppTheme.CreateCodeFont(9.5f);
                btnSubmit.Text = "Validar chave";
                btnSubmit.ButtonStyle = AccentButtonStyle.Primary;
                infoPanel.Visible = true;
            }

            UpdateInfoPanel();
        }

        private void LayoutControls()
        {
            titleBar.SetBounds(0, 0, ClientSize.Width, 38);
            lblWindowTitle.SetBounds(14, 0, 280, 38);
            btnClose.SetBounds(titleBar.Width - 38, 3, 32, 32);

            shellPanel.SetBounds(0, 38, ClientSize.Width, ClientSize.Height - 38);
            headerPanel.SetBounds(0, 0, shellPanel.Width, 92);
            footerPanel.SetBounds(0, shellPanel.Height - 64, shellPanel.Width, 64);
            contentPanel.SetBounds(0, headerPanel.Bottom, shellPanel.Width, footerPanel.Top - headerPanel.Bottom);

            LayoutHeader();
            LayoutContent();
            LayoutFooter();
        }

        private void LayoutHeader()
        {
            int inset = 22;
            int width = headerPanel.Width - (inset * 2);
            lblEyebrow.SetBounds(inset, 16, width, 14);
            lblTitle.SetBounds(inset, 34, width, 22);
            lblDescription.SetBounds(inset, 58, width, 20);
        }

        private void LayoutContent()
        {
            int inset = 22;
            int width = contentPanel.Width - (inset * 2);
            int y = 18;

            if (errorPanel.Visible)
            {
                int errorHeight = Math.Max(36, MeasureTextHeight(lblError, width - 24));
                errorPanel.SetBounds(inset, y, width, errorHeight);
                lblError.SetBounds(12, 9, Math.Max(0, errorPanel.Width - 24), Math.Max(18, errorPanel.Height - 18));
                y += errorHeight + 12;
            }
            else
            {
                errorPanel.SetBounds(0, 0, 0, 0);
            }

            lblPrimary.SetBounds(inset, y, width, 16);
            y += 22;

            int primaryHeight = mode == CredentialDialogMode.UnlockWithRecoveryKey ? 120 : 44;
            primaryShell.SetBounds(inset, y, width, primaryHeight);
            txtPrimary.SetBounds(14, mode == CredentialDialogMode.UnlockWithRecoveryKey ? 12 : 11, primaryShell.Width - 28, primaryShell.Height - (mode == CredentialDialogMode.UnlockWithRecoveryKey ? 24 : 22));
            y += primaryHeight + 16;

            if (confirmShell.Visible)
            {
                lblConfirm.SetBounds(inset, y, width, 16);
                y += 22;
                confirmShell.SetBounds(inset, y, width, 44);
                txtConfirm.SetBounds(14, 11, confirmShell.Width - 28, confirmShell.Height - 22);
                y += 58;
            }
            else
            {
                lblConfirm.SetBounds(0, 0, 0, 0);
                confirmShell.SetBounds(0, 0, 0, 0);
            }

            int hintHeight = Math.Max(18, MeasureTextHeight(lblHint, width));
            lblHint.SetBounds(inset, y, width, hintHeight);
            y += hintHeight + 14;

            if (infoPanel.Visible)
            {
                int infoHeight = mode == CredentialDialogMode.CreatePassword ? 108 : 104;
                infoPanel.SetBounds(inset, y, width, infoHeight);
                lblInfoTitle.SetBounds(14, 12, Math.Max(0, infoPanel.Width - 28), 14);
                lblInfoLineOne.SetBounds(14, 34, Math.Max(0, infoPanel.Width - 28), 16);
                lblInfoLineTwo.SetBounds(14, 54, Math.Max(0, infoPanel.Width - 28), 16);
                lblInfoLineThree.SetBounds(14, 74, Math.Max(0, infoPanel.Width - 28), 16);
            }
            else
            {
                infoPanel.SetBounds(0, 0, 0, 0);
            }
        }

        private void LayoutFooter()
        {
            int buttonTop = 13;
            btnSubmit.SetBounds(footerPanel.Width - 22 - btnSubmit.Width, buttonTop, btnSubmit.Width, 38);
            btnCancel.SetBounds(btnSubmit.Left - 10 - btnCancel.Width, buttonTop, btnCancel.Width, 38);
        }

        private void ValidateAndClose()
        {
            string primaryValue = txtPrimary.Text.Trim();
            if (string.IsNullOrWhiteSpace(primaryValue))
            {
                ShowError("Preencha o campo principal antes de continuar.");
                txtPrimary.Focus();
                return;
            }

            if (mode == CredentialDialogMode.CreatePassword)
            {
                if (primaryValue.Length < VaultCrypto.MinimumPasswordLength)
                {
                    ShowError($"Use pelo menos {VaultCrypto.MinimumPasswordLength} caracteres.");
                    txtPrimary.Focus();
                    return;
                }

                if (!string.Equals(primaryValue, txtConfirm.Text, StringComparison.Ordinal))
                {
                    ShowError("As senhas nao conferem.");
                    txtConfirm.Focus();
                    txtConfirm.SelectAll();
                    return;
                }
            }

            Secret = primaryValue;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            lblError.Visible = true;
            errorPanel.Visible = true;
            LayoutControls();
        }

        private void ClearError()
        {
            if (!errorPanel.Visible) return;
            lblError.Text = string.Empty;
            lblError.Visible = false;
            errorPanel.Visible = false;
            LayoutControls();
        }

        private void UpdateInfoPanel()
        {
            if (mode == CredentialDialogMode.CreatePassword)
            {
                bool lengthOk = txtPrimary.Text.Length >= VaultCrypto.MinimumPasswordLength;
                bool hasConfirm = txtConfirm.Text.Length > 0;
                bool confirmOk = hasConfirm && string.Equals(txtPrimary.Text, txtConfirm.Text, StringComparison.Ordinal);

                lblInfoTitle.Text = "REQUISITOS";
                lblInfoLineOne.Text = lengthOk
                    ? $"OK  Minimo de {VaultCrypto.MinimumPasswordLength} caracteres atendido"
                    : $"Pendente  Use pelo menos {VaultCrypto.MinimumPasswordLength} caracteres";
                lblInfoLineTwo.Text = hasConfirm
                    ? (confirmOk ? "OK  Confirmacao corresponde a senha informada" : "Atencao  A confirmacao ainda nao confere")
                    : "Pendente  Repita a mesma senha no segundo campo";
                lblInfoLineThree.Text = "Info  A recovery key sera exibida assim que o bloqueio terminar";

                lblInfoLineOne.ForeColor = lengthOk ? AppTheme.AccentGreen : AppTheme.TextSecondary;
                lblInfoLineTwo.ForeColor = confirmOk ? AppTheme.AccentGreen : (hasConfirm ? AppTheme.Warning : AppTheme.TextSecondary);
                lblInfoLineThree.ForeColor = AppTheme.TextSecondary;
                return;
            }

            if (mode == CredentialDialogMode.UnlockWithPassword)
            {
                lblInfoTitle.Text = "ORIENTACAO";
                lblInfoLineOne.Text = "Use a senha definida no bloqueio anterior.";
                lblInfoLineTwo.Text = "A validacao acontece antes da restauracao dos arquivos.";
                lblInfoLineThree.Text = "Se necessario, use a recovery key no fluxo alternativo.";
                lblInfoLineOne.ForeColor = AppTheme.TextSecondary;
                lblInfoLineTwo.ForeColor = AppTheme.TextSecondary;
                lblInfoLineThree.ForeColor = AppTheme.TextSecondary;
                return;
            }

            lblInfoTitle.Text = "ORIENTACAO";
            lblInfoLineOne.Text = "Cole a chave inteira, sem alterar linhas ou caracteres.";
            lblInfoLineTwo.Text = "A recovery key so funciona para o cofre que a gerou.";
            lblInfoLineThree.Text = "Se a chave for valida, os arquivos serao restaurados.";
            lblInfoLineOne.ForeColor = AppTheme.TextSecondary;
            lblInfoLineTwo.ForeColor = AppTheme.TextSecondary;
            lblInfoLineThree.ForeColor = AppTheme.TextSecondary;
        }

        private void OnDialogKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void TitleBarMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        private static Panel CreateRoundedShellPanel(Color fillColor)
        {
            Panel panel = new Panel { BackColor = fillColor };
            panel.Resize += (_, _) => UpdateRoundedRegion(panel, 10);
            panel.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                RectangleF bounds = new RectangleF(0.5f, 0.5f, panel.Width - 1f, panel.Height - 1f);
                using var path = AppTheme.CreateRoundedRectangle(bounds, 10);
                using var fillBrush = new SolidBrush(fillColor);
                using var borderPen = new Pen(AppTheme.Border, 1f);
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(borderPen, path);
            };
            return panel;
        }

        private static Panel CreateInputShell(out TextBox textBox)
        {
            Panel panel = new Panel { BackColor = AppTheme.SurfaceInset };

            TextBox innerTextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.SurfaceInset,
                ForeColor = AppTheme.TextPrimary
            };
            textBox = innerTextBox;

            void RefreshShell()
            {
                UpdateRoundedRegion(panel, 8);
                panel.Invalidate();
            }

            panel.Resize += (_, _) => RefreshShell();
            panel.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                RectangleF bounds = new RectangleF(0.5f, 0.5f, panel.Width - 1f, panel.Height - 1f);
                using var path = AppTheme.CreateRoundedRectangle(bounds, 8);
                using var fillBrush = new SolidBrush(AppTheme.SurfaceInset);
                using var borderPen = new Pen(innerTextBox.Focused ? AppTheme.AccentBlue : AppTheme.BorderMid, 1f);
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(borderPen, path);
            };

            innerTextBox.GotFocus += (_, _) => panel.Invalidate();
            innerTextBox.LostFocus += (_, _) => panel.Invalidate();
            panel.Controls.Add(innerTextBox);
            RefreshShell();
            return panel;
        }

        private static void UpdateRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 1 || control.Height <= 1) return;
            control.Region?.Dispose();
            using var path = AppTheme.CreateRoundedRectangle(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius);
            control.Region = new Region(path);
        }

        private static Label CreateTextLabel(string text, float size, FontStyle style, Color color, Color backColor)
        {
            return new Label
            {
                AutoSize = false,
                Text = text,
                Font = AppTheme.CreateBodyFont(size, style),
                ForeColor = color,
                BackColor = backColor,
                UseMnemonic = false
            };
        }

        private static Label CreateCodeLabel(string text, float size, FontStyle style, Color color, Color backColor)
        {
            return new Label
            {
                AutoSize = false,
                Text = text,
                Font = AppTheme.CreateCodeFont(size, style),
                ForeColor = color,
                BackColor = backColor,
                UseMnemonic = false
            };
        }

        private static int MeasureTextHeight(Label label, int width)
        {
            Size measured = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(Math.Max(32, width), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            return measured.Height;
        }
    }
}
