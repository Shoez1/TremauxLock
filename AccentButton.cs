using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TremauxLock
{
    internal enum AccentButtonStyle
    {
        Primary,
        Secondary,
        Ghost,
        Danger
    }

    internal sealed class AccentButton : Button
    {
        private bool hovered;
        private bool pressed;
        private AccentButtonStyle buttonStyle = AccentButtonStyle.Primary;

        public AccentButton()
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
            Font = AppTheme.CreateBodyFont(9f, FontStyle.Regular);
            Height = 38;
            Width = 148;
            Padding = new Padding(16, 0, 16, 0);
            TextAlign = ContentAlignment.MiddleCenter;
            AutoEllipsis = true;
            UseMnemonic = false;
            BackColor = AppTheme.Surface;
            ForeColor = AppTheme.TextPrimary;
        }

        protected override bool ShowFocusCues => false;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AccentButtonStyle ButtonStyle
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

            using var path = AppTheme.CreateRoundedRectangle(bounds, AppTheme.RadiusButton);
            using var fillBrush = new SolidBrush(palette.Back);
            using var borderPen = new Pen(palette.Border, 1f);
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            Rectangle textRect = new Rectangle(14, 0, Math.Max(0, Width - 28), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textRect,
                palette.Fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private (Color Back, Color Border, Color Fore) ResolvePalette()
        {
            if (!Enabled)
            {
                return (Color.FromArgb(20, 24, 31), AppTheme.Border, AppTheme.TextSoft);
            }

            return ButtonStyle switch
            {
                AccentButtonStyle.Primary => ResolvePrimary(),
                AccentButtonStyle.Danger => ResolveDanger(),
                AccentButtonStyle.Secondary => ResolveSecondary(),
                _ => ResolveGhost()
            };
        }

        private (Color Back, Color Border, Color Fore) ResolvePrimary()
        {
            if (pressed) return (Color.FromArgb(38, 97, 177), Color.FromArgb(90, 114, 197, 255), Color.White);
            if (hovered) return (Color.FromArgb(42, 104, 188), Color.FromArgb(102, 114, 197, 255), Color.White);
            return (Color.FromArgb(34, 93, 170), Color.FromArgb(92, 114, 197, 255), Color.White);
        }

        private (Color Back, Color Border, Color Fore) ResolveDanger()
        {
            if (pressed) return (Color.FromArgb(74, 39, 42), Color.FromArgb(136, 248, 81, 73), AppTheme.Danger);
            if (hovered) return (Color.FromArgb(64, 35, 38), Color.FromArgb(118, 248, 81, 73), AppTheme.Danger);
            return (Color.FromArgb(56, 31, 33), Color.FromArgb(104, 248, 81, 73), AppTheme.Danger);
        }

        private (Color Back, Color Border, Color Fore) ResolveSecondary()
        {
            if (pressed) return (Color.FromArgb(28, 34, 42), AppTheme.BorderMid, AppTheme.TextPrimary);
            if (hovered) return (Color.FromArgb(25, 31, 39), AppTheme.BorderMid, AppTheme.TextPrimary);
            return (Color.FromArgb(22, 27, 34), AppTheme.Border, AppTheme.TextPrimary);
        }

        private (Color Back, Color Border, Color Fore) ResolveGhost()
        {
            if (pressed) return (Color.FromArgb(18, 88, 166, 255), Color.FromArgb(92, 88, 166, 255), AppTheme.AccentBlue);
            if (hovered) return (Color.FromArgb(14, 88, 166, 255), Color.FromArgb(82, 88, 166, 255), AppTheme.AccentBlue);
            return (Color.FromArgb(0, 0, 0, 0), AppTheme.BorderMid, AppTheme.TextSecondary);
        }
    }
}
