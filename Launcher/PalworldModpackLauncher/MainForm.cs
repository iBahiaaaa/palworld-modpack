using System.Drawing;

namespace PalworldModpackLauncher;

internal sealed class MainForm : Form
{
    private readonly GameLocationPanel locationPanel;
    private readonly LauncherStatusPanel launcherStatusPanel;
    private readonly Button checkButton;
    private readonly Button installButton;
    private readonly Button removeButton;
    private readonly Button vanillaButton;
    private readonly Button moddedButton;
    private readonly Button restartButton;
    private readonly ModSelectionPanel modSelectionPanel;
    private readonly UpdateCoordinator coordinator = new();
    private readonly LauncherSelfUpdater selfUpdater = new();
    private readonly GameSessionManager gameSession = new();
    private readonly ModpackUninstaller uninstaller = new();
    private readonly LauncherInstallationManager installationManager = new();
    private readonly ModSelectionService modSelectionService = new();
    private CancellationTokenSource? operationCancellation;
    private bool closingForUpdate;
    private bool sessionCloseNoticeShown;
    private readonly bool previewOnly;

    public MainForm(bool previewOnly = false)
    {
        this.previewOnly = previewOnly;
        Text = "Palncher";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(920, 610);
        MinimumSize = new Size(820, 590);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var content = CreateLayout();
        var header = new BrandHeader();

        locationPanel = new GameLocationPanel();
        locationPanel.GamePathChanged += (_, _) =>
        {
            if (!this.previewOnly) RefreshGameState();
        };
        locationPanel.BrowseRequested += (_, _) => Browse();
        locationPanel.DetectRequested += (_, _) => Detect();

        launcherStatusPanel = new LauncherStatusPanel();

        modSelectionPanel = new ModSelectionPanel();
        modSelectionPanel.SelectionChanged += (_, _) => SaveModSelection();

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Window,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var tools = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Window,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        var playActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Theme.Window,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };

        moddedButton = Theme.Button("Jogar com mods", ButtonKind.Primary);
        moddedButton.Margin = Padding.Empty;
        moddedButton.Enabled = false;
        moddedButton.Click += async (_, _) => await PlayModdedAsync();
        restartButton = Theme.Button("Reiniciar");
        restartButton.Enabled = false;
        restartButton.Click += async (_, _) => await RestartGameAsync();
        vanillaButton = Theme.Button("Jogar vanilla");
        vanillaButton.Enabled = false;
        vanillaButton.Click += (_, _) => PlayVanilla();
        removeButton = Theme.Button("Remover", ButtonKind.Danger);
        removeButton.Enabled = false;
        removeButton.Click += (_, _) => RemoveMods();
        installButton = Theme.Button("Instalar", ButtonKind.Subtle);
        installButton.Click += (_, _) => InstallLauncher();
        checkButton = Theme.Button("Atualizar", ButtonKind.Subtle);
        checkButton.Enabled = false;
        checkButton.Click += async (_, _) => await CheckAndUpdateAsync(automatic: false);

        tools.Controls.Add(checkButton);
        tools.Controls.Add(installButton);
        tools.Controls.Add(removeButton);
        playActions.Controls.Add(moddedButton);
        playActions.Controls.Add(restartButton);
        playActions.Controls.Add(vanillaButton);
        actions.Controls.Add(tools, 0, 0);
        actions.Controls.Add(playActions, 1, 0);
        RefreshInstallButton();

        content.Controls.Add(header, 0, 0);
        content.Controls.Add(locationPanel, 0, 1);
        content.Controls.Add(launcherStatusPanel, 0, 2);
        content.Controls.Add(modSelectionPanel, 0, 3);
        content.Controls.Add(actions, 0, 4);
        Controls.Add(content);

