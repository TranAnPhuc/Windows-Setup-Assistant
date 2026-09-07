using System.Windows;
namespace WindowsSetupAssistant.App.Views;
public partial class ScanResultWindow : Window
{
    public ScanResultWindow() => InitializeComponent();
    private void OnSaveClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
