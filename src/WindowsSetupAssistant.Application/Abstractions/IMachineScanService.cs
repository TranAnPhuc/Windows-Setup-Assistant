using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Application.Abstractions;

public interface IMachineScanService
{
    Task<MachineSnapshot> ScanAsync(CancellationToken cancellationToken = default);
}
