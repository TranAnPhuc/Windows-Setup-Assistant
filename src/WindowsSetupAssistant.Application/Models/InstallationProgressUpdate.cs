using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Application.Models;

/// <summary>
/// Thông tin tiến trình gửi về ViewModel qua IProgress&lt;T&gt; (an toàn với UI thread).
/// </summary>
public sealed class InstallationProgressUpdate
{
    public int CompletedCount { get; init; }

    public int TotalCount { get; init; }

    /// <summary>Gói đang xử lý (null khi đã xong toàn bộ).</summary>
    public SoftwarePackage? CurrentPackage { get; init; }

    public string StatusMessage { get; init; } = string.Empty;

    /// <summary>Kết quả vừa hoàn tất (null khi chỉ báo "đang bắt đầu gói X").</summary>
    public InstallationResult? CompletedResult { get; init; }

    public double PercentComplete => TotalCount <= 0
        ? 0
        : Math.Round(CompletedCount * 100.0 / TotalCount, 1);
}
