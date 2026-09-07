using WindowsSetupAssistant.Infrastructure.Winget;

namespace WindowsSetupAssistant.Tests.Winget;

/// <summary>
/// Test cho bộ phân tích bảng text của WinGet.
/// Dữ liệu mẫu bên dưới được chép nguyên văn từ output thật của winget 1.29.
/// </summary>
public class WingetOutputParserTests
{
    private static string MixedListOutput =>
        "Name".PadRight(28) + "Id".PadRight(62) + "Version".PadRight(18) + "Source\n" +
        new string('-', 114) + "\n" +
        "7-Zip 17.00 beta (x64)".PadRight(28) + "7zip.7zip".PadRight(62) + "17.00".PadRight(18) + "winget\n" +
        "Android Studio".PadRight(28) + @"ARP\Machine\X64\Android Studio".PadRight(62) + "2026.1".PadRight(18) + "\n" +
        "3D Viewer".PadRight(28) + @"MSIX\Microsoft.Microsoft3DViewer_7.2602.8012.0".PadRight(62) + "7.2602.8012.0".PadRight(18) + "\n";

    [Fact]
    public void ParseTable_KeepsAppsAndFeaturesRow()
    {
        var packages = WingetOutputParser.ParseTable(MixedListOutput);
        var entry = Assert.Single(packages, p => p.Name == "Android Studio");
        Assert.Equal(@"ARP\Machine\X64\Android Studio", entry.PackageId);
        Assert.Equal("2026.1", entry.Version);
    }

