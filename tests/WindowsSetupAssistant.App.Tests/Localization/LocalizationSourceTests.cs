using System.ComponentModel;
using System.Globalization;
using WindowsSetupAssistant.App.Localization;

namespace WindowsSetupAssistant.App.Tests.Localization;

public class LocalizationSourceTests
{
    [Fact]
    public void SetLanguage_RaisesIndexerPropertyChanged()
    {
        var source = LocalizationSource.Instance;
        var raised = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => raised.Add(e.PropertyName);
        source.PropertyChanged += handler;

        try
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("en"));
        }
        finally
        {
            source.PropertyChanged -= handler;
        }

        // "Item[]" la ten dac biet: WPF hieu la moi binding toi indexer deu phai cap nhat.
        Assert.Contains("Item[]", raised);
    }

    [Fact]
    public void SetLanguage_SetsAllFourCultureProperties()
    {
        var chinese = CultureInfo.GetCultureInfo("zh-Hant");

        LocalizationSource.Instance.SetLanguage(chinese);

        try
        {
            Assert.Equal(chinese, CultureInfo.CurrentUICulture);
            Assert.Equal(chinese, CultureInfo.CurrentCulture);
            Assert.Equal(chinese, CultureInfo.DefaultThreadCurrentUICulture);
            Assert.Equal(chinese, CultureInfo.DefaultThreadCurrentCulture);
        }
        finally
        {
            LocalizationSource.Instance.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        }
    }

    [Fact]
    public void SetLanguage_RaisesLanguageChangedEvent()
    {
        var source = LocalizationSource.Instance;
        CultureInfo? received = null;
        EventHandler<CultureInfo> handler = (_, culture) => received = culture;
        source.LanguageChanged += handler;

        try
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("en"));
        }
        finally
        {
            source.LanguageChanged -= handler;
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        }

        Assert.Equal("en", received?.Name);
    }

    [Fact]
    public void Indexer_ReturnsTranslationOfCurrentLanguage()
    {
        var source = LocalizationSource.Instance;

        source.SetLanguage(CultureInfo.GetCultureInfo("en"));
        var english = source["Ui_ProfileNew"];

        source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        var vietnamese = source["Ui_ProfileNew"];

        Assert.NotEqual(english, vietnamese);
    }
}
