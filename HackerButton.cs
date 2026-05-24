using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TremauxLock
{
    internal enum HackerButtonStyle
    {
        Primary,
        Secondary,
        Accent,
        Danger,
        Warning,
        Ghost
    }

    internal sealed class HackerButton : Button
    {
        private bool hovered;
        private bool pressed;
        private HackerButtonStyle buttonStyle = HackerButtonStyle.Primary;

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
            Font = AppTheme.CreateBodyFont(9.5f, FontStyle.Bold);
            Height = 42;
            Width = 160;
            Padding = new Padding(18, 0, 18, 0);
            TextAlign = ContentAlignment.MiddleCenter;
            AutoEllipsis = true;
            UseMnemonic = false;
            BackColor = AppTheme.BackgroundSurface;
            ForeColor = AppTheme.TextPrimary;
        }

        protected override bool ShowFocusCues => true;

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

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            pressed = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Default;
            e.Graphics.Clear(Parent?.BackColor ?? AppTheme.BackgroundPrimary);

            RectangleF bounds = new RectangleF(0, 0, Width - 1f, Height - 1f);
            if (pressed && Enabled)
            {
                bounds.Offset(0, 1);
            }

            var palette = ResolvePalette();
            using var path = AppTheme.CreateRoundedRectangle(bounds, AppTheme.RadiusButton);
            using var fillBrush = new SolidBrush(palette.Fill);
            using var borderPen = new Pen(palette.Border, hovered || Focused ? 2f : 1f);

            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            if ((hovered || Focused) && Enabled)
            {
                using var focusPen = new Pen(Color.FromArgb(90, Color.White), 1f);
                RectangleF focusBounds = RectangleF.Inflate(bounds, -3f, -3f);
                using var focusPath = AppTheme.CreateRoundedRectangle(focusBounds, Math.Max(3, AppTheme.RadiusButton - 2));
                e.Graphics.DrawPath(focusPen, focusPath);
            }

            Rectangle textRect = new Rectangle(14, 0, Math.Max(0, Width - 28), Height);
            if (pressed && Enabled)
            {
                textRect.Offset(0, 1);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textRect,
                palette.Fore,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding);
        }

        private (Color Fill, Color Border, Color Fore) ResolvePalette()
        {
            if (!Enabled)
            {
                return (
                    Color.FromArgb(18, 24, 34),
                    Color.FromArgb(38, 48, 63),
                    AppTheme.TextMuted);
            }

            return ButtonStyle switch
            {
                HackerButtonStyle.Primary => CreatePalette(Color.FromArgb(32, 105, 224), AppTheme.HackerBlue, Color.White),
                HackerButtonStyle.Accent => CreatePalette(Color.FromArgb(24, 126, 154), AppTheme.HackerCyan, Color.White),
                HackerButtonStyle.Secondary => CreatePalette(Color.FromArgb(41, 53, 73), AppTheme.BorderPrimary, AppTheme.TextPrimary),
                HackerButtonStyle.Danger => CreatePalette(Color.FromArgb(137, 47, 64), AppTheme.HackerRed, Color.White),
                HackerButtonStyle.Warning => CreatePalette(Color.FromArgb(139, 101, 22), AppTheme.HackerYellow, Color.White),
                _ => CreateGhostPalette()
            };
        }

        private (Color Fill, Color Border, Color Fore) CreatePalette(
            Color fill,
            Color border,
            Color fore)
        {
            if (pressed)
            {
                fill = ControlPaint.Dark(fill, 0.10f);
            }
            else if (hovered || Focused)
            {
                fill = ControlPaint.Light(fill, 0.08f);
            }

            return (fill, border, fore);
        }

        private (Color Fill, Color Border, Color Fore) CreateGhostPalette()
        {
            Color fill = hovered || Focused
                ? Color.FromArgb(27, 36, 51)
                : Color.FromArgb(12, 18, 28);
            return (fill, AppTheme.BorderPrimary, AppTheme.TextSecondary);
        }
    }
}
