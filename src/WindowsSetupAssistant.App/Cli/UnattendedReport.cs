using System.Globalization;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Infrastructure.Winget;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>Số lượng theo từng loại kết quả.</summary>
public sealed class UnattendedReportCounts
{
    public int Total { get; init; }
    public int Succeeded { get; init; }
    public int Upgraded { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public int Cancelled { get; init; }
}

/// <summary>Một dòng kết quả cho đúng một gói.</summary>
public sealed class UnattendedReportPackage
{
    public string Name { get; init; } = string.Empty;
    public string PackageId { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public int? ExitCode { get; init; }
    public double DurationSeconds { get; init; }
    public bool RestartRequired { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Báo cáo một lượt chạy không giám sát.
///
/// Tên các thuộc tính ở đây trở thành tên trường JSON, tức là HỢP ĐỒNG với script của người
/// khác. Đổi tên một trường là làm hỏng script của họ mà build vẫn xanh - vì vậy có bộ test
/// riêng canh giữ tên trường.
/// </summary>
public sealed class UnattendedReport
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public double DurationSeconds { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string? WingetVersion { get; init; }
    public string ProfileName { get; init; } = string.Empty;
    public string ExistingPackageAction { get; init; } = nameof(Domain.Enums.ExistingPackageAction.Skip);
    public int ExitCode { get; init; }
    public bool WasCancelled { get; init; }
    public UnattendedReportCounts Counts { get; init; } = new();
    public IReadOnlyList<UnattendedReportPackage> Packages { get; init; } = Array.Empty<UnattendedReportPackage>();

    public static UnattendedReport Create(
        InstallationRunSummary summary,
        string profileName,
        string? wingetVersion,
        ExistingPackageAction existingPackageAction,
        UnattendedExitCode exitCode,
        DateTimeOffset startedAt,
        IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(localizer);

        // Báo cáo luôn tiếng Anh, không theo ngôn ngữ giao diện - xem mục 7 của spec.
        var english = CultureInfo.GetCultureInfo("en");

        var packages = summary.Results.Select(result => new UnattendedReportPackage
        {
            Name = result.DisplayName,
            PackageId = result.PackageId,
            Outcome = result.Outcome.ToString(),
            ExitCode = result.ExitCode,
            DurationSeconds = Math.Round(result.Duration.TotalSeconds, 1),
            RestartRequired = result.ExitCode is not null && WingetExitCodes.RequiresReboot(result.ExitCode.Value),
            ErrorMessage = result.Outcome == InstallOutcome.Failed
                ? localizer.Format(result.Message, english)
                : null
        }).ToList();

        return new UnattendedReport
        {
            StartedAt = startedAt,
            FinishedAt = startedAt + summary.TotalDuration,
            DurationSeconds = Math.Round(summary.TotalDuration.TotalSeconds, 1),
            MachineName = Environment.MachineName,
            WingetVersion = wingetVersion,
            ProfileName = profileName,
            ExistingPackageAction = existingPackageAction.ToString(),
            ExitCode = (int)exitCode,
            WasCancelled = summary.WasCancelled,
            Counts = new UnattendedReportCounts
            {
                Total = summary.Results.Count,
                Succeeded = summary.Results.Count(r => r.Outcome == InstallOutcome.Succeeded),
                Upgraded = summary.Results.Count(r => r.Outcome == InstallOutcome.Upgraded),
                Skipped = summary.SkippedCount,
                Failed = summary.FailedCount,
                Cancelled = summary.CancelledCount
            },
            Packages = packages
        };
    }
}
