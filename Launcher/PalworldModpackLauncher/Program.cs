namespace PalworldModpackLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var previewUi = args.Length == 1 &&
            args[0].Equals("--preview-ui", StringComparison.OrdinalIgnoreCase);
        var renderPreview = args.Length == 2 &&
            args[0].Equals("--render-preview", StringComparison.OrdinalIgnoreCase);

        if (renderPreview)
        {
            ApplicationConfiguration.Initialize();
            return RenderPreview(args[1]);
        }

        if (args.Length > 0 && args[0].Equals("--cleanup-update", StringComparison.OrdinalIgnoreCase))
        {
            LauncherSelfUpdater.CleanupAfterReplacement(args);
            args = Array.Empty<string>();
        }

        if (args.Length > 0 && !previewUi)
        {
            return CommandLine.Run(args);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(previewUi));
        return 0;
    }

    private static int RenderPreview(string destination)
    {
        try
        {
            destination = Path.GetFullPath(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var form = new MainForm(previewOnly: true)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
                ShowInTaskbar = false,
            };
            form.Show();
            Application.DoEvents();
            using var image = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(destination, System.Drawing.Imaging.ImageFormat.Png);
            form.Close();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
