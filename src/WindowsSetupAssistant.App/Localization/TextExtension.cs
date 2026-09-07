using System.Windows.Data;
using System.Windows.Markup;

namespace WindowsSetupAssistant.App.Localization;

/// <summary>
/// Dùng trong XAML: Content="{loc:Text Ui_ProfileNew}"
///
/// Trả về một Binding tới indexer của LocalizationSource, nhờ vậy khi đổi ngôn ngữ
/// thì chuỗi tự cập nhật mà không cần vẽ lại cửa sổ.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TextExtension : MarkupExtension
{
    public TextExtension()
    {
    }

    public TextExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationSource.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
