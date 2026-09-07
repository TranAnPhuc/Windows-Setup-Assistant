using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.App.Tests.Localization;

/// <summary>
/// Đối chiếu ba file .resx với nhau và với danh sách hằng khoá.
/// Đây là lưới an toàn cho ~1170 bản dịch: thiếu một chỗ là test đỏ ngay.
/// </summary>
public class ResourceParityTests
{
    private static string LocalizationDirectory([CallerFilePath] string thisFile = "")
    {
        // <repo>/tests/WindowsSetupAssistant.App.Tests/Localization/ResourceParityTests.cs
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
        return Path.Combine(repositoryRoot, "src", "WindowsSetupAssistant.App", "Localization");
    }

    private static Dictionary<string, string> ReadResx(string fileName)
    {
        var path = Path.Combine(LocalizationDirectory(), fileName);
        Assert.True(File.Exists(path), $"Không tìm thấy {path}");

        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .ToDictionary(
                e => e.Attribute("name")!.Value,
                e => e.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static IEnumerable<string> ConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void VietnameseHasEveryKeyOfEnglish()
    {
        var english = ReadResx("Strings.resx");
        var vietnamese = ReadResx("Strings.vi.resx");

        var missing = english.Keys.Except(vietnamese.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();

        Assert.True(missing.Count == 0, "Thiếu bản dịch tiếng Việt cho: " + string.Join(", ", missing));
    }

    [Fact]
    public void TraditionalChineseHasEveryKeyOfEnglish()
    {
        var english = ReadResx("Strings.resx");
        var chinese = ReadResx("Strings.zh-Hant.resx");

        var missing = english.Keys.Except(chinese.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();

        Assert.True(missing.Count == 0, "Thiếu bản dịch tiếng Trung cho: " + string.Join(", ", missing));
    }

    [Fact]
    public void NoLanguageHasExtraKeys()
    {
        var english = ReadResx("Strings.resx");

        foreach (var fileName in new[] { "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var extra = ReadResx(fileName).Keys.Except(english.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();
            Assert.True(extra.Count == 0, $"{fileName} có khoá thừa: " + string.Join(", ", extra));
        }
    }

    [Fact]
    public void NoValueIsEmpty()
    {
        foreach (var fileName in new[] { "Strings.resx", "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var empty = ReadResx(fileName)
                .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => pair.Key)
                .OrderBy(k => k)
                .ToList();

            Assert.True(empty.Count == 0, $"{fileName} có khoá bỏ trống: " + string.Join(", ", empty));
        }
    }

    [Fact]
    public void EveryMessageKeyConstantExistsInAllLanguages()
    {
        var keys = ConstantsOf(typeof(MessageKeys)).ToList();

        foreach (var fileName in new[] { "Strings.resx", "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var resource = ReadResx(fileName);
            var missing = keys.Except(resource.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, $"{fileName} thiếu khoá MessageKeys: " + string.Join(", ", missing));
        }
    }

    [Fact]
    public void EveryUiKeyConstantExistsInAllLanguages()
    {
        var keys = ConstantsOf(typeof(UiKeys)).ToList();
        Assert.NotEmpty(keys);

        foreach (var fileName in new[] { "Strings.resx", "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var resource = ReadResx(fileName);
            var missing = keys.Except(resource.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, $"{fileName} thiếu khoá UiKeys: " + string.Join(", ", missing));
        }
    }

    [Fact]
    public void PlaceholderCountsMatchAcrossLanguages()
    {
        // Chuoi tieng Viet co {0} ma tieng Trung quen -> string.Format van chay nhung mat thong tin.
        var english = ReadResx("Strings.resx");

        foreach (var fileName in new[] { "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var other = ReadResx(fileName);

            foreach (var pair in english)
            {
                if (!other.TryGetValue(pair.Key, out var translated))
                {
                    continue;
                }

                Assert.True(
                    CountPlaceholders(pair.Value) == CountPlaceholders(translated),
                    $"{fileName}: khoá {pair.Key} có số tham số không khớp bản tiếng Anh");
            }
        }
    }

    private static int CountPlaceholders(string value)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (System.Text.RegularExpressions.Match match in
                 System.Text.RegularExpressions.Regex.Matches(value, @"\{(\d+)\}"))
        {
            found.Add(match.Groups[1].Value);
        }

        return found.Count;
    }
}
