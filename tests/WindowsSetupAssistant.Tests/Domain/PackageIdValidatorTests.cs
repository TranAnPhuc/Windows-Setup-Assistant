using WindowsSetupAssistant.Domain.Localization;
using WindowsSetupAssistant.Domain.Validation;

namespace WindowsSetupAssistant.Tests.Domain;

public class PackageIdValidatorTests
{
    [Theory]
    [InlineData("Google.Chrome")]
    [InlineData("Mozilla.Firefox")]
    [InlineData("Microsoft.VisualStudioCode")]
    [InlineData("Git.Git")]
    [InlineData("OpenJS.NodeJS")]
    [InlineData("7zip.7zip")]
    [InlineData("VideoLAN.VLC")]
    [InlineData("Notepad++.Notepad++")]
    [InlineData("mcmilk.7zip-zstd")]
    [InlineData("Microsoft.VCRedist.2015+.x64")]
    [InlineData("9WZDNCRFHVJL")]
    public void IsValid_RealWingetIds_ReturnsTrue(string packageId)
    {
        Assert.True(PackageIdValidator.IsValid(packageId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Google Chrome")]                       // có khoảng trắng
    [InlineData("--force")]                             // bị hiểu là tham số dòng lệnh
    [InlineData("-Google.Chrome")]
    [InlineData(".Google.Chrome")]                      // không bắt đầu bằng chữ/số
    [InlineData("Google.Chrome & calc.exe")]            // thử chèn lệnh
    [InlineData("Google.Chrome && shutdown /s")]
    [InlineData("Google.Chrome | more")]
    [InlineData("Google.Chrome; rm -rf /")]
    [InlineData("Google.Chrome\nwinget uninstall")]     // xuống dòng
    [InlineData("$(Get-Process)")]
    [InlineData("`whoami`")]
    [InlineData("../../windows/system32")]
    [InlineData("\"Google.Chrome\"")]
    public void IsValid_DangerousOrMalformedInput_ReturnsFalse(string? packageId)
    {
        Assert.False(PackageIdValidator.IsValid(packageId));
    }

    [Fact]
    public void TryValidate_TooLong_ReturnsFalseWithMessage()
    {
        var tooLong = new string('a', PackageIdValidator.MaxLength + 1);

        var isValid = PackageIdValidator.TryValidate(tooLong, out var error);

        Assert.False(isValid);
        Assert.Equal(MessageKeys.PackageIdTooLong, error.Key);
        Assert.Contains(PackageIdValidator.MaxLength, error.Arguments);
    }

    [Fact]
    public void EnsureValid_TrimsAndReturnsId()
    {
        Assert.Equal("Google.Chrome", PackageIdValidator.EnsureValid("  Google.Chrome  "));
    }

    [Fact]
    public void EnsureValid_InvalidId_ThrowsArgumentException()
    {
        Assert.Throws<LocalizedException>(() => PackageIdValidator.EnsureValid("--source"));
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("visual studio code")]
    [InlineData("7-zip")]
    public void SearchQuery_NormalText_IsValid(string query)
    {
        Assert.True(SearchQueryValidator.TryValidate(query, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("--source winget")]
    [InlineData("-q")]
    [InlineData("chrome\nwinget install")]
    public void SearchQuery_DangerousText_IsRejected(string? query)
    {
        Assert.False(SearchQueryValidator.TryValidate(query, out _));
    }
}
