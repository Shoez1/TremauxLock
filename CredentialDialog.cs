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
        private const int MaximumPasswordLength = 1024;
        private const int MaximumRecoveryKeyInputLength = 128;

        private readonly CredentialDialogMode mode;
        private readonly Label lblTitle;
        private readonly InputField fieldPassword;
        private readonly InputField fieldConfirm;
        private readonly TextBox txtRecovery;
        private readonly Label lblStrength;
        private readonly CheckBox chkShowPassword;
        private readonly HackerButton btnOk;
        private readonly HackerButton btnCancel;

        public string Secret { get; private set; } = string.Empty;

        public CredentialDialog(CredentialDialogMode dialogMode)
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

            int height = mode switch
            {
                CredentialDialogMode.CreatePassword => 410,
                CredentialDialogMode.UnlockWithRecoveryKey => 380,
                _ => 320
            };
            ClientSize = new Size(540, height);

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
                CredentialDialogMode.CreatePassword => "Definir senha do cofre",
                CredentialDialogMode.UnlockWithPassword => "Desbloquear com senha",
                _ => "Usar chave de recuperação"
            };

            fieldPassword = new InputField
            {
                Location = new Point(24, 84),
                Width = ClientSize.Width - 48,
                Caption = mode == CredentialDialogMode.CreatePassword
                    ? "Nova senha"
                    : "Senha",
                PlaceholderText = mode == CredentialDialogMode.CreatePassword
                    ? "Digite a senha"
                    : "Senha do cofre",
                UsePassword = mode != CredentialDialogMode.UnlockWithRecoveryKey,
                MaxLength = MaximumPasswordLength,
                AccessibleName = "Senha do cofre"
            };
            fieldPassword.Visible = mode != CredentialDialogMode.UnlockWithRecoveryKey;

            fieldConfirm = new InputField
            {
                Location = new Point(24, 164),
                Width = ClientSize.Width - 48,
                Caption = "Confirmar senha",
                PlaceholderText = "Repita a senha",
                UsePassword = true,
                MaxLength = MaximumPasswordLength,
                AccessibleName = "Confirmação da senha"
            };
            fieldConfirm.Visible = mode == CredentialDialogMode.CreatePassword;

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
                PlaceholderText = "Cole ou digite a chave (hex com hífens)",
                MaxLength = MaximumRecoveryKeyInputLength,
                AccessibleName = "Chave de recuperação"
            };
            txtRecovery.Visible = mode == CredentialDialogMode.UnlockWithRecoveryKey;

            lblStrength = new Label
            {
                AutoSize = false,
                Bounds = new Rectangle(24, 240, ClientSize.Width - 48, 22),
                ForeColor = AppTheme.TextSecondary,
                BackColor = BackColor,
                Font = AppTheme.CreateBodyFont(8.75f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            chkShowPassword = new CheckBox
            {
                Bounds = mode == CredentialDialogMode.CreatePassword
                    ? new Rectangle(24, 268, 220, 24)
                    : new Rectangle(24, 164, 220, 24),
                Text = "Mostrar senhas",
                ForeColor = AppTheme.TextSecondary,
                BackColor = BackColor,
                FlatStyle = FlatStyle.System,
                Visible = mode != CredentialDialogMode.UnlockWithRecoveryKey,
                AccessibleName = "Mostrar senhas"
            };
            chkShowPassword.CheckedChanged += (_, _) =>
            {
                fieldPassword.UsePassword = !chkShowPassword.Checked;
                fieldConfirm.UsePassword = !chkShowPassword.Checked;
            };

            fieldPassword.TextValueChanged += (_, _) => UpdateStrengthLabel();

            btnOk = new HackerButton
            {
                Text = "OK",
                ButtonStyle = HackerButtonStyle.Primary,
                Location = new Point(ClientSize.Width - 24 - 160 - 12 - 160, ClientSize.Height - 24 - 44),
                Width = 160,
                Height = 44,
                AccessibleName = "Confirmar"
            };
            btnOk.Click += BtnOk_Click;

            btnCancel = new HackerButton
            {
                Text = "Cancelar",
                ButtonStyle = HackerButtonStyle.Ghost,
                Location = new Point(ClientSize.Width - 24 - 160, ClientSize.Height - 24 - 44),
                Width = 160,
                Height = 44,
                AccessibleName = "Cancelar"
            };
            btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lblTitle);
            Controls.Add(fieldPassword);
            Controls.Add(fieldConfirm);
            Controls.Add(txtRecovery);
            Controls.Add(lblStrength);
            Controls.Add(chkShowPassword);
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

        private void UpdateStrengthLabel()
        {
            if (mode != CredentialDialogMode.CreatePassword)
            {
                return;
            }

            string password = fieldPassword.TextValue;
            if (string.IsNullOrEmpty(password))
            {
                lblStrength.Visible = false;
                lblStrength.Text = string.Empty;
                return;
            }

            PasswordStrengthLevel level = PasswordStrength.Evaluate(password);
            lblStrength.Text = $"Força da senha: {PasswordStrength.GetLabel(level)}";
            lblStrength.ForeColor = level switch
            {
                PasswordStrengthLevel.VeryWeak => AppTheme.HackerRed,
                PasswordStrengthLevel.Weak => AppTheme.HackerYellow,
                PasswordStrengthLevel.Medium => AppTheme.HackerBlue,
                _ => AppTheme.HackerGreen
            };
            lblStrength.Visible = true;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            switch (mode)
            {
                case CredentialDialogMode.CreatePassword:
                    if (string.IsNullOrWhiteSpace(fieldPassword.TextValue))
                    {
                        MessageBox.Show(
                            "Informe uma senha.",
                            Text,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    if (fieldPassword.TextValue != fieldConfirm.TextValue)
                    {
                        MessageBox.Show("As senhas não coincidem.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    Secret = fieldPassword.TextValue;
                    break;

                case CredentialDialogMode.UnlockWithPassword:
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
                        MessageBox.Show("Informe a chave de recuperação.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!VaultCrypto.IsValidRecoveryKeyFormat(key))
                    {
                        MessageBox.Show(
                            "Formato de chave inválido. A chave deve ter 32 caracteres hexadecimais (ex.: ABCD-1234-...).",
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
            txtRecovery.Text = string.Empty;
            lblStrength.Text = string.Empty;
            lblStrength.Visible = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearSecret();
            }

            base.Dispose(disposing);
        }
    }
}
