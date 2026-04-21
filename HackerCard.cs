using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed class HackerCard : Panel
    {
        private Color fillColor = AppTheme.BackgroundSurface;
        private Color secondaryFillColor = AppTheme.BackgroundPanel;
        private Color borderColor = AppTheme.BorderPrimary;
        private Color innerStrokeColor = Color.FromArgb(10, 255, 255, 255);
        private float borderThickness = 1.5f;
        private int cornerRadius = AppTheme.RadiusCard;
        private System.Windows.Forms.Timer glowTimer;
        private int glowIntensity = 0;
        private bool glowDirection = true;
        private bool isHovered;

        public HackerCard()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            DoubleBuffered = true;
            ResizeRedraw = true;
            AutoScroll = false;
            BackColor = AppTheme.BackgroundPrimary;
            Padding = new Padding(20);

            // Initialize glow animation
            glowTimer = new System.Windows.Forms.Timer { Interval = 50 };
            glowTimer.Tick += (_, _) => UpdateGlow();
            glowTimer.Start();

            // Event handlers
            MouseEnter += (_, _) => { isHovered = true; Invalidate(); };
            MouseLeave += (_, _) => { isHovered = false; Invalidate(); };
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FillColor
        {
            get => fillColor;
            set { fillColor = value; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color SecondaryFillColor
        {
            get => secondaryFillColor;
            set { secondaryFillColor = value; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor
        {
            get => borderColor;
            set { borderColor = value; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color InnerStrokeColor
        {
            get => innerStrokeColor;
            set { innerStrokeColor = value; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float BorderThickness
        {
            get => borderThickness;
            set { borderThickness = value; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius
        {
            get => cornerRadius;
            set { cornerRadius = value; Invalidate(); }
        }

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

            // Draw glow effect when hovered
            if (isHovered)
            {
                int glowAlpha = Math.Clamp(glowIntensity, 0, 255);
                using var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, AppTheme.GlowPrimary));
                using var glowPen = new Pen(glowBrush, 4f);
                e.Graphics.DrawRectangle(glowPen, bounds.X - 2, bounds.Y - 2, bounds.Width + 3, bounds.Height + 3);
            }

            // Create fill brush with gradient
            using Brush fillBrush = CreateFillBrush(bounds);
            using Pen borderPen = new Pen(BorderColor, BorderThickness);

            // Draw card shape
            if (CornerRadius <= 1)
            {
                e.Graphics.FillRectangle(fillBrush, bounds);
                e.Graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
            else
            {
                using GraphicsPath path = AppTheme.CreateRoundedRectangle(bounds, CornerRadius);
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(borderPen, path);

                // Draw inner stroke for depth effect
                if (InnerStrokeColor.A > 0)
                {
                    RectangleF innerBounds = RectangleF.Inflate(bounds, -1f, -1f);
                    using GraphicsPath innerPath = AppTheme.CreateRoundedRectangle(innerBounds, Math.Max(4f, CornerRadius - 1f));
                    using Pen innerPen = new Pen(InnerStrokeColor, 1f);
                    e.Graphics.DrawPath(innerPen, innerPath);
                }
            }

            // Draw scanline effect when hovered
            if (isHovered)
            {
                using var scanlineBrush = new SolidBrush(Color.FromArgb(80, AppTheme.HackerGreen));
                float scanlineY = bounds.Y + (float)(bounds.Height * 0.4);
                e.Graphics.FillRectangle(scanlineBrush, bounds.X, scanlineY, bounds.Width, 2);
            }
        }

        private void UpdateGlow()
        {
            if (!isHovered)
            {
                glowIntensity = Math.Max(0, glowIntensity - 15);
            }
            else
            {
                if (glowDirection)
                {
                    glowIntensity = Math.Min(220, glowIntensity + 12);
                    if (glowIntensity >= 220) glowDirection = false;
                }
                else
                {
                    glowIntensity = Math.Max(120, glowIntensity - 12);
                    if (glowIntensity <= 120) glowDirection = true;
                }
            }

            glowIntensity = Math.Clamp(glowIntensity, 0, 255);
            Invalidate();
        }

        private Brush CreateFillBrush(RectangleF bounds)
        {
            if (FillColor.ToArgb() == SecondaryFillColor.ToArgb())
            {
                return new SolidBrush(FillColor);
            }

            // Create gradient with subtle animation
            var brush = new LinearGradientBrush(bounds, FillColor, SecondaryFillColor, 90f);
            var blend = new ColorBlend(3);
            blend.Colors = new[] { FillColor, SecondaryFillColor, FillColor };
            blend.Positions = new[] { 0f, 0.5f, 1f };
            brush.InterpolationColors = blend;
            return brush;
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