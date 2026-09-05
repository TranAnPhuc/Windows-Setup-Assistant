using System.Windows;
using Microsoft.Win32;
using WindowsSetupAssistant.App.ViewModels;
using WindowsSetupAssistant.App.Views;

namespace WindowsSetupAssistant.App.Services;

/// <summary>
/// Trừu tượng hoá hộp thoại để ViewModel không phải tham chiếu trực tiếp tới cửa sổ WPF.
/// Nhờ vậy ViewModel dễ đọc, dễ test và không bị lệ thuộc vào giao diện.
/// </summary>
public interface IDialogService
{
    void ShowInfo(string title, string message);

    void ShowError(string title, string message);

    bool Confirm(string title, string message);

    string? OpenJsonFile(string title);

    string? SaveJsonFile(string title, string suggestedFileName);

    bool ShowPackageEditor(PackageEditorViewModel viewModel);

    bool ShowInstallConfirmation(InstallConfirmViewModel viewModel);

    string? ShowTextInput(TextInputViewModel viewModel);
}

/// <summary>Hiện thực bằng MessageBox và các cửa sổ WPF thật.</summary>
public sealed class DialogService : IDialogService
{
    private const string JsonFilter = "Tệp JSON (*.json)|*.json|Tất cả tệp (*.*)|*.*";

    private static Window? Owner => WpfApplication.Current?.MainWindow;

    public void ShowInfo(string title, string message) =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string title, string message) =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string title, string message) =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? OpenJsonFile(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = JsonFilter,
            CheckFileExists = true
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? SaveJsonFile(string title, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = JsonFilter,
            FileName = suggestedFileName,
            DefaultExt = ".json",
            AddExtension = true
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public bool ShowPackageEditor(PackageEditorViewModel viewModel) =>
        ShowDialog(new PackageEditorWindow { DataContext = viewModel });

    public bool ShowInstallConfirmation(InstallConfirmViewModel viewModel) =>
        ShowDialog(new InstallConfirmWindow { DataContext = viewModel });

    public string? ShowTextInput(TextInputViewModel viewModel)
    {
        var window = new TextInputWindow { DataContext = viewModel };
        return ShowDialog(window) ? viewModel.Text.Trim() : null;
    }

    private static bool ShowDialog(Window window)
    {
        if (Owner is not null && !ReferenceEquals(Owner, window))
        {
            window.Owner = Owner;
        }

        return window.ShowDialog() == true;
    }
}
