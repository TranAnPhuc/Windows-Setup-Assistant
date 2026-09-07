using System.Text;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Infrastructure.Persistence;

namespace WindowsSetupAssistant.Tests.Persistence;

public class BackupExporterTests
{
    [Fact]
    public async Task ExportAsync_WritesJsonAndUtf8BomCsv()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var profile = new InstallationProfile { Name = "Máy cũ", ManualSoftware = [new("A, B", "ARP\\A", "1.0", null, InstalledSoftwareKind.ManualOnly)] };
            var paths = await new BackupExporter().ExportAsync(profile, Path.Combine(dir.FullName, "backup.json"), 3);
            Assert.True(File.Exists(paths.JsonPath));
            Assert.True(File.Exists(paths.CsvPath));
            var bytes = await File.ReadAllBytesAsync(paths.CsvPath);
            Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            var csv = Encoding.UTF8.GetString(bytes);
            Assert.Contains("sep=,", csv);
            Assert.Contains("\"A, B\"", csv);
            var loaded = await new JsonProfileRepository(paths.JsonPath).LoadAsync();
            Assert.Equal(3, loaded.SchemaVersion);
            Assert.Single(loaded.Profiles[0].ManualSoftware);
        }
        finally { Directory.Delete(dir.FullName, true); }
    }
}
