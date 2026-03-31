using System.Drawing;
using System.Drawing.Drawing2D;

namespace TremauxLock
{
    internal static class AppTheme
    {
        public const int WindowPadding = 28;
        public const int CardPadding = 28;
        public const int GridGap = 16;
        public const int RadiusCard = 20;
        public const int RadiusPanel = 14;
        public const int RadiusButton = 11;
        public const int RadiusBadge = 15;

        public static readonly Color BackgroundTop = Color.FromArgb(10, 17, 30);
        public static readonly Color BackgroundBottom = Color.FromArgb(15, 24, 40);
        public static readonly Color BackgroundGlow = Color.FromArgb(18, 77, 135, 193);
        public static readonly Color CardFill = Color.FromArgb(19, 27, 42);
        public static readonly Color CardFillAlt = Color.FromArgb(16, 23, 37);
        public static readonly Color CardBorder = Color.FromArgb(54, 70, 92);
        public static readonly Color PanelFill = Color.FromArgb(14, 21, 33);
        public static readonly Color PanelFillAlt = Color.FromArgb(14, 20, 31);
        public static readonly Color PanelBorder = Color.FromArgb(44, 57, 75);
        public static readonly Color Separator = Color.FromArgb(42, 57, 77);
        public static readonly Color InputFill = Color.FromArgb(15, 22, 34);
        public static readonly Color InputBorder = Color.FromArgb(61, 76, 99);
        public static readonly Color InputBorderFocus = Color.FromArgb(90, 188, 164);
        public static readonly Color Accent = Color.FromArgb(70, 198, 177);
        public static readonly Color AccentEnd = Color.FromArgb(46, 166, 147);
        public static readonly Color AccentSoft = Color.FromArgb(132, 208, 228);
        public static readonly Color AccentStrong = Color.FromArgb(127, 181, 236);
        public static readonly Color BadgeFill = Color.FromArgb(28, 38, 58, 50);
        public static readonly Color BadgeBorder = Color.FromArgb(78, 92, 128, 112);
        public static readonly Color TextPrimary = Color.FromArgb(244, 247, 252);
        public static readonly Color TextSecondary = Color.FromArgb(176, 188, 202);
        public static readonly Color TextSoft = Color.FromArgb(118, 132, 149);
        public static readonly Color Warning = Color.FromArgb(225, 178, 94);
        public static readonly Color Danger = Color.FromArgb(214, 104, 104);

        public static Font CreateTitleFont(float size) => CreateFont("Segoe UI Semibold", size, FontStyle.Regular);

        public static Font CreateBodyFont(float size, FontStyle style = FontStyle.Regular) => CreateFont("Segoe UI", size, style);

        public static Font CreateCodeFont(float size, FontStyle style = FontStyle.Regular)
            => CreateFont("Cascadia Mono", size, style, "Consolas");

        public static Color WithAlpha(Color color, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), color);
        }

        public static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            return CreateRoundedRectangle(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius);
        }

        public static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
        {
            if (bounds.Width <= 0f || bounds.Height <= 0f)
            {
                return new GraphicsPath();
            }

            float clampedRadius = Math.Max(1f, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f));
            float diameter = clampedRadius * 2f;

            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawSoftShadow(Graphics graphics, Rectangle bounds, int cornerRadius)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var shadowBrushOuter = new SolidBrush(Color.FromArgb(18, 0, 0, 0));
            using var shadowBrushInner = new SolidBrush(Color.FromArgb(26, 0, 0, 0));

            Rectangle outerShadow = bounds;
            outerShadow.Offset(0, 10);
            outerShadow.Inflate(10, 8);

            Rectangle innerShadow = bounds;
            innerShadow.Offset(0, 4);
            innerShadow.Inflate(3, 2);

            using GraphicsPath outerPath = CreateRoundedRectangle(
                new RectangleF(outerShadow.X, outerShadow.Y, outerShadow.Width - 1f, outerShadow.Height - 1f),
                cornerRadius + 10);
            using GraphicsPath innerPath = CreateRoundedRectangle(
                new RectangleF(innerShadow.X, innerShadow.Y, innerShadow.Width - 1f, innerShadow.Height - 1f),
                cornerRadius + 2);

            graphics.FillPath(shadowBrushOuter, outerPath);
            graphics.FillPath(shadowBrushInner, innerPath);
        }

        private static Font CreateFont(string primaryName, float size, FontStyle style, string? fallbackName = null)
        {
            try
            {
                return new Font(primaryName, size, style, GraphicsUnit.Point);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(fallbackName))
                {
                    try
                    {
                        return new Font(fallbackName, size, style, GraphicsUnit.Point);
                    }
                    catch
                    {
                    }
                }

                return new Font(SystemFonts.DefaultFont.FontFamily, size, style, GraphicsUnit.Point);
            }
        }
    }
}
