using System.Drawing;

namespace HoverTransferInstaller;

internal static class Theme
{
    public static readonly Color Window = Color.FromArgb(8, 20, 28);
    public static readonly Color Panel = Color.FromArgb(13, 31, 41);
    public static readonly Color PanelBorder = Color.FromArgb(35, 71, 84);
    public static readonly Color Text = Color.FromArgb(235, 245, 247);
    public static readonly Color Muted = Color.FromArgb(154, 181, 188);
    public static readonly Color Accent = Color.FromArgb(56, 211, 205);
    public static readonly Color AccentDark = Color.FromArgb(18, 89, 92);
    public static readonly Color Danger = Color.FromArgb(232, 110, 110);

    public static Button Button(string text, bool primary = false)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            Height = 36,
            Padding = new Padding(14, 0, 14, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? AccentDark : Panel,
            ForeColor = primary ? Text : Accent,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        }.Also(button => button.FlatAppearance.BorderColor = primary ? Accent : PanelBorder);
    }

    private static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
