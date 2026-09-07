using System.Globalization;
using WindowsSetupAssistant.App.Localization;

namespace WindowsSetupAssistant.App.Tests.Localization;

public class LanguageCatalogTests
{
    [Fact]
    public void Supported_HasExactlyThreeLanguages()
    {
        Assert.Equal(new[] { "en", "vi", "zh-Hant" }, LanguageCatalog.Supported.Select(l => l.Code).OrderBy(c => c));
    }

    [Fact]
    public void Supported_DisplayNamesAreInTheirOwnLanguage()
    {
        // Ten ngon ngu luon hien bang chinh ngon ngu do, khong dich - de nguoi dung nhan ra.
        Assert.Contains(LanguageCatalog.Supported, l => l.Code == "vi" && l.DisplayName == "Tiếng Việt");
        Assert.Contains(LanguageCatalog.Supported, l => l.Code == "en" && l.DisplayName == "English");
        Assert.Contains(LanguageCatalog.Supported, l => l.Code == "zh-Hant" && l.DisplayName == "繁體中文");
    }

    [Theory]
    [InlineData("vi-VN", "vi")]
    [InlineData("vi", "vi")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-Hant-TW", "zh-Hant")]
    [InlineData("zh-CN", "zh-Hant")]
    [InlineData("en-US", "en")]
    [InlineData("fr-FR", "en")]
    [InlineData("ja-JP", "en")]
    public void Detect_MapsWindowsLanguageToSupportedOne(string installed, string expected)
    {
        Assert.Equal(expected, LanguageCatalog.Detect(CultureInfo.GetCultureInfo(installed)));
    }

    [Fact]
    public void Resolve_SavedValueWins()
    {
        var culture = LanguageCatalog.Resolve("zh-Hant", CultureInfo.GetCultureInfo("vi-VN"));

        Assert.Equal("zh-Hant", culture.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("khong-hop-le")]
    [InlineData("de-DE")]
    public void Resolve_InvalidOrMissingSavedValue_FallsBackToDetection(string? saved)
    {
        var culture = LanguageCatalog.Resolve(saved, CultureInfo.GetCultureInfo("vi-VN"));

        Assert.Equal("vi", culture.Name);
    }
}
