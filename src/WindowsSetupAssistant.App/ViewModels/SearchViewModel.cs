using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.Domain.Localization;
using System.Collections.ObjectModel;
using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Domain.Validation;

namespace WindowsSetupAssistant.App.ViewModels;

/// <summary>Một dòng kết quả tìm kiếm từ nguồn WinGet.</summary>
public sealed class SearchResultViewModel : ObservableObject
{
    private readonly IStringLocalizer _localizer;
    private bool _isAlreadyInList;

    public SearchResultViewModel(WingetPackageInfo info, IStringLocalizer localizer)
    {
        Info = info;
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }

    public SearchResultViewModel(WingetPackageInfo info)
        : this(info, LocalizationSource.Instance.Localizer)
    {
    }

    public WingetPackageInfo Info { get; }

    public string Name => Info.Name;

    public string PackageId => Info.PackageId;

    public string Version => Info.Version;

    public string Source => Info.Source ?? "winget";

    public bool IsAlreadyInList
    {
        get => _isAlreadyInList;
        set
        {
            if (SetProperty(ref _isAlreadyInList, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => IsAlreadyInList ? _localizer[UiKeys.SearchAlreadyInList] : string.Empty;
}

/// <summary>
/// Màn hình tìm kiếm phần mềm trực tiếp từ nguồn WinGet.
/// Mỗi lần tìm mới sẽ huỷ lần tìm trước để giao diện không bị chờ vô ích.
/// </summary>
public sealed class SearchViewModel : ObservableObject, IDisposable
{
    private readonly IWingetService _wingetService;
    private readonly Action<WingetPackageInfo> _onAddPackage;
    private readonly Func<string, bool> _isAlreadyInList;
    private readonly Func<bool> _canUseWinget;
    private readonly Func<bool> _canAddPackage;

    private CancellationTokenSource? _searchCts;
    private string _searchText = string.Empty;
    private readonly IStringLocalizer _localizer;
    private string _statusMessage;
    private bool _isSearching;
    private bool _disposed;

    public SearchViewModel(
        IWingetService wingetService,
        Action<WingetPackageInfo> onAddPackage,
        Func<string, bool> isAlreadyInList,
        Func<bool> canUseWinget,
        Func<bool> canAddPackage,
        IStringLocalizer localizer)
    {
        _wingetService = wingetService;
        _onAddPackage = onAddPackage;
        _isAlreadyInList = isAlreadyInList;
        _canUseWinget = canUseWinget;
        _canAddPackage = canAddPackage;
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _statusMessage = _localizer[UiKeys.SearchInitialPrompt];

        SearchCommand = new AsyncRelayCommand(
            SearchAsync,
            () => !string.IsNullOrWhiteSpace(SearchText) && _canUseWinget());
        AddCommand = new RelayCommand(
            AddToList,
            parameter => parameter is SearchResultViewModel && _canAddPackage());
        ClearCommand = new RelayCommand(() =>
        {
            CancelPendingSearch();
            Results.Clear();
            SearchText = string.Empty;
            StatusMessage = _localizer[UiKeys.SearchCleared];
        });
    }

    public SearchViewModel(
        IWingetService wingetService,
        Action<WingetPackageInfo> onAddPackage,
        Func<string, bool> isAlreadyInList,
        Func<bool> canUseWinget,
        Func<bool> canAddPackage)
        : this(wingetService, onAddPackage, isAlreadyInList, canUseWinget, canAddPackage, LocalizationSource.Instance.Localizer)
    {
    }

    public ObservableCollection<SearchResultViewModel> Results { get; } = new();

    public AsyncRelayCommand SearchCommand { get; }

    public RelayCommand AddCommand { get; }

    public RelayCommand ClearCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetProperty(ref _isSearching, value);
    }

    /// <summary>Cập nhật lại nhãn "Đã có trong danh sách" sau khi danh sách chính thay đổi.</summary>
    public void RefreshAlreadyInListFlags()
    {
        foreach (var result in Results)
        {
            result.IsAlreadyInList = _isAlreadyInList(result.PackageId);
        }
    }

    /// <summary>
    /// MainViewModel gọi khi trạng thái hàng đợi hoặc WinGet thay đổi để các nút trong
    /// tab tìm kiếm được khoá/mở cùng lúc với các thao tác trên danh sách chính.
    /// </summary>
    public void RefreshCommandStates()
    {
        SearchCommand.RaiseCanExecuteChanged();
        AddCommand.RaiseCanExecuteChanged();
    }

    private async Task SearchAsync()
    {
        if (!SearchQueryValidator.TryValidate(SearchText, out var validationError))
        {
            StatusMessage = _localizer.Format(validationError);
            return;
        }

        // Huỷ lần tìm trước (nếu người dùng bấm liên tục).
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        IsSearching = true;
        StatusMessage = _localizer[UiKeys.SearchSearching];
        Results.Clear();

        try
        {
            var packages = await _wingetService.SearchAsync(SearchText, token).ConfigureAwait(true);

            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (var package in packages)
            {
                Results.Add(new SearchResultViewModel(package, _localizer)
                {
                    IsAlreadyInList = _isAlreadyInList(package.PackageId)
                });
            }

            StatusMessage = Results.Count == 0
                ? _localizer[UiKeys.SearchNoResults]
                : _localizer.Format(LocalizedText.Of(UiKeys.SearchResultsFound, Results.Count));
        }
        catch (OperationCanceledException)
        {
            // ClearCommand đã đặt thông báo của chính nó; không ghi đè sau khi huỷ.
        }
        catch (TimeoutException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = _localizer.Format(LocalizedText.Of(UiKeys.SearchFailed, ex.Message));
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void AddToList(object? parameter)
    {
        if (parameter is not SearchResultViewModel result)
        {
            return;
        }

        _onAddPackage(result.Info);
        RefreshAlreadyInListFlags();
    }

    public void CancelPendingSearch() => _searchCts?.Cancel();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
