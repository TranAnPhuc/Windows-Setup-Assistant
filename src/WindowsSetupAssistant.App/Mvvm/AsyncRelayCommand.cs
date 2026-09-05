using System.Windows.Input;

namespace WindowsSetupAssistant.App.Mvvm;

/// <summary>
/// Command bất đồng bộ - dùng cho mọi thao tác gọi winget.
///
/// Vì sao KHÔNG dùng RelayCommand cho việc chạy winget?
/// Nếu chạy việc nặng ngay trên UI thread, cửa sổ sẽ "đơ" (không vẽ lại, không bấm được).
/// AsyncRelayCommand chạy Task và tự khoá nút trong lúc đang chạy (IsRunning),
/// nhờ vậy giao diện luôn phản hồi và người dùng không bấm hai lần.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _executeAsync;
    private readonly Func<object?, bool>? _canExecute;
    private readonly bool _allowConcurrentExecution;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null, bool allowConcurrentExecution = false)
        : this(_ => executeAsync(), canExecute is null ? null : _ => canExecute(), allowConcurrentExecution)
    {
    }

    public AsyncRelayCommand(
        Func<object?, Task> executeAsync,
        Func<object?, bool>? canExecute = null,
        bool allowConcurrentExecution = false)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
        _allowConcurrentExecution = allowConcurrentExecution;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>Nơi nhận lỗi không bắt được từ tác vụ nền (mặc định do App gán).</summary>
    public static Action<Exception>? UnhandledExceptionHandler { get; set; }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            RaiseCanExecuteChanged();
        }
    }

    public bool CanExecute(object? parameter)
    {
        if (!_allowConcurrentExecution && IsRunning)
        {
            return false;
        }

        return _canExecute is null || _canExecute(parameter);
    }

    public async void Execute(object? parameter)
    {
        // "async void" chỉ được phép ở đúng chỗ này (event handler của ICommand),
        // nên bắt buộc phải try/catch để lỗi không làm sập ứng dụng.
        if (!CanExecute(parameter))
        {
            return;
        }

        IsRunning = true;

        try
        {
            await _executeAsync(parameter).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Người dùng chủ động huỷ - không phải lỗi.
        }
        catch (Exception ex)
        {
            UnhandledExceptionHandler?.Invoke(ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
