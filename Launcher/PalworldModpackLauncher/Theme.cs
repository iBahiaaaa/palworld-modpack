using System.Drawing;

namespace PalworldModpackLauncher;

internal static class Theme
{
    public static readonly Color Window = Color.FromArgb(7, 18, 27);
    public static readonly Color Panel = Color.FromArgb(13, 31, 43);
    public static readonly Color Border = Color.FromArgb(32, 65, 79);
    public static readonly Color Text = Color.FromArgb(236, 245, 248);
    public static readonly Color Muted = Color.FromArgb(151, 177, 187);
    public static readonly Color Accent = Color.FromArgb(72, 219, 216);
    public static readonly Color AccentDark = Color.FromArgb(17, 69, 76);
    public static readonly Color Warning = Color.FromArgb(245, 190, 73);

    public static Button Button(string text, bool primary = false)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(124, 40),
            Padding = new Padding(14, 0, 14, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? AccentDark : Panel,
            ForeColor = primary ? Accent : Text,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = false,
        }.WithBorder(primary ? Accent : Border);
    }

    private static Button WithBorder(this Button button, Color color)
    {
        button.FlatAppearance.BorderColor = color;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, 52, 65);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(17, 43, 55);
        return button;
    }
}
