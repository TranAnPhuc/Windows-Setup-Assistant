namespace WindowsSetupAssistant.Domain.Models;

/// <summary>
/// Một dòng kết quả đọc được từ bảng text của WinGet
/// (dùng chung cho "winget search" và "winget list").
/// </summary>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="PackageId">Package Id, ví dụ "Google.Chrome".</param>
/// <param name="Version">Phiên bản (với "winget list" là phiên bản đang cài).</param>
/// <param name="AvailableVersion">Phiên bản mới hơn nếu WinGet phát hiện (cột "Available").</param>
/// <param name="Source">Nguồn, ví dụ "winget" hoặc "msstore". Rỗng nếu gói không đến từ WinGet.</param>
public sealed record WingetPackageInfo(
    string Name,
    string PackageId,
    string Version,
    string? AvailableVersion = null,
    string? Source = null)
{
    public bool HasUpdate => !string.IsNullOrWhiteSpace(AvailableVersion);
}
