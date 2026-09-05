namespace WindowsSetupAssistant.Infrastructure.Winget;

/// <summary>
/// Mã lỗi (exit code) chính thức của WinGet.
/// Nguồn: microsoft/winget-cli - doc/windows/package-manager/winget/returnCodes.md
///
/// Lưu ý về kiểu dữ liệu: WinGet trả về HRESULT dạng unsigned (ví dụ 0x8A150014).
/// Process.ExitCode trong .NET là int (có dấu) nên phải dùng unchecked((int)0x...)
/// để so sánh cho đúng, nếu không sẽ bị lỗi tràn số khi biên dịch.
/// </summary>
public static class WingetExitCodes
{
    public const int Success = 0;

    // --- Nhóm lỗi chung ---
    public const int InternalError = unchecked((int)0x8A150001);
    public const int InvalidCommandLineArguments = unchecked((int)0x8A150002);
    public const int CommandFailed = unchecked((int)0x8A150003);
    public const int CtrlSignalReceived = unchecked((int)0x8A150005);
    public const int DownloadFailed = unchecked((int)0x8A150008);
    public const int NoApplicableInstaller = unchecked((int)0x8A150010);
    public const int InstallerHashMismatch = unchecked((int)0x8A150011);
    public const int NoApplicationsFound = unchecked((int)0x8A150014);
    public const int NoSourcesDefined = unchecked((int)0x8A150015);
    public const int MultipleApplicationsFound = unchecked((int)0x8A150016);
    public const int CommandRequiresAdmin = unchecked((int)0x8A150019);
    public const int UpdateNotApplicable = unchecked((int)0x8A15002B);
    public const int BlockedByPolicy = unchecked((int)0x8A15003A);
    public const int SourceOpenFailed = unchecked((int)0x8A150045);
    public const int UpgradeVersionNotNewer = unchecked((int)0x8A15004F);
    public const int InstallerProhibitsElevation = unchecked((int)0x8A150056);
    public const int PackageAlreadyInstalled = unchecked((int)0x8A150061);
    public const int AppTerminationReceived = unchecked((int)0x8A15006A);
    public const int ServiceUnavailable = unchecked((int)0x8A15006D);
    public const int InstallerZeroByteFile = unchecked((int)0x8A150086);

    // --- Nhóm lỗi phát sinh trong lúc chạy trình cài đặt ---
    public const int InstallPackageInUse = unchecked((int)0x8A150101);
    public const int InstallInProgress = unchecked((int)0x8A150102);
    public const int InstallFileInUse = unchecked((int)0x8A150103);
    public const int InstallMissingDependency = unchecked((int)0x8A150104);
    public const int InstallDiskFull = unchecked((int)0x8A150105);
    public const int InstallInsufficientMemory = unchecked((int)0x8A150106);
    public const int InstallNoNetwork = unchecked((int)0x8A150107);
    public const int InstallContactSupport = unchecked((int)0x8A150108);
    public const int InstallRebootRequiredToFinish = unchecked((int)0x8A150109);
    public const int InstallRebootRequiredForInstall = unchecked((int)0x8A15010A);
    public const int InstallRebootInitiated = unchecked((int)0x8A15010B);
    public const int InstallCancelledByUser = unchecked((int)0x8A15010C);
    public const int InstallAlreadyInstalled = unchecked((int)0x8A15010D);
    public const int InstallDowngrade = unchecked((int)0x8A15010E);
    public const int InstallBlockedByPolicy = unchecked((int)0x8A15010F);
    public const int InstallCustomError = unchecked((int)0x8A150115);

    // Mã Win32 hay gặp khi thiếu quyền.
    public const int AccessDenied = unchecked((int)0x80070005);

    /// <summary>Gói đã tồn tại trên máy.</summary>
    public static bool IsAlreadyInstalled(int exitCode) =>
        exitCode is PackageAlreadyInstalled or InstallAlreadyInstalled;

    /// <summary>Không có bản cập nhật nào để nâng cấp (không phải lỗi).</summary>
    public static bool IsNoUpgradeAvailable(int exitCode) =>
        exitCode is UpdateNotApplicable or UpgradeVersionNotNewer;

    /// <summary>Tiến trình bị huỷ (người dùng bấm Huỷ hoặc hệ thống gửi tín hiệu dừng).</summary>
    public static bool IsCancelled(int exitCode) =>
        exitCode is CtrlSignalReceived or AppTerminationReceived or InstallCancelledByUser;

