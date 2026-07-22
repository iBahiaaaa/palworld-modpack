using System.Diagnostics;
using System.Drawing;

namespace HoverTransferInstaller;

internal sealed class MainForm : Form
{
    private readonly TextBox pathTextBox;
    private readonly Label statusLabel;
    private readonly Button installButton;
    private readonly Button removeButton;
    private readonly InstallerService installer = new();

    public MainForm()
    {
        Text = "Hover Transfer - Instalador";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(700, 390);
        MinimumSize = new Size(650, 390);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 24, 28, 22),
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Theme.Window,
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Hover Transfer",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold),
            ForeColor = Theme.Text,
            Margin = new Padding(0, 0, 0, 2),
        };
        var subtitle = new Label
        {
            Text = "Instala o mod do H e o carregador UE4SS necessário.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Margin = new Padding(2, 0, 0, 22),
        };

        var pathLabel = new Label
        {
            Text = "Pasta da Steam ou do Palworld",
            AutoSize = true,
            ForeColor = Theme.Text,
            Margin = new Padding(0, 0, 0, 7),
        };

        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 18),
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        pathTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Height = 36,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 8, 0),
        };
        pathTextBox.TextChanged += (_, _) => RefreshResolvedPath();

        var browseButton = Theme.Button("Selecionar...");
        browseButton.Margin = new Padding(0, 0, 8, 0);
        browseButton.Click += (_, _) => Browse();
        var detectButton = Theme.Button("Detectar");
        detectButton.Margin = Padding.Empty;
        detectButton.Click += (_, _) => Detect();

        pathRow.Controls.Add(pathTextBox, 0, 0);
        pathRow.Controls.Add(browseButton, 1, 0);
        pathRow.Controls.Add(detectButton, 2, 0);

        var infoPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Panel,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 18),
        };
        statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Theme.Muted,
            Text = "Selecione a pasta. O instalador localizará o Palworld automaticamente.",
            AutoEllipsis = true,
        };
        infoPanel.Controls.Add(statusLabel);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        installButton = Theme.Button("Instalar", primary: true);
        installButton.Enabled = false;
        installButton.Click += async (_, _) => await RunInstall();
        removeButton = Theme.Button("Remover mod");
        removeButton.Enabled = false;
        removeButton.Click += async (_, _) => await RunUninstall();
        actions.Controls.Add(installButton);
        actions.Controls.Add(removeButton);

        content.Controls.Add(title, 0, 0);
        content.Controls.Add(subtitle, 0, 1);
        content.Controls.Add(pathLabel, 0, 2);
        content.Controls.Add(pathRow, 0, 3);
        content.Controls.Add(infoPanel, 0, 4);
        content.Controls.Add(actions, 0, 5);
        Controls.Add(content);

        Shown += (_, _) => Detect();
    }

    private string? ResolvedGameRoot => PalworldLocator.Resolve(pathTextBox.Text);

    private void Detect()
    {
        var detected = PalworldLocator.DetectInstalledGame();
        if (detected is null)
        {
            SetStatus("Não encontrei o Palworld automaticamente. Clique em Selecionar.", false);
            return;
        }
        pathTextBox.Text = detected;
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecione a biblioteca da Steam ou a pasta do Palworld",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (Directory.Exists(pathTextBox.Text)) dialog.InitialDirectory = pathTextBox.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK) pathTextBox.Text = dialog.SelectedPath;
    }

    private void RefreshResolvedPath()
    {
        var root = ResolvedGameRoot;
        var valid = root is not null;
        installButton.Enabled = valid;
        removeButton.Enabled = valid && installer.IsModInstalled(root!);
        SetStatus(valid
            ? $"Palworld encontrado em:\n{root}"
            : "A pasta selecionada ainda não contém uma instalação válida do Palworld.", valid);
    }

    private async Task RunInstall()
    {
        var root = ResolvedGameRoot;
        if (root is null) return;
        if (IsPalworldRunning())
        {
            MessageBox.Show(this, "Feche o Palworld antes de instalar. A Steam pode continuar aberta.",
                "Palworld aberto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true, "Instalando e criando backup...");
        var result = await Task.Run(() => installer.Install(root));
        SetBusy(false, result.Message, result.Success);
        MessageBox.Show(this, result.Message, result.Success ? "Instalação concluída" : "Não foi possível instalar",
            MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private async Task RunUninstall()
    {
        var root = ResolvedGameRoot;
        if (root is null) return;
        if (IsPalworldRunning())
        {
            MessageBox.Show(this, "Feche o Palworld antes de remover o mod.", "Palworld aberto",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(this, "Remover somente o Hover Transfer? O UE4SS e outros mods serão preservados.",
                "Confirmar remoção", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true, "Removendo o Hover Transfer...");
        var result = await Task.Run(() => installer.UninstallMod(root));
        SetBusy(false, result.Message, result.Success);
    }

    private void SetBusy(bool busy, string message, bool success = true)
    {
        pathTextBox.Enabled = !busy;
        installButton.Enabled = !busy && ResolvedGameRoot is not null;
        removeButton.Enabled = !busy && ResolvedGameRoot is not null && installer.IsModInstalled(ResolvedGameRoot!);
        SetStatus(message, success);
    }

    private void SetStatus(string message, bool success)
    {
        statusLabel.Text = message;
        statusLabel.ForeColor = success ? Theme.Accent : Theme.Muted;
    }

    private static bool IsPalworldRunning()
    {
        return Process.GetProcessesByName("Palworld-Win64-Shipping").Length > 0 ||
               Process.GetProcessesByName("Palworld").Length > 0;
    }
}
