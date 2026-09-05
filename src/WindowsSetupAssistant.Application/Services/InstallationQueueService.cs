using System.Diagnostics;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Domain.Validation;

namespace WindowsSetupAssistant.Application.Services;

/// <summary>
/// Hàng đợi cài đặt: chạy TUẦN TỰ từng gói để tránh nhiều trình cài đặt tranh nhau,
/// ghi nhận kết quả từng gói, và quan trọng nhất: một gói lỗi KHÔNG làm dừng cả hàng đợi.
/// </summary>
public sealed class InstallationQueueService
{
    private readonly IWingetService _wingetService;
    private readonly IAppLogger _logger;

    public InstallationQueueService(IWingetService wingetService, IAppLogger logger)
    {
        _wingetService = wingetService ?? throw new ArgumentNullException(nameof(wingetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InstallationRunSummary> RunAsync(
        IReadOnlyList<SoftwarePackage> packages,
        InstallationOptions options,
        IProgress<InstallationProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(options);

        // Chụp lại dữ liệu ngay khi hàng đợi bắt đầu. UI vẫn có thể nhận các thao tác
        // riêng của nó trên UI thread; hàng đợi không được để các thay đổi đó làm đổi
        // Package Id, thứ tự hoặc lựa chọn của những gói chưa chạy.
        var ordered = packages
            .Select(package => package.Clone())
            .OrderBy(package => package.SortOrder)
            .ToList();
        var results = new List<InstallationResult>(ordered.Count);
        var stopwatch = Stopwatch.StartNew();
        var cancelled = false;

        _logger.Information($"Bắt đầu hàng đợi cài đặt: {ordered.Count} phần mềm. " +
                            $"Gói đã tồn tại sẽ được {(options.ExistingPackageAction == ExistingPackageAction.Upgrade ? "NÂNG CẤP" : "BỎ QUA")}.");

        for (var index = 0; index < ordered.Count; index++)
        {
            var package = ordered[index];

            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
            }

            if (cancelled)
            {
                // Ghi nhận các gói còn lại là "đã huỷ" để người dùng thấy rõ và có thể thử lại sau.
                results.Add(CreateResult(package, InstallOutcome.Cancelled, "Bị huỷ trước khi bắt đầu."));
                continue;
            }

            progress?.Report(new InstallationProgressUpdate
            {
                CompletedCount = index,
                TotalCount = ordered.Count,
                CurrentPackage = package,
                StatusMessage = $"({index + 1}/{ordered.Count}) Đang xử lý {package.Name}..."
            });

            InstallationResult result;

            try
            {
                result = await ProcessPackageAsync(package, options, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                result = CreateResult(package, InstallOutcome.Cancelled, "Người dùng đã huỷ quá trình cài đặt.");
            }
            catch (Exception ex)
            {
                // Bắt mọi lỗi ngoài dự kiến để hàng đợi vẫn chạy tiếp gói sau.
                result = CreateResult(package, InstallOutcome.Failed, $"Lỗi không mong đợi: {ex.Message}");
            }

            // winget cũng có thể tự báo "đã bị huỷ" qua exit code - coi như người dùng đã huỷ.
            if (result.Outcome == InstallOutcome.Cancelled)
            {
                cancelled = true;
            }

            results.Add(result);
            LogResult(result);

            progress?.Report(new InstallationProgressUpdate
            {
                CompletedCount = index + 1,
                TotalCount = ordered.Count,
                CurrentPackage = package,
                StatusMessage = $"({index + 1}/{ordered.Count}) {package.Name}: {result.Message}",
                CompletedResult = result
            });

            if (result.Outcome == InstallOutcome.Failed && options.StopOnFirstError)
            {
                _logger.Warning("Đã bật tuỳ chọn dừng khi gặp lỗi đầu tiên - hàng đợi dừng lại.");
                break;
            }
        }

        stopwatch.Stop();

        var summary = new InstallationRunSummary
        {
            Results = results,
            WasCancelled = cancelled,
            TotalDuration = stopwatch.Elapsed
        };

        _logger.Information(
            $"Kết thúc hàng đợi sau {summary.TotalDuration.TotalSeconds:F1}s: " +
            $"{summary.SucceededCount} thành công, {summary.SkippedCount} bỏ qua, " +
            $"{summary.FailedCount} thất bại, {summary.CancelledCount} bị huỷ.");

        progress?.Report(new InstallationProgressUpdate
        {
            CompletedCount = results.Count,
            TotalCount = ordered.Count,
            CurrentPackage = null,
            StatusMessage = cancelled ? "Đã huỷ quá trình cài đặt." : "Hoàn tất."
        });

        return summary;
    }

    private async Task<InstallationResult> ProcessPackageAsync(
        SoftwarePackage package,
        InstallationOptions options,
        CancellationToken cancellationToken)
    {
        // Lớp phòng thủ: không bao giờ đưa Id lạ vào tiến trình winget.
        if (!PackageIdValidator.TryValidate(package.PackageId, out var validationError))
        {
            return CreateResult(package, InstallOutcome.Failed, validationError);
        }

        var isInstalled = await ResolveInstalledAsync(package, options, cancellationToken).ConfigureAwait(false);

        if (isInstalled && options.ExistingPackageAction == ExistingPackageAction.Skip)
        {
            return CreateResult(package, InstallOutcome.Skipped, "Đã được cài sẵn - bỏ qua theo lựa chọn của bạn.");
        }

        if (isInstalled && options.ExistingPackageAction == ExistingPackageAction.Upgrade)
        {
            return await _wingetService.UpgradeAsync(package, null, cancellationToken).ConfigureAwait(false);
        }

        return await _wingetService.InstallAsync(package, null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ResolveInstalledAsync(
        SoftwarePackage package,
        InstallationOptions options,
        CancellationToken cancellationToken)
    {
        if (options.PreCheckedInstalledPackageIds is { } known)
        {
            return known.Contains(package.PackageId);
        }

        try
        {
            return await _wingetService.IsInstalledAsync(package.PackageId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Không kiểm tra được thì coi như chưa cài: winget sẽ tự báo "already installed" nếu trùng.
            _logger.Warning($"Không kiểm tra được trạng thái cài đặt của {package.PackageId}: {ex.Message}");
            return false;
        }
    }

    private static InstallationResult CreateResult(SoftwarePackage package, InstallOutcome outcome, string message) =>
        new()
        {
            PackageId = package.PackageId,
            DisplayName = string.IsNullOrWhiteSpace(package.Name) ? package.PackageId : package.Name,
            Outcome = outcome,
            Message = message
        };

    private void LogResult(InstallationResult result)
    {
        var text = $"{result.DisplayName} ({result.PackageId}): {result.Outcome} - {result.Message}";

        if (result.Outcome == InstallOutcome.Failed)
        {
            _logger.Error(text, result.Command, result.ExitCode);
        }
        else
        {
            _logger.Information(text, result.Command);
        }
    }
}
