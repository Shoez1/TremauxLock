using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed partial class MainForm
    {
        private void RefreshOverview()
        {
            currentOverview = vaultService.GetOverview();

            switch (currentOverview.State)
            {
                case VaultState.Empty:
                    ApplyStatus("Pronto", AppTheme.AccentBlue, Color.FromArgb(20, 88, 166, 255), Color.FromArgb(72, 88, 166, 255));
                    ApplyVisibility("Aguardando conteudo", AppTheme.AccentBlue, Color.FromArgb(16, 88, 166, 255), Color.FromArgb(48, 88, 166, 255));
                    lblFilesValue.Text = "0";
                    lblFilesValue.ForeColor = AppTheme.AccentBlue;
                    lblSizeValue.Text = "0 B";
                    lblPanelTitle.Text = "Pasta private";
                    lblPathLabel.Text = "PASTA PRIVATE";
                    lblPathValue.Text = currentOverview.WorkingFolderPath;
                    lblPathHint.Text = "Abra a pasta para adicionar conteudo antes do primeiro bloqueio.";
                    btnPrimary.Text = "Abrir private";
                    btnPrimary.ButtonStyle = AccentButtonStyle.Primary;
                    btnSecondary.Text = "Abrir app";
                    btnSecondary.ButtonStyle = AccentButtonStyle.Secondary;
                    btnRecovery.Visible = false;
                    lblFooterCenter.Text = "PROXIMO BLOQUEIO - NAO DEFINIDO";
                    RenderContentState("Pasta vazia", "Adicione arquivos na pasta private para preparar o proximo bloqueio.", AppTheme.AccentBlue);
                    break;

                case VaultState.Unlocked:
                    ApplyStatus("Desbloqueado", AppTheme.AccentGreen, Color.FromArgb(20, 63, 185, 80), Color.FromArgb(72, 63, 185, 80));
                    ApplyVisibility("Visivel no disco", AppTheme.AccentGreen, Color.FromArgb(16, 63, 185, 80), Color.FromArgb(48, 63, 185, 80));
                    lblFilesValue.Text = currentOverview.FileCount.ToString();
                    lblFilesValue.ForeColor = AppTheme.AccentBlue;
                    lblSizeValue.Text = VaultCrypto.FormatSize(currentOverview.TotalBytes);
                    lblPanelTitle.Text = "Pasta private";
                    lblPathLabel.Text = "PASTA PRIVATE";
                    lblPathValue.Text = currentOverview.WorkingFolderPath;
                    lblPathHint.Text = "Passe o mouse sobre o caminho para ver completo.";
                    btnPrimary.Text = "Bloquear cofre";
                    btnPrimary.ButtonStyle = AccentButtonStyle.Danger;
                    btnSecondary.Text = "Abrir private";
                    btnSecondary.ButtonStyle = AccentButtonStyle.Secondary;
                    btnRecovery.Visible = false;
                    lblFooterCenter.Text = "PROXIMO BLOQUEIO - NAO DEFINIDO";
                    RenderUnlockedFiles();
                    break;

                case VaultState.Locked:
                    ApplyStatus("Bloqueado", AppTheme.AccentOrange, Color.FromArgb(20, 225, 178, 94), Color.FromArgb(72, 225, 178, 94));
                    ApplyVisibility("Oculto e protegido", AppTheme.AccentOrange, Color.FromArgb(16, 225, 178, 94), Color.FromArgb(48, 225, 178, 94));
                    lblFilesValue.Text = currentOverview.FileCount.ToString();
                    lblFilesValue.ForeColor = AppTheme.AccentBlue;
                    lblSizeValue.Text = VaultCrypto.FormatSize(currentOverview.TotalBytes);
                    lblPanelTitle.Text = "Local do cofre";
                    lblPathLabel.Text = "DIRETORIO DO COFRE";
                    lblPathValue.Text = vaultService.ApplicationDirectory;
                    lblPathHint.Text = "Use a senha principal ou a chave de recuperacao para restaurar o conteudo.";
                    btnPrimary.Text = "Desbloquear";
                    btnPrimary.ButtonStyle = AccentButtonStyle.Primary;
                    btnSecondary.Text = "Abrir app";
                    btnSecondary.ButtonStyle = AccentButtonStyle.Secondary;
                    btnRecovery.Visible = true;
                    btnRecovery.Text = "Usar recovery key";
                    btnRecovery.ButtonStyle = AccentButtonStyle.Ghost;
                    lblFooterCenter.Text = "PROTECAO - ATIVA";
                    RenderContentState("Conteudo oculto", "Desbloqueie o cofre para listar novamente os arquivos da pasta private.", AppTheme.AccentOrange);
                    break;

                default:
                    ApplyStatus("Revisar", AppTheme.AccentRed, Color.FromArgb(20, 248, 81, 73), Color.FromArgb(72, 248, 81, 73));
                    ApplyVisibility("Estrutura inconsistente", AppTheme.AccentRed, Color.FromArgb(16, 248, 81, 73), Color.FromArgb(48, 248, 81, 73));
                    lblFilesValue.Text = "!";
                    lblFilesValue.ForeColor = AppTheme.AccentRed;
                    lblSizeValue.Text = "-";
                    lblPanelTitle.Text = "Diretorio do app";
                    lblPathLabel.Text = "DIRETORIO";
                    lblPathValue.Text = vaultService.ApplicationDirectory;
                    lblPathHint.Text = "Abra a pasta do aplicativo para revisar private, private.locked e private.vault.json.";
                    btnPrimary.Text = "Indisponivel";
                    btnPrimary.ButtonStyle = AccentButtonStyle.Ghost;
                    btnSecondary.Text = "Abrir app";
                    btnSecondary.ButtonStyle = AccentButtonStyle.Secondary;
                    btnRecovery.Visible = false;
                    lblFooterCenter.Text = "ESTRUTURA - REQUER REVISAO";
                    RenderContentState("Revisao necessaria", "Existe uma combinacao inesperada de artefatos e a visualizacao foi pausada.", AppTheme.AccentRed);
                    break;
            }

            pathToolTip.SetToolTip(lblPathValue, lblPathValue.Text);
            if (!isBusy && DateTime.UtcNow >= unlockCooldownUntilUtc)
            {
                lblProgress.Visible = false;
                progressTrack.Visible = false;
                lblProgress.Text = string.Empty;
            }

            ApplyInteractiveState();
            LayoutControls();
            RefreshCooldownState();
        }

        private void RenderUnlockedFiles()
        {
            string[] files = Directory.Exists(vaultService.WorkingFolderPath)
                ? Directory.GetFiles(vaultService.WorkingFolderPath, "*", SearchOption.AllDirectories).OrderBy(p => p).Take(12).ToArray()
                : Array.Empty<string>();

            contentHost.Controls.Clear();
            if (files.Length == 0)
            {
                RenderContentState("Pasta vazia", "Nenhum arquivo encontrado na pasta private no momento.", AppTheme.AccentBlue);
                return;
            }

            for (int i = files.Length - 1; i >= 0; i--)
            {
                contentHost.Controls.Add(CreateFileRow(files[i]));
            }
        }

        private void RenderContentState(string title, string description, Color accent)
        {
            contentHost.Controls.Clear();
            contentHost.Controls.Add(CreateEmptyState(title, description, accent));
        }

        private Control CreateEmptyState(string title, string description, Color accent)
        {
            Panel root = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.SurfaceInset };
            Label titleLabel = CreateTextLabel(title, 11f, FontStyle.Bold, AppTheme.TextSecondary, AppTheme.SurfaceInset);
            Label descLabel = CreateTextLabel(description, 8.75f, FontStyle.Regular, AppTheme.TextSoft, AppTheme.SurfaceInset);
            SurfacePanel icon = new SurfacePanel
            {
                FillColor = AppTheme.WithAlpha(accent, 18),
                SecondaryFillColor = AppTheme.WithAlpha(accent, 18),
                BorderColor = AppTheme.WithAlpha(accent, 48),
                InnerStrokeColor = Color.FromArgb(0, 0, 0, 0),
                CornerRadius = 10
            };
            Panel glyph = new Panel { BackColor = icon.FillColor };
            glyph.Paint += (_, e) =>
            {
                using var pen = new Pen(accent, 1f);
                e.Graphics.DrawRectangle(pen, 4, 5, 12, 9);
                e.Graphics.DrawArc(pen, 5, 2, 10, 8, 200, 140);
            };
            icon.Controls.Add(glyph);
            root.Controls.AddRange(new Control[] { icon, titleLabel, descLabel });
            root.Resize += (_, _) =>
            {
                icon.SetBounds((root.Width - 42) / 2, Math.Max(24, (root.Height / 2) - 66), 42, 42);
                glyph.SetBounds(11, 11, 20, 18);
                titleLabel.SetBounds((root.Width - 260) / 2, icon.Bottom + 12, 260, 18);
                titleLabel.TextAlign = ContentAlignment.MiddleCenter;
                descLabel.SetBounds((root.Width - 300) / 2, titleLabel.Bottom + 8, 300, 38);
                descLabel.TextAlign = ContentAlignment.TopCenter;
            };
            return root;
        }

        private Control CreateFileRow(string fullPath)
        {
            string fileName = Path.GetFileName(fullPath);
            string relativePath = Path.GetRelativePath(vaultService.WorkingFolderPath, fullPath);
            long size = new FileInfo(fullPath).Length;

            Panel row = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = AppTheme.SurfaceInset };
            SurfacePanel icon = new SurfacePanel
            {
                FillColor = AppTheme.WithAlpha(AppTheme.AccentBlue, 20),
                SecondaryFillColor = AppTheme.WithAlpha(AppTheme.AccentBlue, 20),
                BorderColor = AppTheme.WithAlpha(AppTheme.AccentBlue, 52),
                InnerStrokeColor = Color.FromArgb(0, 0, 0, 0),
                CornerRadius = 8
            };
            Label iconText = CreateCodeLabel(fileName.Length == 0 ? "?" : fileName[..1].ToUpperInvariant(), 9f, FontStyle.Bold, AppTheme.AccentBlue, icon.FillColor);
            iconText.TextAlign = ContentAlignment.MiddleCenter;
            icon.Controls.Add(iconText);
            Label name = CreateTextLabel(fileName, 9.25f, FontStyle.Bold, AppTheme.TextPrimary, AppTheme.SurfaceInset);
            Label meta = CreateCodeLabel($"{VaultCrypto.FormatSize(size)}  {relativePath}", 7.75f, FontStyle.Regular, AppTheme.TextSoft, AppTheme.SurfaceInset);
            meta.AutoEllipsis = true;
            row.Controls.AddRange(new Control[] { icon, name, meta });
            row.Resize += (_, _) =>
            {
                icon.SetBounds(20, 10, 32, 32);
                iconText.SetBounds(0, 0, 32, 32);
                name.SetBounds(64, 10, row.Width - 84, 16);
                meta.SetBounds(64, 28, row.Width - 84, 14);
            };
            row.Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawLine(pen, 20, row.Height - 1, row.Width - 20, row.Height - 1);
            };
            return row;
        }

        private void ApplyStatus(string text, Color textColor, Color fillColor, Color borderColor)
        {
            statusBadge.Text = text;
            statusBadge.ForeColor = textColor;
            statusBadge.FillColor = fillColor;
            statusBadge.BorderColor = borderColor;
            statusBadge.Invalidate();
        }

        private void ApplyVisibility(string text, Color textColor, Color fillColor, Color borderColor)
        {
            lblVisibilityValue.Text = text;
            lblVisibilityValue.ForeColor = textColor;
            visibilityDot.BackColor = textColor;
            visibilityPill.BackColor = fillColor;
            visibilityPill.Tag = borderColor;
            visibilityPill.Invalidate();
        }
    }
}
