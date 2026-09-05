using System.Windows;

namespace WindowsSetupAssistant.App.Services;

/// <summary>
/// Đổi giữa giao diện Sáng và Tối.
///
/// Kiến thức WPF: mọi màu sắc được khai báo trong ResourceDictionary (Themes/Light.xaml,
/// Themes/Dark.xaml). Giao diện tham chiếu màu bằng DynamicResource nên khi ta thay
/// từ điển màu ở vị trí đầu tiên, toàn bộ cửa sổ đổi màu ngay lập tức mà không cần khởi động lại.
/// </summary>
public sealed class ThemeManager
{
    private const int ThemeDictionaryIndex = 0;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public void Apply(AppTheme theme)
    {
        var uri = theme == AppTheme.Dark
            ? new Uri("/WindowsSetupAssistant;component/Themes/Dark.xaml", UriKind.Relative)
            : new Uri("/WindowsSetupAssistant;component/Themes/Light.xaml", UriKind.Relative);

        var dictionaries = WpfApplication.Current.Resources.MergedDictionaries;
        var newDictionary = new ResourceDictionary { Source = uri };

        if (dictionaries.Count > ThemeDictionaryIndex)
        {
            dictionaries[ThemeDictionaryIndex] = newDictionary;
        }
        else
        {
            dictionaries.Insert(ThemeDictionaryIndex, newDictionary);
        }

        CurrentTheme = theme;
    }

    public AppTheme Toggle()
    {
        Apply(CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
        return CurrentTheme;
    }
}