        if (previewOnly)
        {
            LoadPreview();
        }
        else
        {
            Shown += async (_, _) => await InitializeAsync();
            FormClosing += HandleFormClosing;
            FormClosed += (_, _) => Cleanup();
        }
    }

    private void LoadPreview()
    {
        locationPanel.GamePath = @"E:\SteamLibrary\steamapps\common\Palworld";
        launcherStatusPanel.VersionText = "MODPACK  ·  0.8.0  ·  atualizado";
        launcherStatusPanel.SetStatus(
            "Palworld encontrado  ·  Steam: vanilla  ·  Palncher: mods selecionados",
            true);
        var options = new[]
        {
            new ModOption("HoverTransfer", "Distribuidor rápido de itens", "Passe o mouse e pressione H para mover.", true),
            new ModOption("AltTabWorkContinuation", "Fabricação durante Alt+Tab", "Mantém a fabricação quando o jogo perde foco.", true),
            new ModOption("AccessorySlotsResearch", "Slots extras de acessórios", "Expande e salva até 10 espaços.", true),
            new ModOption("ItemStackExtender", "Pilhas de itens até 100.000", "Aumenta o limite dos itens empilháveis.", true),
        };
        modSelectionPanel.SetMods(options, options.Select(option => option.Id).ToArray());
        checkButton.Enabled = true;
        vanillaButton.Enabled = true;
        moddedButton.Enabled = true;
        removeButton.Enabled = true;
        installButton.Text = "Instalado";
        installButton.Enabled = false;
        installButton.Visible = false;
    }

    private static TableLayoutPanel CreateLayout()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 20),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Theme.Window,
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        return content;
    }

    private string? ResolvedGameRoot => PalworldLocator.Resolve(locationPanel.GamePath);

    private async Task InitializeAsync()
    {
        installationManager.RefreshBranding();
        RefreshInstallButton();
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
        locationPanel.GamePath = detected;
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecione a biblioteca da Steam ou a pasta do Palworld",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (Directory.Exists(locationPanel.GamePath)) dialog.InitialDirectory = locationPanel.GamePath;
        if (dialog.ShowDialog(this) == DialogResult.OK) locationPanel.GamePath = dialog.SelectedPath;
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
            modSelectionPanel.SetMods(Array.Empty<ModOption>(), Array.Empty<string>());
            modSelectionPanel.SetInteractionEnabled(false);
            launcherStatusPanel.VersionText = "MODPACK  ·  não instalado";
            SetStatus("A pasta selecionada não contém uma instalação válida do Palworld.", false);
            return;
        }

        var state = coordinator.ReadState(root!);
        var settings = LauncherSettingsStore.Load();
        var options = modSelectionService.Discover(root!, state);
        var selected = modSelectionService.ResolveSelection(root!, options, settings);
        modSelectionPanel.SetMods(options, selected);
        modSelectionPanel.SetInteractionEnabled(true);
        LauncherSettingsStore.Save(root!, selected);
        launcherStatusPanel.VersionText = "MODPACK  ·  " + (state?.Version ?? "não instalado");
        SetStatus($"Palworld encontrado em:\n{root}\nSteam: vanilla  ·  Palncher: mods selecionados", true);
        moddedButton.Enabled = selected.Count > 0;
    }

    private async Task PlayModdedAsync()
    {
        var root = ResolvedGameRoot;
        if (root is null) return;
        if (!await CheckAndUpdateAsync(automatic: true)) return;
        var selectedMods = modSelectionPanel.SelectedModIds;
        if (selectedMods.Count == 0)
        {
            MessageBox.Show(
                this,
                "Selecione pelo menos um mod antes de iniciar.",
                "Nenhum mod selecionado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        operationCancellation = new CancellationTokenSource();
        SetBusy(true, $"Ativando {selectedMods.Count} mod(s) e iniciando o Palworld...");
        try
        {
            var result = await gameSession.LaunchModdedAsync(
                root,
                selectedMods,
                coordinator.ReadState(root),
                () => BeginInvoke(new Action(() =>
                {
                    restartButton.Enabled = true;
                    WindowState = FormWindowState.Minimized;
                })),
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
            SetBusy(false, launcherStatusPanel.StatusText, launcherStatusPanel.StatusSuccess);
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

    private async Task RestartGameAsync()
    {
        if (!gameSession.IsSessionActive || !GameSessionManager.IsPalworldRunning())
        {
            SetStatus(
                "O reinício fica disponível durante uma sessão iniciada com mods.",
                false);
            restartButton.Enabled = false;
            return;
        }
        if (MessageBox.Show(
                this,
                "Reiniciar o Palworld agora?\n\n" +
                "Salve o mundo antes de continuar. Os mesmos mods permanecerão ativos.",
                "Reiniciar jogo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        restartButton.Enabled = false;
        SetStatus("Encerrando e reiniciando o Palworld...", true);
        var result = await gameSession.RestartAsync(
            operationCancellation?.Token ?? CancellationToken.None);
        SetStatus(result.Message, result.Success);
        restartButton.Enabled =
            result.Success &&
            gameSession.IsSessionActive &&
            GameSessionManager.IsPalworldRunning();
        if (!result.Success)
            MessageBox.Show(
                this,
                result.Message,
                "Não foi possível reiniciar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
        if (result.Success) launcherStatusPanel.VersionText = "MODPACK  ·  não instalado";
        SetBusy(false, result.Message, result.Success);
        MessageBox.Show(this,
            result.Message,
            result.Success ? "Mods removidos" : "Falha na remoção",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private void InstallLauncher()
    {
        if (!installationManager.IsInstalled &&
            MessageBox.Show(this,
                "Instalar o Palncher neste computador?\n\n" +
                "Serão criados atalhos na Área de Trabalho e no Menu Iniciar. Não é necessário acesso de administrador.",
                "Instalar Palncher",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SetBusy(true, "Instalando o Palncher e criando os atalhos...");
        var result = installationManager.Install();
        SetBusy(false, result.Message, result.Success);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Message, "Falha na instalação",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        RefreshInstallButton();
        if (MessageBox.Show(this,
                result.Message + "\n\nAbrir agora a versão instalada?",
                "Palncher instalado",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information) != DialogResult.Yes)
            return;

        var opened = installationManager.OpenInstalled();
        if (!opened.Success)
        {
            MessageBox.Show(this, opened.Message, "Falha ao abrir",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        closingForUpdate = true;
        Close();
    }

    private void RefreshInstallButton()
    {
        if (installationManager.IsRunningInstalled)
        {
            installButton.Text = "Instalado";
            installButton.Enabled = false;
            installButton.Visible = false;
            return;
        }

        installButton.Visible = true;
        installButton.Text = installationManager.IsInstalled
            ? "Atualizar app"
            : "Instalar";
        installButton.Enabled = true;
    }

    private async Task<bool> CheckLauncherUpdateAsync()
    {
        if (operationCancellation is not null) return false;
        operationCancellation = new CancellationTokenSource();
        SetBusy(true, "Verificando atualização do Palncher...");
        try
        {
            var update = await selfUpdater.CheckAsync(operationCancellation.Token);
            if (update is null) return false;

            SetBusy(true, $"Baixando o Palncher {update.Version.ToString(3)}...", showProgress: true);
            var progress = new Progress<int>(value => launcherStatusPanel.ProgressValue = value);
            var result = await selfUpdater.DownloadAndRestartAsync(update, progress, operationCancellation.Token);
            SetStatus(result.Message, result.Success);
            if (!result.Success) return false;

            closingForUpdate = true;
            BeginInvoke(new Action(Close));
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            SetStatus("Não foi possível verificar o Palncher agora. Continuando com a versão atual.", false);
            return false;
        }
        catch (Exception exception)
        {
            SetStatus("Não foi possível atualizar o Palncher agora: " + exception.Message, false);
            return false;
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            if (!closingForUpdate)
                SetBusy(false, launcherStatusPanel.StatusText, launcherStatusPanel.StatusSuccess);
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
                launcherStatusPanel.VersionText = $"MODPACK  ·  {latest.Version.ToString(3)}  ·  atualizado";
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
            var progress = new Progress<int>(value => launcherStatusPanel.ProgressValue = value);
            var result = await coordinator.DownloadAndApplyAsync(
                root,
                latest,
                progress,
                operationCancellation.Token);
            launcherStatusPanel.VersionText = result.Success
                ? $"MODPACK  ·  {latest.Version.ToString(3)}  ·  atualizado"
                : "MODPACK  ·  " + (coordinator.ReadState(root)?.Version ?? "não instalado");
            SetStatus(result.Message, result.Success);
            if (result.Success) RefreshGameState();
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
            SetBusy(false, launcherStatusPanel.StatusText, launcherStatusPanel.StatusSuccess);
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
            "O Palncher continuará minimizado até o Palworld fechar para desativar os mods com segurança.",
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
        locationPanel.SetInteractionEnabled(!busy);
        checkButton.Enabled = !busy && ResolvedGameRoot is not null;
        vanillaButton.Enabled = !busy && ResolvedGameRoot is not null;
        moddedButton.Enabled =
            !busy &&
            ResolvedGameRoot is not null &&
            modSelectionPanel.SelectedModIds.Count > 0;
        removeButton.Enabled = !busy && ResolvedGameRoot is { } root && uninstaller.HasInstalledContent(root);
        restartButton.Enabled =
            ResolvedGameRoot is not null &&
            gameSession.IsSessionActive &&
            !gameSession.IsRestarting &&
            GameSessionManager.IsPalworldRunning();
        installButton.Enabled = !busy && !installationManager.IsRunningInstalled;
        modSelectionPanel.SetInteractionEnabled(!busy && ResolvedGameRoot is not null);
        launcherStatusPanel.SetProgressVisible(busy && showProgress);
        SetStatus(message, success);
    }

    private void SetStatus(string message, bool success)
    {
        launcherStatusPanel.SetStatus(message, success);
    }

    private void SaveModSelection()
    {
        var root = ResolvedGameRoot;
        if (root is null) return;
        var selected = modSelectionPanel.SelectedModIds;
        LauncherSettingsStore.Save(root, selected);
        moddedButton.Enabled = operationCancellation is null && selected.Count > 0;
        SetStatus(
            selected.Count == 0
                ? "Selecione pelo menos um mod para jogar pelo Palncher."
                : $"{selected.Count} mod(s) selecionado(s) para a próxima sessão.",
            selected.Count > 0);
    }
}
