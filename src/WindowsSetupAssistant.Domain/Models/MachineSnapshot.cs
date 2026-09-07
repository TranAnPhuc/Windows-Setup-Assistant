using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Domain.Models;

public sealed class MachineSnapshot
{
    public required string MachineName { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }
    public required IReadOnlyList<InstalledSoftwareEntry> Entries { get; init; }
    public IEnumerable<InstalledSoftwareEntry> WingetPackages => Entries.Where(e => e.Kind == InstalledSoftwareKind.WingetPackage);
    public IEnumerable<InstalledSoftwareEntry> ManualOnly => Entries.Where(e => e.Kind == InstalledSoftwareKind.ManualOnly);
    public IEnumerable<InstalledSoftwareEntry> SystemComponents => Entries.Where(e => e.Kind == InstalledSoftwareKind.SystemComponent);
}
