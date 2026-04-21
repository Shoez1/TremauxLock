using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TremauxLock
{
    internal enum HackerButtonStyle
    {
        Primary,    // Neon Green
        Secondary,  // Neon Magenta
        Accent,     // Neon Cyan
        Danger,     // Neon Red
        Warning,    // Neon Yellow
        Ghost       // Transparent with glow
    }

    internal sealed class HackerButton : Button
    {
        private bool hovered;
        private bool pressed;
        private HackerButtonStyle buttonStyle = HackerButtonStyle.Primary;
        private System.Windows.Forms.Timer glowTimer;
        private int glowIntensity = 0;
        private bool glowDirection = true;

        public HackerButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = AppTheme.CreateBodyFont(10f, FontStyle.Regular);
            Height = 42;
            Width = 160;
            Padding = new Padding(20, 0, 20, 0);
            TextAlign = ContentAlignment.MiddleCenter;
            AutoEllipsis = true;
            UseMnemonic = false;
            BackColor = AppTheme.BackgroundSurface;
            ForeColor = AppTheme.TextPrimary;

            // Initialize glow animation
            glowTimer = new System.Windows.Forms.Timer { Interval = 50 };
            glowTimer.Tick += (_, _) => UpdateGlow();
            glowTimer.Start();
        }

        protected override bool ShowFocusCues => false;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public HackerButtonStyle ButtonStyle
        {
            get => buttonStyle;
            set
            {
                buttonStyle = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.Clear(Parent?.BackColor ?? AppTheme.BackgroundPrimary);

            RectangleF bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            var palette = ResolvePalette();
            int glowAlpha = Math.Clamp(glowIntensity, 0, 255);

            // Draw background with glow effect
            using var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, palette.Glow));
            using var fillBrush = new SolidBrush(palette.Back);
            using var borderPen = new Pen(palette.Border, 2f);
            using var glowPen = new Pen(glowBrush, 4f);

            // Draw glow effect
            if (hovered && Enabled)
            {
                e.Graphics.DrawRectangle(glowPen, bounds.X - 2, bounds.Y - 2, bounds.Width + 3, bounds.Height + 3);
            }

            // Draw button shape
            using var path = AppTheme.CreateRoundedRectangle(bounds, AppTheme.RadiusButton);
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            // Draw border glow
            if (hovered && Enabled)
            {
                using var glowBorderPen = new Pen(Color.FromArgb(glowAlpha, palette.GlowBorder), 1f);
                e.Graphics.DrawPath(glowBorderPen, path);
            }

            // Draw text
            Rectangle textRect = new Rectangle(18, 0, Math.Max(0, Width - 36), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textRect,
                palette.Fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            // Draw scanline effect when pressed
            if (pressed && hovered && Enabled)
            {
                using var scanlineBrush = new SolidBrush(Color.FromArgb(100, palette.Glow));
                float scanlineY = bounds.Y + (float)(bounds.Height * 0.3);
                e.Graphics.FillRectangle(scanlineBrush, bounds.X, scanlineY, bounds.Width, 2);
            }
        }

        private void UpdateGlow()
        {
            if (!hovered || !Enabled)
            {
                glowIntensity = Math.Max(0, glowIntensity - 15);
            }
            else
            {
                if (glowDirection)
                {
                    glowIntensity = Math.Min(200, glowIntensity + 10);
                    if (glowIntensity >= 200) glowDirection = false;
                }
                else
                {
                    // Avoid going negative when intensity was low after decay but direction was still "down"
                    glowIntensity = Math.Max(100, glowIntensity - 10);
                    if (glowIntensity <= 100) glowDirection = true;
                }
            }

            glowIntensity = Math.Clamp(glowIntensity, 0, 255);
            Invalidate();
        }

        private (Color Back, Color Border, Color Fore, Color Glow, Color GlowBorder) ResolvePalette()
        {
            if (!Enabled)
            {
                return (
                    Color.FromArgb(30, 30, 30),
                    AppTheme.BorderPrimary,
                    AppTheme.TextMuted,
                    AppTheme.HackerGreen,
                    AppTheme.HackerGreen
                );
            }

            return ButtonStyle switch
            {
                HackerButtonStyle.Primary => ResolvePrimary(),
                HackerButtonStyle.Danger => ResolveDanger(),
                HackerButtonStyle.Warning => ResolveWarning(),
                HackerButtonStyle.Secondary => ResolveSecondary(),
                HackerButtonStyle.Accent => ResolveAccent(),
                _ => ResolveGhost()
            };
        }

        private (Color Back, Color Border, Color Fore, Color Glow, Color GlowBorder) ResolvePrimary()
        {
            if (pressed) return (
                Color.FromArgb(0, 60, 30),
                AppTheme.HackerGreen,
                AppTheme.TextPrimary,
                AppTheme.HackerGreen,
                AppTheme.HackerGreen
            );
            if (hovered) return (
                Color.FromArgb(0, 70, 40),
                AppTheme.HackerGreen,
                AppTheme.TextPrimary,
                AppTheme.HackerGreen,
                AppTheme.HackerGreen
            );
            return (
                Color.FromArgb(0, 50, 25),
                AppTheme.HackerGreen,
                AppTheme.TextPrimary,
                AppTheme.HackerGreen,
                AppTheme.HackerGreen
            );
        }

        private (Color Back, Color Border, Color Fore, Color Glow, Color GlowBorder) ResolveDanger()
        {
            if (pressed) return (
                Color.FromArgb(40, 0, 0),
                AppTheme.HackerRed,
                AppTheme.TextPrimary,
                AppTheme.HackerRed,
                AppTheme.HackerRed
            );
            if (hovered) return (
                Color.FromArgb(50, 0, 0),
                AppTheme.HackerRed,
                AppTheme.TextPrimary,
                AppTheme.HackerRed,
                AppTheme.HackerRed
            );
            return (
                Color.FromArgb(30, 0, 0),
                AppTheme.HackerRed,
                AppTheme.TextPrimary,
                AppTheme.HackerRed,
                AppTheme.HackerRed
            );
        }

        private (Color Back, Color Border, Color Fore, Color Glow, Color GlowBorder) ResolveWarning()
        {
            if (pressed) return (
                Color.FromArgb(40, 40, 0),
                AppTheme.HackerYellow,
                AppTheme.TextPrimary,
                AppTheme.HackerYellow,
                AppTheme.HackerYellow
            );
            if (hovered) return (
                Color.FromArgb(50, 50, 0),
                AppTheme.HackerYellow,
                AppTheme.TextPrimary,
                AppTheme.HackerYellow,
                AppTheme.HackerYellow
            );
            return (
                Color.FromArgb(30, 30, 0),
                AppTheme.HackerYellow,
                AppTheme.TextPrimary,
                AppTheme.HackerYellow,
                AppTheme.HackerYellow
            );
        }

        private (Color Back, Color Border, Color Fore, Color Glow, Color GlowBorder) ResolveSecondary()
        {
            if (pressed) return (
                Color.FromArgb(30, 0, 30),
                AppTheme.HackerMagenta,
                AppTheme.TextPrimary,
                AppTheme.HackerMagenta,
                AppTheme.HackerMagenta
            );
            if (hovered) return (
                Color.FromArgb(40, 0, 40),
                AppTheme.HackerMagenta,
                AppTheme.TextPrimary,
                AppTheme.HackerMagenta,
                AppTheme.HackerMagenta
            );
            return (
                Color.FromArgb(20, 0, 20),
                AppTheme.HackerMagenta,
                AppTheme.TextPrimary,
                AppTheme.HackerMagenta,
                AppTheme.HackerMagenta
            );
        }

        private (Color Back, Color Border, Color Fore, Color Glow, Color GlowBorder) ResolveAccent()
        {
            if (pressed) return (
                Color.FromArgb(0, 30, 30),
                AppTheme.HackerCyan,
                AppTheme.TextPrimary,
                AppTheme.HackerCyan,
                AppTheme.HackerCyan
            );
            if (hovered) return (
                Color.FromArgb(0, 40, 40),
                AppTheme.HackerCyan,
                AppTheme.TextPrimary,
                AppTheme.HackerCyan,
                AppTheme.HackerCyan
            );
            return (
                Color.FromArgb(0, 20, 20),
                AppTheme.HackerCyan,
                AppTheme.TextPrimary,
                AppTheme.HackerCyan,
                AppTheme.HackerCyan
            );
        }

        private (Color Back, Color Border, Color Fore, Color Glow, Color GlowBorder) ResolveGhost()
        {
            if (pressed) return (
                Color.FromArgb(0, 0, 0, 0),
                AppTheme.HackerCyan,
                AppTheme.HackerCyan,
                AppTheme.HackerCyan,
                AppTheme.HackerCyan
            );
            if (hovered) return (
                Color.FromArgb(0, 0, 0, 0),
                AppTheme.HackerCyan,
                AppTheme.HackerCyan,
                AppTheme.HackerCyan,
                AppTheme.HackerCyan
            );
            return (
                Color.FromArgb(0, 0, 0, 0),
                AppTheme.HackerCyan,
                AppTheme.TextMuted,
                AppTheme.HackerCyan,
                AppTheme.HackerCyan
            );
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                glowTimer?.Stop();
                glowTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}