using System.Windows;
using WindowsSetupAssistant.App.ViewModels;

namespace WindowsSetupAssistant.App.Views;

public partial class TextInputWindow : Window
{
    public TextInputWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is TextInputViewModel viewModel && viewModel.IsValid)
        {
            DialogResult = true;
        }
    }
}
