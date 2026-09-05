using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsSetupAssistant.Infrastructure.Persistence;

/// <summary>
/// Cấu hình System.Text.Json dùng chung cho toàn bộ ứng dụng.
///
/// Vì sao cần cấu hình riêng?
/// - WriteIndented: file JSON dễ đọc, dễ sửa tay và dễ so sánh khi dùng Git.
/// - JsonStringEnumConverter: enum lưu thành "Browser" thay vì 0, đọc hiểu ngay.
/// - CamelCase: đúng quy ước JSON.
/// - UnsafeRelaxedJsonEscaping: giữ nguyên tiếng Việt có dấu thay vì á...
///   ("Unsafe" ở đây chỉ có nghĩa là không escape cho HTML; ta ghi ra file nên an toàn.)
/// </summary>
public static class CatalogJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };
}
