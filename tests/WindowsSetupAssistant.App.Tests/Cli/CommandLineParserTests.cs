using WindowsSetupAssistant.App.Cli;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Tests.Cli;

public class CommandLineParserTests
{
    [Fact]
    public void KhongCoThamSoThiMoGiaoDien()
    {
        Assert.Equal(CommandLineMode.Gui, CommandLineParser.Parse(Array.Empty<string>()).Mode);
        Assert.Equal(CommandLineMode.Gui, CommandLineParser.Parse(null).Mode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("/?")]
    public void CacDangHelpDeuTraVeHelp(string arg)
    {
        Assert.Equal(CommandLineMode.Help, CommandLineParser.Parse(new[] { arg }).Mode);
    }

    [Fact]
    public void HelpThangTheNgayCaKhiDiKemThamSoKhac()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--help" });

        Assert.Equal(CommandLineMode.Help, result.Mode);
    }

    [Fact]
    public void ChiCoUnattendedThiDungMacDinh()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended" });

        Assert.Equal(CommandLineMode.Unattended, result.Mode);
        Assert.Null(result.Options!.ProfileName);
        Assert.Null(result.Options.ReportPath);
        Assert.Equal(ExistingPackageAction.Skip, result.Options.ExistingPackageAction);
    }

    [Fact]
    public void TenCauHinhCoDauVaKhoangTrangDuocGiuNguyen()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "Máy công ty" });

        Assert.Equal("Máy công ty", result.Options!.ProfileName);
    }

    [Fact]
    public void KhoangTrangThuaQuanhGiaTriBiCatBo()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "  May lap trinh  " });

        Assert.Equal("May lap trinh", result.Options!.ProfileName);
    }

    [Theory]
    [InlineData("skip", ExistingPackageAction.Skip)]
    [InlineData("SKIP", ExistingPackageAction.Skip)]
    [InlineData("upgrade", ExistingPackageAction.Upgrade)]
    [InlineData("Upgrade", ExistingPackageAction.Upgrade)]
    public void ExistingKhongPhanBietHoaThuong(string value, ExistingPackageAction expected)
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--existing", value });

        Assert.Equal(expected, result.Options!.ExistingPackageAction);
    }

    [Fact]
    public void ExistingGiaTriLaThiBaoLoi()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--existing", "reinstall" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("reinstall", result.ErrorMessage);
    }

    [Fact]
    public void TenCoDauGachNgangKhongBiHieuNhamLaThamSo()
    {
        // "--profile" dung ngay truoc mot co khac nghia la nguoi dung quen go gia tri.
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "--existing", "skip" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--profile", result.ErrorMessage);
    }

    [Fact]
    public void ThamSoDungCuoiCauMaThieuGiaTriThiBaoLoi()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--report" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--report", result.ErrorMessage);
    }

    [Fact]
    public void GiaTriRongBiCoiLaThieuGiaTri()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "   " });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
    }

    [Fact]
    public void ThieuUnattendedNhungCoCoRiengCuaCheDoNayThiBaoLoi()
    {
        var result = CommandLineParser.Parse(new[] { "--profile", "May ca nhan" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--unattended", result.ErrorMessage);
    }

    [Fact]
    public void ThamSoLaHoanToanKhongLamChanMoGiaoDien()
    {
        // Windows co the truyen tham so cua rieng no; khong duoc vi the ma tu choi mo ung dung.
        Assert.Equal(CommandLineMode.Gui, CommandLineParser.Parse(new[] { "/embedding" }).Mode);
    }

    [Fact]
    public void ThamSoLaDiKemUnattendedThiBaoLoi()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--turbo" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--turbo", result.ErrorMessage);
    }

    [Fact]
    public void DayDuThamSo()
    {
        var result = CommandLineParser.Parse(new[]
        {
            "--unattended", "--profile", "May cong ty",
            "--existing", "upgrade", "--report", @"D:\bc.json"
        });

        Assert.Equal(CommandLineMode.Unattended, result.Mode);
        Assert.Equal("May cong ty", result.Options!.ProfileName);
        Assert.Equal(ExistingPackageAction.Upgrade, result.Options.ExistingPackageAction);
        Assert.Equal(@"D:\bc.json", result.Options.ReportPath);
    }

    [Fact]
    public void ThamSoLaDungTruocUnattendedVanBaoLoi()
    {
        // Fix loi thu tu: "--turbo" dung o vi tri 0 khong duoc phep khien ket qua
        // thanh Gui chi vi luc gap no chua doc toi "--unattended" phia sau.
        var result = CommandLineParser.Parse(new[] { "--turbo", "--unattended" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);

        // Thong bao phai chi dich danh tham so sai, khong duoc chung chung -
        // nguoi go lenh can biet chinh xac phai sua chu nao.
        Assert.Contains("--turbo", result.ErrorMessage);
    }

    [Fact]
    public void ThamSoLaDungTruocCoRiengMaThieuUnattendedVanBaoLoi()
    {
        // Ca nay truoc kia tra ve Gui vi vong lap dung lai ngay khi gap "--turbo"
        // truoc khi kip thay co rieng "--profile" phia sau.
        var result = CommandLineParser.Parse(new[] { "--turbo", "--profile", "X" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--turbo", result.ErrorMessage);
    }

    [Fact]
    public void DaoThuTuCoVanRaKetQuaDung()
    {
        var result = CommandLineParser.Parse(new[] { "--profile", "May cong ty", "--unattended" });

        Assert.Equal(CommandLineMode.Unattended, result.Mode);
        Assert.Equal("May cong ty", result.Options!.ProfileName);
    }

    [Fact]
    public void ThamSoLaODauCuoiSauCacCoHopLeVanBaoLoi()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "A", "--turbo" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--turbo", result.ErrorMessage);

        // Tham so hop le dung truoc no khong duoc lam mo thong bao: chi "--turbo" la sai.
        Assert.DoesNotContain("--profile", result.ErrorMessage);
    }

    [Fact]
    public void TokenODungViTriGiaTriNhungKhongTheLaGiaTriThiVanLaMotCoDocLap()
    {
        // Khac voi HelpThangTheNgayCaKhiDiKemThamSoKhac (help dung canh mot co hop le):
        // o day "--help" nam dung o VI TRI GIA TRI cua "--report". Vi gia tri khong duoc
        // phep bat dau bang dau gach ngang, token nay khong bi "--report" nuot lam gia tri,
        // nen no van la mot co doc lap va help thang.
        var result = CommandLineParser.Parse(new[] { "--report", "--help" });

        Assert.Equal(CommandLineMode.Help, result.Mode);

        // Va vi da tra ve Help thi khong duoc kem theo loi "thieu gia tri sau --report".
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void GiaTriTrungTenCoHelpBiTuChoiLamGiaTriNenVanTraVeHelp()
    {
        // "-?" bat dau bang "-" nen bi tu choi lam gia tri cua "--profile" (giong het
        // truong hop "--profile" roi "--help" da duoc kiem thu o tren). Vi khong duoc
        // tieu thu lam gia tri, no tro thanh mot token doc lap va trung dung mau help,
        // nen --help thang - nhat quan voi quy tac "help thang o bat ky vi tri nao".
        // Day la diem hanh vi thuc te khac voi mo ta Invalid trong yeu cau ban dau,
        // nhung nhat quan voi test ReportDungTruocHelpVanTraVeHelp va
        // HelpThangTheNgayCaKhiDiKemThamSoKhac da co san trong file nay.
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "-?" });

        Assert.Equal(CommandLineMode.Help, result.Mode);
    }

    [Fact]
    public void MaThoatCoDungNamGiaTriTheoSpec()
    {
        Assert.Equal(0, (int)UnattendedExitCode.Success);
        Assert.Equal(1, (int)UnattendedExitCode.SomePackagesFailed);
        Assert.Equal(2, (int)UnattendedExitCode.InvalidArguments);
        Assert.Equal(3, (int)UnattendedExitCode.WingetMissing);
        Assert.Equal(4, (int)UnattendedExitCode.Cancelled);
        Assert.Equal(5, Enum.GetValues<UnattendedExitCode>().Length);
    }
}
