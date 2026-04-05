using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed partial class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 2;

        private readonly VaultService vaultService;
        private readonly ToolTip pathToolTip;
        private readonly System.Windows.Forms.Timer unlockCooldownTimer;

        private readonly Panel titleBar;
        private readonly Label lblTitleBar;
        private readonly Button btnMinimize;
        private readonly Button btnClose;
        private readonly StatusBadge statusBadge;
        private readonly Panel bodyPanel;
        private readonly VaultCard statsCard;
        private readonly Label lblFilesLabel;
        private readonly Label lblFilesValue;
        private readonly Label lblSizeLabel;
        private readonly Label lblSizeValue;
        private readonly Label lblVisibilityLabel;
        private readonly Panel statDividerOne;
        private readonly Panel statDividerTwo;
        private readonly Panel visibilityPill;
        private readonly Panel visibilityDot;
        private readonly Label lblVisibilityValue;
        private readonly VaultCard mainCard;
        private readonly Label lblPanelLabel;
        private readonly Label lblPanelTitle;
        private readonly AccentButton btnPrimary;
        private readonly AccentButton btnSecondary;
        private readonly AccentButton btnRecovery;
        private readonly Label lblPathLabel;
        private readonly SurfacePanel pathPanel;
        private readonly Label lblPathValue;
        private readonly Label lblPathHint;
        private readonly Panel workspaceDivider;
        private readonly Label lblProgress;
        private readonly Panel progressTrack;
        private readonly Panel progressFill;
        private readonly Panel contentHost;
        private readonly Panel footerPanel;
        private readonly Label lblFooterLeft;
        private readonly Label lblFooterCenter;
        private readonly Label lblFooterRight;

        private VaultOverview? currentOverview;
        private bool isBusy;
        private int failedUnlockAttempts;
        private int lastProgressCurrent;
        private int lastProgressTotal = 1;
        private DateTime unlockCooldownUntilUtc = DateTime.MinValue;

        public MainForm(VaultService vaultService)
        {
            this.vaultService = vaultService;
            pathToolTip = new ToolTip();
            unlockCooldownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            unlockCooldownTimer.Tick += (_, _) => RefreshCooldownState();

            Text = "TremauxLock Vault";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1060, 740);
            ClientSize = new Size(1180, 760);
            AutoScroll = false;
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundPrimary;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(9.25f);

            titleBar = new Panel { Height = 40, BackColor = AppTheme.Surface, AutoScroll = false };
            titleBar.MouseDown += TitleBarMouseDown;
            titleBar.Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawLine(pen, 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
            };

            lblTitleBar = CreateTextLabel("TremauxLock Vault", 9f, FontStyle.Bold, AppTheme.AccentBlue, AppTheme.Surface);
            lblTitleBar.MouseDown += TitleBarMouseDown;

            btnClose = CreateWindowButton("X", () => Close());
            btnMinimize = CreateWindowButton("_", () => WindowState = FormWindowState.Minimized);
            titleBar.Controls.AddRange(new Control[] { lblTitleBar, btnMinimize, btnClose });

            bodyPanel = new Panel { BackColor = AppTheme.BackgroundPrimary, Padding = Padding.Empty, AutoScroll = false };

            statsCard = CreateCard();
            statsCard.FillColor = Color.FromArgb(19, 24, 32);
            statsCard.SecondaryFillColor = statsCard.FillColor;
            statsCard.CornerRadius = 8;

            statusBadge = new StatusBadge { BackColor = statsCard.FillColor, ShowDot = true };

            btnPrimary = new AccentButton { Height = 38, ButtonStyle = AccentButtonStyle.Primary };
            btnSecondary = new AccentButton { Height = 38, ButtonStyle = AccentButtonStyle.Secondary };
            btnRecovery = new AccentButton { Height = 38, ButtonStyle = AccentButtonStyle.Ghost, Visible = false };
            btnPrimary.Click += async (_, _) => await HandlePrimaryActionAsync();
            btnSecondary.Click += (_, _) => HandleSecondaryAction();
            btnRecovery.Click += async (_, _) => await UnlockVaultAsync(true);

            lblFilesLabel = CreateCodeLabel("ARQUIVOS", 7.75f, FontStyle.Bold, AppTheme.TextSoft, statsCard.FillColor);
            lblFilesValue = CreateTextLabel("0", 18f, FontStyle.Bold, AppTheme.TextPrimary, statsCard.FillColor);
            lblSizeLabel = CreateCodeLabel("TAMANHO", 7.75f, FontStyle.Bold, AppTheme.TextSoft, statsCard.FillColor);
            lblSizeValue = CreateCodeLabel("0 B", 10f, FontStyle.Regular, AppTheme.TextSecondary, statsCard.FillColor);
            lblVisibilityLabel = CreateCodeLabel("VISIBILIDADE", 7.75f, FontStyle.Bold, AppTheme.TextSoft, statsCard.FillColor);
            statDividerOne = CreateDivider();
            statDividerTwo = CreateDivider();

            visibilityPill = CreatePillPanel();
            visibilityDot = new Panel { Size = new Size(6, 6) };
            lblVisibilityValue = CreateCodeLabel(string.Empty, 8.25f, FontStyle.Bold, AppTheme.AccentBlue, Color.Transparent);
            visibilityPill.Controls.AddRange(new Control[] { visibilityDot, lblVisibilityValue });

            statsCard.Controls.AddRange(new Control[]
            {
                statusBadge,
                btnPrimary, btnSecondary, btnRecovery,
                statDividerOne, lblFilesLabel, lblFilesValue,
                statDividerTwo, lblSizeLabel, lblSizeValue,
                lblVisibilityLabel, visibilityPill
            });

            mainCard = CreateCard();
            mainCard.FillColor = AppTheme.Surface;
            mainCard.SecondaryFillColor = AppTheme.Surface;
            mainCard.CornerRadius = 8;

            lblPanelLabel = CreateCodeLabel("LOCAL", 7.75f, FontStyle.Bold, AppTheme.TextSoft, mainCard.FillColor);
            lblPanelTitle = CreateTextLabel(string.Empty, 15f, FontStyle.Bold, AppTheme.TextPrimary, mainCard.FillColor);
            lblPathLabel = CreateCodeLabel("PASTA PRIVATE", 7.75f, FontStyle.Bold, AppTheme.TextSoft, mainCard.FillColor);

            pathPanel = new SurfacePanel
            {
                FillColor = AppTheme.SurfaceInset,
                SecondaryFillColor = AppTheme.SurfaceInset,
                BorderColor = AppTheme.Border,
                InnerStrokeColor = Color.FromArgb(0, 0, 0, 0),
                CornerRadius = 6
            };
            pathPanel.Padding = new Padding(14, 10, 14, 10);
            lblPathLabel.BackColor = pathPanel.FillColor;

            lblPathValue = CreateCodeLabel(string.Empty, 8.75f, FontStyle.Regular, AppTheme.TextSecondary, pathPanel.FillColor);
            lblPathValue.AutoEllipsis = true;
            lblPathValue.UseMnemonic = false;
            lblPathHint = CreateTextLabel(string.Empty, 8.5f, FontStyle.Regular, AppTheme.TextSoft, mainCard.FillColor);
            lblProgress = CreateCodeLabel(string.Empty, 8.25f, FontStyle.Regular, AppTheme.AccentBlue, mainCard.FillColor);
            lblProgress.Visible = false;

            workspaceDivider = CreateDivider();

            progressTrack = new Panel { BackColor = AppTheme.Border, Visible = false, AutoScroll = false };
            progressFill = new Panel { BackColor = AppTheme.AccentBlue };
            progressTrack.Controls.Add(progressFill);

            contentHost = new Panel
            {
                BackColor = AppTheme.SurfaceInset,
                AutoScroll = false,
                AutoSize = false
            };
            contentHost.Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, contentHost.Width - 1), Math.Max(0, contentHost.Height - 1));
            };

            pathPanel.Controls.Add(lblPathLabel);
            pathPanel.Controls.Add(lblPathValue);
            mainCard.Controls.AddRange(new Control[]
            {
                lblPanelLabel, lblPanelTitle, pathPanel, lblPathHint,
                workspaceDivider, lblProgress, progressTrack, contentHost
            });

            bodyPanel.Controls.Add(mainCard);
            bodyPanel.Controls.Add(statsCard);

            footerPanel = new Panel { Height = 32, BackColor = AppTheme.BackgroundPrimary, AutoScroll = false };
            footerPanel.Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
            };

            lblFooterLeft = CreateCodeLabel("SESSAO - ATIVA", 7.5f, FontStyle.Regular, AppTheme.TextSoft, footerPanel.BackColor);
            lblFooterCenter = CreateCodeLabel("PROXIMO BLOQUEIO - NAO DEFINIDO", 7.5f, FontStyle.Regular, AppTheme.TextSoft, footerPanel.BackColor);
            lblFooterRight = CreateCodeLabel("TREMAUXLOCK - V1.0", 7.5f, FontStyle.Regular, AppTheme.TextSoft, footerPanel.BackColor);
            lblFooterRight.TextAlign = ContentAlignment.MiddleRight;
            footerPanel.Controls.AddRange(new Control[] { lblFooterLeft, lblFooterCenter, lblFooterRight });

            Controls.AddRange(new Control[] { bodyPanel, footerPanel, titleBar });

            Resize += (_, _) => BuildLayout();
            Shown += (_, _) => RefreshOverview();
            Activated += (_, _) => RefreshOverview();
            Paint += (_, e) =>
            {
                using var pen = new Pen(AppTheme.Border, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            };

            BuildLayout();
        }

        private void BuildLayout()
        {
            if (InvokeRequired)
            {
                Invoke((Action)BuildLayout);
                return;
            }

            SuspendLayout();
            bodyPanel.SuspendLayout();
            try
            {
                int cw = ClientSize.Width;
                int ch = ClientSize.Height;
                int outer = 28;
                int gap = 20;
                int bodyGapTop = 18;
                int bodyGapBottom = 18;
                int footerH = footerPanel.Height;

                titleBar.SetBounds(0, 0, cw, 40);
                footerPanel.SetBounds(0, ch - footerH, cw, footerH);

                int bodyTop = titleBar.Bottom + bodyGapTop;
                int panelH = Math.Max(100, footerPanel.Top - bodyTop - bodyGapBottom);
                bodyPanel.SetBounds(0, bodyTop, cw, panelH);

                lblTitleBar.SetBounds(18, 11, 240, 18);
                btnClose.SetBounds(cw - 38, 4, 32, 32);
                btnMinimize.SetBounds(btnClose.Left - 32, 4, 32, 32);

                int availableWidth = Math.Max(520, cw - (outer * 2) - gap);
                int sumW = Math.Min(336, Math.Max(272, (int)Math.Round(availableWidth * 0.29)));
                statsCard.SetBounds(outer, 0, sumW, panelH);

                int cpX = statsCard.Right + gap;
                int cpW = Math.Max(200, cw - outer - cpX);
                mainCard.SetBounds(cpX, 0, cpW, panelH);

                LayoutSidebar();
                LayoutWorkspace(cpW);

                lblFooterLeft.SetBounds(24, 10, 220, 12);
                lblFooterCenter.SetBounds((cw / 2) - 170, 10, 340, 12);
                lblFooterRight.SetBounds(cw - 204, 10, 180, 12);
            }
            finally
            {
                bodyPanel.ResumeLayout(false);
                ResumeLayout(false);
            }
        }

        private void LayoutControls() => BuildLayout();

        private void LayoutSidebar()
        {
            int inset = 24;
            int width = statsCard.Width - (inset * 2);
            int badgeWidth = Math.Min(width, 164);
            statusBadge.SetBounds(inset, 22, badgeWidth, 32);
            int y = 22 + statusBadge.Height + 20;

            btnPrimary.SetBounds(inset, y, width, 38);
            y += 50;
            btnSecondary.SetBounds(inset, y, width, 38);
            y += 50;

            if (btnRecovery.Visible)
            {
                btnRecovery.SetBounds(inset, y, width, 38);
                y += 52;
            }
            else
            {
                btnRecovery.SetBounds(-1000, -1000, 0, 0);
            }

            statDividerOne.SetBounds(inset, y, width, 1);
            y += 22;

            lblFilesLabel.SetBounds(inset, y, width, 14);
            y += 18;
            lblFilesValue.SetBounds(inset, y, width, 26);
            y += 42;

            statDividerTwo.SetBounds(inset, y, width, 1);
            y += 22;

            lblSizeLabel.SetBounds(inset, y, width, 14);
            y += 18;
            lblSizeValue.SetBounds(inset, y, width, 20);
            y += 38;

            lblVisibilityLabel.SetBounds(inset, y, width, 14);
            y += 18;

            int pillWidth = Math.Max(138, TextRenderer.MeasureText(lblVisibilityValue.Text, lblVisibilityValue.Font).Width + 34);
            visibilityPill.SetBounds(inset, y, Math.Min(width, pillWidth), 28);
            visibilityDot.SetBounds(10, 11, 6, 6);
            lblVisibilityValue.SetBounds(22, 6, visibilityPill.Width - 28, 16);
        }

        private void LayoutWorkspace(int cpW)
        {
            int inset = 28;
            int width = Math.Max(0, cpW - (inset * 2));
            int y = 24;
            int pathInner = 16;

            lblPanelLabel.SetBounds(inset, y, width, 14);
            y += 18;
            lblPanelTitle.SetBounds(inset, y, width, 22);
            y += 34;

            int pathW = Math.Max(0, cpW - inset * 2);
            pathPanel.SetBounds(inset, y, pathW, 72);
            lblPathLabel.SetBounds(pathInner, 12, Math.Max(0, pathPanel.Width - pathInner * 2), 13);
            lblPathValue.SetBounds(pathInner, 31, Math.Max(0, pathPanel.Width - pathInner * 2), 24);
            y += 84;

            int hintHeight = Math.Max(18, MeasureTextHeight(lblPathHint, width));
            lblPathHint.SetBounds(inset, y, width, hintHeight);
            y += hintHeight + 18;

            workspaceDivider.SetBounds(inset, y, width, 1);
            y += 18;

            if (lblProgress.Visible)
            {
                lblProgress.SetBounds(inset, y, width, 14);
                y += 20;
                progressTrack.SetBounds(inset, y, width, 4);
                y += 18;
            }
            else
            {
                progressTrack.SetBounds(0, 0, 0, 0);
            }

            int remaining = mainCard.Height - y - 16;
            contentHost.SetBounds(inset, y, width, Math.Max(100, remaining));
            UpdateProgressFill();
        }

        private static VaultCard CreateCard() => new VaultCard
        {
            FillColor = AppTheme.Surface,
            SecondaryFillColor = AppTheme.Surface,
            BorderColor = AppTheme.Border,
            InnerStrokeColor = Color.FromArgb(0, 0, 0, 0),
            CornerRadius = 8
        };

        private static Panel CreateDivider() =>
            new Panel { BackColor = AppTheme.Border, AutoScroll = false };

        private static Button CreateWindowButton(string text, Action click)
        {
            Button button = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Text = text,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextSecondary,
                Cursor = Cursors.Hand,
                TabStop = false,
                Font = AppTheme.CreateBodyFont(10f, FontStyle.Regular)
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(18, 88, 166, 255);
            button.Click += (_, _) => click();
            return button;
        }

        private static Panel CreatePillPanel()
        {
            Panel panel = new Panel { BackColor = Color.FromArgb(16, 88, 166, 255), AutoScroll = false };
            panel.Paint += (_, e) =>
            {
                Color borderColor = panel.Tag is Color color ? color : AppTheme.Border;
                using var pen = new Pen(borderColor, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, panel.Width - 1), Math.Max(0, panel.Height - 1));
            };
            return panel;
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

        private void TitleBarMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }
    }
}
