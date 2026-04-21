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
        private readonly Label lblTitle;
        private readonly InputField fieldPassword;
        private readonly InputField fieldConfirm;
        private readonly TextBox txtRecovery;
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

            int h = mode switch
            {
                CredentialDialogMode.CreatePassword => 400,
                CredentialDialogMode.UnlockWithRecoveryKey => 380,
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
                CredentialDialogMode.CreatePassword => "Definir senha do cofre",
                CredentialDialogMode.UnlockWithPassword => "Desbloquear com senha",
                _ => "Usar chave de recuperacao"
            };

            fieldPassword = new InputField
            {
                Location = new Point(24, 84),
                Width = ClientSize.Width - 48,
                Caption = mode == CredentialDialogMode.CreatePassword
                    ? "Nova senha"
                    : "Senha",
                PlaceholderText = mode == CredentialDialogMode.CreatePassword
                    ? $"Minimo {VaultCrypto.MinimumPasswordLength} caracteres"
                    : "Senha do cofre",
                UsePassword = mode != CredentialDialogMode.UnlockWithRecoveryKey
            };
            fieldPassword.Visible = mode != CredentialDialogMode.UnlockWithRecoveryKey;

            fieldConfirm = new InputField
            {
                Location = new Point(24, 164),
                Width = ClientSize.Width - 48,
                Caption = "Confirmar senha",
                PlaceholderText = "Repita a senha",
                UsePassword = true
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
                PlaceholderText = "Cole ou digite a chave (hex com hifens)"
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
            Controls.Add(fieldPassword);
            Controls.Add(fieldConfirm);
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
                        MessageBox.Show("Informe a chave de recuperacao.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    Secret = key;
                    break;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
