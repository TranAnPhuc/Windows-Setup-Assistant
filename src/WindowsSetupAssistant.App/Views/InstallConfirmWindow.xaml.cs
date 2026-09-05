using System.Windows;

namespace WindowsSetupAssistant.App.Views;

public partial class InstallConfirmWindow : Window
{
    public InstallConfirmWindow() => InitializeComponent();

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
