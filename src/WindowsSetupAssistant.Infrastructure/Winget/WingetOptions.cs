namespace WindowsSetupAssistant.Infrastructure.Winget;

/// <summary>
/// Cấu hình cho <see cref="WingetService"/>. Tách riêng để dễ chỉnh và dễ test.
/// </summary>
public sealed class WingetOptions
{
    /// <summary>Tên file thực thi. Để "winget" cho Windows tự tìm trong PATH.</summary>
    public string ExecutableName { get; init; } = "winget";

    public TimeSpan VersionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan SearchTimeout { get; init; } = TimeSpan.FromSeconds(90);

    public TimeSpan ListTimeout { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>Trình cài đặt lớn (Visual Studio, Office...) có thể chạy rất lâu.</summary>
    public TimeSpan InstallTimeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Số kết quả tìm kiếm tối đa hiển thị lên UI.</summary>
    public int MaxSearchResults { get; init; } = 50;

    /// <summary>
    /// Các nguồn được phép truyền vào tham số --source.
    /// Đây là danh sách trắng: dữ liệu người dùng không nằm trong whitelist sẽ bị ép về "winget".
    /// </summary>
    public IReadOnlyCollection<string> AllowedSources { get; init; } = new[] { "winget", "msstore" };
}
