using WindowsSetupAssistant.Domain.Entities;

namespace WindowsSetupAssistant.Application.Abstractions;

/// <summary>
/// Đọc/ghi danh sách phần mềm ra file JSON nằm cạnh ứng dụng (Data/software-list.json).
/// </summary>
public interface IProfileRepository
{
    /// <summary>Đường dẫn tuyệt đối tới file dữ liệu, hiển thị cho người dùng biết dữ liệu nằm ở đâu.</summary>
    string DataFilePath { get; }

    /// <summary>Đọc catalog. Nếu file chưa tồn tại hoặc hỏng thì trả về dữ liệu mẫu mặc định.</summary>
    Task<SoftwareCatalog> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Ghi catalog xuống đĩa một cách an toàn (ghi file tạm rồi thay thế).</summary>
    Task SaveAsync(SoftwareCatalog catalog, CancellationToken cancellationToken = default);

    /// <summary>Xuất danh sách ra file JSON do người dùng chọn.</summary>
    Task ExportAsync(SoftwareCatalog catalog, string filePath, CancellationToken cancellationToken = default);

    /// <summary>Nhập catalog từ file JSON bên ngoài (không tự động ghi đè file chính).</summary>
    Task<SoftwareCatalog> ImportAsync(string filePath, CancellationToken cancellationToken = default);
}
