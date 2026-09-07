using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Domain.Classification;

public static class SoftwareCategoryGuesser
{
    public static SoftwareCategory Guess(WingetPackageInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        var text = $"{info.Name} {info.PackageId}".ToLowerInvariant();
        if (text.Contains("chrome") || text.Contains("firefox") || text.Contains("edge") || text.Contains("brave") || text.Contains("opera") || text.Contains("browser")) return SoftwareCategory.Browser;
        if (text.Contains("visualstudio") || text.Contains("git") || text.Contains("node") || text.Contains("python") || text.Contains("docker") || text.Contains("sdk") || text.Contains("java") || text.Contains("code")) return SoftwareCategory.Development;
        if (text.Contains("office") || text.Contains("word") || text.Contains("excel") || text.Contains("adobe.acrobat") || text.Contains("libreoffice") || text.Contains("pdf")) return SoftwareCategory.Office;
        if (text.Contains("vlc") || text.Contains("spotify") || text.Contains("steam") || text.Contains("player") || text.Contains("music")) return SoftwareCategory.Entertainment;
        if (text.Contains("zip") || text.Contains("notepad") || text.Contains("everything") || text.Contains("rar") || text.Contains("driver") || text.Contains("tool")) return SoftwareCategory.Utility;
        return SoftwareCategory.Other;
    }
}
