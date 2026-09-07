using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Classification;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Application.Services;

public sealed class MachineScanService : IMachineScanService
{
    private readonly IWingetService _wingetService;
    private readonly IAppLogger _logger;

    public MachineScanService(IWingetService wingetService, IAppLogger logger)
    {
        _wingetService = wingetService ?? throw new ArgumentNullException(nameof(wingetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MachineSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = await _wingetService.GetInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
        var entries = rows.Select(InstalledSoftwareClassifier.ToEntry).ToList();
        _logger.Information($"Đã quét {entries.Count} phần mềm trên máy.");
        return new MachineSnapshot
        {
            MachineName = Environment.MachineName,
            ScannedAt = DateTimeOffset.Now,
            Entries = entries
        };
    }
}
