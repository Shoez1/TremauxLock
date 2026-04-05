namespace TremauxLock
{
    internal sealed class VaultCard : SurfacePanel
    {
        public VaultCard()
        {
            FillColor = AppTheme.Surface;
            SecondaryFillColor = AppTheme.Surface;
            BorderColor = AppTheme.Border;
            InnerStrokeColor = System.Drawing.Color.FromArgb(10, 255, 255, 255);
            CornerRadius = AppTheme.RadiusCard;
            BorderThickness = 1f;
        }
    }
}
