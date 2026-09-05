using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsSetupAssistant.App.Converters;
using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.App.Views;
using WpfApplication = System.Windows.Application;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WindowsSetupAssistant.App.Tests;

/// <summary>
/// One STA dispatcher for this test process. Production App.OnStartup is never run:
/// no ProcessRunner/WinGet/native dialogs are constructed. Windows are laid out offscreen
/// so the real WPF templates and bindings, including DataGrid cells, are evaluated.
/// </summary>
internal static class WpfTestHost
{
    private static readonly Lazy<Task<Dispatcher>> DispatcherTask = new(StartDispatcher);

    public static async Task Run(Func<Task> test)
    {
        var dispatcher = await DispatcherTask.Value.WaitAsync(TimeSpan.FromSeconds(15));
        await dispatcher.InvokeAsync(test).Task.Unwrap().WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static Task<Dispatcher> StartDispatcher()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                // Relative theme/icon resource URIs must resolve to the actual WPF app assembly.
                if (WpfApplication.ResourceAssembly is null)
                {
                    WpfApplication.ResourceAssembly = typeof(MainWindow).Assembly;
                }
                var app = WpfApplication.Current ?? new WpfApplication { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/WindowsSetupAssistant;component/Themes/Light.xaml", UriKind.Relative)
                });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/WindowsSetupAssistant;component/Themes/Controls.xaml", UriKind.Relative)
                });
                app.Resources.Add("ResourceKeyToBrush", new ResourceKeyToBrushConverter());
                app.Resources.Add("InverseBoolean", new InverseBooleanConverter());
                app.Resources.Add("BoolToVisibility", new BoolToVisibilityConverter());
                app.Resources.Add("InverseBoolToVisibility", new InverseBoolToVisibilityConverter());
                app.Resources.Add("StringToVisibility", new StringToVisibilityConverter());
                app.Resources.Add("LogLevelToBrush", new LogLevelToBrushConverter());
                app.Resources.Add("ExitCode", new ExitCodeConverter());
                app.Resources.Add("InstallOutcome", new InstallOutcomeConverter());
                app.Resources.Add("InstallOutcomeToBrush", new InstallOutcomeToBrushConverter());
                app.Resources.Add("Duration", new WindowsSetupAssistant.App.Converters.DurationConverter());
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                ready.SetResult(Dispatcher.CurrentDispatcher);
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                ready.TrySetException(ex);
            }
        }) { IsBackground = true, Name = "WPF test dispatcher" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task;
    }

    public static async Task DrainAsync() =>
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    public static async Task ExecuteAsync(AsyncRelayCommand command)
    {
        Assert.True(command.CanExecute(null), "Command must be enabled before it is executed.");
        command.Execute(null);
        await WaitUntilAsync(() => !command.IsRunning);
        await DrainAsync();
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(10), "Timed out waiting for async WPF operation.");
            await Task.Delay(10);
        }
    }

    public static async Task ShowAsync(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = -20000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Opacity = 0;
        window.Show();
        window.UpdateLayout();
        await DrainAsync();
    }

    public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;
            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }
}

internal sealed class BindingTrace : TraceListener
{
    private readonly StringBuilder _messages = new();
    private readonly SourceLevels _previousLevel;

    public BindingTrace()
    {
        _previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        PresentationTraceSources.DataBindingSource.Listeners.Add(this);
    }

    public override void Write(string? message) => _messages.Append(message);
    public override void WriteLine(string? message) => _messages.AppendLine(message);

    public void AssertClean() => Assert.True(_messages.Length == 0, _messages.ToString());

    protected override void Dispose(bool disposing)
    {
        PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
        PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
        base.Dispose(disposing);
    }
}
