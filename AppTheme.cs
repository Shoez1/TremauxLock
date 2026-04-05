using System.Drawing;
using System.Drawing.Drawing2D;

namespace TremauxLock
{
    internal static class AppTheme
    {
        // Layout
        public const int WindowPadding = 24;
        public const int CardPadding = 20;
        public const int GridGap = 16;
        public const int RadiusCard = 8;
        public const int RadiusPanel = 8;
        public const int RadiusButton = 8;
        public const int RadiusBadge = 999;

        // Background layers — GitHub Dark palette
        public static readonly Color BackgroundPrimary = Color.FromArgb(13, 17, 23);
        public static readonly Color Surface           = Color.FromArgb(22, 27, 34);
        public static readonly Color SurfaceInset      = Color.FromArgb(15, 19, 25);

        // Borders
        public static readonly Color Border    = Color.FromArgb(33, 38, 45);
        public static readonly Color BorderMid = Color.FromArgb(48, 54, 61);

        // Accents
        public static readonly Color AccentBlue   = Color.FromArgb(88, 166, 255);
        public static readonly Color AccentGreen  = Color.FromArgb(63, 185, 80);
        public static readonly Color AccentOrange = Color.FromArgb(225, 178, 94);
        public static readonly Color AccentRed    = Color.FromArgb(248, 81, 73);

        // Text
        public static readonly Color TextPrimary   = Color.FromArgb(230, 237, 243);
        public static readonly Color TextSecondary = Color.FromArgb(139, 148, 158);
        public static readonly Color TextSoft      = Color.FromArgb(72, 79, 88);

        // Legacy aliases
        public static readonly Color BackgroundTop    = Color.FromArgb(13, 17, 23);
        public static readonly Color BackgroundBottom = Color.FromArgb(13, 17, 23);
        public static readonly Color BackgroundGlow   = Color.FromArgb(18, 88, 166, 255);
        public static readonly Color CardFill         = Color.FromArgb(22, 27, 34);
        public static readonly Color CardFillAlt      = Color.FromArgb(22, 27, 34);
        public static readonly Color CardBorder       = Color.FromArgb(33, 38, 45);
        public static readonly Color PanelFill        = Color.FromArgb(22, 27, 34);
        public static readonly Color PanelFillAlt     = Color.FromArgb(22, 27, 34);
        public static readonly Color PanelBorder      = Color.FromArgb(33, 38, 45);
        public static readonly Color Separator        = Color.FromArgb(33, 38, 45);
        public static readonly Color InputFill        = Color.FromArgb(13, 17, 23);
        public static readonly Color InputBorder      = Color.FromArgb(48, 54, 61);
        public static readonly Color InputBorderFocus = Color.FromArgb(88, 166, 255);
        public static readonly Color Accent           = Color.FromArgb(88, 166, 255);
        public static readonly Color AccentEnd        = Color.FromArgb(88, 166, 255);
        public static readonly Color AccentSoft       = Color.FromArgb(88, 166, 255);
        public static readonly Color AccentStrong     = Color.FromArgb(88, 166, 255);
        public static readonly Color BadgeFill        = Color.FromArgb(25, 63, 185, 80);
        public static readonly Color BadgeBorder      = Color.FromArgb(64, 63, 185, 80);
        public static readonly Color Warning          = Color.FromArgb(225, 178, 94);
        public static readonly Color Danger           = Color.FromArgb(248, 81, 73);

        public static Font CreateTitleFont(float size) =>
            CreateFont("Segoe UI", size, FontStyle.Bold);

        public static Font CreateBodyFont(float size, FontStyle style = FontStyle.Regular) =>
            CreateFont("Segoe UI", size, style);

        public static Font CreateCodeFont(float size, FontStyle style = FontStyle.Regular) =>
            CreateFont("Consolas", size, style);

        public static Color WithAlpha(Color color, int alpha) =>
            Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), color);

        public static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius) =>
            CreateRoundedRectangle(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius);

        public static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
        {
            if (bounds.Width <= 0f || bounds.Height <= 0f) return new GraphicsPath();
            float r = Math.Max(1f, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f));
            float d = r * 2f;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawSoftShadow(Graphics g, Rectangle bounds, int cornerRadius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var outer = new SolidBrush(Color.FromArgb(18, 0, 0, 0));
            using var inner = new SolidBrush(Color.FromArgb(26, 0, 0, 0));
            Rectangle os = bounds; os.Offset(0, 10); os.Inflate(10, 8);
            Rectangle is_ = bounds; is_.Offset(0, 4); is_.Inflate(3, 2);
            using var op = CreateRoundedRectangle(new RectangleF(os.X, os.Y, os.Width - 1f, os.Height - 1f), cornerRadius + 10);
            using var ip = CreateRoundedRectangle(new RectangleF(is_.X, is_.Y, is_.Width - 1f, is_.Height - 1f), cornerRadius + 2);
            g.FillPath(outer, op);
            g.FillPath(inner, ip);
        }

        private static Font CreateFont(string primary, float size, FontStyle style, string? fallback = null)
        {
            try { return new Font(primary, size, style, GraphicsUnit.Point); }
            catch
            {
                if (!string.IsNullOrWhiteSpace(fallback))
                    try { return new Font(fallback, size, style, GraphicsUnit.Point); } catch { }
                return new Font(SystemFonts.DefaultFont.FontFamily, size, style, GraphicsUnit.Point);
            }
        }
    }
}
