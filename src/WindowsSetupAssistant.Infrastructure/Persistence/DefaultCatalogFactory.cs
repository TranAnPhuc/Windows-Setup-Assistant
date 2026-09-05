using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Infrastructure.Persistence;

/// <summary>
/// Tạo danh sách mẫu cho lần chạy đầu tiên.
///
/// Toàn bộ Package Id dưới đây đã được kiểm chứng bằng lệnh
/// "winget search &lt;tên&gt; --source winget" chứ không phải đoán:
///   Google.Chrome, Mozilla.Firefox, Microsoft.VisualStudioCode, Git.Git,
///   OpenJS.NodeJS, 7zip.7zip, VideoLAN.VLC, Notepad++.Notepad++
/// </summary>
public static class DefaultCatalogFactory
{
    public static SoftwareCatalog Create()
    {
        var personal = new InstallationProfile
        {
            Name = "Máy cá nhân",
            Description = "Bộ phần mềm cơ bản cho máy dùng hằng ngày.",
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
            Name = "Máy lập trình",
            Description = "Công cụ cho lập trình viên.",
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
            Name = "Máy công ty",
            Description = "Bộ tối thiểu cho máy văn phòng.",
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
