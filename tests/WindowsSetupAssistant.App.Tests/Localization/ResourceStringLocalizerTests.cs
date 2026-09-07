using System.Globalization;
using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.App.Tests.Localization;

public class ResourceStringLocalizerTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi");
    private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-Hant");

    private readonly ResourceStringLocalizer _localizer = new();

    [Fact]
    public void MissingKey_ReturnsTheKeyItself()
    {
        Assert.Equal("Khong_Ton_Tai", _localizer["Khong_Ton_Tai"]);
    }

    [Fact]
    public void RawText_IsReturnedUnchanged()
    {
        var path = @"C:\Users\test\Data\software-list.json";

        Assert.Equal(path, _localizer.Format(LocalizedText.Raw(path)));
    }

    [Fact]
    public void Format_FillsArguments()
    {
        var text = LocalizedText.Of(MessageKeys.PackageIdTooLong, 200);

        var result = _localizer.Format(text, English);

        Assert.Contains("200", result);
        Assert.DoesNotContain("{0}", result);
    }

    [Fact]
    public void Format_WrongArgumentCount_DoesNotThrow()
    {
        // Bản dịch chờ 1 tham số nhưng không truyền tham số nào.
        var text = LocalizedText.Of(MessageKeys.PackageIdTooLong);

        var result = _localizer.Format(text, English);

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("vi")]
    [InlineData("zh-Hant")]
    public void EveryLanguageResolvesAKnownKey(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        var result = _localizer.Format(LocalizedText.Of(MessageKeys.InstallSucceeded), culture);

        Assert.NotEqual(MessageKeys.InstallSucceeded, result);
    }

    [Fact]
    public void ThreeLanguagesGiveThreeDifferentStrings()
    {
        var text = LocalizedText.Of(MessageKeys.InstallSucceeded);

        var english = _localizer.Format(text, English);
        var vietnamese = _localizer.Format(text, Vietnamese);
        var chinese = _localizer.Format(text, Chinese);

        Assert.NotEqual(english, vietnamese);
        Assert.NotEqual(english, chinese);
        Assert.NotEqual(vietnamese, chinese);
    }
}
