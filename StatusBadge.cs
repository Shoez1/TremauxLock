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
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint,
                true);
            DoubleBuffered = true;
            Size = new Size(124, 32);
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.CreateBodyFont(8.75f, FontStyle.Bold);
            BackColor = AppTheme.CardFill;
            Resize += (_, _) => UpdateRegion();
            UpdateRegion();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FillColor { get; set; } = AppTheme.BadgeFill;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = AppTheme.BadgeBorder;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = AppTheme.RadiusBadge;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

            RectangleF bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using GraphicsPath path = AppTheme.CreateRoundedRectangle(bounds, CornerRadius);
            using SolidBrush fillBrush = new SolidBrush(FillColor);
            using Pen borderPen = new Pen(BorderColor, 1f);

            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                Rectangle.Round(bounds),
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private void UpdateRegion()
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            Region?.Dispose();
            using GraphicsPath path = AppTheme.CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
            Region = new Region(path);
        }
    }
}
