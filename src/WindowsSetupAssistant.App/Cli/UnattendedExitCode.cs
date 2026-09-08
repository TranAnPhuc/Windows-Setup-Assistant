namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Mã thoát trả về cho script gọi ứng dụng. Đây là HỢP ĐỒNG với bên ngoài:
/// không đổi giá trị số, không thêm giá trị mới nếu chưa cập nhật README.
/// </summary>
public enum UnattendedExitCode
{
    /// <summary>Mọi gói thành công (gói bỏ qua vì đã có sẵn cũng tính là thành công).</summary>
    Success = 0,

    /// <summary>Chạy hết hàng đợi nhưng có ít nhất một gói lỗi.</summary>
    SomePackagesFailed = 1,

    /// <summary>Tham số sai, hoặc không tìm thấy cấu hình theo tên.</summary>
    InvalidArguments = 2,

    /// <summary>Máy không có WinGet.</summary>
    WingetMissing = 3,

    /// <summary>Người dùng nhấn Ctrl+C.</summary>
    Cancelled = 4
}
