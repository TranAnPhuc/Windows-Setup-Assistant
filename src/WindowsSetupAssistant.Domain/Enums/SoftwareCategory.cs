namespace WindowsSetupAssistant.Domain.Enums;

/// <summary>
/// Nhóm phần mềm dùng để lọc và sắp xếp trong giao diện.
/// Giá trị được lưu xuống JSON dưới dạng chuỗi (xem JsonStringEnumConverter).
/// </summary>
public enum SoftwareCategory
{
    Browser = 0,
    Development = 1,
    Office = 2,
    Entertainment = 3,
    Utility = 4,
    Other = 5
}
