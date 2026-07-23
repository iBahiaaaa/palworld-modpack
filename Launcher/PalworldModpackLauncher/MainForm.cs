using System.Drawing;

namespace PalworldModpackLauncher;

internal sealed class MainForm : Form
{
    private readonly TextBox pathTextBox;
    private readonly Label versionLabel;
    private readonly Label statusLabel;
    private readonly ProgressBar progressBar;
    private readonly Button checkButton;
    private readonly Button removeButton;
    private readonly Button vanillaButton;
    private readonly Button moddedButton;
    private readonly UpdateCoordinator coordinator = new();
    private readonly LauncherSelfUpdater selfUpdater = new();
    private readonly GameSessionManager gameSession = new();
    private readonly ModpackUninstaller uninstaller = new();
    private CancellationTokenSource? operationCancellation;
    private bool closingForUpdate;
    private bool sessionCloseNoticeShown;

    public MainForm()
    {
        Text = "Palworld Modpack - Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(800, 490);
        MinimumSize = new Size(720, 470);
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
            Text = "Pela Steam o jogo fica vanilla. Por aqui, ele inicia com o modpack.",
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
        moddedButton = Theme.Button("Jogar com mods", primary: true);
        moddedButton.Enabled = false;
        moddedButton.Click += async (_, _) => await PlayModdedAsync();
        vanillaButton = Theme.Button("Jogar vanilla");
        vanillaButton.Enabled = false;
        vanillaButton.Click += (_, _) => PlayVanilla();
        removeButton = Theme.Button("Remover mods");
        removeButton.Enabled = false;
        removeButton.Click += (_, _) => RemoveMods();
        checkButton = Theme.Button("Verificar atualização");
        checkButton.Enabled = false;
        checkButton.Click += async (_, _) => await CheckAndUpdateAsync(automatic: false);
        actions.Controls.Add(moddedButton);
        actions.Controls.Add(vanillaButton);
        actions.Controls.Add(removeButton);
        actions.Controls.Add(checkButton);

        content.Controls.Add(title, 0, 0);
        content.Controls.Add(subtitle, 0, 1);
        content.Controls.Add(pathLabel, 0, 2);
        content.Controls.Add(pathRow, 0, 3);
        content.Controls.Add(infoPanel, 0, 4);
        content.Controls.Add(actions, 0, 5);
        Controls.Add(content);

        Shown += async (_, _) => await InitializeAsync();
        FormClosing += HandleFormClosing;
        FormClosed += (_, _) => Cleanup();
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

    private async Task InitializeAsync()
    {
        Detect();
        var root = ResolvedGameRoot;
        if (root is null) return;

        var vanilla = gameSession.EnsureVanilla(root, coordinator.ReadState(root));
        if (!vanilla.Success) SetStatus(vanilla.Message, false);

        if (await CheckLauncherUpdateAsync()) return;
        await CheckAndUpdateAsync(automatic: true);
    }

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
        vanillaButton.Enabled = valid;
        moddedButton.Enabled = valid;
        removeButton.Enabled = valid && uninstaller.HasInstalledContent(root!);
        if (!valid)
        {
            versionLabel.Text = "Versão instalada: —";
            SetStatus("A pasta selecionada não contém uma instalação válida do Palworld.", false);
            return;
        }

        LauncherSettingsStore.Save(root!);
        var state = coordinator.ReadState(root!);
        versionLabel.Text = "Versão instalada: " + (state?.Version ?? "não instalada");
        SetStatus($"Palworld encontrado em:\n{root}\nSteam: vanilla | Launcher: com mods", true);
    }

