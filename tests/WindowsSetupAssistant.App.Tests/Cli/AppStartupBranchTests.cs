using System.IO;
using System.Reflection;
using WindowsSetupAssistant.App.Cli;

namespace WindowsSetupAssistant.App.Tests.Cli;

/// <summary>
/// Canh giu cac quyet dinh khoi dong ma neu lam sai se chi lo ra khi chay ban publish that.
/// </summary>
public class AppStartupBranchTests
{
    private static string ReadAppSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return File.ReadAllText(Path.Combine(
            directory!.FullName, "src", "WindowsSetupAssistant.App", "App.xaml.cs"));
    }

    [Fact]
    public void OnStartupPhanNhanhTheoThamSoDongLenh()
    {
        var source = ReadAppSource();

        Assert.Contains("CommandLineParser.Parse(e.Args)", source);
    }

    [Fact]
    public void CheDoKhongGiamSatDatShutdownModeTuongMinh()
    {
        // Khong co cua so nao, nen de mac dinh OnLastWindowClose thi tien trinh co the
        // tu thoat truoc khi cai xong.
        Assert.Contains("ShutdownMode.OnExplicitShutdown", ReadAppSource());
    }

    [Fact]
    public void KhongHienMessageBoxKhiChayKhongGiamSat()
    {
        var source = ReadAppSource();
        var handlerIndex = source.IndexOf("OnDispatcherUnhandledException", StringComparison.Ordinal);

        Assert.True(handlerIndex > 0);

        // Trinh xu ly loi phai kiem tra co che do truoc khi nghi den viec hien hop thoai.
        var handler = source[handlerIndex..];
        var messageBoxIndex = handler.IndexOf("MessageBox.Show", StringComparison.Ordinal);
        var guardIndex = handler.IndexOf("_unattendedOutput", StringComparison.Ordinal);

        Assert.True(guardIndex > 0, "Trinh xu ly loi phai biet dang chay o che do khong giam sat.");
        Assert.True(guardIndex < messageBoxIndex, "Phai kiem tra che do TRUOC khi hien MessageBox.");
    }

    [Fact]
    public void CoDangKyCtrlC()
    {
        Assert.Contains("CancelKeyPress", ReadAppSource());
    }

    [Fact]
    public void MoiNhanhDeuKetThucBangShutdownCoMaThoat()
    {
        Assert.Contains("Shutdown((int)", ReadAppSource());
    }
}
