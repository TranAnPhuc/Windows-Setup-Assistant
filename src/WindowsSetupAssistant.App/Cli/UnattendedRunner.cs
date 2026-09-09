using System.IO;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Application.Services;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Chạy trọn một lượt cài đặt không giám sát.
///
/// Lớp này CỐ Ý không tự gọi WinGet và không tự quyết định bỏ qua hay nâng cấp. Nó dùng đúng
/// InstallationQueueService mà giao diện đang dùng, nên quy tắc cài đặt của hai chế độ
/// không thể lệch nhau.
/// </summary>
public sealed class UnattendedRunner
{
    private readonly IWingetService _wingetService;
    private readonly IProfileRepository _repository;
    private readonly InstallationQueueService _queueService;
    private readonly IStringLocalizer _localizer;
    private readonly IUnattendedOutput _output;
    private readonly IAppLogger _logger;

    public UnattendedRunner(
        IWingetService wingetService,
        IProfileRepository repository,
        InstallationQueueService queueService,
        IStringLocalizer localizer,
        IUnattendedOutput output,
        IAppLogger logger)
    {
        _wingetService = wingetService ?? throw new ArgumentNullException(nameof(wingetService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UnattendedExitCode> RunAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var startedAt = DateTimeOffset.Now;

        _output.WriteLine(ConsoleMessages.ModeBanner);

        var availability = await _wingetService.CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false);

        if (!availability.IsAvailable)
        {
            _output.WriteError(ConsoleMessages.WingetMissing);
            return UnattendedExitCode.WingetMissing;
        }

        _output.WriteLine(ConsoleMessages.WingetDetected(availability.Version));

        // Phải kiểm tra TRƯỚC khi gọi LoadAsync: khi file chưa tồn tại, LoadAsync tự ghi
        // ngay file mẫu xuống đĩa như một tác dụng phụ, nên kiểm tra sau đó sẽ luôn thấy
        // "đã có file" dù thực ra ban đầu không có gì.
        var dataFileExisted = File.Exists(_repository.DataFilePath);

        var catalog = await _repository.LoadAsync(cancellationToken).ConfigureAwait(false);

        // Repository âm thầm trả về danh sách mẫu khi không đọc được file. Ở giao diện thì
        // người dùng nhìn thấy ngay là danh sách lạ; ở đây không ai nhìn cả, nên phải nói to.
        if (!dataFileExisted)
        {
            _output.WriteError(ConsoleMessages.DataFileMissing(_repository.DataFilePath));
        }

        var profile = ResolveProfile(catalog, options.ProfileName);

        if (profile is null)
        {
            _output.WriteError(ConsoleMessages.ProfileNotFound(
                options.ProfileName ?? string.Empty,
                catalog.Profiles.Select(p => p.Name)));

            return UnattendedExitCode.InvalidArguments;
        }

        var selected = profile.Packages.Where(package => package.IsSelected).ToList();

        // Giữ null nghĩa là "chưa hỏi WinGet xem máy đã cài những gì" - đúng hợp đồng
        // của InstallationOptions.PreCheckedInstalledPackageIds.
        IReadOnlySet<string>? installedIds = null;

        if (selected.Count == 0)
        {
            // Không gói nào được tick thì bỏ hẳn lượt quét "winget list": nó mất vài giây
            // mà kết quả chắc chắn không dùng tới, vì hàng đợi rỗng.
            _output.WriteLine(ConsoleMessages.ProfileSummary(profile.Name, 0, 0));
            _output.WriteLine(ConsoleMessages.NothingToDo);
        }
        else
        {
            installedIds = await GetInstalledIdsAsync(cancellationToken).ConfigureAwait(false);

            if (installedIds is null)
            {
                // Quét thất bại: KHÔNG được coi "không biết" là "biết chắc rỗng" rồi suy ra
                // "không gói nào đã cài". In một dòng riêng nói rõ là không biết, thay vì bịa ra
                // một con số "not installed" trông như sự thật. Hàng đợi phía dưới vẫn nhận
                // installedIds = null nguyên vẹn để nó tự hỏi WinGet từng gói.
                //
                // Tuyệt đối không kết luận NothingToDo ở nhánh này: có gói được tick mà chưa
                // biết trạng thái thì vẫn phải chạy hàng đợi.
                _output.WriteLine(ConsoleMessages.ProfileSummaryUnknownInstallState(profile.Name, selected.Count));
            }
            else
            {
                var notInstalled = selected.Count(package => !installedIds.Contains(package.PackageId));

                _output.WriteLine(ConsoleMessages.ProfileSummary(profile.Name, selected.Count, notInstalled));

                if (options.ExistingPackageAction == ExistingPackageAction.Skip && notInstalled == 0)
                {
                    _output.WriteLine(ConsoleMessages.NothingToDo);
                }
            }
        }

        var summary = await _queueService.RunAsync(
            selected,
            new InstallationOptions
            {
                ExistingPackageAction = options.ExistingPackageAction,
                PreCheckedInstalledPackageIds = installedIds
            },
            new Progress<InstallationProgressUpdate>(ReportProgress),
            cancellationToken).ConfigureAwait(false);

        var exitCode = summary.WasCancelled
            ? UnattendedExitCode.Cancelled
            : summary.FailedCount > 0
                ? UnattendedExitCode.SomePackagesFailed
                : UnattendedExitCode.Success;

        _output.WriteLine();
        _output.WriteLine(ConsoleMessages.Elapsed(summary.TotalDuration));
        _output.WriteLine(ConsoleMessages.Totals(
            succeeded: summary.Results.Count(r => r.Outcome == InstallOutcome.Succeeded),
            upgraded: summary.Results.Count(r => r.Outcome == InstallOutcome.Upgraded),
            skipped: summary.SkippedCount,
            failed: summary.FailedCount,
            cancelled: summary.CancelledCount));

        WriteReport(summary, profile.Name, availability.Version, options, exitCode, startedAt);

        _output.WriteLine(ConsoleMessages.ExitLine(exitCode));

        return exitCode;
    }

    /// <summary>
    /// Tìm cấu hình theo tên, bỏ qua hoa thường và khoảng trắng thừa - người gõ lệnh
    /// không nên bị đánh trượt chỉ vì gõ thiếu một chữ hoa.
    /// </summary>
    private static InstallationProfile? ResolveProfile(SoftwareCatalog catalog, string? requestedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return catalog.GetActiveProfile();
        }

        var wanted = requestedName.Trim();

        return catalog.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Name?.Trim(), wanted, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlySet<string>?> GetInstalledIdsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var installed = await _wingetService.GetInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);

