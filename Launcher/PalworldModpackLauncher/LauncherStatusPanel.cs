using System.ComponentModel;

namespace PalworldModpackLauncher;

internal sealed class LauncherStatusPanel : SurfacePanel
{
    private readonly Label versionLabel;
    private readonly Label statusLabel;
    private readonly ProgressBar progressBar;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string VersionText
    {
        get => versionLabel.Text;
        set => versionLabel.Text = value;
    }

    public string StatusText => statusLabel.Text;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool StatusSuccess { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ProgressValue
    {
        get => progressBar.Value;
        set => progressBar.Value = Math.Clamp(value, progressBar.Minimum, progressBar.Maximum);
    }

    public LauncherStatusPanel()
    {
        Dock = DockStyle.Fill;
        Height = 90;
        Padding = new Padding(16, 13, 16, 13);
        Margin = new Padding(0, 0, 0, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        versionLabel = new Label
        {
            AutoSize = true,
            Text = "MODPACK  ·  verificando...",
            ForeColor = Theme.Primary,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6),
        };
        statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Localizando o Palworld...",
            ForeColor = Theme.Soft,
            Font = new Font("Segoe UI", 9.25F),
            AutoEllipsis = true,
        };
        progressBar = new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 5,
            Minimum = 0,
            Maximum = 100,
            Visible = false,
            Style = ProgressBarStyle.Continuous,
            Margin = new Padding(0, 8, 0, 0),
        };

        layout.Controls.Add(versionLabel, 0, 0);
        layout.Controls.Add(statusLabel, 0, 1);
        layout.Controls.Add(progressBar, 0, 2);
        Controls.Add(layout);
    }

    public void SetStatus(string message, bool success)
    {
        StatusSuccess = success;
        statusLabel.Text = message;
        statusLabel.ForeColor = success ? Theme.Secondary : Theme.Soft;
    }

    public void SetProgressVisible(bool visible)
    {
        progressBar.Visible = visible;
        if (!visible) progressBar.Value = 0;
    }
}
