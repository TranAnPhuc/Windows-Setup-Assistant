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
    public void DangKyDispatcherUnhandledExceptionTruocKhiPhanNhanhDongLenh()
    {
        var source = ReadAppSource();

        var registerIndex = source.IndexOf("DispatcherUnhandledException +=", StringComparison.Ordinal);
        var parseIndex = source.IndexOf("CommandLineParser.Parse(e.Args)", StringComparison.Ordinal);

        Assert.True(registerIndex > 0, "Phai tim thay noi dang ky DispatcherUnhandledException.");
        Assert.True(parseIndex > 0, "Phai tim thay noi goi CommandLineParser.Parse.");

        // Nhanh khong giam sat return som ngay sau khi phan nhanh dong lenh. Neu dong dang ky
        // nam SAU diem phan nhanh nay, nhanh khong giam sat se chay ma khong co trinh bat loi
        // toan cuc nao: mot ngoai le trong callback ProcessRunner hay Progress<T> (chay ngoai
        // ngan xep try/catch cua RunCommandLineAsync) se lam sap cung tien trinh, khong in loi,
        // khong tra ve ma thoat nao trong hop dong 0..4.
        Assert.True(
            registerIndex < parseIndex,
            "DispatcherUnhandledException phai duoc dang ky TRUOC khi phan nhanh dong lenh, " +
            "de ca che do GUI lan che do khong giam sat deu duoc bao ve.");
    }

    [Fact]
    public void DangKyAppDomainUnhandledExceptionTruocKhiPhanNhanhDongLenh()
    {
        var source = ReadAppSource();

        var registerIndex = source.IndexOf("AppDomain.CurrentDomain.UnhandledException +=", StringComparison.Ordinal);
        var parseIndex = source.IndexOf("CommandLineParser.Parse(e.Args)", StringComparison.Ordinal);

        Assert.True(registerIndex > 0, "Phai tim thay noi dang ky AppDomain.CurrentDomain.UnhandledException.");
        Assert.True(parseIndex > 0, "Phai tim thay noi goi CommandLineParser.Parse.");

        // Cung ly do nhu DispatcherUnhandledException o tren: day la lop bao ve cuoi cung cho
        // cac ngoai le khong dong bo (vi du tren luong nen cua tien trinh con), va nhanh khong
        // giam sat phai duoc bao ve boi no giong het nhu nhanh giao dien.
        Assert.True(
            registerIndex < parseIndex,
            "AppDomain.CurrentDomain.UnhandledException phai duoc dang ky TRUOC khi phan nhanh dong lenh.");
    }

    [Fact]
    public void MoiNhanhDeuKetThucBangShutdownCoMaThoat()
    {
        Assert.Contains("Shutdown((int)", ReadAppSource());
    }
}
