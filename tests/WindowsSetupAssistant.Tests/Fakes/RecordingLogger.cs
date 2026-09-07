using WindowsSetupAssistant.Domain.Localization;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Tests.Fakes;

/// <summary>Logger giả: giữ lại toàn bộ log trong bộ nhớ để test kiểm tra.</summary>
public sealed class RecordingLogger : IAppLogger
{
    public List<LogEntry> Entries { get; } = new();

    public event EventHandler<LogEntry>? EntryLogged;

    public IEnumerable<LogEntry> Errors => Entries.Where(e => e.Level == LogLevel.Error);

    public IEnumerable<LogEntry> Warnings => Entries.Where(e => e.Level == LogLevel.Warning);

    public void Log(LogEntry entry)
    {
        Entries.Add(entry);
        EntryLogged?.Invoke(this, entry);
    }

    public void Information(LocalizedText message, string? command = null, string? details = null) =>
        Log(new LogEntry { Level = LogLevel.Information, Message = message, Command = command, Details = details });

    public void Warning(LocalizedText message, string? command = null, string? details = null) =>
        Log(new LogEntry { Level = LogLevel.Warning, Message = message, Command = command, Details = details });

    public void Error(LocalizedText message, string? command = null, int? exitCode = null, string? details = null) =>
        Log(new LogEntry
        {
            Level = LogLevel.Error,
            Message = message,
            Command = command,
            ExitCode = exitCode,
            Details = details
        });

    public void Information(string message, string? command = null, string? details = null) =>
        Information(LocalizedText.Raw(message), command, details);

    public void Warning(string message, string? command = null, string? details = null) =>
        Warning(LocalizedText.Raw(message), command, details);

    public void Error(string message, string? command = null, int? exitCode = null, string? details = null) =>
        Error(LocalizedText.Raw(message), command, exitCode, details);
}
