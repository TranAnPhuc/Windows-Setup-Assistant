using System.Text;
using System.Text.Json;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Entities;

namespace WindowsSetupAssistant.Infrastructure.Persistence;

public sealed class BackupExporter : IBackupExporter
{
    public async Task<BackupPaths> ExportAsync(InstallationProfile profile, string jsonPath, int schemaVersion = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);
        cancellationToken.ThrowIfCancellationRequested();
        var fullJson = Path.GetFullPath(jsonPath);
        var csvPath = Path.Combine(Path.GetDirectoryName(fullJson) ?? string.Empty,
            Path.GetFileNameWithoutExtension(fullJson) + "-cai-tay.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(fullJson)!);

        var catalog = new SoftwareCatalog { SchemaVersion = schemaVersion, ActiveProfileId = profile.Id, Profiles = [profile.Clone()] };
        var json = JsonSerializer.Serialize(catalog, CatalogJson.Options);
        var csv = BuildCsv(profile);
        var tempJson = fullJson + ".tmp";
        var tempCsv = csvPath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempJson, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(tempCsv, csv, new UTF8Encoding(true), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempJson, fullJson, true);
            File.Move(tempCsv, csvPath, true);
            return new BackupPaths(fullJson, csvPath);
        }
        finally
        {
            TryDelete(tempJson);
            TryDelete(tempCsv);
        }
    }

    private static string BuildCsv(InstallationProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("sep=,");
        sb.AppendLine("STT,Tên phần mềm,Phiên bản,Mã định danh,Đã cài lại (x)");
        var index = 0;
        foreach (var item in profile.ManualSoftware)
        {
            index++;
            sb.Append(index).Append(',').Append(Escape(item.Name)).Append(',').Append(Escape(item.Version))
                .Append(',').Append(Escape(item.RawId)).AppendLine(",");
        }
        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? '"' + value.Replace("\"", "\"\"") + '"' : value;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
