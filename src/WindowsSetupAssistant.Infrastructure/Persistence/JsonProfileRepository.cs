using System.Text.Json;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Entities;

namespace WindowsSetupAssistant.Infrastructure.Persistence;

/// <summary>
/// Lưu danh sách phần mềm vào file JSON nằm CẠNH ứng dụng: &lt;thư mục chạy&gt;/Data/software-list.json
///
/// Nhờ vậy toàn bộ công cụ (exe + Data) có thể chép vào USB / OneDrive và dùng lại
/// trên máy vừa cài lại Windows mà không mất danh sách.
///
/// Ghi file theo kiểu "ghi tạm rồi thay thế" để không làm hỏng dữ liệu nếu mất điện giữa chừng.
/// </summary>
public sealed class JsonProfileRepository : IProfileRepository
{
    private const string DefaultFolderName = "Data";
    private const string DefaultFileName = "software-list.json";

    // Chặn ghi đồng thời từ nhiều tác vụ (ví dụ tự động lưu trong lúc người dùng bấm Lưu).
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly IAppLogger? _logger;

    public JsonProfileRepository(string? dataFilePath = null, IAppLogger? logger = null)
    {
        DataFilePath = string.IsNullOrWhiteSpace(dataFilePath)
            ? Path.Combine(AppContext.BaseDirectory, DefaultFolderName, DefaultFileName)
            : Path.GetFullPath(dataFilePath);

        _logger = logger;
    }

    public string DataFilePath { get; }

    public async Task<SoftwareCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string json;
            try
            {
                // File.Exists cũng trả false khi bị từ chối truy cập. Đọc trực tiếp
                // để phân biệt file thật sự chưa tồn tại với dữ liệu không đọc được.
                json = await File.ReadAllTextAsync(DataFilePath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                var seeded = DefaultCatalogFactory.Create();
                await SaveAsync(seeded, cancellationToken).ConfigureAwait(false);
                _logger?.Information($"Chưa có dữ liệu - đã tạo danh sách mẫu tại {DataFilePath}.");
                return seeded;
            }

            try
            {
                var catalog = JsonSerializer.Deserialize<SoftwareCatalog>(json, CatalogJson.Options);
                if (catalog is null)
                {
                    throw new JsonException("Nội dung file JSON rỗng hoặc không đúng định dạng SoftwareCatalog.");
                }

                var normalized = CatalogNormalizer.Normalize(catalog, out var removed);

                foreach (var item in removed)
                {
                    _logger?.Warning($"Bỏ qua mục có Package Id không hợp lệ: {item}");
                }

                return normalized;
            }
            catch (JsonException ex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger?.Error(
                    $"File dữ liệu không đọc được tại {DataFilePath}; giữ nguyên file cũ, không ghi đè bằng dữ liệu mẫu.",
                    details: ex.Message);
                throw;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger?.Error(
                $"Không tải được dữ liệu tại {DataFilePath}; dừng tải để tránh ghi đè dữ liệu chưa đọc được.",
                details: ex.ToString());
            throw;
        }
    }

    public async Task SaveAsync(SoftwareCatalog catalog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var directory = Path.GetDirectoryName(DataFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(catalog, CatalogJson.Options);
            var tempPath = DataFilePath + ".tmp";

            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, DataFilePath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ExportAsync(SoftwareCatalog catalog, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(catalog, CatalogJson.Options);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);

        _logger?.Information($"Đã xuất danh sách ra {filePath}.");
    }

    public async Task<SoftwareCatalog> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Không tìm thấy file cần nhập.", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

        SoftwareCatalog? catalog;

        try
        {
            catalog = JsonSerializer.Deserialize<SoftwareCatalog>(json, CatalogJson.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"File JSON không hợp lệ: {ex.Message}", ex);
        }

        if (catalog is null || catalog.Profiles is null || catalog.Profiles.Count == 0)
        {
            throw new InvalidDataException("File không chứa cấu hình nào.");
        }

        var normalized = CatalogNormalizer.Normalize(catalog, out var removed);

        foreach (var item in removed)
        {
            _logger?.Warning($"Mục bị loại khi nhập vì Package Id không hợp lệ: {item}");
        }

        _logger?.Information($"Đã nhập {normalized.Profiles.Count} cấu hình từ {filePath}.");
        return normalized;
    }
}
