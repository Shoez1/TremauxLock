using System;
using System.Drawing;
using System.Windows.Forms;

namespace TremauxLock
{
    internal enum CredentialDialogMode
    {
        CreatePassword,
        UnlockWithPassword,
        UnlockWithRecoveryKey,
        CreateQuickPassword,
        UnlockQuickPassword
    }

    internal sealed class CredentialDialog : Form
    {
        private const int MaximumPasswordLength = 1024;
        private const int MaximumRecoveryKeyInputLength = 128;
        private const int MaximumHintLength = 140;

        private readonly CredentialDialogMode mode;
        private readonly Label lblTitle;
        private readonly InputField fieldPassword;
        private readonly InputField fieldConfirm;
        private readonly InputField fieldHint;
        private readonly Label lblHintDisplay;
        private readonly TextBox txtRecovery;
        private readonly HackerButton btnOk;
        private readonly HackerButton btnCancel;

        public string Secret { get; private set; } = string.Empty;

        /// <summary>
        /// Dica de senha opcional informada na criacao (ja normalizada/trim).
        /// Vazio quando o usuario nao informou dica.
        /// </summary>
        public string Hint { get; private set; } = string.Empty;

        public CredentialDialog(CredentialDialogMode dialogMode, string? passwordHint = null)
        {
            mode = dialogMode;
            Text = "TremauxLock";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.BackgroundPrimary;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.5f);
            AutoScaleMode = AutoScaleMode.Font;

            bool isCreate = mode == CredentialDialogMode.CreatePassword
                || mode == CredentialDialogMode.CreateQuickPassword;
            bool isUnlockWithHint = (mode == CredentialDialogMode.UnlockWithPassword
                || mode == CredentialDialogMode.UnlockQuickPassword)
                && !string.IsNullOrWhiteSpace(passwordHint);

            int h = mode switch
            {
                CredentialDialogMode.CreatePassword => 478,
                CredentialDialogMode.CreateQuickPassword => 478,
                CredentialDialogMode.UnlockWithRecoveryKey => 380,
                CredentialDialogMode.UnlockWithPassword when isUnlockWithHint => 352,
                CredentialDialogMode.UnlockQuickPassword when isUnlockWithHint => 352,
                _ => 300
            };
            ClientSize = new Size(540, h);

