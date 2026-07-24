using System.Drawing;

namespace PalworldModpackLauncher;

internal enum ButtonKind
{
    Standard,
    Primary,
    Subtle,
    Danger,
}

internal static class Theme
{
    public static readonly Color Window = ColorTranslator.FromHtml("#111111");
    public static readonly Color Surface = ColorTranslator.FromHtml("#1D1D1D");
    public static readonly Color SurfaceMuted = ColorTranslator.FromHtml("#242424");
    public static readonly Color SurfaceSoft = ColorTranslator.FromHtml("#151515");
    public static readonly Color Border = ColorTranslator.FromHtml("#3F3F46");
    public static readonly Color BorderStrong = ColorTranslator.FromHtml("#555555");
    public static readonly Color Text = ColorTranslator.FromHtml("#F8FAFC");
    public static readonly Color Muted = ColorTranslator.FromHtml("#CBD5E1");
    public static readonly Color Soft = ColorTranslator.FromHtml("#94A3B8");
    public static readonly Color Primary = ColorTranslator.FromHtml("#FF6600");
    public static readonly Color PrimaryHover = ColorTranslator.FromHtml("#FF7A26");
    public static readonly Color PrimaryPressed = ColorTranslator.FromHtml("#E65C00");
    public static readonly Color Secondary = ColorTranslator.FromHtml("#26A69A");
    public static readonly Color Accent = ColorTranslator.FromHtml("#9C27B0");
    public static readonly Color Positive = ColorTranslator.FromHtml("#21BA45");
    public static readonly Color Warning = ColorTranslator.FromHtml("#F2C037");
    public static readonly Color Negative = ColorTranslator.FromHtml("#C10015");

    public static Button Button(string text, ButtonKind kind = ButtonKind.Standard)
    {
        var (background, foreground, border, hover, pressed) = kind switch
        {
            ButtonKind.Primary => (Primary, Window, Primary, PrimaryHover, PrimaryPressed),
            ButtonKind.Subtle => (SurfaceSoft, Muted, Border, SurfaceMuted, Surface),
            ButtonKind.Danger => (Surface, Color.FromArgb(255, 177, 177), Negative, Color.FromArgb(53, 26, 29), Color.FromArgb(65, 21, 26)),
            _ => (SurfaceMuted, Text, BorderStrong, Color.FromArgb(48, 48, 48), Surface),
        };

        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(kind == ButtonKind.Primary ? 138 : 104, 38),
            Padding = new Padding(13, 0, 13, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = background,
            ForeColor = foreground,
            Font = new Font("Segoe UI Semibold", 9.25F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.FlatAppearance.MouseDownBackColor = pressed;
        return button;
    }
}