    private async Task PlayModdedAsync()
    {
        var root = ResolvedGameRoot;
        if (root is null) return;
        if (!await CheckAndUpdateAsync(automatic: true)) return;

        operationCancellation = new CancellationTokenSource();
        SetBusy(true, "Ativando os mods e iniciando o Palworld...");
        try
        {
            var result = await gameSession.LaunchModdedAsync(
                root,
                () => BeginInvoke(new Action(() => WindowState = FormWindowState.Minimized)),
                operationCancellation.Token);
            SetStatus(result.Message, result.Success);
            if (!result.Success)
                MessageBox.Show(this, result.Message, "Não foi possível jogar com mods",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            SetBusy(false, statusLabel.Text, statusLabel.ForeColor == Theme.Accent);
        }
    }

    private void PlayVanilla()
    {
        var root = ResolvedGameRoot;
        if (root is null) return;
        var result = gameSession.LaunchVanilla(root, coordinator.ReadState(root));
        SetStatus(result.Message, result.Success);
        if (!result.Success)
            MessageBox.Show(this, result.Message, "Não foi possível jogar vanilla",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void RemoveMods()
    {
        var root = ResolvedGameRoot;
        if (root is null) return;
        if (GameSessionManager.IsPalworldRunning(root))
        {
            MessageBox.Show(this, "Feche o Palworld antes de remover os mods.",
                "Palworld em execução", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show(this,
                "Remover todos os arquivos gerenciados pelo modpack deste cliente?\n\n" +
                "Um backup será criado automaticamente. Você poderá reinstalar depois usando Jogar com mods.",
                "Remover mods",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        SetBusy(true, "Criando backup e removendo os mods...");
        var result = uninstaller.Uninstall(root);
        if (result.Success) versionLabel.Text = "Versão instalada: não instalada";
        SetBusy(false, result.Message, result.Success);
        MessageBox.Show(this,
            result.Message,
            result.Success ? "Mods removidos" : "Falha na remoção",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private async Task<bool> CheckLauncherUpdateAsync()
    {
        if (operationCancellation is not null) return false;
        operationCancellation = new CancellationTokenSource();
        SetBusy(true, "Verificando atualização do launcher...");
        try
        {
            var update = await selfUpdater.CheckAsync(operationCancellation.Token);
            if (update is null) return false;
            if (GameSessionManager.IsPalworldRunning())
            {
                SetStatus($"Launcher {update.Version.ToString(3)} disponível. Feche o Palworld para atualizar.", false);
                return false;
            }

            SetBusy(true, $"Baixando o launcher {update.Version.ToString(3)}...", showProgress: true);
            var progress = new Progress<int>(value => progressBar.Value = value);
            var result = await selfUpdater.DownloadAndRestartAsync(update, progress, operationCancellation.Token);
            SetStatus(result.Message, result.Success);
            if (!result.Success) return false;

            closingForUpdate = true;
            BeginInvoke(new Action(Close));
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            SetStatus("Não foi possível verificar o launcher agora. Continuando com a versão atual.", false);
            return false;
        }
        catch (Exception exception)
        {
            SetStatus("Não foi possível atualizar o launcher agora: " + exception.Message, false);
            return false;
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            if (!closingForUpdate)
                SetBusy(false, statusLabel.Text, statusLabel.ForeColor == Theme.Accent);
        }
    }

    private async Task<bool> CheckAndUpdateAsync(bool automatic)
    {
        var root = ResolvedGameRoot;
        if (root is null) return false;
        if (operationCancellation is not null) return false;

        operationCancellation = new CancellationTokenSource();
        SetBusy(true, "Consultando a última versão do modpack no GitHub...");
        try
        {
            var (latest, needsUpdate) = await coordinator.CheckAsync(root, operationCancellation.Token);
            if (!needsUpdate)
            {
                versionLabel.Text = $"Versão instalada: {latest.Version.ToString(3)} — atualizada";
                SetStatus("Seu modpack já está atualizado. Steam permanece vanilla.", true);
                return true;
            }

            if (GameSessionManager.IsPalworldRunning())
            {
                SetStatus($"Atualização {latest.Version.ToString(3)} pendente. Feche o Palworld para instalar.", false);
                return false;
            }

            if (!automatic && MessageBox.Show(this,
                    $"A versão {latest.Version.ToString(3)} está disponível. Baixar e instalar agora?",
                    "Atualização disponível",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                SetStatus("Atualização adiada.", false);
                return true;
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
            return result.Success;
        }
        catch (HttpRequestException)
        {
            var installed = coordinator.ReadState(root) is not null;
            SetStatus("Sem acesso ao GitHub. Você ainda pode jogar com a versão instalada.", false);
            return installed;
        }
        catch (TaskCanceledException)
        {
            SetStatus("A verificação de atualização foi cancelada.", false);
            return false;
        }
        catch (Exception exception)
        {
            SetStatus("Não foi possível verificar atualizações: " + exception.Message, false);
            return coordinator.ReadState(root) is not null;
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            SetBusy(false, statusLabel.Text, statusLabel.ForeColor == Theme.Accent);
        }
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (closingForUpdate || !gameSession.IsSessionActive) return;
        if (eventArgs.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing) return;

        eventArgs.Cancel = true;
        WindowState = FormWindowState.Minimized;
        if (sessionCloseNoticeShown) return;
        sessionCloseNoticeShown = true;
        MessageBox.Show(this,
            "O launcher continuará minimizado até o Palworld fechar para desativar os mods com segurança.",
            "Sessão com mods ativa",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void Cleanup()
    {
        operationCancellation?.Cancel();
        var root = ResolvedGameRoot;
        if (root is not null && !GameSessionManager.IsPalworldRunning())
            gameSession.EnsureVanilla(root, coordinator.ReadState(root));
        selfUpdater.Dispose();
        coordinator.Dispose();
    }

    private void SetBusy(bool busy, string message, bool success = true, bool showProgress = false)
    {
        pathTextBox.Enabled = !busy;
        checkButton.Enabled = !busy && ResolvedGameRoot is not null;
        vanillaButton.Enabled = !busy && ResolvedGameRoot is not null;
        moddedButton.Enabled = !busy && ResolvedGameRoot is not null;
        removeButton.Enabled = !busy && ResolvedGameRoot is { } root && uninstaller.HasInstalledContent(root);
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
