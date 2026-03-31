using System;
using System.Drawing;
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
        private readonly CredentialDialogMode mode;
        private readonly Panel shellPanel;
        private readonly Panel headerPanel;
        private readonly Panel bodyPanel;
        private readonly Panel footerPanel;
        private readonly TableLayoutPanel fieldsLayout;
        private readonly Label lblEyebrow;
        private readonly Label lblTitle;
        private readonly Label lblDescription;
        private readonly Label lblHint;
        private readonly Label lblPrimaryCaption;
        private readonly Label lblConfirmCaption;
        private readonly Panel primaryShell;
        private readonly Panel confirmShell;
        private readonly TextBox txtPrimary;
        private readonly TextBox txtConfirm;
        private readonly AccentButton btnConfirm;
        private readonly AccentButton btnCancel;

        private bool primaryFocused;
        private bool confirmFocused;

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
                ? new Size(540, 416)
                : new Size(540, 340);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = Color.FromArgb(11, 16, 27);
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.5f);
            KeyPreview = true;

            shellPanel = CreateBorderPanel(Color.FromArgb(15, 22, 35), Color.FromArgb(38, 52, 72));
            headerPanel = CreateBorderPanel(Color.FromArgb(18, 27, 41), Color.FromArgb(42, 57, 78));
            bodyPanel = new Panel
            {
                BackColor = shellPanel.BackColor
            };
            footerPanel = CreateBorderPanel(Color.FromArgb(13, 20, 31), Color.FromArgb(42, 57, 78));
            fieldsLayout = new TableLayoutPanel
            {
                BackColor = bodyPanel.BackColor,
                ColumnCount = 1,
                RowCount = 5,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            fieldsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            fieldsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
            fieldsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            fieldsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
            fieldsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            fieldsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16f));

            lblEyebrow = CreateLabel(string.Empty, 8.25f, FontStyle.Bold, AppTheme.AccentSoft, headerPanel.BackColor);
            lblTitle = CreateLabel(string.Empty, 15f, FontStyle.Regular, AppTheme.TextPrimary, headerPanel.BackColor);
            lblDescription = CreateLabel(string.Empty, 9f, FontStyle.Regular, AppTheme.TextSecondary, headerPanel.BackColor);
            lblHint = CreateLabel(string.Empty, 8.5f, FontStyle.Regular, AppTheme.TextSoft, bodyPanel.BackColor);
            lblPrimaryCaption = CreateLabel(string.Empty, 8.75f, FontStyle.Bold, AppTheme.TextSecondary, bodyPanel.BackColor);
            lblConfirmCaption = CreateLabel(string.Empty, 8.75f, FontStyle.Bold, AppTheme.TextSecondary, bodyPanel.BackColor);

            lblTitle.Font = AppTheme.CreateTitleFont(15f);
            lblDescription.AutoEllipsis = true;
            lblHint.AutoEllipsis = true;
            lblPrimaryCaption.Dock = DockStyle.Fill;
            lblConfirmCaption.Dock = DockStyle.Fill;
            lblHint.Dock = DockStyle.Fill;

            primaryShell = CreateInputShell(() => primaryFocused);
            confirmShell = CreateInputShell(() => confirmFocused);
            confirmShell.Visible = false;
            primaryShell.Dock = DockStyle.Fill;
            confirmShell.Dock = DockStyle.Fill;

            txtPrimary = CreateTextBox();
            txtConfirm = CreateTextBox();

            txtPrimary.Enter += (_, _) =>
            {
                primaryFocused = true;
                primaryShell.Invalidate();
            };
            txtPrimary.Leave += (_, _) =>
            {
                primaryFocused = false;
                primaryShell.Invalidate();
            };

            txtConfirm.Enter += (_, _) =>
            {
                confirmFocused = true;
                confirmShell.Invalidate();
            };
            txtConfirm.Leave += (_, _) =>
            {
                confirmFocused = false;
                confirmShell.Invalidate();
            };

            primaryShell.Controls.Add(txtPrimary);
            confirmShell.Controls.Add(txtConfirm);

            btnConfirm = new AccentButton
            {
                Text = "Continuar",
                Width = 146,
                Height = 36,
                ButtonStyle = AccentButtonStyle.Primary
            };
            btnConfirm.Click += (_, _) => ValidateAndClose();

            btnCancel = new AccentButton
            {
                Text = "Cancelar",
                Width = 120,
                Height = 36,
                ButtonStyle = AccentButtonStyle.Secondary,
                DialogResult = DialogResult.Cancel
            };

            headerPanel.Controls.Add(lblEyebrow);
            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblDescription);

            fieldsLayout.Controls.Add(lblPrimaryCaption, 0, 0);
            fieldsLayout.Controls.Add(primaryShell, 0, 1);
            fieldsLayout.Controls.Add(lblConfirmCaption, 0, 2);
            fieldsLayout.Controls.Add(confirmShell, 0, 3);
            fieldsLayout.Controls.Add(lblHint, 0, 4);

            bodyPanel.Controls.Add(fieldsLayout);

            footerPanel.Controls.Add(btnCancel);
            footerPanel.Controls.Add(btnConfirm);

            shellPanel.Controls.Add(headerPanel);
            shellPanel.Controls.Add(bodyPanel);
            shellPanel.Controls.Add(footerPanel);

            Controls.Add(shellPanel);

            AcceptButton = btnConfirm;
            CancelButton = btnCancel;

            Resize += (_, _) => LayoutControls();
            Shown += (_, _) => txtPrimary.Focus();
            KeyDown += OnDialogKeyDown;

            ApplyMode();
            LayoutControls();
        }

        public string Secret { get; private set; } = string.Empty;

        private void ApplyMode()
        {
            primaryFocused = false;
            confirmFocused = false;
            txtPrimary.Text = string.Empty;
            txtConfirm.Text = string.Empty;
            txtPrimary.UseSystemPasswordChar = false;
            txtConfirm.UseSystemPasswordChar = false;
            txtPrimary.Font = AppTheme.CreateBodyFont(10f);
            txtConfirm.Font = AppTheme.CreateBodyFont(10f);

            if (mode == CredentialDialogMode.CreatePassword)
            {
                lblEyebrow.Text = "CONFIGURACAO DE SENHA";
                lblTitle.Text = "Proteja o cofre com uma senha";
                lblDescription.Text = "Defina a senha que sera exigida para restaurar os arquivos ocultos do cofre.";
                lblHint.Text = $"Use pelo menos {VaultCrypto.MinimumPasswordLength} caracteres. A chave de recuperacao sera exibida ao final.";

                lblPrimaryCaption.Text = "Senha";
                txtPrimary.PlaceholderText = "Digite uma senha forte";
                txtPrimary.UseSystemPasswordChar = true;

                lblConfirmCaption.Text = "Confirmar senha";
                txtConfirm.PlaceholderText = "Repita a mesma senha";
                txtConfirm.UseSystemPasswordChar = true;
                lblConfirmCaption.Visible = true;
                confirmShell.Visible = true;
                fieldsLayout.RowStyles[2].Height = 18f;
                fieldsLayout.RowStyles[3].Height = 52f;

                btnConfirm.Text = "Bloquear cofre";
            }
            else if (mode == CredentialDialogMode.UnlockWithPassword)
            {
                lblEyebrow.Text = "ACESSO POR SENHA";
                lblTitle.Text = "Desbloqueie com sua senha";
                lblDescription.Text = "Digite a senha definida no ultimo bloqueio para restaurar a pasta private.";
                lblHint.Text = "Se a senha nao estiver disponivel, voce ainda pode usar a chave de recuperacao.";

                lblPrimaryCaption.Text = "Senha do cofre";
                txtPrimary.PlaceholderText = "Digite sua senha";
                txtPrimary.UseSystemPasswordChar = true;

                lblConfirmCaption.Visible = false;
                confirmShell.Visible = false;
                fieldsLayout.RowStyles[2].Height = 0f;
                fieldsLayout.RowStyles[3].Height = 0f;
                btnConfirm.Text = "Desbloquear";
            }
            else
            {
                lblEyebrow.Text = "CHAVE DE RECUPERACAO";
                lblTitle.Text = "Restaure com a chave";
                lblDescription.Text = "Cole a chave de recuperacao gerada no ultimo bloqueio para restaurar o cofre.";
                lblHint.Text = "A chave deve ser informada por completo.";

                lblPrimaryCaption.Text = "Chave de recuperacao";
                txtPrimary.PlaceholderText = "Cole a chave completa";
                txtPrimary.Font = AppTheme.CreateCodeFont(10f);

                lblConfirmCaption.Visible = false;
                confirmShell.Visible = false;
                fieldsLayout.RowStyles[2].Height = 0f;
                fieldsLayout.RowStyles[3].Height = 0f;
                btnConfirm.Text = "Validar chave";
            }

            primaryShell.Invalidate();
            confirmShell.Invalidate();
        }

        private void LayoutControls()
        {
            shellPanel.SetBounds(16, 16, ClientSize.Width - 32, ClientSize.Height - 32);

            const int headerHeight = 96;
            const int footerHeight = 68;

            headerPanel.SetBounds(0, 0, shellPanel.Width, headerHeight);
            bodyPanel.SetBounds(0, headerPanel.Bottom, shellPanel.Width, shellPanel.Height - headerHeight - footerHeight);
            footerPanel.SetBounds(0, shellPanel.Height - footerHeight, shellPanel.Width, footerHeight);

            int headerInset = 20;
            lblEyebrow.SetBounds(headerInset, 16, headerPanel.Width - (headerInset * 2), 14);
            lblTitle.SetBounds(headerInset, 34, headerPanel.Width - (headerInset * 2), 24);
            lblDescription.SetBounds(headerInset, 60, headerPanel.Width - (headerInset * 2), 18);

            int bodyInset = 20;
            int fieldWidth = bodyPanel.Width - (bodyInset * 2);
            fieldsLayout.SetBounds(bodyInset, 18, fieldWidth, bodyPanel.Height - 36);
            lblPrimaryCaption.Margin = Padding.Empty;
            primaryShell.Margin = Padding.Empty;
            lblConfirmCaption.Margin = Padding.Empty;
            confirmShell.Margin = Padding.Empty;
            lblHint.Margin = Padding.Empty;
            fieldsLayout.PerformLayout();

            txtPrimary.SetBounds(12, 14, Math.Max(0, primaryShell.Width - 24), 18);

            if (confirmShell.Visible)
            {
                txtConfirm.SetBounds(12, 14, Math.Max(0, confirmShell.Width - 24), 18);
            }

            int footerInset = 16;
            int buttonTop = (footerPanel.Height - btnConfirm.Height) / 2;
            btnConfirm.SetBounds(footerPanel.Width - footerInset - btnConfirm.Width, buttonTop, btnConfirm.Width, btnConfirm.Height);
            btnCancel.SetBounds(btnConfirm.Left - 8 - btnCancel.Width, buttonTop, btnCancel.Width, btnCancel.Height);
        }

        private void ValidateAndClose()
        {
            string primaryValue = txtPrimary.Text.Trim();
            if (string.IsNullOrWhiteSpace(primaryValue))
            {
                MessageBox.Show("Preencha o campo principal antes de continuar.", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPrimary.Focus();
                return;
            }

            if (mode == CredentialDialogMode.CreatePassword)
            {
                if (primaryValue.Length < VaultCrypto.MinimumPasswordLength)
                {
                    MessageBox.Show($"Use pelo menos {VaultCrypto.MinimumPasswordLength} caracteres.", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtPrimary.Focus();
                    return;
                }

                if (!string.Equals(primaryValue, txtConfirm.Text, StringComparison.Ordinal))
                {
                    MessageBox.Show("As senhas nao conferem.", "TremauxLock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtConfirm.Focus();
                    txtConfirm.SelectAll();
                    return;
                }
            }

            Secret = primaryValue;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnDialogKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private static Panel CreateInputShell(Func<bool> isFocused)
        {
            Panel panel = new Panel
            {
                BackColor = AppTheme.InputFill
            };

            panel.Paint += (_, e) =>
            {
                using var pen = new Pen(isFocused() ? AppTheme.InputBorderFocus : AppTheme.InputBorder, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, panel.Width - 1), Math.Max(0, panel.Height - 1));
            };

            return panel;
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.InputFill,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateBodyFont(10f),
                Multiline = false
            };
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
