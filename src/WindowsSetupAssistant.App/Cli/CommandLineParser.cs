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

        // Lượt 1: chỉ để xác định --help và có cờ nào của chế độ này hay không.
        // Phải quét TOÀN BỘ mảng trước khi quyết định Gui hay Invalid - không được
        // dừng lại giữa chừng vì kết quả không được phép phụ thuộc thứ tự tham số.
        // Token đứng ngay sau --profile/--existing/--report là GIÁ TRỊ, không phải cờ,
        // nên phải bỏ qua đúng như TryTakeValue sẽ làm ở lượt 2 - nếu không, giá trị
        // trùng tên cờ (vd --profile --help) sẽ bị nhận nhầm.
        var hasHelp = false;
        var hasOwnFlag = false;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index].Trim();

            if (IsHelpFlag(token))
            {
                hasHelp = true;
            }

            if (Matches(token, UnattendedFlag))
            {
                hasOwnFlag = true;
                continue;
            }

            if (Matches(token, ProfileFlag) || Matches(token, ReportFlag) || Matches(token, ExistingFlag))
            {
                hasOwnFlag = true;

                // Cùng điều kiện với TryTakeValue: chỉ coi token kế tiếp là giá trị
                // (và bỏ qua nó khi quét help) nếu nó thực sự hợp lệ làm giá trị.
                if (index + 1 < args.Count)
                {
                    var candidate = args[index + 1];

                    if (!candidate.StartsWith('-') && !string.IsNullOrWhiteSpace(candidate))
                    {
                        index++;
                    }
                }

                continue;
            }
        }

        if (hasHelp)
        {
            return CommandLineParseResult.Help();
        }

        if (!hasOwnFlag)
        {
            // Không hề dùng cờ nào của chế độ này - rất có thể đây là tham số do Windows
            // tự truyền vào, cứ mở giao diện, đừng chặn người dùng lại.
            return CommandLineParseResult.Gui();
        }

        // Lượt 2: phân tích giá trị như bình thường. Từ đây, cờ nào của chế độ này cũng
        // đã được xác nhận là có mặt, nên tham số lạ luôn là lỗi, không còn nhánh Gui nữa.
        var unattended = false;
        string? profileName = null;
        string? reportPath = null;
        var existingAction = ExistingPackageAction.Skip;

        for (var index = 0; index < args.Count; index++)
        {
            var flag = args[index].Trim();

            if (Matches(flag, UnattendedFlag))
            {
                unattended = true;
                continue;
            }

            if (Matches(flag, ProfileFlag))
            {
                if (!TryTakeValue(args, ref index, out var value))
                {
                    return MissingValue(ProfileFlag);
                }

                profileName = value;
                continue;
            }

            if (Matches(flag, ReportFlag))
            {
                if (!TryTakeValue(args, ref index, out var value))
                {
                    return MissingValue(ReportFlag);
                }

                reportPath = value;
                continue;
            }

            if (Matches(flag, ExistingFlag))
            {
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

            // Tham số lạ. Lượt 1 đã xác nhận có ít nhất một cờ của chế độ này trong toàn
            // mảng, nên đây luôn là lỗi - không còn khả năng "cứ mở giao diện" nữa.
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
