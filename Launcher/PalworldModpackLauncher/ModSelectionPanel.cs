using System.Drawing;

namespace PalworldModpackLauncher;

internal sealed class ModSelectionPanel : UserControl
{
    private readonly FlowLayoutPanel optionsFlow;
    private readonly Label emptyLabel;
    private readonly List<CheckBox> checkBoxes = new();
    private bool updating;

    public event EventHandler? SelectionChanged;

    public ModSelectionPanel()
    {
        Height = 180;
        Dock = DockStyle.Top;
        BackColor = Theme.Panel;
        Padding = new Padding(18, 14, 18, 14);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Panel,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "Mods desta sessão",
            AutoSize = true,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2),
        };
        var subtitle = new Label
        {
            Text = "Somente os selecionados serão carregados ao clicar em Jogar com mods.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Margin = new Padding(0, 0, 0, 9),
        };

        optionsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Theme.Panel,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        emptyLabel = new Label
        {
            Text = "Nenhum mod selecionável foi encontrado.",
            AutoSize = true,
            ForeColor = Theme.Muted,
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
                var checkBox = new CheckBox
                {
                    Tag = option.Id,
                    Text = option.DisplayName + Environment.NewLine + option.Description,
                    Checked = selectedSet.Contains(option.Id),
                    AutoSize = false,
                    Height = 48,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Theme.Panel,
                    ForeColor = Theme.Text,
                    Font = new Font("Segoe UI", 9F),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(2, 0, 5, 0),
                    Margin = new Padding(0, 0, 12, 7),
                    Cursor = Cursors.Hand,
                    UseVisualStyleBackColor = false,
                };
                checkBox.FlatAppearance.BorderColor = Theme.Border;
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
        var available = Math.Max(300, optionsFlow.ClientSize.Width - 18);
        var columns = available >= 650 ? 2 : 1;
        var width = Math.Max(280, available / columns - 12);
        foreach (var checkBox in checkBoxes) checkBox.Width = width;
    }
}
