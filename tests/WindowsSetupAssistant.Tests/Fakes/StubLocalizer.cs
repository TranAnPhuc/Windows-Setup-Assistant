using System.Globalization;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.Tests.Fakes;

/// <summary>Trả về "<culture>:<key>" để test kiểm chứng được đã dùng ngôn ngữ nào.</summary>
internal sealed class StubLocalizer : IStringLocalizer
{
    public string this[string key] => Format(LocalizedText.Of(key));

    public string Format(LocalizedText text) => Format(text, CultureInfo.CurrentUICulture);

    public string Format(LocalizedText text, CultureInfo culture) =>
        text.IsRaw ? text.Key : $"{culture.TwoLetterISOLanguageName}:{text.Key}";
}
