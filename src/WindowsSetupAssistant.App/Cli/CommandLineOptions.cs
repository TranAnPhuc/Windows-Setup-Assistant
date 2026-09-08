using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>Tham số của một lượt chạy không giám sát, sau khi đã phân tích xong.</summary>
public sealed class CommandLineOptions
{
    /// <summary>Tên cấu hình cần cài. null nghĩa là dùng cấu hình đang chọn trong file dữ liệu.</summary>
    public string? ProfileName { get; init; }

    /// <summary>Gói đã có trên máy: bỏ qua hay nâng cấp.</summary>
    public ExistingPackageAction ExistingPackageAction { get; init; } = ExistingPackageAction.Skip;

    /// <summary>Đường dẫn file báo cáo. null nghĩa là tự sinh trong thư mục Reports cạnh .exe.</summary>
    public string? ReportPath { get; init; }
}
