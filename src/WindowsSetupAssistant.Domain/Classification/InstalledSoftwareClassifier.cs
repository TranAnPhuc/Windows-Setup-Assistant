using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Domain.Validation;

namespace WindowsSetupAssistant.Domain.Classification;

public static class InstalledSoftwareClassifier
{
    private static readonly string[] ReinstallableSources = ["winget", "msstore"];

    public static InstalledSoftwareKind Classify(WingetPackageInfo row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.PackageId.StartsWith(@"MSIX\", StringComparison.OrdinalIgnoreCase))
        {
            return InstalledSoftwareKind.SystemComponent;
        }

        var sourceSupported = row.Source is not null && ReinstallableSources.Contains(row.Source.Trim(), StringComparer.OrdinalIgnoreCase);
        return sourceSupported && PackageIdValidator.IsValid(row.PackageId)
            ? InstalledSoftwareKind.WingetPackage
            : InstalledSoftwareKind.ManualOnly;
    }

    public static InstalledSoftwareEntry ToEntry(WingetPackageInfo row) =>
        new(row.Name, row.PackageId, row.Version, row.Source, Classify(row));
}
