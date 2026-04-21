using System.Drawing;
using System.Drawing.Drawing2D;

namespace TremauxLock
{
    internal static class AppTheme
    {
        // Layout
        public const int WindowPadding = 24;
        public const int CardPadding = 24;
        public const int GridGap = 16;
        public const int RadiusCard = 12;
        public const int RadiusPanel = 12;
        public const int RadiusButton = 10;
        public const int RadiusBadge = 999;

        // Background layers — Hacker Impact Palette
        public static readonly Color BackgroundPrimary = Color.FromArgb(0, 0, 0);
        public static readonly Color BackgroundSurface = Color.FromArgb(10, 10, 10);
        public static readonly Color BackgroundPanel = Color.FromArgb(20, 20, 20);
        public static readonly Color BackgroundOverlay = Color.FromArgb(17, 17, 17, 100);

        // Borders and Glow Effects
        public static readonly Color BorderPrimary = Color.FromArgb(51, 51, 51);
        public static readonly Color BorderSecondary = Color.FromArgb(34, 34, 34);
        public static readonly Color GlowPrimary = Color.FromArgb(0, 255, 136, 200);
        public static readonly Color GlowSecondary = Color.FromArgb(255, 0, 255, 200);
        public static readonly Color GlowAccent = Color.FromArgb(0, 255, 255, 150);
        public static readonly Color GlowDanger = Color.FromArgb(255, 0, 0, 150);

        // Hacker Impact Colors
        public static readonly Color HackerGreen = Color.FromArgb(0, 255, 136);
        public static readonly Color HackerMagenta = Color.FromArgb(255, 0, 255);
        public static readonly Color HackerCyan = Color.FromArgb(0, 255, 255);
        public static readonly Color HackerRed = Color.FromArgb(255, 0, 0);
        public static readonly Color HackerYellow = Color.FromArgb(255, 255, 0);
        public static readonly Color HackerBlue = Color.FromArgb(0, 128, 255);

        // Text Colors with Impact
        public static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
        public static readonly Color TextSecondary = Color.FromArgb(200, 200, 200);
        public static readonly Color TextMuted = Color.FromArgb(128, 128, 128);
        public static readonly Color TextCode = Color.FromArgb(0, 255, 136);
        public static readonly Color TextAccent = Color.FromArgb(255, 0, 255);
        public static readonly Color TextWarning = Color.FromArgb(255, 255, 0);

        // Legacy aliases for compatibility
        public static readonly Color BackgroundTop = BackgroundPrimary;
        public static readonly Color BackgroundBottom = BackgroundPrimary;
        public static readonly Color BackgroundGlow = GlowPrimary;
        public static readonly Color CardFill = BackgroundSurface;
        public static readonly Color CardFillAlt = BackgroundPanel;
        public static readonly Color CardBorder = BorderPrimary;
        public static readonly Color PanelFill = BackgroundSurface;
        public static readonly Color PanelFillAlt = BackgroundPanel;
        public static readonly Color PanelBorder = BorderPrimary;
        public static readonly Color Separator = BorderPrimary;
        public static readonly Color InputFill = BackgroundSurface;
        public static readonly Color InputBorder = BorderSecondary;
        public static readonly Color InputBorderFocus = HackerGreen;
        public static readonly Color Accent = HackerGreen;
        public static readonly Color AccentEnd = HackerGreen;
        public static readonly Color AccentSoft = HackerGreen;
        public static readonly Color AccentStrong = HackerGreen;
        public static readonly Color BadgeFill = Color.FromArgb(25, 0, 255, 0);
        public static readonly Color BadgeBorder = Color.FromArgb(64, 0, 255, 0);
        public static readonly Color Warning = HackerYellow;
        public static readonly Color Danger = HackerRed;

        // Legacy aliases used by AccentButton, VaultCard, InfoRow, MainForm
        public static readonly Color Border = BorderPrimary;
        public static readonly Color BorderMid = BorderSecondary;
        public static readonly Color TextSoft = TextSecondary;
        public static readonly Color Surface = BackgroundSurface;
        public static readonly Color AccentBlue = HackerBlue;
        public static readonly Color AccentGreen = HackerGreen;

        // Hacker Impact Fonts
        public static Font CreateDisplayFont(float size) =>
            CreateFont("Orbitron", size, FontStyle.Bold);

        public static Font CreateTitleFont(float size) =>
            CreateFont("Rajdhani", size, FontStyle.Bold);

        public static Font CreateBodyFont(float size, FontStyle style = FontStyle.Regular) =>
            CreateFont("Fira Code", size, style);

        public static Font CreateCodeFont(float size, FontStyle style = FontStyle.Regular) =>
            CreateFont("Fira Code", size, style);

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
