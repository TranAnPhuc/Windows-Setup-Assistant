using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Ghi báo cáo ra file JSON. KHÔNG ném lỗi ra ngoài: ghi báo cáo thất bại không được phép
/// làm đổi mã thoát của lượt cài - phần mềm đã cài xong vẫn là đã cài xong.
/// </summary>
public static class UnattendedReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(UnattendedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return JsonSerializer.Serialize(report, Options);
    }

    /// <summary>Đường dẫn mặc định: Reports\unattended-yyyyMMdd-HHmmss.json cạnh file .exe.</summary>
    public static string DefaultPath(DateTimeOffset timestamp) => Path.Combine(
        AppContext.BaseDirectory,
        "Reports",
        $"unattended-{timestamp.LocalDateTime:yyyyMMdd-HHmmss}.json");

    public static bool TryWrite(UnattendedReport report, string path, out string? error)
    {
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, Serialize(report));
            error = null;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            error = exception.Message;
            return false;
        }
    }
}
