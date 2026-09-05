using System.IO;
using System.Text.Json;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Infrastructure.Persistence;

namespace WindowsSetupAssistant.App.Services;

/// <summary>Tuỳ chọn giao diện của người dùng (không chứa thông tin nhạy cảm).</summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Light;

    public ExistingPackageAction ExistingPackageAction { get; set; } = ExistingPackageAction.Skip;

    /// <summary>Có tự kiểm tra phần mềm đã cài khi khởi động hay không.</summary>
    public bool CheckInstalledOnStartup { get; set; } = true;
}

public enum AppTheme
{
    Light = 0,
    Dark = 1
}

/// <summary>
/// Đọc/ghi Data/app-settings.json. Lỗi ở đây không bao giờ được làm sập ứng dụng:
/// thiếu file hay file hỏng thì dùng thiết lập mặc định.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "Data", "app-settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, CatalogJson.Options) ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, CatalogJson.Options));
        }
        catch (Exception)
        {
            // Không ghi được thiết lập thì bỏ qua - không ảnh hưởng chức năng chính.
        }
    }
}
