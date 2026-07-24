using System.Drawing;

namespace PalworldModpackLauncher;

internal sealed class ModSelectionPanel : SurfacePanel
{
    private readonly FlowLayoutPanel optionsFlow;
    private readonly Label emptyLabel;
    private readonly List<CheckBox> checkBoxes = new();
    private bool updating;

    public event EventHandler? SelectionChanged;

    public ModSelectionPanel()
    {
        Height = 230;
        Dock = DockStyle.Fill;
        BackColor = Theme.Surface;
        Padding = new Padding(16, 13, 16, 12);
        Margin = new Padding(0, 0, 0, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "MODS DESTA SESSÃO",
            AutoSize = true,
            ForeColor = Theme.Soft,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 3),
        };
        var subtitle = new Label
        {
            Text = "Somente os selecionados serão carregados ao clicar em Jogar com mods.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 9F),
            Margin = new Padding(0, 0, 0, 10),
        };

        optionsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Theme.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        emptyLabel = new Label
        {
            Text = "Nenhum mod selecionável foi encontrado.",
            AutoSize = true,
            ForeColor = Theme.Soft,
            Margin = new Padding(0, 8, 0, 0),
        };

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(optionsFlow, 0, 2);
        Controls.Add(layout);
        SizeChanged += (_, _) => ResizeOptions();
    }

    public IReadOnlyCollection<string> SelectedModIds =>
        checkBoxes
            .Where(checkBox => checkBox.Checked)
            .Select(checkBox => (string)checkBox.Tag!)
            .ToArray();

    public void SetMods(
        IReadOnlyList<ModOption> options,
        IReadOnlyCollection<string> selected)
    {
        updating = true;
        try
        {
            optionsFlow.SuspendLayout();
            optionsFlow.Controls.Clear();
            checkBoxes.Clear();
            var selectedSet = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (options.Count == 0)
            {
                optionsFlow.Controls.Add(emptyLabel);
                return;
            }

            foreach (var option in options)
            {
                var checkBox = new ModOptionCard(option, selectedSet.Contains(option.Id));
                checkBox.CheckedChanged += (_, _) =>
                {
                    if (!updating) SelectionChanged?.Invoke(this, EventArgs.Empty);
                };
                checkBoxes.Add(checkBox);
                optionsFlow.Controls.Add(checkBox);
            }
            ResizeOptions();
        }
        finally
        {
            optionsFlow.ResumeLayout();
            updating = false;
        }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        foreach (var checkBox in checkBoxes) checkBox.Enabled = enabled;
    }

    private void ResizeOptions()
    {
        if (checkBoxes.Count == 0) return;
        var available = Math.Max(300, optionsFlow.ClientSize.Width - 4);
        var columns = available >= 680 ? 2 : 1;
        var width = Math.Max(280, available / columns - 18);
        foreach (var checkBox in checkBoxes) checkBox.Width = width;
    }
}
