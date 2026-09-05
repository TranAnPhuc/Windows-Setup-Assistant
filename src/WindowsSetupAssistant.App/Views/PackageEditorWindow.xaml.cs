using System.Windows;
using WindowsSetupAssistant.App.ViewModels;

namespace WindowsSetupAssistant.App.Views;

public partial class PackageEditorWindow : Window
{
    public PackageEditorWindow() => InitializeComponent();

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Chỉ đóng hộp thoại khi dữ liệu hợp lệ; nếu sai, thông báo lỗi hiện ngay trên form.
        if (DataContext is PackageEditorViewModel viewModel && viewModel.Validate())
        {
            DialogResult = true;
        }
    }
}
