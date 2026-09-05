using WindowsSetupAssistant.App.Mvvm;

namespace WindowsSetupAssistant.App.ViewModels;

/// <summary>ViewModel cho hộp thoại nhập một dòng chữ (đặt tên / đổi tên cấu hình).</summary>
public sealed class TextInputViewModel : ObservableObject
{
    private string _text;

    public TextInputViewModel(string title, string prompt, string initialText = "")
    {
        Title = title;
        Prompt = prompt;
        _text = initialText;
    }

    public string Title { get; }

    public string Prompt { get; }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(Text);
}
