using System.Globalization;
using WindowsSetupAssistant.App.Converters;
using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.App.ViewModels;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Tests.Localization;

public class CategoryAndStatusLocalizationTests
{
    [Fact]
    public void InstallOutcomeConverter_TranslatesAcrossLanguages()
    {
        var converter = new InstallOutcomeConverter();
        var source = LocalizationSource.Instance;

        try
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
            Assert.Equal("Đã cài xong", converter.Convert(InstallOutcome.Succeeded, typeof(string), null, CultureInfo.CurrentUICulture));
            Assert.Equal("Thất bại", converter.Convert(InstallOutcome.Failed, typeof(string), null, CultureInfo.CurrentUICulture));
            Assert.Equal("Đã huỷ", converter.Convert(InstallOutcome.Cancelled, typeof(string), null, CultureInfo.CurrentUICulture));

            source.SetLanguage(CultureInfo.GetCultureInfo("en"));
            Assert.Equal("Installed", converter.Convert(InstallOutcome.Succeeded, typeof(string), null, CultureInfo.CurrentUICulture));
            Assert.Equal("Failed", converter.Convert(InstallOutcome.Failed, typeof(string), null, CultureInfo.CurrentUICulture));
            Assert.Equal("Cancelled", converter.Convert(InstallOutcome.Cancelled, typeof(string), null, CultureInfo.CurrentUICulture));

            source.SetLanguage(CultureInfo.GetCultureInfo("zh-Hant"));
            Assert.Equal("已安裝", converter.Convert(InstallOutcome.Succeeded, typeof(string), null, CultureInfo.CurrentUICulture));
            Assert.Equal("失敗", converter.Convert(InstallOutcome.Failed, typeof(string), null, CultureInfo.CurrentUICulture));
            Assert.Equal("已取消", converter.Convert(InstallOutcome.Cancelled, typeof(string), null, CultureInfo.CurrentUICulture));
        }
        finally
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        }
    }

    [Fact]
    public void DurationConverter_TranslatesAcrossLanguages()
    {
        var converter = new DurationConverter();
        var source = LocalizationSource.Instance;
        var minutes = TimeSpan.FromMinutes(2.5);
        var seconds = TimeSpan.FromSeconds(45);

        try
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
            Assert.Contains("phút", (string)converter.Convert(minutes, typeof(string), null, CultureInfo.CurrentUICulture));
            Assert.Contains("giây", (string)converter.Convert(seconds, typeof(string), null, CultureInfo.CurrentUICulture));

            source.SetLanguage(CultureInfo.GetCultureInfo("en"));
            Assert.Contains("min", (string)converter.Convert(minutes, typeof(string), null, CultureInfo.CurrentUICulture));
            Assert.Contains("sec", (string)converter.Convert(seconds, typeof(string), null, CultureInfo.CurrentUICulture));

            source.SetLanguage(CultureInfo.GetCultureInfo("zh-Hant"));
            Assert.Contains("分鐘", (string)converter.Convert(minutes, typeof(string), null, CultureInfo.CurrentUICulture));
            Assert.Contains("秒", (string)converter.Convert(seconds, typeof(string), null, CultureInfo.CurrentUICulture));
        }
        finally
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        }
    }

    [Fact]
    public void SoftwarePackageViewModel_RefreshesCategoryAndStatusOnLanguageChange()
    {
        var source = LocalizationSource.Instance;
        var package = new SoftwarePackage
        {
            Name = "Chrome",
            PackageId = "Google.Chrome",
            Category = SoftwareCategory.Browser
        };

        var vm = new SoftwarePackageViewModel(package, source.Localizer);
        vm.InstallState = InstallState.NotInstalled;

        try
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
            vm.RefreshLocalization();
            Assert.Equal("Trình duyệt", vm.CategoryDisplayName);
            Assert.Equal("Chưa cài", vm.StatusText);

            source.SetLanguage(CultureInfo.GetCultureInfo("en"));
            vm.RefreshLocalization();
            Assert.Equal("Browser", vm.CategoryDisplayName);
            Assert.Equal("Not installed", vm.StatusText);

            source.SetLanguage(CultureInfo.GetCultureInfo("zh-Hant"));
            vm.RefreshLocalization();
            Assert.Equal("瀏覽器", vm.CategoryDisplayName);
            Assert.Equal("未安裝", vm.StatusText);
        }
        finally
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        }
    }

    [Fact]
    public Task MainViewModel_LanguageChange_UpdatesCategoryFiltersAndPackages() => WpfTestHost.Run(async () =>
    {
        var source = LocalizationSource.Instance;
        try
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
            await using var fixture = new ViewModelFixture();
            await fixture.InitializeAsync();
            var vm = fixture.ViewModel;

            // Đặt package đầu tiên là Browser để kiểm tra
            vm.Packages[0].Model.Category = SoftwareCategory.Browser;
            vm.Packages[0].RefreshLocalization();

            // Kiểm tra CategoryFilters ban đầu bằng tiếng Việt
            Assert.Equal("Tất cả nhóm", vm.CategoryFilters[0].DisplayName);
            Assert.Equal("Trình duyệt", vm.Packages[0].CategoryDisplayName);

            // Chọn lọc theo Trình duyệt
            var browserFilter = vm.CategoryFilters.First(c => c.Value == SoftwareCategory.Browser);
            vm.CategoryFilter = browserFilter;
            Assert.Equal(SoftwareCategory.Browser, vm.CategoryFilter.Value);

            // Đổi sang tiếng Anh
            var enOption = vm.Languages.First(l => l.Code == "en");
            vm.SelectedLanguage = enOption;

            // CategoryFilters và CategoryFilter phải đổi sang tiếng Anh và giữ nguyên filter Browser
            Assert.Equal("All categories", vm.CategoryFilters[0].DisplayName);
            Assert.Equal(SoftwareCategory.Browser, vm.CategoryFilter.Value);
            Assert.Equal("Browser", vm.CategoryFilter.DisplayName);
            Assert.Equal("Browser", vm.Packages[0].CategoryDisplayName);

            // Đổi sang tiếng Trung phồn thể
            var zhOption = vm.Languages.First(l => l.Code == "zh-Hant");
            vm.SelectedLanguage = zhOption;

            Assert.Equal("所有類別", vm.CategoryFilters[0].DisplayName);
            Assert.Equal(SoftwareCategory.Browser, vm.CategoryFilter.Value);
            Assert.Equal("瀏覽器", vm.CategoryFilter.DisplayName);
            Assert.Equal("瀏覽器", vm.Packages[0].CategoryDisplayName);
        }
        finally
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        }
    });
}
