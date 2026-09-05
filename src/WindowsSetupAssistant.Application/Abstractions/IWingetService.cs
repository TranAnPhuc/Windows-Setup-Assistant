using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Application.Abstractions;

/// <summary>
/// Cổng giao tiếp duy nhất với WinGet. Toàn bộ ứng dụng chỉ phụ thuộc interface này,
/// nhờ vậy unit test có thể thay bằng fake mà không cài phần mềm thật.
/// </summary>
public interface IWingetService
{
    /// <summary>Kiểm tra winget.exe có sẵn không (gọi lúc khởi động ứng dụng).</summary>
    Task<WingetAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <summary>Tìm phần mềm trong kho winget.</summary>
    Task<IReadOnlyList<WingetPackageInfo>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Lấy toàn bộ phần mềm đang cài trên máy (1 lần gọi, nhanh hơn hỏi từng gói).</summary>
    Task<IReadOnlyList<WingetPackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra đúng một Package Id đã được cài hay chưa.</summary>
    Task<bool> IsInstalledAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>Cài đặt một gói ở chế độ im lặng.</summary>
    Task<InstallationResult> InstallAsync(
        SoftwarePackage package,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Nâng cấp một gói đã cài.</summary>
    Task<InstallationResult> UpgradeAsync(
        SoftwarePackage package,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default);
}
