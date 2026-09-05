using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Domain.Models;

/// <summary>
/// Kết quả cài đặt của đúng một phần mềm. Dùng cho màn hình tiến trình, nhật ký và retry.
/// </summary>
public sealed class InstallationResult
{
    public required string PackageId { get; init; }

    public required string DisplayName { get; init; }

    public InstallOutcome Outcome { get; init; }

    /// <summary>Exit code của tiến trình winget. null nếu chưa từng chạy (ví dụ bị bỏ qua).</summary>
    public int? ExitCode { get; init; }

    /// <summary>Câu lệnh đã chạy (đã được che/kiểm tra), phục vụ nhật ký.</summary>
    public string? Command { get; init; }

    /// <summary>Thông điệp thân thiện cho người dùng (thành công / lý do lỗi).</summary>
    public string Message { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;

    public TimeSpan Duration { get; init; }

    public bool IsSuccess => Outcome is InstallOutcome.Succeeded
        or InstallOutcome.Upgraded
        or InstallOutcome.Skipped
        or InstallOutcome.AlreadyInstalled;

    public bool CanRetry => Outcome is InstallOutcome.Failed or InstallOutcome.Cancelled;
}
