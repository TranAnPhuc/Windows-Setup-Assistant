using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Domain.Entities;

/// <summary>
/// Một phần mềm trong danh sách cài đặt của người dùng.
/// Đây là model được lưu xuống file JSON nên chỉ chứa dữ liệu bền vững,
/// không chứa trạng thái runtime (đang cài, % tiến trình...).
/// </summary>
public sealed class SoftwarePackage
{
    /// <summary>Khoá nội bộ, giúp sửa/xoá/sắp xếp an toàn kể cả khi trùng tên.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tên hiển thị, ví dụ "Google Chrome".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>WinGet Package Id, ví dụ "Google.Chrome". Bắt buộc và phải hợp lệ.</summary>
    public string PackageId { get; set; } = string.Empty;

    public SoftwareCategory Category { get; set; } = SoftwareCategory.Other;

    /// <summary>Nguồn WinGet, mặc định "winget" (kho cộng đồng chính thức).</summary>
    public string Source { get; set; } = "winget";

    /// <summary>Người dùng có chọn gói này để cài trong lượt tới hay không.</summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>Thứ tự hiển thị / thứ tự cài đặt (nhỏ hơn được cài trước).</summary>
    public int SortOrder { get; set; }

    /// <summary>Phiên bản ghi nhận lúc thêm vào danh sách (chỉ để tham khảo).</summary>
    public string? Version { get; set; }

    public string? Notes { get; set; }

    public SoftwarePackage Clone() => new()
    {
        Id = Id,
        Name = Name,
        PackageId = PackageId,
        Category = Category,
        Source = Source,
        IsSelected = IsSelected,
        SortOrder = SortOrder,
        Version = Version,
        Notes = Notes
    };
}
