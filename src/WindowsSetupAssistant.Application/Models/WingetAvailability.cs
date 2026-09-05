namespace WindowsSetupAssistant.Application.Models;

/// <summary>
/// Kết quả kiểm tra WinGet lúc khởi động ứng dụng.
/// </summary>
/// <param name="IsAvailable">winget.exe có tồn tại và chạy được hay không.</param>
/// <param name="Version">Phiên bản winget, ví dụ "v1.9.25200".</param>
/// <param name="ErrorMessage">Lý do không dùng được (hiển thị kèm hướng dẫn cài App Installer).</param>
public sealed record WingetAvailability(bool IsAvailable, string? Version, string? ErrorMessage)
{
    public static WingetAvailability Available(string version) => new(true, version, null);

    public static WingetAvailability NotAvailable(string errorMessage) => new(false, null, errorMessage);
}
