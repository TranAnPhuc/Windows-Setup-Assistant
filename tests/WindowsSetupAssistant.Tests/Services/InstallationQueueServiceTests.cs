using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Application.Services;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Tests.Fakes;

namespace WindowsSetupAssistant.Tests.Services;

/// <summary>
/// Test hàng đợi cài đặt. IWingetService luôn là bản giả nên không có phần mềm thật nào được cài.
/// </summary>
public class InstallationQueueServiceTests
{
    private readonly FakeWingetService _winget = new();
    private readonly RecordingLogger _logger = new();

    private InstallationQueueService CreateQueue() => new(_winget, _logger);

    private static SoftwarePackage Package(string name, string packageId, int sortOrder = 0) => new()
    {
        Name = name,
        PackageId = packageId,
        SortOrder = sortOrder,
        IsSelected = true
    };

    private static List<SoftwarePackage> ThreePackages() => new()
    {
        Package("Google Chrome", "Google.Chrome", 0),
        Package("Git", "Git.Git", 1),
        Package("7-Zip", "7zip.7zip", 2)
    };

    [Fact]
    public async Task RunAsync_InstallsSequentiallyInSortOrder()
    {
        var packages = new List<SoftwarePackage>
        {
            Package("C", "C.C", 2),
            Package("A", "A.A", 0),
            Package("B", "B.B", 1)
        };

        var summary = await CreateQueue().RunAsync(packages, new InstallationOptions());

        Assert.Equal(new[] { "A.A", "B.B", "C.C" }, _winget.InstallCalls);
        Assert.Equal(3, summary.SucceededCount);
        Assert.False(summary.WasCancelled);
    }

    [Fact]
    public async Task RunAsync_UsesSnapshotWhenCallerChangesAPackageWhileRunning()
    {
        var packages = ThreePackages();

        // Mô phỏng thao tác UI sửa một gói chưa chạy trong lúc gói đầu đang cài.
        // Hàng đợi phải tiếp tục dùng danh sách đã xác nhận khi lượt chạy bắt đầu.
        _winget.OnInstalling = package =>
        {
            if (package.PackageId == "Google.Chrome")
            {
                packages[1].PackageId = "Malicious.Replacement";
            }
        };

        var summary = await CreateQueue().RunAsync(packages, new InstallationOptions());

        Assert.Equal(new[] { "Google.Chrome", "Git.Git", "7zip.7zip" }, _winget.InstallCalls);
        Assert.Equal(new[] { "Google.Chrome", "Git.Git", "7zip.7zip" }, summary.Results.Select(result => result.PackageId));
    }

    [Fact]
    public async Task RunAsync_OneFailure_DoesNotStopTheQueue()
    {
        var packages = ThreePackages();
        _winget.Outcomes["Git.Git"] = InstallOutcome.Failed;

        var summary = await CreateQueue().RunAsync(packages, new InstallationOptions());

        Assert.Equal(3, _winget.InstallCalls.Count);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(2, summary.SucceededCount);
    }

    [Fact]
    public async Task RunAsync_UnexpectedException_IsRecordedAndQueueContinues()
    {
        var packages = ThreePackages();
        _winget.ThrowOnInstall["Git.Git"] = new InvalidOperationException("Mất kết nối mạng");

        var summary = await CreateQueue().RunAsync(packages, new InstallationOptions());

        Assert.Equal(3, summary.Results.Count);

        var failed = summary.Results.Single(r => r.PackageId == "Git.Git");
        Assert.Equal(InstallOutcome.Failed, failed.Outcome);
        Assert.Contains("Mất kết nối mạng", failed.Message);

        // Gói cuối cùng vẫn được cài.
        Assert.Contains("7zip.7zip", _winget.InstallCalls);
    }

    [Fact]
    public async Task RunAsync_InvalidPackageId_FailsWithoutCallingWinget()
    {
        var packages = new List<SoftwarePackage>
        {
            Package("Xấu", "Google.Chrome && shutdown /s", 0),
            Package("Git", "Git.Git", 1)
        };

        var summary = await CreateQueue().RunAsync(packages, new InstallationOptions());

        Assert.Equal(new[] { "Git.Git" }, _winget.InstallCalls);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(1, summary.SucceededCount);
    }

    [Fact]
    public async Task RunAsync_AlreadyInstalled_WithSkipOption_SkipsWithoutInstalling()
    {
        var packages = ThreePackages();
        _winget.InstalledPackageIds.Add("Git.Git");

        var options = new InstallationOptions { ExistingPackageAction = ExistingPackageAction.Skip };
        var summary = await CreateQueue().RunAsync(packages, options);

        Assert.DoesNotContain("Git.Git", _winget.InstallCalls);
        Assert.DoesNotContain("Git.Git", _winget.UpgradeCalls);
        Assert.Equal(InstallOutcome.Skipped, summary.Results.Single(r => r.PackageId == "Git.Git").Outcome);
        Assert.Equal(1, summary.SkippedCount);
    }