            lblTitle = new Label
            {
                AutoSize = false,
                Bounds = new Rectangle(24, 20, ClientSize.Width - 48, 48),
                ForeColor = AppTheme.HackerCyan,
                Font = AppTheme.CreateTitleFont(16f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblTitle.Text = mode switch
            {
                CredentialDialogMode.CreatePassword => "Definir senha",
                CredentialDialogMode.UnlockWithPassword => "Desbloquear",
                CredentialDialogMode.CreateQuickPassword => "Ocultar pasta",
                CredentialDialogMode.UnlockQuickPassword => "Mostrar pasta",
                _ => "Usar chave de recuperacao"
            };

            lblHintDisplay = new Label
            {
                AutoSize = false,
                Bounds = new Rectangle(24, 72, ClientSize.Width - 48, 40),
                ForeColor = AppTheme.HackerYellow,
                BackColor = AppTheme.BackgroundPrimary,
                Font = AppTheme.CreateBodyFont(9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            int passwordTop = isUnlockWithHint ? 118 : 84;

            fieldPassword = new InputField
            {
                Location = new Point(24, passwordTop),
                Width = ClientSize.Width - 48,
                Caption = mode == CredentialDialogMode.CreatePassword
                    ? "Nova senha"
                    : mode == CredentialDialogMode.CreateQuickPassword
                        ? "Nova senha simples"
                        : mode == CredentialDialogMode.UnlockQuickPassword
                            ? "Senha da ocultacao"
                            : "Senha",
                PlaceholderText = mode == CredentialDialogMode.CreatePassword
                    ? $"Minimo {VaultCrypto.MinimumPasswordLength} caracteres"
                    : mode == CredentialDialogMode.CreateQuickPassword
                        ? $"Minimo {QuickHideService.MinimumPasswordLength} caracteres"
                        : mode == CredentialDialogMode.UnlockQuickPassword
                            ? "Senha usada ao ocultar"
                            : "Senha",
                UsePassword = mode != CredentialDialogMode.UnlockWithRecoveryKey,
                MaxLength = MaximumPasswordLength
            };
            fieldPassword.Visible = mode != CredentialDialogMode.UnlockWithRecoveryKey;

            fieldConfirm = new InputField
            {
                Location = new Point(24, passwordTop + 80),
                Width = ClientSize.Width - 48,
                Caption = "Confirmar senha",
                PlaceholderText = "Repita a senha",
                UsePassword = true,
                MaxLength = MaximumPasswordLength
            };
            fieldConfirm.Visible = isCreate;

            fieldHint = new InputField
            {
                Location = new Point(24, passwordTop + 160),
                Width = ClientSize.Width - 48,
                Caption = "Dica de senha (opcional)",
                PlaceholderText = "Ex: nome do primeiro pet — sem colocar a senha",
                UsePassword = false,
                MaxLength = MaximumHintLength
            };
            fieldHint.Visible = isCreate;

            if (isUnlockWithHint)
            {
                string clean = (passwordHint ?? string.Empty).Trim();
                lblHintDisplay.Text = $"Dica: {clean}";
                lblHintDisplay.Visible = true;
            }

            txtRecovery = new TextBox
            {
                Location = new Point(24, 84),
                Width = ClientSize.Width - 48,
                Height = 160,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = AppTheme.InputFill,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateCodeFont(10f),
                PlaceholderText = "Cole ou digite a chave (hex com hifens)",
                MaxLength = MaximumRecoveryKeyInputLength
            };
            txtRecovery.Visible = mode == CredentialDialogMode.UnlockWithRecoveryKey;

            btnOk = new HackerButton
            {
                Text = "OK",
                ButtonStyle = HackerButtonStyle.Primary,
                Location = new Point(ClientSize.Width - 24 - 160 - 12 - 160, ClientSize.Height - 24 - 44),
                Width = 160,
                Height = 44
            };
            btnOk.Click += BtnOk_Click;

            btnCancel = new HackerButton
            {
                Text = "Cancelar",
                ButtonStyle = HackerButtonStyle.Ghost,
                Location = new Point(ClientSize.Width - 24 - 160, ClientSize.Height - 24 - 44),
                Width = 160,
                Height = 44
            };
            btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lblTitle);
            Controls.Add(lblHintDisplay);
            Controls.Add(fieldPassword);
            Controls.Add(fieldConfirm);
            Controls.Add(fieldHint);
            Controls.Add(txtRecovery);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            Shown += (_, _) =>
            {
                if (mode == CredentialDialogMode.UnlockWithRecoveryKey)
                {
                    txtRecovery.Focus();
                }
                else
                {
                    fieldPassword.Focus();
                }
            };
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            switch (mode)
            {
                case CredentialDialogMode.CreatePassword:
                    if (fieldPassword.TextValue.Length < VaultCrypto.MinimumPasswordLength)
                    {
                        MessageBox.Show(
                            $"A senha precisa ter pelo menos {VaultCrypto.MinimumPasswordLength} caracteres.",
                            Text,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    if (fieldPassword.TextValue != fieldConfirm.TextValue)
                    {
                        MessageBox.Show("As senhas nao coincidem.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        Hint = VaultService.NormalizeAndValidateHint(fieldPassword.TextValue, fieldHint.TextValue);
                    }
                    catch (InvalidOperationException ex)
                    {
                        MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    Secret = fieldPassword.TextValue;
                    break;

                case CredentialDialogMode.CreateQuickPassword:
                    if (fieldPassword.TextValue.Length < QuickHideService.MinimumPasswordLength)
                    {
                        MessageBox.Show(
                            $"A senha simples precisa ter pelo menos {QuickHideService.MinimumPasswordLength} caracteres.",
                            Text,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    if (fieldPassword.TextValue != fieldConfirm.TextValue)
                    {
                        MessageBox.Show("As senhas nao coincidem.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        Hint = QuickHideService.NormalizeAndValidateHint(fieldPassword.TextValue, fieldHint.TextValue);
                    }
                    catch (InvalidOperationException ex)
                    {
                        MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    Secret = fieldPassword.TextValue;
                    break;

                case CredentialDialogMode.UnlockWithPassword:
                case CredentialDialogMode.UnlockQuickPassword:
                    if (string.IsNullOrWhiteSpace(fieldPassword.TextValue))
                    {
                        MessageBox.Show("Informe a senha.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    Secret = fieldPassword.TextValue;
                    break;

                case CredentialDialogMode.UnlockWithRecoveryKey:
                    string key = txtRecovery.Text.Trim();
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        MessageBox.Show("Informe a chave de recuperacao.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!VaultCrypto.IsValidRecoveryKeyFormat(key))
                    {
                        MessageBox.Show(
                            "Formato de chave invalido. A chave deve ter 32 caracteres hexadecimais (ex: ABCD-1234-...)",
                            Text,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    Secret = key;
                    break;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        public void ClearSecret()
        {
            Secret = string.Empty;
            fieldPassword.TextValue = string.Empty;
            fieldConfirm.TextValue = string.Empty;
            fieldHint.TextValue = string.Empty;
            txtRecovery.Text = string.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearSecret();
                Hint = string.Empty;
            }

            base.Dispose(disposing);
        }
    }
}
