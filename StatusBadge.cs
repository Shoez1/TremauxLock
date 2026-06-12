using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed class StatusBadge : Control
    {
        public StatusBadge()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            DoubleBuffered = true;
            Size = new Size(138, 30);
            ForeColor = AppTheme.AccentGreen;
            Font = AppTheme.CreateCodeFont(8.25f, FontStyle.Bold);
            BackColor = AppTheme.Surface;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FillColor { get; set; } = Color.FromArgb(25, 63, 185, 80);

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Color.FromArgb(64, 63, 185, 80);

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowDot { get; set; } = true;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 1 || Height <= 1) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            RectangleF bounds = new RectangleF(0, 0, Width - 1f, Height - 1f);
            using var path = AppTheme.CreateRoundedRectangle(bounds, AppTheme.RadiusBadge);
            using var fillBrush = new SolidBrush(FillColor);
            using var borderPen = new Pen(BorderColor, 1f);
            g.FillPath(fillBrush, path);
            g.DrawPath(borderPen, path);

            int textLeft = 12;
            if (ShowDot)
            {
                using var dotBrush = new SolidBrush(ForeColor);
                using var dotBorder = new Pen(Color.FromArgb(180, ForeColor), 1f);
                g.FillEllipse(dotBrush, 11, (Height / 2) - 4, 8, 8);
                g.DrawEllipse(dotBorder, 11, (Height / 2) - 4, 8, 8);
                textLeft = 24;
            }

            Rectangle textRect = new Rectangle(textLeft, 0, Width - textLeft - 10, Height);
            TextRenderer.DrawText(
                g,
                Text.ToUpperInvariant(),
                Font,
                textRect,
                ForeColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding);
        }
    }
}
