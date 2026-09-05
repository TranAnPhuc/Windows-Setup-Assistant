using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Validation;

namespace WindowsSetupAssistant.Infrastructure.Persistence;

/// <summary>
/// Làm sạch dữ liệu đọc từ file JSON.
///
/// File có thể do người dùng sửa tay hoặc nhận từ máy khác, nên không được tin tưởng tuyệt đối:
/// thiếu Id, trùng Guid, Package Id sai định dạng, ActiveProfileId trỏ vào profile không tồn tại...
/// Hàm này đưa dữ liệu về trạng thái hợp lệ trước khi ứng dụng sử dụng.
/// </summary>
public static class CatalogNormalizer
{
    /// <param name="catalog">Catalog vừa đọc từ JSON.</param>
    /// <param name="removedPackages">Danh sách gói bị loại vì Package Id không hợp lệ.</param>
    public static SoftwareCatalog Normalize(SoftwareCatalog? catalog, out IReadOnlyList<string> removedPackages)
    {
        var removed = new List<string>();
        catalog ??= new SoftwareCatalog();

        if (catalog.SchemaVersion <= 0)
        {
            catalog.SchemaVersion = 1;
        }

        catalog.Profiles ??= new List<InstallationProfile>();
        // JSON có thể chứa phần tử null dù model khai báo danh sách không nullable.
        // Bỏ qua mục trống, giữ nguyên mọi profile và phần mềm hợp lệ còn lại.
        catalog.Profiles.RemoveAll(profile => profile is null);

        var seenProfileIds = new HashSet<Guid>();

        foreach (var profile in catalog.Profiles)
        {
            if (profile.Id == Guid.Empty || !seenProfileIds.Add(profile.Id))
            {
                profile.Id = Guid.NewGuid();
                seenProfileIds.Add(profile.Id);
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                profile.Name = "Cấu hình không tên";
            }

            profile.Packages ??= new List<SoftwarePackage>();

            var validPackages = new List<SoftwarePackage>(profile.Packages.Count);
            var seenPackageIds = new HashSet<Guid>();
            var seenWingetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in profile.Packages)
            {
                if (package is null)
                {
                    continue;
                }

                if (!PackageIdValidator.IsValid(package.PackageId))
                {
                    removed.Add($"{profile.Name}: {package.Name} ({package.PackageId})");
                    continue;
                }

                package.PackageId = package.PackageId.Trim();

                // Trùng Package Id trong cùng một profile là vô nghĩa - giữ bản đầu tiên.
                if (!seenWingetIds.Add(package.PackageId))
                {
                    continue;
                }

                if (package.Id == Guid.Empty || !seenPackageIds.Add(package.Id))
                {
                    package.Id = Guid.NewGuid();
                    seenPackageIds.Add(package.Id);
                }

                if (string.IsNullOrWhiteSpace(package.Name))
                {
                    package.Name = package.PackageId;
                }

                if (string.IsNullOrWhiteSpace(package.Source))
                {
                    package.Source = "winget";
                }

                validPackages.Add(package);
            }

            // Đánh lại SortOrder liên tục 0,1,2... theo thứ tự hiện có.
            profile.Packages = validPackages
                .OrderBy(p => p.SortOrder)
                .Select((p, index) =>
                {
                    p.SortOrder = index;
                    return p;
                })
                .ToList();
        }

        if (catalog.Profiles.Count == 0)
        {
            catalog.Profiles.Add(new InstallationProfile { Name = "Cấu hình mặc định" });
        }

        if (catalog.ActiveProfileId is null || catalog.Profiles.All(p => p.Id != catalog.ActiveProfileId))
        {
            catalog.ActiveProfileId = catalog.Profiles[0].Id;
        }

        removedPackages = removed;
        return catalog;
    }
}
