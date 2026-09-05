using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowsSetupAssistant.App.Mvvm;

/// <summary>
/// Lớp cơ sở cho mọi ViewModel.
///
/// Kiến thức WPF cần nhớ: giao diện chỉ tự cập nhật khi ViewModel bắn sự kiện
/// <see cref="INotifyPropertyChanged.PropertyChanged"/>. Hàm <see cref="SetProperty{T}"/>
/// gói gọn việc "gán giá trị mới + bắn sự kiện" để code property gọn hơn.
///
/// [CallerMemberName] giúp trình biên dịch tự điền tên property, không cần viết chuỗi tay
/// (viết tay dễ sai chính tả và binding sẽ im lặng không hoạt động).
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
