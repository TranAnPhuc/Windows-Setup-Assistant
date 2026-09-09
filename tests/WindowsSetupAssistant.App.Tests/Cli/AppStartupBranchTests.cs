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
    public void DangKyCtrlCVaGoBoLaiSauKhiChayXong()
    {
        var source = ReadAppSource();

        var registerIndex = source.IndexOf("Console.CancelKeyPress +=", StringComparison.Ordinal);
        var unregisterIndex = source.IndexOf("Console.CancelKeyPress -=", StringComparison.Ordinal);

        Assert.True(registerIndex > 0, "Phai dang ky Console.CancelKeyPress de Ctrl+C huy duoc luot cai.");

        // Dang ky ma khong go bo la ro ri trinh xu ly: no giu tham chieu toi ConsoleSession
        // va CancellationTokenSource cua luot chay da ket thuc.
        Assert.True(unregisterIndex > registerIndex, "Phai go bo Console.CancelKeyPress sau khi chay xong.");

        // Go bo phai nam trong finally, neu khong thi mot ngoai le giua chung se bo qua no.
        // Tim tu khoa finally o dau dong chu khong tim chuoi "finally" bat ky, vi trong file
        // co nhung dong chu thich cung chua tu do.
        var finallyIndex = source.IndexOf("\n        finally", registerIndex, StringComparison.Ordinal);

        Assert.True(
            finallyIndex > 0 && finallyIndex < unregisterIndex,
            "Viec go bo Console.CancelKeyPress phai nam trong khoi finally.");
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
        var source = ReadAppSource();

        var runIndex = source.IndexOf("private async Task RunCommandLineAsync", StringComparison.Ordinal);

        Assert.True(runIndex > 0, "Phai tim thay RunCommandLineAsync.");

        // Nhanh khong giam sat dat ShutdownMode = OnExplicitShutdown va khong tao cua so nao,
        // nen KHONG co gi tu dong ket thuc tien trinh. Neu loi goi thoat khong nam trong
        // finally, mot ngoai le hoac mot nhanh return som se de tien trinh song mai mai
        // tren may khong co ai trong.
        // Tim tu khoa finally o dau dong chu khong tim chuoi "finally" bat ky, vi ngay trong
        // RunCommandLineAsync co dong chu thich cung chua tu do - neu khop nham vao do thi
        // test se van xanh ke ca khi loi goi thoat bi dua ra ngoai khoi finally.
        var finallyIndex = source.IndexOf("\n        finally", runIndex, StringComparison.Ordinal);

        // Ket thuc RunCommandLineAsync = khai bao thanh vien ke tiep. Can chan tren nay vi
        // chuoi "ShutdownOnce(" con khop voi chinh dong khai bao ham ShutdownOnce ben duoi;
        // khong chan thi test van xanh ke ca khi loi goi trong finally bi xoa han.
        var methodEndIndex = source.IndexOf("\n    private ", runIndex + 1, StringComparison.Ordinal);

        Assert.True(finallyIndex > runIndex, "RunCommandLineAsync phai co khoi finally.");
        Assert.True(methodEndIndex > finallyIndex, "Phai xac dinh duoc diem ket thuc RunCommandLineAsync.");

        var thanKhoiFinally = source[finallyIndex..methodEndIndex];

        Assert.Contains(
            "ShutdownOnce(",
            thanKhoiFinally);
    }

    [Fact]
    public void ChiDuyNhatMotNoiGoiShutdownTrucTiep()
    {
        // Truoc day co hai noi goi thang Shutdown((int)...): trong
        // OnDispatcherUnhandledException va trong finally cua RunCommandLineAsync. Neu ca hai
        // cung chay (vd. loi nem tu callback Progress<T>.Report, chay ngoai ngan xep
        // try/catch/finally cua RunCommandLineAsync vi duoc marshal qua
        // SynchronizationContext.Post), loi goi sau se GHI DE ma thoat cua loi goi truoc: trinh
        // bat loi toan cuc bao Shutdown(1) nhung sau do finally lai goi Shutdown(0), va script
        // goi ung dung se tuong moi thu thanh cong trong khi console da in loi ra man hinh - hong
        // am tham, con te hon ca sap han. Ma thoat la hop dong voi script ben ngoai nen phai
        // gom moi loi goi Shutdown ve dung MOT ham (ShutdownOnce) de loi goi dau tien luon thang.
        var source = ReadAppSource();
        var soLanXuatHien = System.Text.RegularExpressions.Regex.Matches(
            source, System.Text.RegularExpressions.Regex.Escape("Shutdown((int)")).Count;

        Assert.Equal(1, soLanXuatHien);
    }

    [Fact]
    public void OnDispatcherUnhandledExceptionPhaiDiQuaShutdownOnce()
    {
        // Trinh bat loi toan cuc khong duoc goi thang Shutdown((int)...) - no phai di qua
        // ShutdownOnce de dam bao "loi goi dau tien thang" duoc ap dung dong nhat, du loi
        // goi den tu day hay tu finally cua RunCommandLineAsync.
        var source = ReadAppSource();
        var handlerIndex = source.IndexOf("private void OnDispatcherUnhandledException", StringComparison.Ordinal);

        Assert.True(handlerIndex > 0, "Phai tim thay noi khai bao OnDispatcherUnhandledException.");

        var ketThucHam = source.IndexOf("\n    }", handlerIndex, StringComparison.Ordinal);
        Assert.True(ketThucHam > handlerIndex, "Phai xac dinh duoc diem ket thuc cua ham.");

        var than = source[handlerIndex..ketThucHam];

        Assert.Contains("ShutdownOnce(", than);
        Assert.DoesNotContain("Shutdown((int)", than);
    }
}
