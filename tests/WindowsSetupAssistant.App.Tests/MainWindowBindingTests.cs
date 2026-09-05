using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsSetupAssistant.App.ViewModels;
using WindowsSetupAssistant.App.Views;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.App.Tests;

public sealed class MainWindowBindingTests
{
    [Fact]
    public Task AllMainTabsAndQueueProgressHaveCleanRuntimeBindings() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.SeedAsync();
        var vm = fixture.ViewModel;
        var gate = new TaskCompletionSource<bool>();
        var started = false;
        fixture.Winget.BeforeInstall = async (_, token) =>
        {
            started = true;
            await gate.Task.WaitAsync(token);
        };
        var progressChanges = new List<double>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ProgressValue))
                progressChanges.Add(vm.ProgressValue);
        };
        using var trace = new BindingTrace();
        var window = new MainWindow { DataContext = vm };
        try
        {
            await WpfTestHost.ShowAsync(window);
            await WpfTestHost.WaitUntilAsync(() => !vm.LoadedCommand.IsRunning);
            fixture.AssertHealthy();
            Assert.True(window.IsLoaded);
            Assert.NotNull(window.Icon);
            Assert.Equal(3, vm.TotalCount);
            vm.Search.Results.Add(new SearchResultViewModel(new WingetPackageInfo("Search item", "Test.Search", "2.0", null, "winget")));
            vm.Results.Add(new InstallationResult
            {
                PackageId = "Test.Example", DisplayName = "Example", Outcome = InstallOutcome.Failed,
                ExitCode = -1, Duration = TimeSpan.FromSeconds(2), Message = "Test result only"
            });
            fixture.Logger.Information("Binding test entry", details: "No process is started.");
            var tabs = Assert.Single(WpfTestHost.Descendants<TabControl>(window));
            for (var index = 0; index < tabs.Items.Count; index++)
            {
                tabs.SelectedIndex = index;
                window.UpdateLayout();
                await WpfTestHost.DrainAsync();
                foreach (var grid in WpfTestHost.Descendants<DataGrid>(window))
                    Assert.True(grid.IsReadOnly);
            }
            tabs.SelectedIndex = 0;
            window.UpdateLayout();
            var progress = Assert.Single(WpfTestHost.Descendants<ProgressBar>(window));
            var binding = BindingOperations.GetBinding(progress, RangeBase.ValueProperty);
            Assert.NotNull(binding);
            Assert.Equal(nameof(MainViewModel.ProgressValue), binding.Path.Path);
            Assert.Equal(BindingMode.OneWay, binding.Mode);
            Assert.True(typeof(MainViewModel).GetProperty(nameof(MainViewModel.ProgressValue))!.SetMethod!.IsPrivate);

            vm.SelectedPackage = vm.Packages[0];
            vm.InstallSelectedCommand.Execute(null);
            await WpfTestHost.WaitUntilAsync(() => started);
            await WpfTestHost.DrainAsync();
            Assert.True(vm.IsInstalling);
            Assert.True(vm.CancelInstallCommand.CanExecute(null));
            Assert.False(vm.AddPackageCommand.CanExecute(null));
            Assert.False(vm.EditPackageCommand.CanExecute(null));
            Assert.False(vm.DeletePackageCommand.CanExecute(null));
            Assert.False(vm.ImportCommand.CanExecute(null));
            Assert.False(vm.NewProfileCommand.CanExecute(null));
            Assert.False(vm.SelectAllCommand.CanExecute(null));
            Assert.False(vm.MoveDownCommand.CanExecute(null));
            Assert.False(vm.Search.SearchCommand.CanExecute(null));
            Assert.False(vm.Search.AddCommand.CanExecute(vm.Search.Results[0]));
            foreach (var checkbox in WpfTestHost.Descendants<CheckBox>(window)
                         .Where(c => c.DataContext is SoftwarePackageViewModel))
                Assert.False(checkbox.IsEnabled);

            gate.TrySetResult(true);
            await WpfTestHost.WaitUntilAsync(() => !vm.InstallSelectedCommand.IsRunning);
            await WpfTestHost.DrainAsync();
            Assert.Equal(100d, vm.ProgressValue);
            Assert.Equal(100d, progress.Value);
            Assert.Contains(100d, progressChanges);
            Assert.Equal(3, vm.Results.Count);
            Assert.Equal(new[] { "Test.Alpha", "Test.Beta", "Test.Gamma" }, fixture.Winget.InstallCalls);
            Assert.True(vm.AddPackageCommand.CanExecute(null));
            Assert.False(vm.CancelInstallCommand.CanExecute(null));
            fixture.AssertHealthy();
            trace.AssertClean();
        }
        finally
        {
            gate.TrySetResult(true);
            await WpfTestHost.WaitUntilAsync(() => !vm.InstallSelectedCommand.IsRunning);
            window.Close();
            await WpfTestHost.WaitUntilAsync(() => !window.IsVisible);
        }
    });

    [Fact]
    public Task ProfileComboBoxDisplaysProfileNameNotObjectTypeName() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.SeedAsync();
        using var trace = new BindingTrace();
        var window = new MainWindow { DataContext = fixture.ViewModel };
        try
        {
            await WpfTestHost.ShowAsync(window);
            await WpfTestHost.WaitUntilAsync(() => !fixture.ViewModel.LoadedCommand.IsRunning);

            Assert.Equal(980d, window.MinWidth);
            Assert.Equal(620d, window.MinHeight);

            var combo = Assert.Single(WpfTestHost.Descendants<ComboBox>(window)
                .Where(c => c.ItemsSource == fixture.ViewModel.Profiles));
            Assert.NotNull(combo.SelectedItem);

            var activeProfile = Assert.IsType<Domain.Entities.InstallationProfile>(combo.SelectedItem);
            Assert.Equal("Personal test", activeProfile.Name);
            Assert.Equal("Personal test", activeProfile.ToString());

            // Switch to another profile
            fixture.ViewModel.SelectedProfile = fixture.ViewModel.Profiles[1];
            window.UpdateLayout();
            await WpfTestHost.DrainAsync();
            Assert.Equal("Developer test", fixture.ViewModel.SelectedProfile.Name);
            Assert.Equal("Developer test", fixture.ViewModel.SelectedProfile.ToString());

            var textBlocks = WpfTestHost.Descendants<TextBlock>(combo).ToList();
            Assert.DoesNotContain(textBlocks, tb => tb.Text.Contains("WindowsSetupAssistant.Domain"));

            fixture.AssertHealthy();
            trace.AssertClean();
        }
        finally
        {
            window.Close();
            await WpfTestHost.WaitUntilAsync(() => !window.IsVisible);
        }
    });

    [Theory]
    [InlineData("package")]
    [InlineData("install")]
    [InlineData("text")]
    public Task DialogsEvaluateBindingsAndEditablePropertiesWithoutWarnings(string kind) => WpfTestHost.Run(async () =>
    {
        using var trace = new BindingTrace();
        var package = new SoftwarePackageViewModel(ViewModelFixture.Package("Example", "Test.Example", 0));
        Window window = kind switch
        {
            "package" => new PackageEditorWindow { DataContext = new PackageEditorViewModel(package.Model) },
            "install" => new InstallConfirmWindow
            {
                DataContext = new InstallConfirmViewModel(new[] { package }, ExistingPackageAction.Skip)
            },
            _ => new TextInputWindow { DataContext = new TextInputViewModel("Test title", "Test prompt", "Original") }
        };
        try
        {
            await WpfTestHost.ShowAsync(window);
            Assert.True(window.IsLoaded);
            if (window.DataContext is PackageEditorViewModel editor)
            {
                var nameBox = WpfTestHost.Descendants<TextBox>(window).Single(box =>
                    BindingOperations.GetBinding(box, TextBox.TextProperty)?.Path.Path == nameof(editor.Name));
                nameBox.Text = "Edited in WPF";
                nameBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal("Edited in WPF", editor.Name);
                Assert.True(editor.Validate());
            }
            else if (window.DataContext is InstallConfirmViewModel confirm)
            {
                var radio = WpfTestHost.Descendants<RadioButton>(window).Single(button =>
                    BindingOperations.GetBinding(button, ToggleButton.IsCheckedProperty)?.Path.Path == nameof(confirm.UpgradeExisting));
                radio.IsChecked = true;
                radio.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                Assert.Equal(ExistingPackageAction.Upgrade, confirm.SelectedAction);
            }
            else if (window.DataContext is TextInputViewModel input)
            {
                var inputBox = Assert.Single(WpfTestHost.Descendants<TextBox>(window));
                inputBox.Text = "New profile name";
                inputBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal("New profile name", input.Text);
                Assert.True(input.IsValid);
            }
            await WpfTestHost.DrainAsync();
            trace.AssertClean();
        }
        finally
        {
            window.Close();
            await WpfTestHost.DrainAsync();
        }
    });

    [Fact]
    public Task EmbeddedIconContainsExpectedSizesAndTransparency() => WpfTestHost.Run(() =>
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/WindowsSetupAssistant;component/Assets/AppIcon.ico"));
        Assert.NotNull(resource);
        using var stream = resource.Stream;
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.Equal(new[] { 16, 24, 32, 48, 64, 128, 256 }, decoder.Frames.Select(f => f.PixelWidth).OrderBy(size => size));
        foreach (var frame in decoder.Frames)
        {
            Assert.Equal(frame.PixelWidth, frame.PixelHeight);
            var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            Assert.Equal(0, pixels[3]);
            Assert.Contains(Enumerable.Range(0, bitmap.PixelWidth * bitmap.PixelHeight), pixel => pixels[pixel * 4 + 3] > 0);
        }
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(980d, 620d, 96d)]
    [InlineData(1200d, 780d, 120d)]
    [InlineData(1920d, 1080d, 144d)]
    public Task LightAndDarkLayoutsRenderAtRepresentativeSizesAndSimulatedDpi(double width, double height, double dpi) =>
        WpfTestHost.Run(async () =>
        {
            await using var fixture = new ViewModelFixture();
            await fixture.SeedAsync();
            using var trace = new BindingTrace();
            var window = new MainWindow { DataContext = fixture.ViewModel, Width = width, Height = height };
            try
            {
                await WpfTestHost.ShowAsync(window);
                await WpfTestHost.WaitUntilAsync(() => !fixture.ViewModel.LoadedCommand.IsRunning);
                for (var theme = 0; theme < 2; theme++)
                {
                    if (theme == 1)
                        fixture.ViewModel.ToggleThemeCommand.Execute(null);
                    window.UpdateLayout();
                    await WpfTestHost.DrainAsync();
                    var content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                    Assert.True(content.ActualWidth > 0 && content.ActualHeight > 0);
                    Assert.True(content.ActualWidth <= window.ActualWidth);
                    Assert.True(content.ActualHeight <= window.ActualHeight);
                    var render = new RenderTargetBitmap((int)Math.Ceiling(width * dpi / 96),
                        (int)Math.Ceiling(height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
                    render.Render(content);
                    Assert.True(render.PixelWidth > 0 && render.PixelHeight > 0);
                    foreach (var element in WpfTestHost.Descendants<FrameworkElement>(content))
                    {
                        Assert.True(double.IsFinite(element.ActualWidth) && element.ActualWidth >= 0);
                        Assert.True(double.IsFinite(element.ActualHeight) && element.ActualHeight >= 0);
                    }
                }
                fixture.AssertHealthy();
                trace.AssertClean();
            }
            finally
            {
                window.Close();
                await WpfTestHost.WaitUntilAsync(() => !window.IsVisible);
            }
        });
}
