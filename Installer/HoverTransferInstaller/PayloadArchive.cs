using System.IO.Compression;
using System.Reflection;

namespace HoverTransferInstaller;

internal static class PayloadArchive
{
    private const string ResourceName = "HoverTransferInstaller.Payload.zip";
    private static readonly string[] RequiredEntries =
    {
        "dwmapi.dll",
        "ue4ss/UE4SS.dll",
        "ue4ss/UE4SS-settings.ini",
        "ue4ss/MemberVariableLayout.ini",
        "ue4ss/LICENSE",
        "ue4ss/Mods/HoverTransfer/enabled.txt",
        "ue4ss/Mods/HoverTransfer/Scripts/main.lua",
        "ue4ss/Mods/HoverTransfer/Scripts/config.lua",
        "ue4ss/Mods/HoverTransfer/Scripts/hover_transfer.lua",
        "ue4ss/Mods/HoverTransfer/Scripts/HoverTransferKeys.dll",
    };

    public static ZipArchive Open()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Pacote interno do instalador não foi encontrado.");
        return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
    }

    public static void Validate(ZipArchive archive)
    {
        var names = archive.Entries.Select(entry => Normalize(entry.FullName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in RequiredEntries)
        {
            if (!names.Contains(required))
                throw new InvalidDataException($"Arquivo obrigatório ausente no instalador: {required}");
        }

        if (names.Any(name => name.Contains("../", StringComparison.Ordinal) || Path.IsPathRooted(name)))
            throw new InvalidDataException("O pacote interno contém um caminho inseguro.");
    }

    public static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
