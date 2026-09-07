using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace WindowsSetupAssistant.App.Tests.Localization;

/// <summary>
/// Quét mã nguồn để bảo đảm chuỗi tiếng Việt không lẻn trở lại các file đã chuyển sang khoá.
/// Danh sách file được bổ sung dần theo tiến độ chuyển đổi.
/// </summary>
public class NoHardcodedTextTests
{
    /// <summary>Các file đã chuyển xong. Thêm dần khi hoàn thành từng task.</summary>
    public static readonly string[] ConvertedFiles =
    {
        @"src\WindowsSetupAssistant.App\Views\MainWindow.xaml",
        @"src\WindowsSetupAssistant.App\Views\PackageEditorWindow.xaml",
        @"src\WindowsSetupAssistant.App\Views\InstallConfirmWindow.xaml",
        @"src\WindowsSetupAssistant.App\Views\TextInputWindow.xaml",
        @"src\WindowsSetupAssistant.App\Views\ScanResultWindow.xaml",
        @"src\WindowsSetupAssistant.App\ViewModels\MainViewModel.cs",
        @"src\WindowsSetupAssistant.App\ViewModels\SearchViewModel.cs",
        @"src\WindowsSetupAssistant.App\ViewModels\SoftwarePackageViewModel.cs",
        @"src\WindowsSetupAssistant.App\ViewModels\ScanResultViewModel.cs",
        @"src\WindowsSetupAssistant.App\ViewModels\PackageEditorViewModel.cs",
        @"src\WindowsSetupAssistant.App\ViewModels\InstallConfirmViewModel.cs",
        @"src\WindowsSetupAssistant.App\ViewModels\LogViewModel.cs"
    };

    private static readonly Regex VietnameseLetters = new(
        "[àáảãạăằắẳẵặâầấẩẫậđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));

    public static TheoryData<string> Files()
    {
        var data = new TheoryData<string>();
        foreach (var file in ConvertedFiles)
        {
            data.Add(file);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void ConvertedFileHasNoVietnameseOutsideComments(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot(), relativePath);
        Assert.True(File.Exists(path), $"Không tìm thấy {path}");

        var offending = new List<string>();
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            var trimmed = line.TrimStart();

            // Chú thích được phép viết tiếng Việt.
            if (trimmed.StartsWith("<!--", StringComparison.Ordinal)
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("///", StringComparison.Ordinal)
                || trimmed.StartsWith("*", StringComparison.Ordinal))
            {
                continue;
            }

            if (VietnameseLetters.IsMatch(line))
            {
                offending.Add($"dòng {lineNumber}: {trimmed}");
            }
        }

        Assert.True(offending.Count == 0,
            $"{relativePath} còn chuỗi tiếng Việt cứng:{Environment.NewLine}{string.Join(Environment.NewLine, offending)}");
    }
}
