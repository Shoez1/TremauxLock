using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TremauxLock
{
    internal enum HackerStatusType
    {
        Success,    // Green LED
        Warning,    // Yellow LED
        Danger,     // Red LED
        Info,       // Cyan LED
        Loading,    // Pulsing LED
        Unknown     // Gray LED
    }

    internal sealed class HackerStatus : Control
    {
        private HackerStatusType statusType = HackerStatusType.Info;
        private bool showDot = true;
        private System.Windows.Forms.Timer blinkTimer;
        private bool isBlinking = false;
        private int pulseIntensity = 0;
        private bool pulseDirection = true;

        public HackerStatus()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundSurface;
            Size = new Size(160, 32);
            MinimumSize = new Size(80, 24);

            // Initialize blink animation
            blinkTimer = new System.Windows.Forms.Timer { Interval = 500 };
            blinkTimer.Tick += (_, _) => { isBlinking = !isBlinking; Invalidate(); };
            blinkTimer.Start();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public HackerStatusType StatusType
        {
            get => statusType;
            set { statusType = value; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowDot
        {
            get => showDot;
            set { showDot = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.Clear(Parent?.BackColor ?? AppTheme.BackgroundPrimary);

            Rectangle bounds = new Rectangle(0, 0, Width, Height);
            var palette = ResolvePalette();

            // Draw background
            using var bgBrush = new SolidBrush(AppTheme.BackgroundSurface);
            e.Graphics.FillRectangle(bgBrush, bounds);

            // Draw border
            using var borderPen = new Pen(palette.Border, 1f);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            // Draw status dot
            if (ShowDot)
            {
                int dotSize = Math.Min(Height - 8, 12);
                int dotX = 12;
                int dotY = (Height - dotSize) / 2;

                // Create dot with glow effect
                using var dotBrush = new SolidBrush(palette.DotColor);
                int pulseAlpha = Math.Clamp(pulseIntensity, 0, 255);
                using var glowBrush = new SolidBrush(Color.FromArgb(pulseAlpha, palette.DotColor));
                using var glowPen = new Pen(glowBrush, 2f);

                // Draw glow
                if (statusType == HackerStatusType.Loading)
                {
                    e.Graphics.FillEllipse(glowBrush, dotX - 3, dotY - 3, dotSize + 6, dotSize + 6);
                    e.Graphics.DrawEllipse(glowPen, dotX - 3, dotY - 3, dotSize + 6, dotSize + 6);
                }

                // Draw dot
                e.Graphics.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                using var dotBorderPen = new Pen(palette.DotBorderColor, 1f);
                e.Graphics.DrawEllipse(dotBorderPen, dotX, dotY, dotSize, dotSize);
            }

            // Draw text
            int textX = ShowDot ? 40 : 12;
            Rectangle textRect = new Rectangle(textX, 0, Width - textX - 12, Height);

            using var textBrush = new SolidBrush(palette.TextColor);
            using var textFont = AppTheme.CreateCodeFont(9.5f, FontStyle.Bold);

            TextRenderer.DrawText(
                e.Graphics,
                GetStatusText(),
                textFont,
                textRect,
                palette.TextColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            // Draw scanline effect for loading
            if (statusType == HackerStatusType.Loading && isBlinking)
            {
                using var scanlineBrush = new SolidBrush(Color.FromArgb(100, palette.DotColor));
                float scanlineY = (float)(Height * 0.3);
                e.Graphics.FillRectangle(scanlineBrush, 0, scanlineY, Width, 2);
            }
        }

        private (Color Border, Color DotColor, Color DotBorderColor, Color TextColor) ResolvePalette()
        {
            return statusType switch
            {
                HackerStatusType.Success => (
                    AppTheme.HackerGreen,
                    AppTheme.HackerGreen,
                    Color.FromArgb(20, 20, 20),
                    AppTheme.HackerGreen
                ),
                HackerStatusType.Warning => (
                    AppTheme.HackerYellow,
                    AppTheme.HackerYellow,
                    Color.FromArgb(20, 20, 20),
                    AppTheme.HackerYellow
                ),
                HackerStatusType.Danger => (
                    AppTheme.HackerRed,
                    AppTheme.HackerRed,
                    Color.FromArgb(20, 20, 20),
                    AppTheme.HackerRed
                ),
                HackerStatusType.Info => (
                    AppTheme.HackerCyan,
                    AppTheme.HackerCyan,
                    Color.FromArgb(20, 20, 20),
                    AppTheme.HackerCyan
                ),
                HackerStatusType.Loading => (
                    AppTheme.HackerBlue,
                    GetLoadingColor(),
                    Color.FromArgb(20, 20, 20),
                    AppTheme.HackerBlue
                ),
                _ => (
                    AppTheme.BorderPrimary,
                    AppTheme.TextMuted,
                    Color.FromArgb(20, 20, 20),
                    AppTheme.TextMuted
                )
            };
        }

        private Color GetLoadingColor()
        {
            // Cycle through colors for loading animation
            int cycle = Environment.TickCount / 100 % 4;
            return cycle switch
            {
                0 => AppTheme.HackerGreen,
                1 => AppTheme.HackerCyan,
                2 => AppTheme.HackerMagenta,
                _ => AppTheme.HackerBlue
            };
        }

        private string GetStatusText()
        {
            return statusType switch
            {
                HackerStatusType.Success => "SUCESSO",
                HackerStatusType.Warning => "ATENÇÃO",
                HackerStatusType.Danger => "PERIGO",
                HackerStatusType.Info => "INFO",
                HackerStatusType.Loading => "CARREGANDO",
                _ => "DESCONHECIDO"
            };
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                blinkTimer?.Stop();
                blinkTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdatePulse()
        {
            if (statusType == HackerStatusType.Loading)
            {
                if (pulseDirection)
                {
                    pulseIntensity = Math.Min(200, pulseIntensity + 15);
                    if (pulseIntensity >= 200) pulseDirection = false;
                }
                else
                {
                    pulseIntensity = Math.Max(50, pulseIntensity - 15);
                    if (pulseIntensity <= 50) pulseDirection = true;
                }
            }
            else
            {
                pulseIntensity = Math.Min(200, pulseIntensity + 10);
            }

            pulseIntensity = Math.Clamp(pulseIntensity, 0, 255);
            Invalidate();
        }
    }
}