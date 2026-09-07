using System.Globalization;
using WindowsSetupAssistant.Domain.Localization;
using WindowsSetupAssistant.Infrastructure.Logging;
using WindowsSetupAssistant.Tests.Fakes;

namespace WindowsSetupAssistant.Tests.Logging;

public class AppLoggerTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "wsa-log-" + Guid.NewGuid().ToString("N"));

    public AppLoggerTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FileLogIsAlwaysEnglishEvenWhenUiIsVietnamese()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");

        try
        {
            var localizer = new StubLocalizer();
            var logger = new AppLogger(_directory, writeToFile: true, localizer);

            logger.Information(LocalizedText.Of(MessageKeys.AppStarted));

            var text = File.ReadAllText(logger.LogFilePath!);

            // StubLocalizer tra ve "<culture>:<key>" nen kiem tra duoc culture da dung.
            Assert.Contains("en:" + MessageKeys.AppStarted, text);
            Assert.DoesNotContain("vi:" + MessageKeys.AppStarted, text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
