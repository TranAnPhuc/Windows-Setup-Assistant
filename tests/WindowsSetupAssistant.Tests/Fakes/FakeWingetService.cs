using WindowsSetupAssistant.Domain.Localization;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Tests.Fakes;

/// <summary>
/// Bản giả của <see cref="IWingetService"/>.
/// Yêu cầu bắt buộc của dự án: KHÔNG cài phần mềm thật trong automated test.
/// </summary>
public sealed class FakeWingetService : IWingetService
{
    public HashSet<string> InstalledPackageIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<WingetPackageInfo> InstalledRows { get; } = new();
    public Exception? ThrowOnGetInstalled { get; set; }

    /// <summary>Thứ tự các gói đã được gọi Install (dùng để kiểm tra thứ tự hàng đợi).</summary>
    public List<string> InstallCalls { get; } = new();

    public List<string> UpgradeCalls { get; } = new();

    public List<string> IsInstalledCalls { get; } = new();

    /// <summary>Kết quả mong muốn cho từng Package Id (mặc định là Succeeded).</summary>
    public Dictionary<string, InstallOutcome> Outcomes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ném lỗi khi cài gói có Id tương ứng (mô phỏng mất mạng, winget crash...).</summary>
    public Dictionary<string, Exception> ThrowOnInstall { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Chạy trước khi trả kết quả - test dùng để huỷ hàng đợi giữa chừng.</summary>
    public Action<SoftwarePackage>? OnInstalling { get; set; }

    public Exception? ThrowOnIsInstalled { get; set; }

    /// <summary>Cho test mô phỏng máy không có winget.</summary>
    public WingetAvailability Availability { get; set; } = WingetAvailability.Available("v1.9.0 (fake)");

    public Task<WingetAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Availability);

    public Task<IReadOnlyList<WingetPackageInfo>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WingetPackageInfo>>(Array.Empty<WingetPackageInfo>());

    /// <summary>
    /// Số lần GetInstalledPackagesAsync bị gọi. Dùng để test chứng minh một lượt quét
    /// "winget list" đã được bỏ qua khi chắc chắn không cần tới kết quả.
    /// </summary>
    public int GetInstalledCallCount { get; private set; }

    public Task<IReadOnlyList<WingetPackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetInstalledCallCount++;
        if (ThrowOnGetInstalled is not null) throw ThrowOnGetInstalled;
        return Task.FromResult<IReadOnlyList<WingetPackageInfo>>(InstalledRows.Count > 0
            ? InstalledRows.ToList()
            : InstalledPackageIds.Select(id => new WingetPackageInfo(id, id, "1.0", null, "winget")).ToList());
    }

    public Task<bool> IsInstalledAsync(string packageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsInstalledCalls.Add(packageId);

        if (ThrowOnIsInstalled is not null)
        {
            throw ThrowOnIsInstalled;
        }

        return Task.FromResult(InstalledPackageIds.Contains(packageId));
    }

    public Task<InstallationResult> InstallAsync(
        SoftwarePackage package,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InstallCalls.Add(package.PackageId);

        // Cho test cơ hội huỷ ngay giữa lúc "đang cài".
        OnInstalling?.Invoke(package);
        cancellationToken.ThrowIfCancellationRequested();

        if (ThrowOnInstall.TryGetValue(package.PackageId, out var exception))
        {
            throw exception;
        }

        var outcome = Outcomes.TryGetValue(package.PackageId, out var configured)
            ? configured
            : InstallOutcome.Succeeded;

        if (outcome is InstallOutcome.Succeeded)
        {
            InstalledPackageIds.Add(package.PackageId);
        }

        return Task.FromResult(BuildResult(package, outcome));
    }

    public Task<InstallationResult> UpgradeAsync(
        SoftwarePackage package,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpgradeCalls.Add(package.PackageId);

        var outcome = Outcomes.TryGetValue(package.PackageId, out var configured)
            ? configured
            : InstallOutcome.Upgraded;

        return Task.FromResult(BuildResult(package, outcome));
    }

    private static InstallationResult BuildResult(SoftwarePackage package, InstallOutcome outcome) => new()
    {
        PackageId = package.PackageId,
        DisplayName = package.Name,
        Outcome = outcome,
        ExitCode = outcome == InstallOutcome.Failed ? unchecked((int)0x8A150003) : 0,
        Command = $"winget install --id {package.PackageId} (fake)",
        Message = LocalizedText.Raw(outcome.ToString())
    };
}