    /// <summary>Cài xong nhưng cần khởi động lại máy - vẫn coi là thành công.</summary>
    public static bool RequiresReboot(int exitCode) =>
        exitCode is InstallRebootRequiredToFinish or InstallRebootRequiredForInstall or InstallRebootInitiated;

    /// <summary>Thiếu quyền Administrator.</summary>
    public static bool RequiresAdmin(int exitCode) =>
        exitCode is CommandRequiresAdmin or AccessDenied;

    /// <summary>Thông điệp tiếng Việt dễ hiểu cho người dùng cuối.</summary>
    public static string Describe(int exitCode) => exitCode switch
    {
        Success => "Thành công.",
        NoApplicationsFound => "Không tìm thấy gói này trong nguồn WinGet. Hãy kiểm tra lại Package Id.",
        PackageAlreadyInstalled or InstallAlreadyInstalled => "Phần mềm đã được cài trên máy.",
        UpdateNotApplicable or UpgradeVersionNotNewer => "Không có bản cập nhật mới - phiên bản hiện tại đã là mới nhất.",
        CommandRequiresAdmin => "Lệnh này cần quyền Administrator. Hãy chạy lại ứng dụng bằng 'Run as administrator'.",
        AccessDenied => "Bị từ chối quyền truy cập. Có thể cần quyền Administrator.",
        InstallerProhibitsElevation => "Trình cài đặt của phần mềm này không cho phép chạy dưới quyền Administrator. Hãy chạy ứng dụng ở chế độ người dùng thường.",
        DownloadFailed => "Tải trình cài đặt thất bại. Kiểm tra kết nối mạng rồi thử lại.",
        InstallerZeroByteFile => "File tải về rỗng (0 byte). Kết nối mạng có thể đang gặp sự cố.",
        InstallNoNetwork => "Phần mềm này cần kết nối Internet để cài đặt.",
        InstallerHashMismatch => "Mã băm của file tải về không khớp với manifest - dừng để đảm bảo an toàn.",
        NoApplicableInstaller => "Không có trình cài đặt phù hợp với hệ thống này (kiến trúc/phiên bản Windows).",
        NoSourcesDefined => "WinGet chưa có nguồn nào. Chạy 'winget source reset --force' rồi thử lại.",
        SourceOpenFailed => "Không mở được nguồn WinGet. Kiểm tra kết nối mạng.",
        ServiceUnavailable => "Dịch vụ cần thiết đang bận hoặc không sẵn sàng. Thử lại sau.",
        MultipleApplicationsFound => "Có nhiều gói khớp với tiêu chí. Hãy dùng Package Id chính xác hơn.",
        InstallInProgress => "Đang có một tiến trình cài đặt khác chạy. Thử lại sau.",
        InstallPackageInUse or InstallFileInUse => "Phần mềm đang chạy hoặc file đang được sử dụng. Hãy đóng ứng dụng rồi thử lại.",
        InstallMissingDependency => "Thiếu thành phần phụ thuộc trên hệ thống.",
        InstallDiskFull => "Ổ đĩa đã đầy. Giải phóng dung lượng rồi thử lại.",
        InstallInsufficientMemory => "Không đủ bộ nhớ để cài đặt. Đóng bớt ứng dụng rồi thử lại.",
        InstallRebootRequiredToFinish or InstallRebootRequiredForInstall or InstallRebootInitiated =>
            "Cài đặt xong nhưng cần khởi động lại máy để hoàn tất.",
        InstallCancelledByUser or CtrlSignalReceived or AppTerminationReceived => "Quá trình cài đặt đã bị huỷ.",
        InstallDowngrade => "Trên máy đã có phiên bản mới hơn.",
        BlockedByPolicy or InstallBlockedByPolicy => "Bị chặn bởi chính sách của tổ chức (Group Policy).",
        InvalidCommandLineArguments => "Tham số dòng lệnh không hợp lệ - có thể phiên bản WinGet trên máy quá cũ.",
        InstallCustomError => "Trình cài đặt của phần mềm báo lỗi riêng.",
        InternalError => "WinGet gặp lỗi nội bộ.",
        CommandFailed => "WinGet thực thi lệnh thất bại.",
        _ => $"WinGet kết thúc với mã lỗi 0x{exitCode:X8}."
    };
}
