using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Domain.Models;

public sealed record InstalledSoftwareEntry(
    string Name,
    string RawId,
    string Version,
    string? Source,
    InstalledSoftwareKind Kind);
