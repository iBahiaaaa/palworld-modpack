using System.ComponentModel;

namespace PalworldModpackLauncher;

internal sealed class GameLocationPanel : SurfacePanel
{
    private readonly TextBox pathTextBox;
    private readonly Button browseButton;
    private readonly Button detectButton;

    public event EventHandler? GamePathChanged;
    public event EventHandler? BrowseRequested;
    public event EventHandler? DetectRequested;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string GamePath
    {
        get => pathTextBox.Text;
        set => pathTextBox.Text = value;
    }

    public GameLocationPanel()
    {
        Dock = DockStyle.Fill;
        Height = 92;
        Padding = new Padding(16, 12, 16, 14);
        Margin = new Padding(0, 0, 0, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "INSTALAÇÃO DO PALWORLD",
            AutoSize = true,
            ForeColor = Theme.Soft,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8),
        };

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var inputShell = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 8,
            BorderColor = Theme.BorderStrong,
            BackColor = Theme.SurfaceMuted,
            Padding = new Padding(12, 10, 12, 8),
            Margin = new Padding(0, 0, 8, 0),
        };
        pathTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.SurfaceMuted,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 9.5F),
        };
        pathTextBox.TextChanged += (_, _) => GamePathChanged?.Invoke(this, EventArgs.Empty);
        inputShell.Controls.Add(pathTextBox);

        browseButton = Theme.Button("Procurar", ButtonKind.Subtle);
        browseButton.Margin = new Padding(0, 0, 8, 0);
        browseButton.Click += (_, _) => BrowseRequested?.Invoke(this, EventArgs.Empty);
        detectButton = Theme.Button("Detectar", ButtonKind.Subtle);
        detectButton.Margin = Padding.Empty;
        detectButton.Click += (_, _) => DetectRequested?.Invoke(this, EventArgs.Empty);

        row.Controls.Add(inputShell, 0, 0);
        row.Controls.Add(browseButton, 1, 0);
        row.Controls.Add(detectButton, 2, 0);
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(row, 0, 1);
        Controls.Add(layout);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        pathTextBox.Enabled = enabled;
        browseButton.Enabled = enabled;
        detectButton.Enabled = enabled;
    }
}
