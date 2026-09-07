using System.ComponentModel;
using System.Globalization;
using WindowsSetupAssistant.Application.Abstractions;

namespace WindowsSetupAssistant.App.Localization;

/// <summary>
/// Lớp bọc singleton phục vụ binding trong XAML.
///
/// Cách hoạt động giống hệt ThemeManager tráo Light/Dark: khi đổi ngôn ngữ, bắn
/// PropertyChanged với tên đặc biệt "Item[]" - WPF hiểu là MỌI binding tới indexer
/// phải lấy giá trị mới, nên toàn bộ chuỗi trên màn hình đổi cùng lúc.
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationSource> LazyInstance = new(() => new LocalizationSource());

    private LocalizationSource()
    {
        Localizer = new ResourceStringLocalizer();
    }

    public static LocalizationSource Instance => LazyInstance.Value;

    /// <summary>Nguồn sự thật của việc dịch; ViewModel và AppLogger dùng chung đối tượng này.</summary>
    public IStringLocalizer Localizer { get; }

    public CultureInfo CurrentLanguage { get; private set; } = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Bắn sau khi đã đổi ngôn ngữ, để ViewModel tự làm mới chuỗi của mình.</summary>
    public event EventHandler<CultureInfo>? LanguageChanged;

    public string this[string key] => Localizer[key];

    public void SetLanguage(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        CurrentLanguage = culture;

        // Đặt cả bốn để chuỗi, số và ngày tháng đều theo ngôn ngữ đã chọn.
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, culture);
    }
}
