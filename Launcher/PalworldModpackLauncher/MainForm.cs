using System.Drawing;

namespace PalworldModpackLauncher;

internal sealed class MainForm : Form
{
    private readonly TextBox pathTextBox;
    private readonly Label versionLabel;
    private readonly Label statusLabel;
    private readonly ProgressBar progressBar;
    private readonly Button checkButton;
    private readonly Button playButton;
    private readonly UpdateCoordinator coordinator = new();
    private CancellationTokenSource? operationCancellation;

    public MainForm()
    {
        Text = "Palworld Modpack - Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 470);
        MinimumSize = new Size(680, 450);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var content = CreateLayout();
        var title = new Label
        {
            Text = "Palworld Modpack",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold),
            ForeColor = Theme.Text,
            Margin = new Padding(0, 0, 0, 2),
        };
        var subtitle = new Label
        {
            Text = "Atualiza seus mods automaticamente antes de abrir o jogo.",
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
        pathTextBox.TextChanged += (_, _) => RefreshGameState();
        var browseButton = Theme.Button("Selecionar...");
        browseButton.Margin = new Padding(0, 0, 8, 0);
        browseButton.Click += (_, _) => Browse();
        var detectButton = Theme.Button("Detectar");
        detectButton.Margin = Padding.Empty;
        detectButton.Click += (_, _) => Detect();
        pathRow.Controls.Add(pathTextBox, 0, 0);
        pathRow.Controls.Add(browseButton, 1, 0);
        pathRow.Controls.Add(detectButton, 2, 0);

        var infoPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Panel,
            Padding = new Padding(18),
            RowCount = 3,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 0, 18),
        };
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        versionLabel = new Label
        {
            AutoSize = true,
            Text = "Versão instalada: verificando...",
            ForeColor = Theme.Accent,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12),
        };
        statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Localizando o Palworld...",
            ForeColor = Theme.Muted,
            AutoEllipsis = true,
        };
        progressBar = new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 8,
            Minimum = 0,
            Maximum = 100,
            Visible = false,
            Style = ProgressBarStyle.Continuous,
            Margin = new Padding(0, 14, 0, 0),
        };
        infoPanel.Controls.Add(versionLabel, 0, 0);
        infoPanel.Controls.Add(statusLabel, 0, 1);
        infoPanel.Controls.Add(progressBar, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        playButton = Theme.Button("Jogar Palworld", primary: true);
        playButton.Enabled = false;
        playButton.Click += async (_, _) => await PlayAsync();
        checkButton = Theme.Button("Verificar atualização");
        checkButton.Enabled = false;
        checkButton.Click += async (_, _) => await CheckAndUpdateAsync(automatic: false);
        actions.Controls.Add(playButton);
        actions.Controls.Add(checkButton);

        content.Controls.Add(title, 0, 0);
        content.Controls.Add(subtitle, 0, 1);
        content.Controls.Add(pathLabel, 0, 2);
        content.Controls.Add(pathRow, 0, 3);
        content.Controls.Add(infoPanel, 0, 4);
        content.Controls.Add(actions, 0, 5);
        Controls.Add(content);

        Shown += async (_, _) =>
        {
            Detect();
            if (ResolvedGameRoot is not null) await CheckAndUpdateAsync(automatic: true);
        };
        FormClosed += (_, _) =>
        {
            operationCancellation?.Cancel();
            coordinator.Dispose();
        };
    }

    private static TableLayoutPanel CreateLayout()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 25, 30, 24),
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
        return content;
    }

    private string? ResolvedGameRoot => PalworldLocator.Resolve(pathTextBox.Text);

    private void Detect()
    {
        var saved = LauncherSettingsStore.Load().GameRoot;
        var detected = PalworldLocator.Resolve(saved) ?? PalworldLocator.DetectInstalledGame();
        if (detected is null)
        {
            SetStatus("Não encontrei o Palworld. Clique em Selecionar.", false);
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

    private void RefreshGameState()
    {
        var root = ResolvedGameRoot;
        var valid = root is not null;
        checkButton.Enabled = valid;
        playButton.Enabled = valid;
        if (!valid)
        {
            versionLabel.Text = "Versão instalada: —";
            SetStatus("A pasta selecionada não contém uma instalação válida do Palworld.", false);
            return;
        }

        LauncherSettingsStore.Save(root!);
        var state = coordinator.ReadState(root!);
        versionLabel.Text = "Versão instalada: " + (state?.Version ?? "não instalada");
        SetStatus($"Palworld encontrado em:\n{root}", true);
    }

    private async Task PlayAsync()
    {
        if (ResolvedGameRoot is null) return;
        await CheckAndUpdateAsync(automatic: true);
        if (!UpdateCoordinator.IsPalworldRunning()) UpdateCoordinator.LaunchGame();
    }

    private async Task CheckAndUpdateAsync(bool automatic)
    {
        var root = ResolvedGameRoot;
        if (root is null) return;
        if (operationCancellation is not null) return;

        operationCancellation = new CancellationTokenSource();
        SetBusy(true, "Consultando a última versão no GitHub...");
        try
        {
            var (latest, needsUpdate) = await coordinator.CheckAsync(root, operationCancellation.Token);
            if (!needsUpdate)
            {
                versionLabel.Text = $"Versão instalada: {latest.Version.ToString(3)} — atualizada";
                SetStatus("Seu modpack já está atualizado.", true);
                return;
            }

            if (UpdateCoordinator.IsPalworldRunning())
            {
                SetStatus($"Atualização {latest.Version.ToString(3)} pendente. Feche o Palworld para instalar.", false);
                return;
            }

            if (!automatic && MessageBox.Show(this,
                    $"A versão {latest.Version.ToString(3)} está disponível. Baixar e instalar agora?",
                    "Atualização disponível",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                SetStatus("Atualização adiada.", false);
                return;
            }

            SetBusy(true, $"Baixando o modpack {latest.Version.ToString(3)}...", showProgress: true);
            var progress = new Progress<int>(value => progressBar.Value = value);
            var result = await coordinator.DownloadAndApplyAsync(
                root,
                latest,
                progress,
                operationCancellation.Token);
            versionLabel.Text = result.Success
                ? $"Versão instalada: {latest.Version.ToString(3)} — atualizada"
                : "Versão instalada: " + (coordinator.ReadState(root)?.Version ?? "não instalada");
            SetStatus(result.Message, result.Success);
            if (!result.Success)
                MessageBox.Show(this, result.Message, "Falha na atualização", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (HttpRequestException)
        {
            SetStatus("Sem acesso ao GitHub. Você ainda pode jogar com a versão instalada.", false);
        }
        catch (TaskCanceledException)
        {
            SetStatus("A verificação de atualização foi cancelada.", false);
        }
        catch (Exception exception)
        {
            SetStatus("Não foi possível verificar atualizações: " + exception.Message, false);
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            SetBusy(false, statusLabel.Text, statusLabel.ForeColor == Theme.Accent);
        }
    }

    private void SetBusy(bool busy, string message, bool success = true, bool showProgress = false)
    {
        pathTextBox.Enabled = !busy;
        checkButton.Enabled = !busy && ResolvedGameRoot is not null;
        playButton.Enabled = !busy && ResolvedGameRoot is not null;
        progressBar.Visible = busy && showProgress;
        if (!progressBar.Visible) progressBar.Value = 0;
        SetStatus(message, success);
    }

    private void SetStatus(string message, bool success)
    {
        statusLabel.Text = message;
        statusLabel.ForeColor = success ? Theme.Accent : Theme.Muted;
    }
}
