using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TremauxLock
{
    internal enum AccentButtonStyle
    {
        Primary,
        Secondary,
        Ghost
    }

    internal sealed class AccentButton : Button
    {
        private bool hovered;
        private bool pressed;
        private AccentButtonStyle buttonStyle = AccentButtonStyle.Primary;

        public AccentButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = AppTheme.CreateBodyFont(9f, FontStyle.Bold);
            Height = 38;
            Width = 156;
            Padding = new Padding(14, 0, 14, 0);
            TextAlign = ContentAlignment.MiddleCenter;
            AutoEllipsis = true;
            ApplyPalette();
        }

        protected override bool ShowFocusCues => false;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AccentButtonStyle ButtonStyle
        {
            get => buttonStyle;
            set
            {
                buttonStyle = value;
                ApplyPalette();
            }
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            ApplyPalette();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            ApplyPalette();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            ApplyPalette();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            pressed = false;
            ApplyPalette();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (mevent.Button == MouseButtons.Left)
            {
                pressed = true;
                ApplyPalette();
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            pressed = false;
            ApplyPalette();
        }

        private void ApplyPalette()
        {
            (Color backColor, Color borderColor, Color textColor) = ResolvePalette();

            BackColor = backColor;
            ForeColor = textColor;
            FlatAppearance.BorderColor = borderColor;
            FlatAppearance.MouseOverBackColor = backColor;
            FlatAppearance.MouseDownBackColor = backColor;
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        }

        private (Color BackColor, Color BorderColor, Color TextColor) ResolvePalette()
        {
            if (!Enabled)
            {
                return (
                    Color.FromArgb(29, 38, 52),
                    Color.FromArgb(46, 58, 76),
                    AppTheme.TextSoft);
            }

            if (ButtonStyle == AccentButtonStyle.Primary)
            {
                if (pressed)
                {
                    return (
                        Color.FromArgb(41, 145, 134),
                        Color.FromArgb(57, 163, 151),
                        AppTheme.TextPrimary);
                }

                if (hovered)
                {
                    return (
                        Color.FromArgb(53, 164, 152),
                        Color.FromArgb(69, 181, 168),
                        AppTheme.TextPrimary);
                }

                return (
                    Color.FromArgb(47, 154, 143),
                    Color.FromArgb(61, 170, 158),
                    AppTheme.TextPrimary);
            }

            if (ButtonStyle == AccentButtonStyle.Secondary)
            {
                if (pressed)
                {
                    return (
                        Color.FromArgb(19, 27, 40),
                        Color.FromArgb(58, 71, 91),
                        AppTheme.TextPrimary);
                }

                if (hovered)
                {
                    return (
                        Color.FromArgb(24, 33, 48),
                        Color.FromArgb(74, 90, 113),
                        AppTheme.TextPrimary);
                }

                return (
                    Color.FromArgb(21, 29, 43),
                    Color.FromArgb(60, 74, 94),
                    AppTheme.TextPrimary);
            }

            if (pressed)
            {
                return (
                    Color.FromArgb(16, 23, 35),
                    Color.FromArgb(48, 60, 79),
                    AppTheme.TextSecondary);
            }

            if (hovered)
            {
                return (
                    Color.FromArgb(20, 29, 43),
                    Color.FromArgb(60, 74, 94),
                    AppTheme.TextPrimary);
            }

            return (
                Color.FromArgb(18, 26, 38),
                Color.FromArgb(45, 58, 76),
                AppTheme.TextSecondary);
        }
    }
}
