using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed class InfoRow : UserControl
    {
        private readonly Label lblCaption;
        private readonly Label lblValue;
        private readonly Panel separator;

        private bool useCodeFont;
        private ContentAlignment valueAlignment = ContentAlignment.MiddleRight;

        public InfoRow()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint,
                true);

            BackColor = AppTheme.CardFill;
            DoubleBuffered = true;
            Height = 46;

            lblCaption = new Label
            {
                AutoSize = false,
                BackColor = AppTheme.CardFill,
                ForeColor = AppTheme.TextSoft,
                Font = AppTheme.CreateBodyFont(8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblValue = new Label
            {
                AutoSize = false,
                BackColor = AppTheme.CardFill,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateBodyFont(9.75f),
                TextAlign = valueAlignment,
                AutoEllipsis = true,
                UseMnemonic = false
            };

            separator = new Panel
            {
                BackColor = AppTheme.Separator
            };

            Controls.Add(lblCaption);
            Controls.Add(lblValue);
            Controls.Add(separator);

            Resize += (_, _) => LayoutControls();
            LayoutControls();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Caption
        {
            get => lblCaption.Text;
            set => lblCaption.Text = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ValueText
        {
            get => lblValue.Text;
            set => lblValue.Text = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowSeparator
        {
            get => separator.Visible;
            set => separator.Visible = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool UseCodeFont
        {
            get => useCodeFont;
            set
            {
                useCodeFont = value;
                lblValue.Font = value ? AppTheme.CreateCodeFont(9.25f) : AppTheme.CreateBodyFont(9.75f);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ContentAlignment ValueAlignment
        {
            get => valueAlignment;
            set
            {
                valueAlignment = value;
                lblValue.TextAlign = value;
                LayoutControls();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color ValueColor
        {
            get => lblValue.ForeColor;
            set => lblValue.ForeColor = value;
        }

        private void LayoutControls()
        {
            int captionWidth = 112;
            lblCaption.SetBounds(0, 0, captionWidth, Height - 1);
            lblValue.SetBounds(captionWidth + 8, 0, Width - captionWidth - 8, Height - 1);
            separator.SetBounds(0, Height - 1, Width, 1);
        }
    }
}
