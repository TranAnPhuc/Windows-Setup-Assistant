using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Application.Models;

/// <summary>
/// Tổng kết một lượt chạy hàng đợi cài đặt.
/// </summary>
public sealed class InstallationRunSummary
{
    public required IReadOnlyList<InstallationResult> Results { get; init; }

    public bool WasCancelled { get; init; }

    public TimeSpan TotalDuration { get; init; }

    public int SucceededCount => Results.Count(r =>
        r.Outcome is InstallOutcome.Succeeded or InstallOutcome.Upgraded);

    public int SkippedCount => Results.Count(r =>
        r.Outcome is InstallOutcome.Skipped or InstallOutcome.AlreadyInstalled);

    public int FailedCount => Results.Count(r => r.Outcome == InstallOutcome.Failed);

    public int CancelledCount => Results.Count(r => r.Outcome == InstallOutcome.Cancelled);
}
