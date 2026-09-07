using WindowsSetupAssistant.Infrastructure.Persistence;
using WindowsSetupAssistant.Infrastructure.Winget;
using WindowsSetupAssistant.Tests.Fakes;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Application.Services;

namespace WindowsSetupAssistant.Tests.Services;

public class MachineScanServiceTests
{
    [Fact]
    public async Task ScanAsync_ClassifiesRowsAndPreservesRawIds()
    {
        var winget = new FakeWingetService();
        winget.InstalledRows.AddRange([
            new WingetPackageInfo("Git", "Git.Git", "2.0", null, "winget"),
            new WingetPackageInfo("Android", @"ARP\Machine\Android", "1.0", null, null),
            new WingetPackageInfo("Viewer", @"MSIX\Microsoft.Viewer", "1.0", null, null)]);
        var snapshot = await new MachineScanService(winget, new RecordingLogger()).ScanAsync();
        Assert.Equal(3, snapshot.Entries.Count);
        Assert.Equal(InstalledSoftwareKind.WingetPackage, snapshot.Entries[0].Kind);
        Assert.Equal(InstalledSoftwareKind.ManualOnly, snapshot.Entries[1].Kind);
        Assert.Equal(InstalledSoftwareKind.SystemComponent, snapshot.Entries[2].Kind);
    }
}
