using WindowsSetupAssistant.Domain.Classification;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Tests.Domain;

public class InstalledSoftwareClassifierTests
{
    private static WingetPackageInfo Row(string name, string id, string? source) => new(name, id, "1.0", null, source);

    [Theory]
    [InlineData("7zip.7zip", "winget")]
    [InlineData("VNGCorp.Zalo", "winget")]
    [InlineData("9WZDNCRFHVJL", "msstore")]
    [InlineData("Notepad++.Notepad++", "WinGet")]
    public void Classify_WingetSourceWithValidId_IsWingetPackage(string id, string source) =>
        Assert.Equal(InstalledSoftwareKind.WingetPackage, InstalledSoftwareClassifier.Classify(Row("Tên", id, source)));

    [Fact]
    public void Classify_MsixPrefix_IsSystemComponent() => Assert.Equal(InstalledSoftwareKind.SystemComponent,
        InstalledSoftwareClassifier.Classify(Row("3D Viewer", @"MSIX\Microsoft.Microsoft3DViewer_7.2602.8012.0", null)));

    [Fact]
    public void Classify_MsixPrefixWinsOverWingetSource() => Assert.Equal(InstalledSoftwareKind.SystemComponent,
        InstalledSoftwareClassifier.Classify(Row("App", @"MSIX\Microsoft.Something_1.0", "winget")));

    [Fact]
    public void Classify_AppsAndFeaturesEntry_IsManualOnly() => Assert.Equal(InstalledSoftwareKind.ManualOnly,
        InstalledSoftwareClassifier.Classify(Row("Android Studio", @"ARP\Machine\X64\Android Studio", null)));

    [Fact]
    public void Classify_NoSource_IsManualOnly() => Assert.Equal(InstalledSoftwareKind.ManualOnly,
        InstalledSoftwareClassifier.Classify(Row("Nội bộ", "NoiBo.KeToan", null)));

    [Fact]
    public void Classify_UnknownSource_IsManualOnly() => Assert.Equal(InstalledSoftwareKind.ManualOnly,
        InstalledSoftwareClassifier.Classify(Row("Lạ", "Nguon.La", "nguonla")));

    [Fact]
    public void ToEntry_CopiesEveryFieldAndSetsKind()
    {
        var entry = InstalledSoftwareClassifier.ToEntry(new WingetPackageInfo("Git", "Git.Git", "2.55.0", null, "winget"));
        Assert.Equal("Git", entry.Name);
        Assert.Equal("Git.Git", entry.RawId);
        Assert.Equal("2.55.0", entry.Version);
        Assert.Equal("winget", entry.Source);
        Assert.Equal(InstalledSoftwareKind.WingetPackage, entry.Kind);
    }
}
