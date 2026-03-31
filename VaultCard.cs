namespace TremauxLock
{
    internal sealed class VaultCard : SurfacePanel
    {
        public VaultCard()
        {
            FillColor = AppTheme.CardFill;
            SecondaryFillColor = AppTheme.CardFill;
            BorderColor = AppTheme.CardBorder;
            InnerStrokeColor = AppTheme.WithAlpha(System.Drawing.Color.White, 10);
            CornerRadius = AppTheme.RadiusCard;
            BorderThickness = 1.2f;
        }
    }
}
