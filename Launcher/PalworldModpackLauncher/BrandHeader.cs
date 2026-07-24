namespace PalworldModpackLauncher;

internal sealed class BrandHeader : UserControl
{
    public BrandHeader()
    {
        Dock = DockStyle.Fill;
        Height = 66;
        BackColor = Theme.Window;
        Margin = new Padding(0, 0, 0, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Window,
            Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var mark = new SurfacePanel
        {
            Width = 48,
            Height = 48,
            CornerRadius = 12,
            BorderColor = Theme.Primary,
            BackColor = Theme.Primary,
            Margin = new Padding(0, 2, 14, 0),
        };
        mark.Controls.Add(new Label
        {
            Text = "P",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Black", 20F, FontStyle.Bold),
            ForeColor = Theme.Window,
            BackColor = Theme.Primary,
        });

        var names = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Theme.Window,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        names.Controls.Add(new Label
        {
            Text = "Palncher",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 23F, FontStyle.Bold),
            ForeColor = Theme.Text,
            Margin = Padding.Empty,
        }, 0, 0);
        names.Controls.Add(new Label
        {
            Text = "Seu Palworld, do seu jeito.",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Theme.Soft,
            Margin = new Padding(2, 0, 0, 0),
        }, 0, 1);

        var modeBadge = new Label
        {
            Text = "STEAM  ·  VANILLA",
            AutoSize = true,
            Padding = new Padding(13, 8, 13, 8),
            BackColor = Theme.SurfaceMuted,
            ForeColor = Theme.Secondary,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            Margin = new Padding(12, 9, 0, 0),
        };

        layout.Controls.Add(mark, 0, 0);
        layout.Controls.Add(names, 1, 0);
        layout.Controls.Add(modeBadge, 2, 0);
        Controls.Add(layout);
    }
}
