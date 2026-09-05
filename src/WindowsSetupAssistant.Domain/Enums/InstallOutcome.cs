namespace WindowsSetupAssistant.Domain.Enums;

/// <summary>
/// Kết quả của một lượt cài đặt/nâng cấp cho đúng một gói.
/// </summary>
public enum InstallOutcome
{
    /// <summary>Cài mới thành công.</summary>
    Succeeded = 0,

    /// <summary>Nâng cấp thành công.</summary>
    Upgraded = 1,

    /// <summary>Bỏ qua vì đã cài sẵn (người dùng chọn Skip).</summary>
    Skipped = 2,

    /// <summary>Đã cài sẵn và không có bản mới để nâng cấp.</summary>
    AlreadyInstalled = 3,

    /// <summary>Thất bại - xem ExitCode và Message.</summary>
    Failed = 4,

    /// <summary>Người dùng huỷ hàng đợi.</summary>
    Cancelled = 5
}
