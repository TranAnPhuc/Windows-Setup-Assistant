namespace WindowsSetupAssistant.Application.Models;

/// <summary>
/// Kết quả chạy một tiến trình bên ngoài (winget.exe).
/// </summary>
public sealed class ProcessRunResult
{
    public required string Command { get; init; }

    public int ExitCode { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }

    /// <summary>True nếu tiến trình bị buộc dừng vì quá thời gian chờ (winget treo).</summary>
    public bool TimedOut { get; init; }

    public bool IsSuccess => ExitCode == 0 && !TimedOut;

    /// <summary>Gộp stdout + stderr để hiển thị trong nhật ký.</summary>
    public string CombinedOutput =>
        string.IsNullOrWhiteSpace(StandardError)
            ? StandardOutput
            : $"{StandardOutput}{Environment.NewLine}{StandardError}";
}
