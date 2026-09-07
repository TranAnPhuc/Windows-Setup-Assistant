using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.Infrastructure.Persistence;

/// <summary>
/// Tạo danh sách mẫu cho lần chạy đầu tiên.
///
/// Toàn bộ Package Id dưới đây đã được kiểm chứng bằng lệnh
/// "winget search <tên> --source winget" chứ không phải đoán:
///   Google.Chrome, Mozilla.Firefox, Microsoft.VisualStudioCode, Git.Git,
///   OpenJS.NodeJS, 7zip.7zip, VideoLAN.VLC, Notepad++.Notepad++
/// </summary>
public static class DefaultCatalogFactory
{
    public static SoftwareCatalog Create() => Create(new SeedFallbackLocalizer());

    public static SoftwareCatalog Create(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var personal = new InstallationProfile
        {
            Name = localizer[MessageKeys.SeedProfilePersonal],
            Description = localizer[MessageKeys.SeedProfilePersonalDescription],
            Packages = Order(new List<SoftwarePackage>
            {
                Package("Google Chrome", "Google.Chrome", SoftwareCategory.Browser),
                Package("Mozilla Firefox", "Mozilla.Firefox", SoftwareCategory.Browser),
                Package("7-Zip", "7zip.7zip", SoftwareCategory.Utility),
                Package("VLC media player", "VideoLAN.VLC", SoftwareCategory.Entertainment),
                Package("Notepad++", "Notepad++.Notepad++", SoftwareCategory.Utility)
            })
        };

        var developer = new InstallationProfile
        {
            Name = localizer[MessageKeys.SeedProfileDeveloper],
            Description = localizer[MessageKeys.SeedProfileDeveloperDescription],
            Packages = Order(new List<SoftwarePackage>
            {
                Package("Google Chrome", "Google.Chrome", SoftwareCategory.Browser),
                Package("Visual Studio Code", "Microsoft.VisualStudioCode", SoftwareCategory.Development),
                Package("Git", "Git.Git", SoftwareCategory.Development),
                Package("Node.js", "OpenJS.NodeJS", SoftwareCategory.Development),
                Package("7-Zip", "7zip.7zip", SoftwareCategory.Utility),
                Package("Notepad++", "Notepad++.Notepad++", SoftwareCategory.Utility)
            })
        };

        var company = new InstallationProfile
        {
            Name = localizer[MessageKeys.SeedProfileCompany],
            Description = localizer[MessageKeys.SeedProfileCompanyDescription],
            Packages = Order(new List<SoftwarePackage>
            {
                Package("Google Chrome", "Google.Chrome", SoftwareCategory.Browser),
                Package("7-Zip", "7zip.7zip", SoftwareCategory.Utility),
                Package("Notepad++", "Notepad++.Notepad++", SoftwareCategory.Utility),
                Package("VLC media player", "VideoLAN.VLC", SoftwareCategory.Entertainment)
            })
        };

        return new SoftwareCatalog
        {
            SchemaVersion = 1,
            ActiveProfileId = personal.Id,
            Profiles = new List<InstallationProfile> { personal, developer, company }
        };
    }

    private sealed class SeedFallbackLocalizer : IStringLocalizer
    {
        public string this[string key] => key switch
        {
            MessageKeys.SeedProfilePersonal => "Máy cá nhân",
            MessageKeys.SeedProfilePersonalDescription => "Bộ phần mềm cơ bản cho máy dùng hằng ngày.",
            MessageKeys.SeedProfileDeveloper => "Máy lập trình",
            MessageKeys.SeedProfileDeveloperDescription => "Công cụ cho lập trình viên.",
            MessageKeys.SeedProfileCompany => "Máy công ty",
            MessageKeys.SeedProfileCompanyDescription => "Bộ tối thiểu cho máy văn phòng.",
            _ => key
        };

        public string Format(LocalizedText text) => this[text.Key];
        public string Format(LocalizedText text, System.Globalization.CultureInfo culture) => this[text.Key];
    }

    private static SoftwarePackage Package(string name, string packageId, SoftwareCategory category) => new()
    {
        Name = name,
        PackageId = packageId,
        Category = category,
        Source = "winget",
        IsSelected = true
    };

    private static List<SoftwarePackage> Order(List<SoftwarePackage> packages)
    {
        for (var i = 0; i < packages.Count; i++)
        {
            packages[i].SortOrder = i;
        }

        return packages;
    }
}
