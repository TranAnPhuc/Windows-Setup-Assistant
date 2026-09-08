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