            return installed
                .Select(row => row.PackageId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Không quét được thì trả về null - "không biết" - chứ KHÔNG được trả về tập
            // rỗng, vì tập rỗng lại có nghĩa hoàn toàn khác: "biết chắc chưa gói nào được
            // cài". null đúng với hợp đồng của InstallationOptions.PreCheckedInstalledPackageIds:
            // cứ để hàng đợi tự hỏi WinGet từng gói - chậm hơn nhưng vẫn đúng.
            _logger.Warning($"Could not list installed packages: {exception.Message}");
            return null;
        }
    }

    private void ReportProgress(InstallationProgressUpdate update)
    {
        if (update.CurrentPackage is not null && update.CompletedResult is null)
        {
            _output.WriteLine(ConsoleMessages.PackageStarting(
                update.CompletedCount + 1,
                update.TotalCount,
                update.CurrentPackage.PackageId));

            return;
        }

        if (update.CompletedResult is not { } result)
        {
            return;
        }

        if (result.Outcome == InstallOutcome.Failed)
        {
            _output.WriteError(ConsoleMessages.PackageFailed(
                result.PackageId,
                result.ExitCode,
                _localizer.Format(result.Message, System.Globalization.CultureInfo.GetCultureInfo("en"))));

            return;
        }

        _output.WriteLine(ConsoleMessages.PackageFinished(result.Outcome.ToString(), result.Duration));
    }

    private void WriteReport(
        InstallationRunSummary summary,
        string profileName,
        string? wingetVersion,
        CommandLineOptions options,
        UnattendedExitCode exitCode,
        DateTimeOffset startedAt)
    {
        var report = UnattendedReport.Create(
            summary,
            profileName,
            wingetVersion,
            options.ExistingPackageAction,
            exitCode,
            startedAt,
            _localizer);

        var path = options.ReportPath ?? UnattendedReportWriter.DefaultPath(startedAt);

        if (UnattendedReportWriter.TryWrite(report, path, out var error))
        {
            _output.WriteLine(ConsoleMessages.ReportWritten(Path.GetFullPath(path)));
            return;
        }

        // Ghi báo cáo hỏng KHÔNG được đổi mã thoát: việc cài đã xong rồi.
        _output.WriteError(ConsoleMessages.ReportFailed(path, error ?? "unknown error"));
    }
}
