using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed class RecoveryKeyDialog : Form
    {
        private const int ClipboardClearDelayMs = 60000;

        private readonly string recoveryKey;
        private readonly string applicationDirectory;

        public RecoveryKeyDialog(
            string recoveryKey,
            int fileCount,
            long totalBytes,
            string? backupWarning,
            string applicationDirectory)
        {
            this.recoveryKey = recoveryKey;
            this.applicationDirectory = applicationDirectory;

            Text = "Chave de recuperação";
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
                Bounds = new Rectangle(24, 56, ClientSize.Width - 48, 42),
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(9.5f),
                Text = "Sem esta chave, apenas a senha principal desbloqueia o cofre. Ela muda a cada bloqueio, então guarde-a em local seguro."
            };

            var txtKey = new TextBox
            {
                Bounds = new Rectangle(24, 104, ClientSize.Width - 48, 120),
                ReadOnly = true,
                Multiline = true,
                WordWrap = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = AppTheme.BackgroundPanel,
                ForeColor = AppTheme.TextCode,
                Font = AppTheme.CreateCodeFont(11f),
                Text = recoveryKey,
                AccessibleName = "Chave de recuperação"
            };

            string stats = $"Arquivos: {fileCount}   |   Volume: {VaultCrypto.FormatSize(totalBytes)}";
            var lblStats = new Label
            {
                Bounds = new Rectangle(24, 234, ClientSize.Width - 48, 22),
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.CreateBodyFont(9f),
                Text = stats
            };

            Label? lblWarn = null;
            if (!string.IsNullOrWhiteSpace(backupWarning))
            {
                lblWarn = new Label
                {
                    Bounds = new Rectangle(24, 262, ClientSize.Width - 48, 76),
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
                Height = 44,
                AccessibleName = "Copiar chave de recuperação"
            };
            btnCopy.Click += (_, _) =>
            {
                try
                {
                    Clipboard.SetText(recoveryKey);
                    clipboardTimer.Stop();
                    clipboardTimer.Start();
                    MessageBox.Show(
                        "Chave copiada. A área de transferência será limpa em 60 segundos se continuar igual.",
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Não foi possível copiar: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            var btnSave = new HackerButton
            {
                Text = "Salvar em arquivo",
                ButtonStyle = HackerButtonStyle.Secondary,
                Location = new Point(214, ClientSize.Height - 24 - 44),
                Width = 180,
                Height = 44,
                AccessibleName = "Salvar chave de recuperação em arquivo"
            };
            btnSave.Click += (_, _) => SaveKeyToFile(fileCount, totalBytes);

            var btnOk = new HackerButton
            {
                Text = "Entendi",
                ButtonStyle = HackerButtonStyle.Primary,
                Location = new Point(ClientSize.Width - 24 - 180, ClientSize.Height - 24 - 44),
                Width = 180,
                Height = 44,
                DialogResult = DialogResult.OK,
                AccessibleName = "Fechar"
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
            Controls.Add(btnSave);
            Controls.Add(btnOk);

            Shown += (_, _) => txtKey.SelectAll();
        }

        private void SaveKeyToFile(int fileCount, long totalBytes)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Salvar chave de recuperação",
                Filter = "Arquivo de texto (*.txt)|*.txt",
                DefaultExt = "txt",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = $"TremauxLock-recovery-{DateTime.Now:yyyy-MM-dd}.txt",
                InitialDirectory = GetDefaultSaveDirectory()
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                string fullPath = Path.GetFullPath(dialog.FileName);
                if (IsPathInside(fullPath, applicationDirectory))
                {
                    MessageBox.Show(
                        "Não salve a chave dentro da pasta do aplicativo. Escolha um local fora dela.",
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                string content = $"""
                    TremauxLock - chave de recuperação
                    Gerada em: {DateTime.Now:O}
                    Arquivos: {fileCount}
                    Volume: {VaultCrypto.FormatSize(totalBytes)}

                    Chave:
                    {recoveryKey}

                    Guarde este arquivo em local seguro, fora deste computador.
                    Qualquer pessoa com acesso a esta chave pode desbloquear o cofre.
                    Esta chave deixa de funcionar quando o cofre for bloqueado novamente.
                    """;

                File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                MessageBox.Show(
                    $"Chave salva em:\n{fullPath}",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Acesso negado ao salvar o arquivo.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível salvar: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string GetDefaultSaveDirectory()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Directory.Exists(desktop))
            {
                return desktop;
            }

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Directory.Exists(documents) ? documents : AppContext.BaseDirectory;
        }

        private static bool IsPathInside(string path, string directory)
        {
            string fullDirectory = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return path.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
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
