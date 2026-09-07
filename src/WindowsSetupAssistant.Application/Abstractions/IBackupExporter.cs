using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Entities;

namespace WindowsSetupAssistant.Application.Abstractions;

public interface IBackupExporter
{
    Task<BackupPaths> ExportAsync(InstallationProfile profile, string jsonPath, int schemaVersion = 1,
        CancellationToken cancellationToken = default);
}