    [Fact]
    public void ParseTable_KeepsMsixRow() => Assert.Contains(
        WingetOutputParser.ParseTable(MixedListOutput), p => p.PackageId.StartsWith(@"MSIX\", StringComparison.Ordinal));

    [Fact]
    public void ParseTable_StillReadsNormalWingetRow()
    {
        var sevenZip = Assert.Single(WingetOutputParser.ParseTable(MixedListOutput), p => p.PackageId == "7zip.7zip");
        Assert.Equal("17.00", sevenZip.Version);
        Assert.Equal("winget", sevenZip.Source);
    }

    [Fact]
    public void ParseTable_SummaryLineIsStillRejected()
    {
        var packages = WingetOutputParser.ParseTable(MixedListOutput + "2 upgrades available.\n");
        Assert.Equal(3, packages.Count);
        Assert.DoesNotContain(packages, p => p.Name.Contains("upgrades available"));
    }
    // Output thật của: winget search "Google Chrome" --source winget
    private const string SearchOutput =
        "Name                                   Id                                 Version        Match\n" +
        "-------------------------------------------------------------------------------------------------------------------\n" +
        "Google Chrome Beta (EXE)               Google.Chrome.Beta.EXE             153.0.8010.5   ProductCode: google chrome\n" +
        "Google Chrome Canary                   Google.Chrome.Canary               155.0.8042.0   ProductCode: google chrome\n" +
        "Google Chrome                          Google.Chrome                      152.0.7977.83  \n" +
        "Google Chrome OS Readiness Tool Bundle Google.ChromeOSReadinessToolBundle 1.0.4.0        \n";

    // Output thật của: winget list --id Git.Git --exact
    private const string ListSingleOutput =
        "Name Id      Version  Source\n" +
        "-----------------------------\n" +
        "Git  Git.Git 2.55.0.5 winget\n";

    private const string NoResultsOutput = "No installed package found matching input criteria.\n";

    [Fact]
    public void ParseTable_DocSearchOutput_ReadsEveryRow()
    {
        var packages = WingetOutputParser.ParseTable(SearchOutput);

        Assert.Equal(4, packages.Count);
        Assert.Equal("Google Chrome Beta (EXE)", packages[0].Name);
        Assert.Equal("Google.Chrome.Beta.EXE", packages[0].PackageId);
        Assert.Equal("153.0.8010.5", packages[0].Version);
    }

    [Fact]
    public void ParseTable_RowWithEmptyLastColumn_StillParsed()
    {
        var packages = WingetOutputParser.ParseTable(SearchOutput);

        var chrome = packages.Single(p => p.PackageId == "Google.Chrome");

        Assert.Equal("Google Chrome", chrome.Name);
        Assert.Equal("152.0.7977.83", chrome.Version);
    }

    [Fact]
    public void ParseTable_NameContainingSpaces_IsNotSplit()
    {
        // Đây là lý do không được dùng Split(' '): tên có nhiều khoảng trắng.
        var packages = WingetOutputParser.ParseTable(SearchOutput);

        Assert.Contains(packages, p => p.Name == "Google Chrome OS Readiness Tool Bundle");
    }

    [Fact]
    public void ParseTable_ListOutput_ReadsSourceColumn()
    {
        var packages = WingetOutputParser.ParseTable(ListSingleOutput);

        var git = Assert.Single(packages);
        Assert.Equal("Git.Git", git.PackageId);
        Assert.Equal("2.55.0.5", git.Version);
        Assert.Equal("winget", git.Source);
    }

    [Fact]
    public void ParseTable_ListWithAvailableColumn_DetectsUpdate()
    {
        var output =
            "Name".PadRight(20) + "Id".PadRight(22) + "Version".PadRight(11) + "Available".PadRight(11) + "Source\n" +
            new string('-', 75) + "\n" +
            "Git".PadRight(20) + "Git.Git".PadRight(22) + "2.50.0".PadRight(11) + "2.55.0.5".PadRight(11) + "winget\n" +
            "Mozilla Firefox".PadRight(20) + "Mozilla.Firefox".PadRight(22) + "140.0".PadRight(11) + new string(' ', 11) + "winget\n";

        var packages = WingetOutputParser.ParseTable(output);

        Assert.Equal(2, packages.Count);

        var git = packages[0];
        Assert.True(git.HasUpdate);
        Assert.Equal("2.55.0.5", git.AvailableVersion);
        Assert.Equal("winget", git.Source);

        var firefox = packages[1];
        Assert.False(firefox.HasUpdate);
        Assert.Null(firefox.AvailableVersion);
        Assert.Equal("winget", firefox.Source);
    }

    [Fact]
    public void ParseTable_TrailingSummaryLine_IsIgnored()
    {
        var output = ListSingleOutput + "2 upgrades available.\n";

        var packages = WingetOutputParser.ParseTable(output);

        Assert.Single(packages);
        Assert.Equal("Git.Git", packages[0].PackageId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(NoResultsOutput)]
    [InlineData("No package found matching input criteria.")]
    public void ParseTable_WithoutTable_ReturnsEmpty(string? output)
    {
        Assert.Empty(WingetOutputParser.ParseTable(output));
    }

    [Fact]
    public void ContainsPackageId_IsCaseInsensitive()
    {
        Assert.True(WingetOutputParser.ContainsPackageId(ListSingleOutput, "git.git"));
        Assert.True(WingetOutputParser.ContainsPackageId(ListSingleOutput, "Git.Git"));
        Assert.True(WingetOutputParser.ContainsPackageId(ListSingleOutput, "  Git.Git  "));
    }

    [Fact]
    public void ContainsPackageId_PartialMatch_ReturnsFalse()
    {
        // "Git" là tiền tố của "Git.Git" nhưng không phải cùng một gói.
        Assert.False(WingetOutputParser.ContainsPackageId(ListSingleOutput, "Git"));
        Assert.False(WingetOutputParser.ContainsPackageId(ListSingleOutput, "Microsoft.Git"));
        Assert.False(WingetOutputParser.ContainsPackageId(NoResultsOutput, "Git.Git"));
    }

    [Fact]
    public void StripProgressNoise_RemovesSpinnerLines()
    {
        var noisy = "  -\\|/\n\nĐang tải...\n   \n██████\nHoàn tất\n";

        var cleaned = WingetOutputParser.StripProgressNoise(noisy);

        Assert.Contains("Đang tải...", cleaned);
        Assert.Contains("Hoàn tất", cleaned);
        Assert.DoesNotContain("██", cleaned);
        Assert.DoesNotContain("|", cleaned);
    }

    [Fact]
    public void ParseTable_HandlesWindowsLineEndings()
    {
        var packages = WingetOutputParser.ParseTable(ListSingleOutput.Replace("\n", "\r\n"));

        Assert.Single(packages);
    }
}
