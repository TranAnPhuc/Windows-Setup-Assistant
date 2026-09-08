using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Phân tích tham số dòng lệnh. Hàm thuần: không đọc file, không gọi WinGet,
/// nên kiểm thử được đầy đủ mọi tổ hợp mà không cần môi trường thật.
/// </summary>
public static class CommandLineParser
{
    private const string UnattendedFlag = "--unattended";
    private const string ProfileFlag = "--profile";
    private const string ExistingFlag = "--existing";
    private const string ReportFlag = "--report";

    public static CommandLineParseResult Parse(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
        {
            return CommandLineParseResult.Gui();
        }

        // --help thắng tất cả: người dùng đang bối rối thì đừng bắt họ sửa lệnh cho đúng
        // rồi mới được xem hướng dẫn.
        foreach (var arg in args)
        {
            if (IsHelpFlag(arg))
            {
                return CommandLineParseResult.Help();
            }
        }

        var unattended = false;
        var sawOwnFlag = false;
        string? profileName = null;
        string? reportPath = null;
        var existingAction = ExistingPackageAction.Skip;

        for (var index = 0; index < args.Count; index++)
        {
            var flag = args[index].Trim();

            if (Matches(flag, UnattendedFlag))
            {
                unattended = true;
                sawOwnFlag = true;
                continue;
            }

            if (Matches(flag, ProfileFlag))
            {
                sawOwnFlag = true;

                if (!TryTakeValue(args, ref index, out var value))
                {
                    return MissingValue(ProfileFlag);
                }

                profileName = value;
                continue;
            }

            if (Matches(flag, ReportFlag))
            {
                sawOwnFlag = true;

                if (!TryTakeValue(args, ref index, out var value))
                {
                    return MissingValue(ReportFlag);
                }

                reportPath = value;
                continue;
            }

            if (Matches(flag, ExistingFlag))
            {
                sawOwnFlag = true;

                if (!TryTakeValue(args, ref index, out var value))
                {
                    return MissingValue(ExistingFlag);
                }

                if (string.Equals(value, "skip", StringComparison.OrdinalIgnoreCase))
                {
                    existingAction = ExistingPackageAction.Skip;
                }
                else if (string.Equals(value, "upgrade", StringComparison.OrdinalIgnoreCase))
                {
                    existingAction = ExistingPackageAction.Upgrade;
                }
                else
                {
                    return CommandLineParseResult.Invalid(
                        $"Unknown value for {ExistingFlag}: \"{value}\". Use \"skip\" or \"upgrade\".");
                }

                continue;
            }

            // Tham số lạ. Nếu người dùng KHÔNG hề dùng cờ nào của chế độ này thì rất có thể
            // đây là tham số do Windows tự truyền vào - cứ mở giao diện, đừng chặn họ lại.
            if (!sawOwnFlag && !unattended)
            {
                return CommandLineParseResult.Gui();
            }

            return CommandLineParseResult.Invalid($"Unknown argument: \"{args[index]}\".");
        }

        if (!unattended)
        {
            return CommandLineParseResult.Invalid(
                $"{ProfileFlag} / {ExistingFlag} / {ReportFlag} only work together with {UnattendedFlag}.");
        }

        return CommandLineParseResult.Unattended(new CommandLineOptions
        {
            ProfileName = profileName,
            ExistingPackageAction = existingAction,
            ReportPath = reportPath
        });
    }

    private static bool IsHelpFlag(string arg)
    {
        var trimmed = arg.Trim();

        return Matches(trimmed, "--help")
            || Matches(trimmed, "-h")
            || trimmed == "-?"
            || trimmed == "/?";
    }

    private static bool Matches(string arg, string flag) =>
        string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase);

    private static CommandLineParseResult MissingValue(string flag) =>
        CommandLineParseResult.Invalid($"Missing value after {flag}.");

    /// <summary>
    /// Lấy giá trị đứng ngay sau một cờ. Giá trị bắt đầu bằng "-" được coi là cờ tiếp theo
    /// chứ không phải giá trị - trường hợp người dùng quên gõ giá trị.
    /// </summary>
    private static bool TryTakeValue(IReadOnlyList<string> args, ref int index, out string value)
    {
        value = string.Empty;

        if (index + 1 >= args.Count)
        {
            return false;
        }

        var candidate = args[index + 1];

        if (candidate.StartsWith('-') || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        index++;
        value = candidate.Trim();
        return true;
    }
}