    [Fact]
    public async Task RunAsync_AlreadyInstalled_WithUpgradeOption_CallsUpgrade()
    {
        var packages = ThreePackages();
        _winget.InstalledPackageIds.Add("Git.Git");

        var options = new InstallationOptions { ExistingPackageAction = ExistingPackageAction.Upgrade };
        var summary = await CreateQueue().RunAsync(packages, options);

        Assert.Equal(new[] { "Git.Git" }, _winget.UpgradeCalls);
        Assert.DoesNotContain("Git.Git", _winget.InstallCalls);
        Assert.Equal(InstallOutcome.Upgraded, summary.Results.Single(r => r.PackageId == "Git.Git").Outcome);
    }

    [Fact]
    public async Task RunAsync_PreCheckedInstalledIds_AvoidsExtraWingetCalls()
    {
        var packages = ThreePackages();

        var options = new InstallationOptions
        {
            PreCheckedInstalledPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Git.Git" }
        };

        var summary = await CreateQueue().RunAsync(packages, options);

        Assert.Empty(_winget.IsInstalledCalls);
        Assert.Equal(1, summary.SkippedCount);
    }

    [Fact]
    public async Task RunAsync_IsInstalledThrows_TreatsPackageAsNotInstalled()
    {
        var packages = new List<SoftwarePackage> { Package("Git", "Git.Git") };
        _winget.ThrowOnIsInstalled = new InvalidOperationException("winget không phản hồi");

        var summary = await CreateQueue().RunAsync(packages, new InstallationOptions());

        Assert.Equal(new[] { "Git.Git" }, _winget.InstallCalls);
        Assert.Equal(1, summary.SucceededCount);
        Assert.Contains(_logger.Warnings, w => w.Message.Contains("Không kiểm tra được"));
    }

    [Fact]
    public async Task RunAsync_CancelDuringInstall_StopsAndMarksRemainingAsCancelled()
    {
        var packages = ThreePackages();
        using var cts = new CancellationTokenSource();

        // Huỷ ngay khi gói thứ hai bắt đầu cài.
        _winget.OnInstalling = package =>
        {
            if (package.PackageId == "Git.Git")
            {
                cts.Cancel();
            }
        };

        var summary = await CreateQueue().RunAsync(packages, new InstallationOptions(), null, cts.Token);

        Assert.True(summary.WasCancelled);
        Assert.Equal(3, summary.Results.Count);
        Assert.Equal(InstallOutcome.Succeeded, summary.Results[0].Outcome);
        Assert.Equal(InstallOutcome.Cancelled, summary.Results[1].Outcome);
        Assert.Equal(InstallOutcome.Cancelled, summary.Results[2].Outcome);

        // Gói thứ ba không bao giờ được đụng tới.
        Assert.DoesNotContain("7zip.7zip", _winget.InstallCalls);
    }

    [Fact]
    public async Task RunAsync_TokenAlreadyCancelled_InstallsNothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var summary = await CreateQueue().RunAsync(ThreePackages(), new InstallationOptions(), null, cts.Token);

        Assert.Empty(_winget.InstallCalls);
        Assert.True(summary.WasCancelled);
        Assert.Equal(3, summary.CancelledCount);
    }

    [Fact]
    public async Task RunAsync_StopOnFirstError_StopsQueue()
    {
        var packages = ThreePackages();
        _winget.Outcomes["Google.Chrome"] = InstallOutcome.Failed;

        var options = new InstallationOptions { StopOnFirstError = true };
        var summary = await CreateQueue().RunAsync(packages, options);

        Assert.Single(summary.Results);
        Assert.Equal(1, summary.FailedCount);
    }

    [Fact]
    public async Task RunAsync_ReportsProgressForEveryPackage()
    {
        var packages = ThreePackages();
        var updates = new List<InstallationProgressUpdate>();
        var progress = new Progress<InstallationProgressUpdate>(updates.Add);

        // Progress<T> gửi callback qua SynchronizationContext nên có thể chạy bất đồng bộ;
        // dùng IProgress tự viết để test tất định.
        var deterministicProgress = new SynchronousProgress<InstallationProgressUpdate>(updates.Add);

        var summary = await CreateQueue().RunAsync(packages, new InstallationOptions(), deterministicProgress);

        Assert.Equal(3, summary.Results.Count);

        // Mỗi gói có 1 lần báo "đang xử lý" + 1 lần báo kết quả, cộng thêm 1 lần tổng kết.
        Assert.Equal(7, updates.Count);
        Assert.Equal(3, updates.Count(u => u.CompletedResult is not null));
        Assert.Equal("Hoàn tất.", updates[^1].StatusMessage);
        Assert.Equal(100, updates[^1].PercentComplete);

        _ = progress;
    }

    [Fact]
    public async Task RunAsync_EmptyList_ReturnsEmptySummary()
    {
        var summary = await CreateQueue().RunAsync(new List<SoftwarePackage>(), new InstallationOptions());

        Assert.Empty(summary.Results);
        Assert.False(summary.WasCancelled);
    }

    [Fact]
    public async Task RunAsync_LogsStartAndEnd()
    {
        await CreateQueue().RunAsync(ThreePackages(), new InstallationOptions());

        Assert.Contains(_logger.Entries, e => e.Message.Contains("Bắt đầu hàng đợi"));
        Assert.Contains(_logger.Entries, e => e.Message.Contains("Kết thúc hàng đợi"));
    }

    /// <summary>IProgress gọi callback ngay lập tức, giúp test không phụ thuộc thời gian.</summary>
    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler) => _handler = handler;

        public void Report(T value) => _handler(value);
    }
}
