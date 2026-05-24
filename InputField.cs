using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TremauxLock
{
    internal sealed class InputField : UserControl
    {
        private readonly Label lblCaption;
        private readonly Panel inputShell;
        private readonly TextBox txtInput;

        private bool isFocused;
        private bool useCodeStyle;
        private string? placeholderText;

        public InputField()
        {
            Size = new Size(360, 68);

            lblCaption = new Label
            {
                AutoSize = false,
                Height = 18,
                ForeColor = AppTheme.TextSecondary,
                BackColor = BackColor,
                Font = AppTheme.CreateBodyFont(8.75f, FontStyle.Bold)
            };

            inputShell = new Panel
            {
                BackColor = AppTheme.InputFill
            };
            inputShell.Paint += InputShell_Paint;

            txtInput = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.InputFill,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.CreateBodyFont(10f),
                Multiline = false
            };

            txtInput.Enter += (_, _) =>
            {
                isFocused = true;
                inputShell.Invalidate();
            };

            txtInput.Leave += (_, _) =>
            {
                isFocused = false;
                inputShell.Invalidate();
            };

            txtInput.TextChanged += (_, _) =>
            {
                TextValueChanged?.Invoke(this, System.EventArgs.Empty);
                inputShell.Invalidate();
            };

            inputShell.Controls.Add(txtInput);
            Controls.Add(lblCaption);
            Controls.Add(inputShell);

            BackColor = AppTheme.BackgroundTop;

            Resize += (_, _) => LayoutControls();
            LayoutControls();
        }

        private void InputShell_Paint(object? sender, PaintEventArgs e)
        {
            using var pen = new Pen(isFocused ? AppTheme.InputBorderFocus : AppTheme.InputBorder, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, inputShell.Width - 1), Math.Max(0, inputShell.Height - 1));

            // Draw placeholder text when empty and not focused
            if (!string.IsNullOrWhiteSpace(placeholderText) && string.IsNullOrEmpty(txtInput.Text) && !isFocused)
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    placeholderText,
                    txtInput.Font,
                    new Rectangle(12, 11, Math.Max(0, inputShell.Width - 24), 18),
                    AppTheme.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnBackColorChanged(System.EventArgs e)
        {
            base.OnBackColorChanged(e);

            if (lblCaption != null)
            {
                lblCaption.BackColor = BackColor;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public event System.EventHandler? TextValueChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Caption
        {
            get => lblCaption.Text;
            set => lblCaption.Text = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? PlaceholderText
        {
            get => placeholderText;
            set
            {
                placeholderText = value;
                inputShell.Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TextValue
        {
            get => txtInput.Text;
            set => txtInput.Text = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool UsePassword
        {
            get => txtInput.UseSystemPasswordChar;
            set => txtInput.UseSystemPasswordChar = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool UseCodeStyle
        {
            get => useCodeStyle;
            set
            {
                useCodeStyle = value;
                txtInput.Font = value ? AppTheme.CreateCodeFont(10f) : AppTheme.CreateBodyFont(10f);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TextBox InnerTextBox => txtInput;

        public new void Focus()
        {
            txtInput.Focus();
        }

        public void SelectAll()
        {
            txtInput.SelectAll();
        }

        private void LayoutControls()
        {
            lblCaption.SetBounds(0, 0, Width, 18);
            inputShell.SetBounds(0, 24, Width, 40);
            txtInput.SetBounds(12, 11, Math.Max(0, inputShell.Width - 24), 18);
        }
    }
}
