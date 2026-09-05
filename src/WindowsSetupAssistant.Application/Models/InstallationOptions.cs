using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Application.Models;

/// <summary>
/// Tuỳ chọn cho một lượt chạy hàng đợi cài đặt.
/// </summary>
public sealed class InstallationOptions
{
    /// <summary>Làm gì khi phần mềm đã có sẵn trên máy: bỏ qua hay nâng cấp.</summary>
    public ExistingPackageAction ExistingPackageAction { get; init; } = ExistingPackageAction.Skip;

    /// <summary>
    /// Mặc định false: một gói lỗi không làm dừng cả hàng đợi (yêu cầu bắt buộc của dự án).
    /// </summary>
    public bool StopOnFirstError { get; init; }

    /// <summary>
    /// Danh sách Package Id đã biết chắc là đang cài trên máy (lấy từ lần quét lúc khởi động).
    /// Nếu null, hàng đợi sẽ tự hỏi WinGet từng gói (chậm hơn).
    /// </summary>
    public IReadOnlySet<string>? PreCheckedInstalledPackageIds { get; init; }
}
