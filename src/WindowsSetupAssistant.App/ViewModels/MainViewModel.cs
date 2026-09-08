using WindowsSetupAssistant.Domain.Localization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Globalization;
using System.Windows.Data;
using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.App.Services;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Application.Services;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Classification;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.App.ViewModels;

/// <summary>Tuỳ chọn lọc theo nhóm, có thêm mục "Tất cả".</summary>
public sealed record CategoryFilterOption(SoftwareCategory? Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// ViewModel chính - điều phối toàn bộ màn hình.
///
/// Nguyên tắc xuyên suốt: mọi thao tác chạm tới winget đều là async + CancellationToken,
/// mọi thay đổi dữ liệu đều được lưu ngay xuống Data/software-list.json.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IProfileRepository _repository;
    private readonly IWingetService _wingetService;
    private readonly InstallationQueueService _queueService;
    private readonly IAppLogger _logger;
    private readonly IDialogService _dialogService;
    private readonly ThemeManager _themeManager;
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly IMachineScanService? _scanService;
    private readonly IBackupExporter? _backupExporter;
    private readonly IStringLocalizer _localizer;

    private SoftwareCatalog _catalog = new();
    private InstallationProfile? _selectedProfile;
    private SoftwarePackageViewModel? _selectedPackage;
    private CancellationTokenSource? _installCts;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private Task? _activeQueueTask;
    private bool _catalogLoaded;
    private bool _isClosing;
    private bool _disposed;

    private bool _isLoadingCatalog;
    private bool _installedScanCompleted;
    private bool _suppressAutoSave;

    private string _filterText = string.Empty;
    private CategoryFilterOption _categoryFilter = null!;
    private IReadOnlyList<CategoryFilterOption> _categoryFilters = Array.Empty<CategoryFilterOption>();
    private string _statusMessage;
    private LocalizedText? _lastStatusText;
    private bool _isBusy;
    private bool _isInstalling;
    private double _progressValue;
    private string _progressText = string.Empty;
    private string _currentPackageText = string.Empty;
    private bool _isWingetAvailable = true;
    private string _wingetWarning = string.Empty;
    private string _dataErrorMessage = string.Empty;

    public MainViewModel(
        IProfileRepository repository,
        IWingetService wingetService,
        InstallationQueueService queueService,
        IAppLogger logger,
        IDialogService dialogService,
        ThemeManager themeManager,
        SettingsStore settingsStore,
        AppSettings settings,
        LogViewModel logViewModel) : this(repository, wingetService, queueService, null, null, logger, dialogService, themeManager, settingsStore, settings, logViewModel, LocalizationSource.Instance.Localizer)
    {
    }

    public MainViewModel(
        IProfileRepository repository,
        IWingetService wingetService,
        InstallationQueueService queueService,
        IMachineScanService? scanService,
        IBackupExporter? backupExporter,
        IAppLogger logger,
        IDialogService dialogService,
        ThemeManager themeManager,
        SettingsStore settingsStore,
        AppSettings settings,
        LogViewModel logViewModel) : this(repository, wingetService, queueService, scanService, backupExporter, logger, dialogService, themeManager, settingsStore, settings, logViewModel, LocalizationSource.Instance.Localizer)
    {
    }

    public MainViewModel(
        IProfileRepository repository,
        IWingetService wingetService,
        InstallationQueueService queueService,
        IMachineScanService? scanService,
        IBackupExporter? backupExporter,
        IAppLogger logger,
        IDialogService dialogService,
        ThemeManager themeManager,
        SettingsStore settingsStore,
        AppSettings settings,
        LogViewModel logViewModel,
        IStringLocalizer localizer)
    {
        _repository = repository;
        _wingetService = wingetService;
        _queueService = queueService;
        _logger = logger;
        _dialogService = dialogService;
        _themeManager = themeManager;
        _settingsStore = settingsStore;
        _settings = settings;
        _scanService = scanService;
        _backupExporter = backupExporter;
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _lastStatusText = LocalizedText.Of(UiKeys.StatusReady);
        _statusMessage = _localizer.Format(_lastStatusText);

        _selectedLanguage = LanguageCatalog.Supported
            .FirstOrDefault(l => string.Equals(l.Code, settings.Language, StringComparison.OrdinalIgnoreCase))
            ?? LanguageCatalog.Supported[0];

        Logs = logViewModel;
        Search = new SearchViewModel(
            wingetService,
            AddPackageFromSearch,
            IsPackageInCurrentProfile,
            () => !_isClosing && !IsInstalling && IsWingetAvailable,
            () => !_isClosing && !IsInstalling,
            _localizer);

        RefreshCategoryFilters();

        PackagesView = CollectionViewSource.GetDefaultView(Packages);
        PackagesView.Filter = FilterPackage;

        // --- Danh sách phần mềm ---
        AddPackageCommand = new RelayCommand(AddPackage, () => !_isClosing && !IsInstalling && SelectedProfile is not null);
        EditPackageCommand = new RelayCommand(EditPackage, () => !_isClosing && !IsInstalling && SelectedPackage is not null);
        DeletePackageCommand = new RelayCommand(DeletePackage, () => !_isClosing && !IsInstalling && SelectedPackage is not null);
        MoveUpCommand = new RelayCommand(() => MovePackage(-1), () => !_isClosing && !IsInstalling && CanMovePackage(-1));
        MoveDownCommand = new RelayCommand(() => MovePackage(1), () => !_isClosing && !IsInstalling && CanMovePackage(1));

        SelectAllCommand = new RelayCommand(() => SetSelectionForVisible(true), () => !_isClosing && !IsInstalling);
        SelectNoneCommand = new RelayCommand(() => SetSelectionForVisible(false), () => !_isClosing && !IsInstalling);
        InvertSelectionCommand = new RelayCommand(InvertSelection, () => !_isClosing && !IsInstalling);
        SelectNotInstalledCommand = new RelayCommand(SelectNotInstalled, () => !_isClosing && !IsInstalling);

        // --- Cài đặt ---
        InstallSelectedCommand = new AsyncRelayCommand(InstallSelectedAsync, () => !_isClosing && !IsInstalling && IsWingetAvailable && SelectedCount > 0);
        CancelInstallCommand = new RelayCommand(CancelInstall, () => !_isClosing && IsInstalling);
        RetryFailedCommand = new AsyncRelayCommand(RetryFailedAsync, () => !_isClosing && !IsInstalling && HasFailedResults);
        RefreshInstalledCommand = new AsyncRelayCommand(
            () => RefreshInstalledStatesAsync(showDialog: true),
            () => !_isClosing && !IsInstalling && IsWingetAvailable);
        ScanAndBackupCommand = new AsyncRelayCommand(ScanAndBackupAsync,
            () => _scanService is not null && _backupExporter is not null && !_isClosing && !IsInstalling);

        // --- Cấu hình / dữ liệu ---
        NewProfileCommand = new RelayCommand(NewProfile, () => !_isClosing && !IsInstalling);
        RenameProfileCommand = new RelayCommand(RenameProfile, () => !_isClosing && !IsInstalling && SelectedProfile is not null);
        DuplicateProfileCommand = new RelayCommand(DuplicateProfile, () => !_isClosing && !IsInstalling && SelectedProfile is not null);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => !_isClosing && !IsInstalling && Profiles.Count > 1 && SelectedProfile is not null);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => !_isClosing && !IsInstalling);
        ExportCommand = new AsyncRelayCommand(() => ExportAsync(onlyCurrentProfile: false), () => !_isClosing && !IsInstalling);
        ExportProfileCommand = new AsyncRelayCommand(
            () => ExportAsync(onlyCurrentProfile: true),
            () => !_isClosing && !IsInstalling && SelectedProfile is not null);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder, () => !_isClosing);

        // --- Khác ---
        OpenAppInstallerPageCommand = new RelayCommand(OpenAppInstallerPage, () => !_isClosing);
        ToggleThemeCommand = new RelayCommand(ToggleTheme, () => !_isClosing);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdministrator, () => !_isClosing && !IsInstalling && !IsAdministrator);
        LoadedCommand = new AsyncRelayCommand(InitializeAsync, () => !_isClosing);
        LocalizationSource.Instance.LanguageChanged += OnLanguageChanged;
    }

    // ---------------------------------------------------------------- Dữ liệu

    public ObservableCollection<InstallationProfile> Profiles { get; } = new();

    public ObservableCollection<SoftwarePackageViewModel> Packages { get; } = new();

    public ObservableCollection<InstallationResult> Results { get; } = new();

    public ICollectionView PackagesView { get; }

    public IReadOnlyList<CategoryFilterOption> CategoryFilters
    {
        get => _categoryFilters;
        private set => SetProperty(ref _categoryFilters, value);
    }

    public SearchViewModel Search { get; }

    public LogViewModel Logs { get; }

    public string DataFilePath => _repository.DataFilePath;

    // ---------------------------------------------------------------- Command

    public AsyncRelayCommand LoadedCommand { get; }
    public AsyncRelayCommand ScanAndBackupCommand { get; }

    public RelayCommand AddPackageCommand { get; }

    public RelayCommand EditPackageCommand { get; }

    public RelayCommand DeletePackageCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand SelectNoneCommand { get; }

    public RelayCommand InvertSelectionCommand { get; }

    public RelayCommand SelectNotInstalledCommand { get; }

    public AsyncRelayCommand InstallSelectedCommand { get; }

    public RelayCommand CancelInstallCommand { get; }

    public AsyncRelayCommand RetryFailedCommand { get; }

    public AsyncRelayCommand RefreshInstalledCommand { get; }

    public RelayCommand NewProfileCommand { get; }

    public RelayCommand RenameProfileCommand { get; }

    public RelayCommand DuplicateProfileCommand { get; }

    public RelayCommand DeleteProfileCommand { get; }

    public AsyncRelayCommand ImportCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public AsyncRelayCommand ExportProfileCommand { get; }

    public RelayCommand OpenDataFolderCommand { get; }

    public RelayCommand OpenAppInstallerPageCommand { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public RelayCommand RestartAsAdminCommand { get; }

    // ---------------------------------------------------------------- Thuộc tính hiển thị

    public IReadOnlyList<LanguageOption> Languages => LanguageCatalog.Supported;

    private LanguageOption _selectedLanguage = LanguageCatalog.Supported[0];

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetProperty(ref _selectedLanguage, value) || value is null)
            {
                return;
            }

            LocalizationSource.Instance.SetLanguage(CultureInfo.GetCultureInfo(value.Code));
            _settings.Language = value.Code;
            _settingsStore.Save(_settings);

            // Chuỗi tính toán trong ViewModel không tự đổi như binding trong XAML,
            // nên báo cho WPF biết mọi thuộc tính đều đã thay đổi.
            OnPropertyChanged(string.Empty);
        }
    }

    public InstallationProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value))
            {
                return;
            }

            LoadPackagesFromProfile();

            if (!_isLoadingCatalog && value is not null)
            {
                _catalog.ActiveProfileId = value.Id;
                SaveCatalogInBackground();
            }

            RefreshCommandStates();
        }
    }

    public SoftwarePackageViewModel? SelectedPackage
    {
        get => _selectedPackage;
        set
        {
            if (SetProperty(ref _selectedPackage, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                PackagesView.Refresh();
                OnPropertyChanged(nameof(VisibleCount));
            }
        }
    }

    public CategoryFilterOption CategoryFilter
    {
        get => _categoryFilter;
        set
        {
            if (SetProperty(ref _categoryFilter, value))
            {
                PackagesView?.Refresh();
                OnPropertyChanged(nameof(VisibleCount));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        private set
        {
            if (SetProperty(ref _isInstalling, value))
            {
                OnPropertyChanged(nameof(IsNotInstalling));
                RefreshCommandStates();
            }
        }
    }

    public bool IsNotInstalling => !IsInstalling;

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public string CurrentPackageText
    {
        get => _currentPackageText;
        private set => SetProperty(ref _currentPackageText, value);
    }

    public bool IsWingetAvailable
    {
        get => _isWingetAvailable;
        private set
        {
            if (SetProperty(ref _isWingetAvailable, value))
            {
                OnPropertyChanged(nameof(ShowWingetWarning));
                RefreshCommandStates();
            }
        }
    }

    public string WingetWarning
    {
        get => _wingetWarning;
        private set => SetProperty(ref _wingetWarning, value);
    }

    public bool ShowWingetWarning => !IsWingetAvailable;

    public string DataErrorMessage
    {
        get => _dataErrorMessage;
        private set
        {
            if (SetProperty(ref _dataErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasDataError));
            }
        }
    }

    public bool HasDataError => !string.IsNullOrWhiteSpace(DataErrorMessage);

    public int TotalCount => Packages.Count;

    public int VisibleCount => PackagesView.Cast<object>().Count();

    public int SelectedCount => Packages.Count(p => p.IsSelected);

    public int InstalledCount => Packages.Count(p => p.InstallState == InstallState.Installed);

    public string SummaryText =>
        _localizer.Format(LocalizedText.Of(UiKeys.SelectionSummary, TotalCount, SelectedCount, InstalledCount));

    public bool HasFailedResults => Packages.Any(p => p.HasError);

    public string ThemeButtonText => _themeManager.CurrentTheme == AppTheme.Dark ? _localizer[UiKeys.ThemeSwitchLight] : _localizer[UiKeys.ThemeSwitchDark];

    public static bool IsAdministrator
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public string PrivilegeText => IsAdministrator
        ? _localizer[UiKeys.AdminRunningAsAdmin]
        : _localizer[UiKeys.AdminRunningAsUser];

    private void RefreshCategoryFilters()
    {
        var currentVal = _categoryFilter?.Value;
        CategoryFilters = new List<CategoryFilterOption>
        {
            new(null, _localizer[UiKeys.CategoryAll])
        }
        .Concat(CategoryNames.GetAll(_localizer).Select(c => new CategoryFilterOption(c.Value, c.DisplayName)))
        .ToList();

        CategoryFilter = CategoryFilters.FirstOrDefault(c => c.Value == currentVal) ?? CategoryFilters[0];
    }

    private void SetStatus(LocalizedText text)
    {
        _lastStatusText = text;
        StatusMessage = _localizer.Format(text);
    }

    private void SetStatus(string key) => SetStatus(LocalizedText.Of(key));

    private void OnLanguageChanged(object? sender, CultureInfo culture)
    {
        RefreshCategoryFilters();

        foreach (var package in Packages)
        {
            package.RefreshLocalization();
        }

        Search.RefreshLocalization();
        Logs.RefreshLocalization();

        if (_lastStatusText is not null)
        {
            StatusMessage = _localizer.Format(_lastStatusText);
        }

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(ThemeButtonText));
        OnPropertyChanged(nameof(PrivilegeText));

        PackagesView?.Refresh();
        var resultsView = CollectionViewSource.GetDefaultView(Results);
        resultsView?.Refresh();
    }

    // ---------------------------------------------------------------- Khởi động

    private async Task InitializeAsync()
    {
        IsBusy = true;
        SetStatus(UiKeys.StatusLoadingCatalog);

        try
        {
            _catalog = await _repository.LoadAsync(_lifetimeCts.Token).ConfigureAwait(true);
            if (_isClosing || _disposed)
            {
                return;
            }

            _catalogLoaded = true;
            ReloadProfiles();

            SetStatus(LocalizedText.Of(UiKeys.StatusLoadedFrom, _repository.DataFilePath));

            await CheckWingetAsync().ConfigureAwait(true);

            if (IsWingetAvailable && _settings.CheckInstalledOnStartup)
            {
                await RefreshInstalledStatesAsync(showDialog: false).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (_isClosing || _disposed || _lifetimeCts.IsCancellationRequested)
        {
            // Đóng cửa sổ trong lúc khởi động không phải lỗi và không được ghi catalog rỗng.
            //
            // Phải xét cả _lifetimeCts: cờ _isClosing chỉ bật trong lúc PrepareForCloseAsync chạy
            // rồi được trả về false ngay sau đó, trong khi ngoại lệ huỷ ở đây thường lan tới MUỘN HƠN.
            // Nếu chỉ dựa vào _isClosing thì người dùng đóng app lúc đang nạp dữ liệu
            // sẽ bị hiện hộp "Lỗi khởi động" một cách vô lý.
        }
        catch (Exception ex)
        {
            _logger.Error($"Startup failed: {ex.Message}", details: ex.ToString());
            if (!_catalogLoaded)
            {
                DataErrorMessage = _localizer.Format(LocalizedText.Of(UiKeys.DataErrorLoadFailed, DataFilePath, ex.Message));
                SetStatus(UiKeys.StatusLoadFailedAutoSaveDisabled);
            }
            _dialogService.ShowError(_localizer[UiKeys.DialogStartupError], ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckWingetAsync()
    {
        SetStatus(UiKeys.StatusCheckingWinget);

        var availability = await _wingetService.CheckAvailabilityAsync(_lifetimeCts.Token).ConfigureAwait(true);

        IsWingetAvailable = availability.IsAvailable;

        if (availability.IsAvailable)
        {
            SetStatus(LocalizedText.Of(UiKeys.StatusWingetReady, availability.Version));
            WingetWarning = string.Empty;
            return;
        }

        WingetWarning = availability.ErrorMessage ?? _localizer[UiKeys.WarningWingetUnavailable];

        SetStatus(UiKeys.StatusWingetNotReady);
        _logger.Warning(WingetWarning);
    }

    private void ReloadProfiles()
    {
        _isLoadingCatalog = true;

        try
        {
            Profiles.Clear();

            foreach (var profile in _catalog.Profiles)
            {
                Profiles.Add(profile);
            }

            SelectedProfile = _catalog.GetActiveProfile();
        }
        finally
        {
            _isLoadingCatalog = false;
        }
    }

    private void LoadPackagesFromProfile()
    {
        _installedScanCompleted = false;
        SelectedPackage = null;
        Packages.Clear();

        if (SelectedProfile is not null)
        {
            foreach (var package in SelectedProfile.Packages.OrderBy(p => p.SortOrder))
            {
                Packages.Add(CreatePackageViewModel(package));
            }
        }

        PackagesView.Refresh();
        Search.RefreshAlreadyInListFlags();
        RaiseCountsChanged();
    }

    private SoftwarePackageViewModel CreatePackageViewModel(SoftwarePackage package) =>
        new(package, () =>
        {
            RaiseCountsChanged();
            SaveCatalogInBackground();
        });

    // ---------------------------------------------------------------- Lọc danh sách

    private bool FilterPackage(object item)
    {
        if (item is not SoftwarePackageViewModel package)
        {
            return false;
        }

        if (CategoryFilter.Value is { } category && package.Category != category)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FilterText))
        {
            return true;
        }

        var keyword = FilterText.Trim();

        return package.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || package.PackageId.Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || package.CategoryDisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<SoftwarePackageViewModel> VisiblePackages =>
        PackagesView.Cast<SoftwarePackageViewModel>();

    // ---------------------------------------------------------------- Thêm / sửa / xoá

    private void AddPackage()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var editor = new PackageEditorViewModel(null, _localizer);

        if (!_dialogService.ShowPackageEditor(editor))
        {
            return;
        }

        AddPackageCore(editor.CreatePackage(NextSortOrder()));
    }

    private void AddPackageFromSearch(WingetPackageInfo info)
    {
        if (SelectedProfile is null)
        {
            _dialogService.ShowInfo(_localizer[UiKeys.DialogNoProfile], _localizer[UiKeys.DialogNoProfilePrompt]);
            return;
        }

        if (IsPackageInCurrentProfile(info.PackageId))
        {
            _dialogService.ShowInfo(_localizer[UiKeys.DialogAlreadyExists], _localizer.Format(LocalizedText.Of(UiKeys.DialogPackageAlreadyInProfile, info.PackageId)));
            return;
        }

        // Mở hộp thoại đã điền sẵn để người dùng chọn nhóm phần mềm.
        var template = new SoftwarePackage
        {
            Name = info.Name,
            PackageId = info.PackageId,
            Category = SoftwareCategoryGuesser.Guess(info),
            Version = info.Version
        };

        var editor = new PackageEditorViewModel(template);

        if (!_dialogService.ShowPackageEditor(editor))
        {
            return;
        }

        var package = editor.CreatePackage(NextSortOrder());
        package.Version = info.Version;

        AddPackageCore(package);
    }

    private void AddPackageCore(SoftwarePackage package)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (IsPackageInCurrentProfile(package.PackageId))
        {
            _dialogService.ShowInfo(_localizer[UiKeys.DialogAlreadyExists], _localizer.Format(LocalizedText.Of(UiKeys.DialogPackageAlreadyInList, package.PackageId)));
            return;
        }

        SelectedProfile.Packages.Add(package);
        SelectedProfile.UpdatedAt = DateTimeOffset.Now;

        var viewModel = CreatePackageViewModel(package);
        Packages.Add(viewModel);
        SelectedPackage = viewModel;

        PackagesView.Refresh();
        Search.RefreshAlreadyInListFlags();
        RaiseCountsChanged();
        SaveCatalogInBackground();

        SetStatus(LocalizedText.Of(UiKeys.StatusPackageAdded, package.Name));
        _logger.Information($"Added package to list: {package.Name} ({package.PackageId}).");

        // Biết ngay gói vừa thêm đã có trên máy hay chưa.
        _ = UpdateSingleInstallStateAsync(viewModel);
    }

    private void EditPackage()
    {
        if (SelectedPackage is null || SelectedProfile is null)
        {
            return;
        }

        var editor = new PackageEditorViewModel(SelectedPackage.Model, _localizer);

        if (!_dialogService.ShowPackageEditor(editor))
        {
            return;
        }

        var duplicated = Packages.Any(p =>
            p != SelectedPackage &&
            string.Equals(p.PackageId, editor.PackageId.Trim(), StringComparison.OrdinalIgnoreCase));

        if (duplicated)
        {
            _dialogService.ShowError(_localizer[UiKeys.DialogDuplicatePackageId], _localizer[UiKeys.DialogDuplicatePackageIdPrompt]);
            return;
        }

        var packageIdChanged = !string.Equals(
            SelectedPackage.PackageId, editor.PackageId.Trim(), StringComparison.OrdinalIgnoreCase);
        editor.ApplyTo(SelectedPackage.Model);
        if (packageIdChanged)
        {
            SelectedPackage.LastResult = null;
            SelectedPackage.InstallState = InstallState.Unknown;
            SelectedPackage.InstalledVersion = null;
        }
        SelectedPackage.RefreshAll();
        SelectedProfile.UpdatedAt = DateTimeOffset.Now;

        PackagesView.Refresh();
        Search.RefreshAlreadyInListFlags();
        RaiseCountsChanged();
        SaveCatalogInBackground();
        SetStatus(LocalizedText.Of(UiKeys.StatusPackageUpdated, SelectedPackage.Name));

        if (packageIdChanged)
        {
            _ = UpdateSingleInstallStateAsync(SelectedPackage);
        }
    }

    private void DeletePackage()
    {
        if (SelectedPackage is null || SelectedProfile is null)
        {
            return;
        }

        var package = SelectedPackage;

        if (!_dialogService.Confirm(_localizer[UiKeys.DialogDeletePackage], _localizer.Format(LocalizedText.Of(UiKeys.DialogDeletePackageConfirm, package.Name))))
        {
            return;
        }

        SelectedProfile.Packages.Remove(package.Model);
        SelectedProfile.UpdatedAt = DateTimeOffset.Now;
        Packages.Remove(package);
        SelectedPackage = null;

        Renumber();
        PackagesView.Refresh();
        Search.RefreshAlreadyInListFlags();
        RaiseCountsChanged();
        SaveCatalogInBackground();

        SetStatus(LocalizedText.Of(UiKeys.StatusPackageDeleted, package.Name));
    }

    private bool CanMovePackage(int offset)
    {
        if (SelectedPackage is null)
        {
            return false;
        }

        var index = Packages.IndexOf(SelectedPackage);
        var target = index + offset;

        return index >= 0 && target >= 0 && target < Packages.Count;
    }

    private void MovePackage(int offset)
    {
        if (SelectedPackage is null || !CanMovePackage(offset))
        {
            return;
        }

        var package = SelectedPackage;
        var index = Packages.IndexOf(package);

        Packages.Move(index, index + offset);
        Renumber();

        PackagesView.Refresh();
        SelectedPackage = package;
        RefreshCommandStates();
        SaveCatalogInBackground();
    }

    /// <summary>Ghi lại SortOrder theo đúng thứ tự đang hiển thị.</summary>
    private void Renumber()
    {
        for (var i = 0; i < Packages.Count; i++)
        {
            Packages[i].Model.SortOrder = i;
        }

        if (SelectedProfile is not null)
        {
            SelectedProfile.Packages = Packages.Select(p => p.Model).ToList();
        }
    }

    private int NextSortOrder() => Packages.Count == 0 ? 0 : Packages.Max(p => p.Model.SortOrder) + 1;

    private bool IsPackageInCurrentProfile(string packageId) =>
        Packages.Any(p => string.Equals(p.PackageId, packageId, StringComparison.OrdinalIgnoreCase));

    private static SoftwareCategory GuessCategory(WingetPackageInfo info)
    {
        var text = $"{info.Name} {info.PackageId}".ToLowerInvariant();

        if (text.Contains("chrome") || text.Contains("firefox") || text.Contains("edge") ||
            text.Contains("brave") || text.Contains("opera") || text.Contains("browser"))
        {
            return SoftwareCategory.Browser;
        }

        if (text.Contains("visualstudio") || text.Contains("git") || text.Contains("node") ||
            text.Contains("python") || text.Contains("docker") || text.Contains("sdk") ||
            text.Contains("java") || text.Contains("code"))
        {
            return SoftwareCategory.Development;
        }

        if (text.Contains("office") || text.Contains("word") || text.Contains("excel") ||
            text.Contains("adobe.acrobat") || text.Contains("libreoffice") || text.Contains("pdf"))
        {
            return SoftwareCategory.Office;
        }

        if (text.Contains("vlc") || text.Contains("spotify") || text.Contains("steam") ||
            text.Contains("player") || text.Contains("music"))
        {
            return SoftwareCategory.Entertainment;
        }

        if (text.Contains("zip") || text.Contains("notepad") || text.Contains("everything") ||
            text.Contains("rar") || text.Contains("driver") || text.Contains("tool"))
        {
            return SoftwareCategory.Utility;
        }

        return SoftwareCategory.Other;
    }

    // ---------------------------------------------------------------- Chọn hàng loạt

    private void SetSelectionForVisible(bool isSelected) =>
        ApplySelection(_ => isSelected);

    private void InvertSelection() =>
        ApplySelection(package => !package.IsSelected);

    private void SelectNotInstalled() =>
        ApplySelection(package => package.InstallState != InstallState.Installed);

    /// <summary>
    /// Đổi lựa chọn cho các dòng đang hiển thị.
    /// Tạm khoá tự động lưu để 100 dòng chỉ ghi file MỘT lần thay vì 100 lần.
    /// </summary>
    private void ApplySelection(Func<SoftwarePackageViewModel, bool> selector)
    {
        _suppressAutoSave = true;

        try
        {
            foreach (var package in VisiblePackages.ToList())
            {
                package.IsSelected = selector(package);
            }
        }
        finally
        {
            _suppressAutoSave = false;
        }

        RaiseCountsChanged();
        SaveCatalogInBackground();
    }

    // ---------------------------------------------------------------- Kiểm tra đã cài

    private async Task RefreshInstalledStatesAsync(bool showDialog)
    {
        if (!IsWingetAvailable)
        {
            return;
        }

        IsBusy = true;
        SetStatus(UiKeys.StatusCheckingInstalled);

        foreach (var package in Packages)
        {
            package.InstallState = InstallState.Checking;
        }

        try
        {
            var installed = await _wingetService.GetInstalledPackagesAsync(_lifetimeCts.Token).ConfigureAwait(true);

            var map = installed
                .GroupBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var package in Packages)
            {
                if (map.TryGetValue(package.PackageId, out var info))
                {
                    package.InstallState = InstallState.Installed;
                    package.InstalledVersion = info.Version;
                }
                else
                {
                    package.InstallState = InstallState.NotInstalled;
                    package.InstalledVersion = null;
                }
            }

            _installedScanCompleted = true;
            RaiseCountsChanged();

            SetStatus(LocalizedText.Of(UiKeys.StatusCheckedInstalledSummary, InstalledCount, TotalCount));

            if (showDialog)
            {
                _dialogService.ShowInfo(_localizer[UiKeys.DialogCheckCompleted], StatusMessage);
            }
        }
        catch (OperationCanceledException) when (_isClosing || _disposed)
        {
            // Tác vụ đọc WinGet dừng cùng cửa sổ.
        }
        catch (Exception ex)
        {
            foreach (var package in Packages)
            {
                package.InstallState = InstallState.Unknown;
            }

            _logger.Error($"Check installed packages failed: {ex.Message}");
            SetStatus(UiKeys.StatusCheckInstalledFailed);

            if (showDialog)
            {
                _dialogService.ShowError(_localizer[UiKeys.DialogError], ex.Message);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task UpdateSingleInstallStateAsync(SoftwarePackageViewModel package)
    {
        if (!IsWingetAvailable)
        {
            return;
        }

        try
        {
            package.InstallState = InstallState.Checking;
            var installed = await _wingetService.IsInstalledAsync(package.PackageId, _lifetimeCts.Token).ConfigureAwait(true);
            package.InstallState = installed ? InstallState.Installed : InstallState.NotInstalled;
            RaiseCountsChanged();
        }
        catch (Exception)
        {
            package.InstallState = InstallState.Unknown;
        }
    }

    // ---------------------------------------------------------------- Cài đặt

    private async Task InstallSelectedAsync()
    {
        var selected = Packages.Where(p => p.IsSelected).OrderBy(p => p.Model.SortOrder).ToList();

        if (selected.Count == 0)
        {
            _dialogService.ShowInfo(_localizer[UiKeys.DialogNoSoftwareSelected], _localizer[UiKeys.DialogNoSoftwareSelectedPrompt]);
            return;
        }

        var confirm = new InstallConfirmViewModel(selected, _settings.ExistingPackageAction);

        if (!_dialogService.ShowInstallConfirmation(confirm))
        {
            SetStatus(UiKeys.StatusInstallCancelledNoop);
            return;
        }

        _settings.ExistingPackageAction = confirm.SelectedAction;
        _settingsStore.Save(_settings);

        await RunQueueAsync(selected, confirm.SelectedAction).ConfigureAwait(true);
    }

    private async Task RetryFailedAsync()
    {
        var failed = Packages.Where(p => p.HasError).ToList();

        if (failed.Count == 0)
        {
            return;
        }

        if (!_dialogService.Confirm(_localizer[UiKeys.DialogRetry], _localizer.Format(LocalizedText.Of(UiKeys.DialogRetryFailedConfirm, failed.Count))))
        {
            return;
        }

        await RunQueueAsync(failed, _settings.ExistingPackageAction).ConfigureAwait(true);
    }

    private Task RunQueueAsync(IReadOnlyList<SoftwarePackageViewModel> packages, ExistingPackageAction action)
    {
        _activeQueueTask = RunQueueCoreAsync(packages, action);
        return _activeQueueTask;
    }

    private async Task RunQueueCoreAsync(IReadOnlyList<SoftwarePackageViewModel> packages, ExistingPackageAction action)
    {
        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();

        IsInstalling = true;
        Results.Clear();
        ProgressValue = 0;
        ProgressText = $"0/{packages.Count}";
        CurrentPackageText = _localizer[UiKeys.StatusPreparing];

        foreach (var package in packages)
        {
            package.LastResult = null;
        }

        var byPackageId = packages
            .GroupBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Chỉ dùng kết quả quét sẵn khi thực sự đã quét, nếu không hàng đợi sẽ tự hỏi winget.
        IReadOnlySet<string>? preChecked = _installedScanCompleted && packages.All(p =>
            p.InstallState is InstallState.Installed or InstallState.NotInstalled)
            ? Packages
                .Where(p => p.InstallState == InstallState.Installed)
                .Select(p => p.PackageId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var options = new InstallationOptions
        {
            ExistingPackageAction = action,
            PreCheckedInstalledPackageIds = preChecked
        };

        // Progress<T> được tạo trên UI thread nên callback tự động chạy trên UI thread.
        var progress = new Progress<InstallationProgressUpdate>(update => OnInstallProgress(update, byPackageId));

        try
        {
            var summary = await _queueService
                .RunAsync(packages.Select(p => p.Model).ToList(), options, progress, _installCts.Token)
                .ConfigureAwait(true);

            if (!_isClosing && !_disposed)
            {
                ShowSummary(summary);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Install queue error: {ex.Message}", details: ex.ToString());
            if (!_isClosing && !_disposed)
            {
                _dialogService.ShowError(_localizer[UiKeys.DialogInstallError], ex.Message);
            }
        }
        finally
        {
            IsInstalling = false;
            CurrentPackageText = string.Empty;
            RaiseCountsChanged();
        }
    }

    private void OnInstallProgress(
        InstallationProgressUpdate update,
        IReadOnlyDictionary<string, SoftwarePackageViewModel> byPackageId)
    {
        ProgressValue = update.PercentComplete;
        ProgressText = $"{update.CompletedCount}/{update.TotalCount}";
        CurrentPackageText = update.StatusMessage;
        _lastStatusText = LocalizedText.Raw(update.StatusMessage);
        StatusMessage = update.StatusMessage;

        if (update.CompletedResult is not { } result)
        {
            return;
        }

        Results.Add(result);

        if (!byPackageId.TryGetValue(result.PackageId, out var package))
        {
            return;
        }

        package.LastResult = result;

        package.InstallState = result.Outcome switch
        {
            InstallOutcome.Succeeded or InstallOutcome.Upgraded or InstallOutcome.AlreadyInstalled
                or InstallOutcome.Skipped => InstallState.Installed,
            InstallOutcome.Failed => InstallState.NotInstalled,
            _ => package.InstallState
        };

        OnPropertyChanged(nameof(HasFailedResults));
        RetryFailedCommand.RaiseCanExecuteChanged();
    }

    private void ShowSummary(InstallationRunSummary summary)
    {
        var lines = new List<string>
        {
            summary.WasCancelled ? _localizer[UiKeys.InstallSummaryCancelled] : _localizer[UiKeys.InstallSummaryFinished],
            string.Empty,
            _localizer.Format(LocalizedText.Of(UiKeys.InstallSummarySucceeded, summary.SucceededCount)),
            _localizer.Format(LocalizedText.Of(UiKeys.InstallSummarySkipped, summary.SkippedCount)),
            _localizer.Format(LocalizedText.Of(UiKeys.InstallSummaryFailed, summary.FailedCount)),
            _localizer.Format(LocalizedText.Of(UiKeys.InstallSummaryCancelledCount, summary.CancelledCount)),
            _localizer.Format(LocalizedText.Of(UiKeys.InstallSummaryTotalDuration, $"{summary.TotalDuration.TotalMinutes:F1}"))
        };

        if (summary.FailedCount > 0)
        {
            lines.Add(string.Empty);
            lines.Add(_localizer[UiKeys.InstallSummaryFailedPackages]);
            lines.AddRange(summary.Results
                .Where(r => r.Outcome == InstallOutcome.Failed)
                .Select(r => $"  - {r.DisplayName}: {_localizer.Format(r.Message)}"));
            lines.Add(string.Empty);
            lines.Add(_localizer[UiKeys.InstallSummaryFailedHint]);
        }

        SetStatus(summary.WasCancelled
            ? LocalizedText.Of(UiKeys.StatusInstallCancelled)
            : LocalizedText.Of(UiKeys.StatusInstallCompleted, summary.SucceededCount, summary.FailedCount));

        _dialogService.ShowInfo(_localizer[UiKeys.DialogInstallResultTitle], string.Join(Environment.NewLine, lines));
    }

    private void CancelInstall()
    {
        if (_installCts is null || _installCts.IsCancellationRequested)
        {
            return;
        }

        _installCts.Cancel();
        CurrentPackageText = _localizer[UiKeys.StatusCancelling];
        _logger.Warning("User requested cancellation of installation.");
    }

    // ---------------------------------------------------------------- Cấu hình

    private void NewProfile()
    {
        var name = _dialogService.ShowTextInput(
            new TextInputViewModel(_localizer[UiKeys.DialogNewProfileTitle], _localizer[UiKeys.DialogProfileNamePrompt], _localizer[UiKeys.DialogNewProfileDefault]));

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var profile = new InstallationProfile { Name = name };

        _catalog.Profiles.Add(profile);
        Profiles.Add(profile);
        SelectedProfile = profile;

        SaveCatalogInBackground();
        SetStatus(LocalizedText.Of(UiKeys.StatusProfileCreated, name));
    }

    private void RenameProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var name = _dialogService.ShowTextInput(
            new TextInputViewModel(_localizer[UiKeys.DialogRenameProfileTitle], _localizer[UiKeys.DialogNewNamePrompt], SelectedProfile.Name));

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var profile = SelectedProfile;
        profile.Name = name;
        profile.UpdatedAt = DateTimeOffset.Now;

        // ComboBox hiển thị theo Name nên cần đẩy lại item để nó vẽ lại.
        var index = Profiles.IndexOf(profile);
        Profiles.RemoveAt(index);
        Profiles.Insert(index, profile);
        SelectedProfile = profile;

        SaveCatalogInBackground();
    }

    private void DuplicateProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var copy = SelectedProfile.Clone();
        copy.Id = Guid.NewGuid();
        copy.Name = _localizer.Format(LocalizedText.Of(UiKeys.ProfileCopySuffix, SelectedProfile.Name));
        copy.CreatedAt = DateTimeOffset.Now;
        copy.UpdatedAt = DateTimeOffset.Now;

        foreach (var package in copy.Packages)
        {
            package.Id = Guid.NewGuid();
        }

        _catalog.Profiles.Add(copy);
        Profiles.Add(copy);
        SelectedProfile = copy;

        SaveCatalogInBackground();
        SetStatus(LocalizedText.Of(UiKeys.StatusProfileDuplicated, copy.Name));
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null || Profiles.Count <= 1)
        {
            return;
        }

        var profile = SelectedProfile;

        if (!_dialogService.Confirm(_localizer[UiKeys.DialogDeleteProfileTitle],
                _localizer.Format(LocalizedText.Of(UiKeys.DialogDeleteProfileConfirm, profile.Name, profile.Packages.Count))))
        {
            return;
        }

        _catalog.Profiles.Remove(profile);
        Profiles.Remove(profile);
        SelectedProfile = Profiles.FirstOrDefault();

        SaveCatalogInBackground();
        SetStatus(LocalizedText.Of(UiKeys.StatusProfileDeleted, profile.Name));
    }

    // ---------------------------------------------------------------- Import / Export

    private async Task ScanAndBackupAsync()
    {
        if (_scanService is null || _backupExporter is null) return;
        IsBusy = true;
        SetStatus(UiKeys.StatusScanningMachine);
        MachineSnapshot snapshot;
        try { snapshot = await _scanService.ScanAsync(_lifetimeCts.Token).ConfigureAwait(true); }
        catch (OperationCanceledException) { SetStatus(UiKeys.StatusScanCancelled); return; }
        catch (Exception ex) { _logger.Error($"Scan machine failed: {ex.Message}"); _dialogService.ShowError(_localizer[UiKeys.DialogScanFailed], ex.Message); return; }
        finally { IsBusy = false; }
        if (snapshot.Entries.Count == 0) { _dialogService.ShowInfo(_localizer[UiKeys.DialogScanFinished], _localizer[UiKeys.DialogNoSoftwareFound]); return; }
        var review = new ScanResultViewModel(snapshot);
        if (!_dialogService.ShowScanResult(review) || !review.HasAnySelected) return;
        var profile = review.BuildProfile(_catalog.Profiles.Select(p => p.Name));
        _catalog.Profiles.Add(profile); Profiles.Add(profile); SelectedProfile = profile;
        await SaveCatalogAsync().ConfigureAwait(true);
        var path = _dialogService.SaveJsonFile(_localizer[UiKeys.DialogSaveBackupTitle], $"sao-luu-{snapshot.MachineName}-{snapshot.ScannedAt:yyyyMMdd-HHmm}.json");
        if (path is null) return;
        try
        {
            var paths = await _backupExporter.ExportAsync(profile, path, _catalog.SchemaVersion, _lifetimeCts.Token).ConfigureAwait(true);
            _dialogService.ShowInfo(_localizer[UiKeys.DialogBackupSuccess], _localizer.Format(LocalizedText.Of(UiKeys.DialogBackupSavedFiles, paths.JsonPath, paths.CsvPath)));
        }
        catch (Exception ex) { _logger.Error($"Write backup file failed: {ex.Message}"); _dialogService.ShowError(_localizer[UiKeys.DialogWriteFileFailed], ex.Message); }
    }

    private async Task ImportAsync()
    {
        var path = _dialogService.OpenJsonFile(_localizer[UiKeys.DialogOpenJsonTitle]);

        if (path is null)
        {
            return;
        }

        try
        {
            var imported = await _repository.ImportAsync(path).ConfigureAwait(true);

            var replace = _dialogService.Confirm(
                _localizer[UiKeys.DialogImportTitle],
                _localizer.Format(LocalizedText.Of(UiKeys.DialogImportPrompt, imported.Profiles.Count)));

            if (replace)
            {
                _catalog = imported;
            }
            else
            {
                foreach (var profile in imported.Profiles)
                {
                    profile.Id = Guid.NewGuid();

                    if (_catalog.Profiles.Any(p => p.Name == profile.Name))
                    {
                        profile.Name = _localizer.Format(LocalizedText.Of(UiKeys.ProfileImportSuffix, profile.Name));
                    }

                    _catalog.Profiles.Add(profile);
                }
            }

            await _repository.SaveAsync(_catalog).ConfigureAwait(true);
            ReloadProfiles();

            SetStatus(LocalizedText.Of(UiKeys.StatusImportSuccess, path));
            _dialogService.ShowInfo(_localizer[UiKeys.DialogImportSuccessTitle], StatusMessage);

            if (IsWingetAvailable)
            {
                await RefreshInstalledStatesAsync(showDialog: false).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Import list failed: {ex.Message}");
            _dialogService.ShowError(_localizer[UiKeys.DialogImportFailedTitle], ex.Message);
        }
    }

    private async Task ExportAsync(bool onlyCurrentProfile)
    {
        var suggestedName = onlyCurrentProfile && SelectedProfile is not null
            ? $"{MakeSafeFileName(SelectedProfile.Name)}.json"
            : "software-list.json";

        var path = _dialogService.SaveJsonFile(_localizer[UiKeys.DialogExportTitle], suggestedName);

        if (path is null)
        {
            return;
        }

        try
        {
            var catalog = onlyCurrentProfile && SelectedProfile is not null
                ? new SoftwareCatalog
                {
                    SchemaVersion = _catalog.SchemaVersion,
                    ActiveProfileId = SelectedProfile.Id,
                    Profiles = new List<InstallationProfile> { SelectedProfile }
                }
                : _catalog;

            await _repository.ExportAsync(catalog, path).ConfigureAwait(true);

            SetStatus(LocalizedText.Of(UiKeys.StatusExportSuccess, path));
            _dialogService.ShowInfo(_localizer[UiKeys.DialogExportSuccessTitle], StatusMessage);
        }
        catch (Exception ex)
        {
            _logger.Error($"Export list failed: {ex.Message}");
            _dialogService.ShowError(_localizer[UiKeys.DialogExportFailedTitle], ex.Message);
        }
    }

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? "danh-sach" : cleaned;
    }

    private void OpenDataFolder()
    {
        try
        {
            var folder = Path.GetDirectoryName(_repository.DataFilePath);

            if (folder is null)
            {
                return;
            }

            Directory.CreateDirectory(folder);

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(_localizer[UiKeys.DialogError], _localizer.Format(LocalizedText.Of(UiKeys.DialogCannotOpenDataFolder, ex.Message)));
        }
    }

    // ---------------------------------------------------------------- Giao diện / quyền

    /// <summary>
    /// Mở đúng trang Microsoft Store của "App Installer" (gói chứa WinGet).
    /// Chỉ mở trang chính thức của Microsoft - ứng dụng KHÔNG tự tải hay chạy file cài đặt nào.
    /// </summary>
    private void OpenAppInstallerPage()
    {
        const string storeUri = "ms-windows-store://pdp/?productid=9NBLGGH4NNS1";
        const string webUri = "https://apps.microsoft.com/detail/9NBLGGH4NNS1";

        try
        {
            Process.Start(new ProcessStartInfo { FileName = storeUri, UseShellExecute = true });
        }
        catch (Exception)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = webUri, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(_localizer[UiKeys.DialogCannotOpen], _localizer.Format(LocalizedText.Of(UiKeys.DialogStoreInstallHint, ex.Message)));
            }
        }
    }

    private void ToggleTheme()
    {
        _settings.Theme = _themeManager.Toggle();
        _settingsStore.Save(_settings);
        OnPropertyChanged(nameof(ThemeButtonText));
    }

    private void RestartAsAdministrator()
    {
        if (!_dialogService.Confirm(
                _localizer[UiKeys.DialogRestartAdminTitle],
                _localizer[UiKeys.DialogRestartAdminPrompt]))
        {
            return;
        }

        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            _dialogService.ShowError(_localizer[UiKeys.DialogCannotExecute], _localizer[UiKeys.DialogCannotDetermineAppPath]);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                Verb = "runas"
            });

            WpfApplication.Current.Shutdown();
        }
        catch (Exception ex)
        {
            // Người dùng bấm "No" ở hộp thoại UAC cũng rơi vào đây.
            _logger.Warning($"Could not restart as Administrator: {ex.Message}");
            _dialogService.ShowInfo(_localizer[UiKeys.DialogCancelled], _localizer[UiKeys.DialogAdminElevationCancelled]);
        }
    }

    // ---------------------------------------------------------------- Tiện ích chung

    private void RaiseCountsChanged()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(InstalledCount));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasFailedResults));
        RetryFailedCommand.RaiseCanExecuteChanged();
        InstallSelectedCommand.RaiseCanExecuteChanged();
    }

    private void RefreshCommandStates()
    {
        AddPackageCommand.RaiseCanExecuteChanged();
        EditPackageCommand.RaiseCanExecuteChanged();
        DeletePackageCommand.RaiseCanExecuteChanged();
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
        SelectAllCommand.RaiseCanExecuteChanged();
        SelectNoneCommand.RaiseCanExecuteChanged();
        InvertSelectionCommand.RaiseCanExecuteChanged();
        SelectNotInstalledCommand.RaiseCanExecuteChanged();
        InstallSelectedCommand.RaiseCanExecuteChanged();
        CancelInstallCommand.RaiseCanExecuteChanged();
        RetryFailedCommand.RaiseCanExecuteChanged();
        RefreshInstalledCommand.RaiseCanExecuteChanged();
        NewProfileCommand.RaiseCanExecuteChanged();
        RenameProfileCommand.RaiseCanExecuteChanged();
        DuplicateProfileCommand.RaiseCanExecuteChanged();
        DeleteProfileCommand.RaiseCanExecuteChanged();
        ImportCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
        ExportProfileCommand.RaiseCanExecuteChanged();
        ScanAndBackupCommand.RaiseCanExecuteChanged();
        RestartAsAdminCommand.RaiseCanExecuteChanged();
        Search.RefreshCommandStates();
    }

    /// <summary>
    /// Lưu file JSON mà không chặn giao diện. Lỗi được ghi vào nhật ký thay vì làm sập ứng dụng.
    /// </summary>
    private void SaveCatalogInBackground()
    {
        if (!_catalogLoaded || _isLoadingCatalog || _suppressAutoSave || _isClosing || _disposed)
        {
            return;
        }

        _ = SaveCatalogAsync();
    }

    private async Task<bool> SaveCatalogAsync()
    {
        try
        {
            if (SelectedProfile is not null)
            {
                Renumber();
            }

            await _repository.SaveAsync(_catalog).ConfigureAwait(true);
            DataErrorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Save list failed: {ex.Message}");
            DataErrorMessage = _localizer.Format(LocalizedText.Of(UiKeys.DataErrorSaveFailed, DataFilePath, ex.Message));
            return false;
        }
    }

    /// <summary>Chỉ cho đóng sau khi hàng đợi đã dừng và dữ liệu đã được lưu; không chặn UI thread.</summary>
    public async Task<bool> PrepareForCloseAsync()
    {
        if (_isClosing)
        {
            return false;
        }

        if (IsInstalling && !_dialogService.Confirm(
                _localizer[UiKeys.DialogInstallingTitle],
                _localizer[UiKeys.DialogCloseWhileInstallingPrompt]))
        {
            return false;
        }

        _isClosing = true;
        RefreshCommandStates();
        try
        {
            _installCts?.Cancel();
            Search.CancelPendingSearch();

            if (_activeQueueTask is not null)
            {
                await _activeQueueTask.ConfigureAwait(true);
            }

            // Khi người dùng đóng ngay lúc mở app, không thay dữ liệu chưa đọc bằng catalog rỗng.
            var canClose = !_catalogLoaded || await SaveCatalogAsync().ConfigureAwait(true);
            if (canClose)
            {
                _lifetimeCts.Cancel();
            }

            return canClose;
        }
        finally
        {
            _isClosing = false;
            RefreshCommandStates();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        LocalizationSource.Instance.LanguageChanged -= OnLanguageChanged;
        _installCts?.Cancel();
        _lifetimeCts.Cancel();
        Search.Dispose();
        Logs.Dispose();
        _installCts?.Dispose();
        _lifetimeCts.Dispose();
    }
}
