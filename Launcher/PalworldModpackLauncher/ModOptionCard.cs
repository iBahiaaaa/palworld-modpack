using System.Drawing.Drawing2D;

namespace PalworldModpackLauncher;

internal sealed class ModOptionCard : CheckBox
{
    private readonly string title;
    private readonly string description;
    private readonly Font titleFont = new("Segoe UI Semibold", 9.25F, FontStyle.Bold);
    private readonly Font descriptionFont = new("Segoe UI", 8.25F);

    public ModOptionCard(ModOption option, bool selected)
    {
        title = option.DisplayName;
        description = option.Description;
        Tag = option.Id;
        Checked = selected;
        AutoSize = false;
        Height = 58;
        Appearance = Appearance.Button;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.SurfaceSoft;
        ForeColor = Theme.Text;
        Margin = new Padding(0, 0, 10, 8);
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
        using var path = SurfacePanel.CreateRoundedRectangle(bounds, 10);
        using var fill = new SolidBrush(Checked ? Color.FromArgb(38, 31, 26) : Theme.SurfaceSoft);
        using var border = new Pen(Checked ? Theme.Primary : Theme.Border);
        eventArgs.Graphics.FillPath(fill, path);
        eventArgs.Graphics.DrawPath(border, path);

        var checkBounds = new Rectangle(14, 19, 18, 18);
        using var checkBackground = new SolidBrush(Checked ? Theme.Primary : Theme.SurfaceMuted);
        using var checkBorder = new Pen(Checked ? Theme.Primary : Theme.BorderStrong);
        eventArgs.Graphics.FillRectangle(checkBackground, checkBounds);
        eventArgs.Graphics.DrawRectangle(checkBorder, checkBounds);
        if (Checked)
        {
            using var checkPen = new Pen(Theme.Window, 2F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            eventArgs.Graphics.DrawLines(checkPen,
            [
                new Point(18, 28),
                new Point(22, 32),
                new Point(29, 24),
            ]);
        }

        var titleBounds = new Rectangle(44, 10, Math.Max(20, Width - 56), 20);
        var descriptionBounds = new Rectangle(44, 30, Math.Max(20, Width - 56), 19);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            title,
            titleFont,
            titleBounds,
            Theme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            description,
            descriptionFont,
            descriptionBounds,
            Theme.Soft,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
    }

    protected override void OnCheckedChanged(EventArgs eventArgs)
    {
        base.OnCheckedChanged(eventArgs);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            titleFont.Dispose();
            descriptionFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
