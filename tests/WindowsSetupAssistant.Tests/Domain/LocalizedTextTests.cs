using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.Tests.Domain;

public class LocalizedTextTests
{
    [Fact]
    public void Of_KeepsKeyAndArguments()
    {
        var text = LocalizedText.Of(MessageKeys.InstallFailed, "Google.Chrome", 5);

        Assert.Equal(MessageKeys.InstallFailed, text.Key);
        Assert.Equal(new object?[] { "Google.Chrome", 5 }, text.Arguments);
        Assert.False(text.IsRaw);
    }

    [Fact]
    public void Of_WithoutArguments_HasEmptyArgumentList()
    {
        var text = LocalizedText.Of(MessageKeys.InstallSucceeded);

        Assert.Empty(text.Arguments);
    }

    [Fact]
    public void Raw_MarksTextAsNotTranslatable()
    {
        var text = LocalizedText.Raw(@"C:\Users\test\Data\software-list.json");

        Assert.True(text.IsRaw);
        Assert.Equal(@"C:\Users\test\Data\software-list.json", text.Key);
        Assert.Empty(text.Arguments);
    }

    [Fact]
    public void Of_NullKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LocalizedText.Of(null!));
    }

    [Fact]
    public void LocalizedException_CarriesKeyAsExceptionMessage()
    {
        var text = LocalizedText.Of(MessageKeys.PackageIdInvalidCharacters);

        var error = new LocalizedException(text);

        Assert.Same(text, error.LocalizedMessage);
        Assert.Equal(MessageKeys.PackageIdInvalidCharacters, error.Message);
    }

    [Fact]
    public void MessageKeys_AreAllDistinct()
    {
        var values = typeof(MessageKeys)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(values);
        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void MessageKeys_AllStartWithMsgPrefix()
    {
        var values = typeof(MessageKeys)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

        Assert.All(values, v => Assert.StartsWith("Msg_", v, StringComparison.Ordinal));
    }
}
