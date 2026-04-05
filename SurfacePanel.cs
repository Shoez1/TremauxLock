using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TremauxLock
{
    internal class SurfacePanel : Panel
    {
        public SurfacePanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint,
                true);
            DoubleBuffered = true;
            ResizeRedraw = true;
            AutoScroll = false;
            BackColor = AppTheme.CardFill;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FillColor { get; set; } = AppTheme.CardFill;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color SecondaryFillColor { get; set; } = AppTheme.CardFillAlt;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = AppTheme.CardBorder;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color InnerStrokeColor { get; set; } = Color.FromArgb(0, 0, 0, 0);

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float BorderThickness { get; set; } = 1f;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = AppTheme.RadiusPanel;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

            RectangleF bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using Brush fillBrush = CreateFillBrush(bounds);
            using Pen borderPen = new Pen(BorderColor, BorderThickness);

            if (CornerRadius <= 1)
            {
                e.Graphics.FillRectangle(fillBrush, bounds);
                e.Graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                return;
            }

            using GraphicsPath path = AppTheme.CreateRoundedRectangle(bounds, CornerRadius);
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            if (InnerStrokeColor.A > 0)
            {
                RectangleF innerBounds = RectangleF.Inflate(bounds, -1f, -1f);
                using GraphicsPath innerPath = AppTheme.CreateRoundedRectangle(innerBounds, Math.Max(4f, CornerRadius - 1f));
                using Pen innerPen = new Pen(InnerStrokeColor, 1f);
                e.Graphics.DrawPath(innerPen, innerPath);
            }
        }

        private Brush CreateFillBrush(RectangleF bounds)
        {
            if (FillColor.ToArgb() == SecondaryFillColor.ToArgb())
            {
                return new SolidBrush(FillColor);
            }

            return new LinearGradientBrush(bounds, FillColor, SecondaryFillColor, 90f);
        }

    }
}
