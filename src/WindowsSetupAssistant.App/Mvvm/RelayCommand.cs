using System.Windows.Input;

namespace WindowsSetupAssistant.App.Mvvm;

/// <summary>
/// Command đồng bộ cho các nút bấm.
///
/// Kiến thức WPF: nút bấm được nối với ViewModel qua ICommand (thuộc tính Command),
/// gồm Execute (làm gì khi bấm) và CanExecute (có cho bấm không - WPF tự bật/tắt nút).
/// Gọi <see cref="RaiseCanExecuteChanged"/> khi điều kiện thay đổi để nút cập nhật lại.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute is null || _canExecute(parameter);

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
