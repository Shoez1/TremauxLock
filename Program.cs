using System;
using System.IO;
using System.Security.AccessControl;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Text;

namespace LockerApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(exePath))
            {
                MessageBox.Show("Não foi possível determinar o caminho do executável.");
                return;
            }
            string baseDir = Path.GetDirectoryName(exePath) ?? "";
            if (string.IsNullOrEmpty(baseDir))
            {
                MessageBox.Show("Não foi possível determinar o diretório base.");
                return;
            }
            string privateDir = Path.Combine(baseDir, "private");

            if (!Directory.Exists(privateDir))
            {
                Directory.CreateDirectory(privateDir);
                MessageBox.Show("A pasta padrão 'private' foi criada. Coloque seus arquivos nela e execute o programa novamente para bloquear.", "Pasta criada");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(privateDir));
        }
    }

    public class MainForm : Form
    {
        private string privateDir;
        private string GetLockInfoPath() => Path.Combine(privateDir, ".lockinfo");

        public MainForm(string dir)
        {
            privateDir = dir;
            Text = "Locker - Pasta Private";
            Width = 370;
            Height = 250;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            // Tema escuro estilo hacker
            this.BackColor = System.Drawing.Color.Black;
            this.ForeColor = System.Drawing.Color.FromArgb(120, 255, 120);
            this.Font = new System.Drawing.Font("Consolas", 11, System.Drawing.FontStyle.Bold);

            // Define o ícone da janela (ladybug.ico) a partir do recurso embutido
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("LockerApp.ladybug.ico"))
                {
                    if (stream != null)
                        this.Icon = new System.Drawing.Icon(stream);
                }
            }
            catch { /* se falhar, ignora e segue sem ícone */ }

            var title = new Label {
                Text = "Tremaux Folder Locker",
                Font = new System.Drawing.Font("Consolas", 18, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(100, 255, 100),
                BackColor = System.Drawing.Color.Black,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Width = 340,
                Height = 40,
                Left = 10,
                Top = 10
            };
            Controls.Add(title);

            var btnLock = new Button {
                Text = "Bloquear pasta",
                Width = 180,
                Height = 40,
                Top = 65,
                Left = 90,
                BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
                ForeColor = System.Drawing.Color.FromArgb(100, 255, 100),
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Consolas", 12, System.Drawing.FontStyle.Bold)
            };
            btnLock.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(100, 255, 100);
            btnLock.FlatAppearance.BorderSize = 2;

            var btnUnlock = new Button {
                Text = "Desbloquear pasta",
                Width = 180,
                Height = 40,
                Top = 120,
                Left = 90,
                BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
                ForeColor = System.Drawing.Color.FromArgb(100, 255, 100),
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Consolas", 12, System.Drawing.FontStyle.Bold)
            };
            btnUnlock.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(100, 255, 100);
            btnUnlock.FlatAppearance.BorderSize = 2;

            btnLock.Click += (s, e) => LockFolder();
            btnUnlock.Click += (s, e) => UnlockFolder();
            Controls.Add(btnLock);
            Controls.Add(btnUnlock);

            var footer = new Label {
                Text = "by Shoez3 - Hacker Edition",
                Font = new System.Drawing.Font("Consolas", 9, System.Drawing.FontStyle.Italic),
                ForeColor = System.Drawing.Color.FromArgb(100, 255, 100),
                BackColor = System.Drawing.Color.Black,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Width = 340,
                Height = 20,
                Left = 10,
                Top = 190
            };
            Controls.Add(footer);
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private void LockFolder()
        {
            try
            {
                // Solicitar senha
                string pwd1 = Prompt.ShowDialog("Defina uma senha para desbloquear:", "Senha");
                if (string.IsNullOrEmpty(pwd1)) return;
                string pwd2 = Prompt.ShowDialog("Confirme a senha:", "Senha");
                if (pwd1 != pwd2)
                {
                    MessageBox.Show("As senhas não coincidem!");
                    return;
                }

                // Salvar hash da senha DENTRO da pasta private
                File.WriteAllText(GetLockInfoPath(), HashPassword(pwd1));

                File.SetAttributes(privateDir, FileAttributes.Hidden | FileAttributes.System);

                var dirInfo = new DirectoryInfo(privateDir);
                DirectorySecurity ds = dirInfo.GetAccessControl();
                var everyone = new System.Security.Principal.SecurityIdentifier(
                    System.Security.Principal.WellKnownSidType.WorldSid, null);
                var denyRule = new FileSystemAccessRule(
                    everyone,
                    FileSystemRights.ListDirectory | FileSystemRights.ReadData | FileSystemRights.ReadAttributes,
                    AccessControlType.Deny);
                ds.AddAccessRule(denyRule);
                dirInfo.SetAccessControl(ds);

                MessageBox.Show("Pasta bloqueada/oculta com sucesso!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao bloquear: " + ex.Message);
            }
        }

        private void UnlockFolder()
        {
            try
            {
                // Solicitar senha
                string pwd = Prompt.ShowDialog("Digite a senha para desbloquear:", "Senha");
                if (string.IsNullOrEmpty(pwd)) return;

                // Verificar hash (arquivo .lockinfo DENTRO da pasta private)
                string hashSalvo = File.Exists(GetLockInfoPath()) ? File.ReadAllText(GetLockInfoPath()) : "";
                if (HashPassword(pwd) != hashSalvo)
                {
                    MessageBox.Show("Senha incorreta!");
                    return;
                }

                File.SetAttributes(privateDir, FileAttributes.Normal);

                var dirInfo = new DirectoryInfo(privateDir);
                DirectorySecurity ds = dirInfo.GetAccessControl();
                var everyone = new System.Security.Principal.SecurityIdentifier(
                    System.Security.Principal.WellKnownSidType.WorldSid, null);
                var denyRule = new FileSystemAccessRule(
                    everyone,
                    FileSystemRights.ListDirectory | FileSystemRights.ReadData | FileSystemRights.ReadAttributes,
                    AccessControlType.Deny);
                ds.RemoveAccessRule(denyRule);
                dirInfo.SetAccessControl(ds);

                // Remove o arquivo de senha (.lockinfo) de dentro da pasta private
                File.Delete(GetLockInfoPath());

                MessageBox.Show("Pasta desbloqueada/visível com sucesso!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao desbloquear: " + ex.Message + "\nTente executar como administrador.");
            }
        }
    }

    // Classe auxiliar para prompt de senha
    public static class Prompt
    {
        public static string ShowDialog(string text, string caption)
        {
            Form prompt = new Form()
            {
                Width = 350,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 20, Top = 20, Text = text, Width = 280 };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 280, PasswordChar = '*' };
            Button confirmation = new Button() { Text = "OK", Left = 220, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }
}