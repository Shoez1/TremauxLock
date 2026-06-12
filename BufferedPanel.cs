using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint,
                true);

            DoubleBuffered = true;
        }
    }
}
