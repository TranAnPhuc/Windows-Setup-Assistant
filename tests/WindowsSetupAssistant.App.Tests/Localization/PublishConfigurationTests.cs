using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace WindowsSetupAssistant.App.Tests.Localization;

public class PublishConfigurationTests
{
    private static string GetFilePath(string relativePath, [CallerFilePath] string thisFile = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
        return Path.Combine(repositoryRoot, relativePath);
    }

    private static string ProjectFile() =>
        GetFilePath(Path.Combine("src", "WindowsSetupAssistant.App", "WindowsSetupAssistant.App.csproj"));

    private static string ControlsFile() =>
        GetFilePath(Path.Combine("src", "WindowsSetupAssistant.App", "Themes", "Controls.xaml"));

    [Fact]
    public void SatelliteResourceLanguagesKeepsAllThreeLanguages()
    {
        // Bay im lang: de nguyen "en" thi ban publish mat sach ban dich ma khong bao loi.
        var value = XDocument.Load(ProjectFile())
            .Descendants("SatelliteResourceLanguages")
            .Select(e => e.Value)
            .SingleOrDefault();

        Assert.NotNull(value);
        foreach (var code in new[] { "en", "vi", "zh-Hant" })
        {
            Assert.Contains(code, value!.Split(';', StringSplitOptions.TrimEntries));
        }
    }

    [Fact]
    public void AppFontFamilyHasChineseFallback()
    {
        var text = File.ReadAllText(ControlsFile());

        // Segoe UI khong co glyph chu Han - phai co font du phong.
        Assert.Contains("JhengHei", text, StringComparison.OrdinalIgnoreCase);
    }
}
