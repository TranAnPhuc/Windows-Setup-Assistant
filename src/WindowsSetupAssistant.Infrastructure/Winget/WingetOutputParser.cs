using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Infrastructure.Winget;

/// <summary>
/// Bộ phân tích bảng text của WinGet.
///
/// WinGet in kết quả dưới dạng bảng canh cột bằng khoảng trắng, ví dụ:
///
///   Name                         Id                          Version    Source
///   ------------------------------------------------------------------------
///   Google Chrome                Google.Chrome               152.0.0    winget
///
/// Vì tên phần mềm và cột "Match" đều có thể chứa khoảng trắng nên KHÔNG được tách bằng Split.
/// Cách làm đúng: đọc vị trí bắt đầu của từng cột trên dòng tiêu đề rồi cắt chuỗi theo vị trí đó.
///
/// Ta cũng KHÔNG so khớp chữ "Name"/"Id" vì WinGet hiển thị theo ngôn ngữ của Windows.
/// Thay vào đó dùng THỨ TỰ cột: cột 1 = Name, cột 2 = Id, cột 3 = Version.
/// </summary>
public static class WingetOutputParser
{
    // Ký tự winget dùng cho thanh tiến trình / spinner - cần loại bỏ khỏi output.
    private static readonly char[] SpinnerChars =
    {
        '-', '\\', '|', '/', '█', '▒', '░', '.', ' ', '\r'
    };

    /// <summary>Phân tích output của "winget search" hoặc "winget list".</summary>
    public static IReadOnlyList<WingetPackageInfo> ParseTable(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<WingetPackageInfo>();
        }

        var lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var separatorIndex = FindSeparatorLineIndex(lines);
        if (separatorIndex <= 0)
        {
            // Không có bảng: thường là "No installed package found matching input criteria."
            return Array.Empty<WingetPackageInfo>();
        }

        var headerLine = lines[separatorIndex - 1];

        var dataLines = lines
            .Skip(separatorIndex + 1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var candidateColumns = GetColumnStarts(headerLine);
        if (candidateColumns.Count < 2)
        {
            return Array.Empty<WingetPackageInfo>();
        }

        // Chỉ dùng các dòng "trông giống dòng dữ liệu" để tinh chỉnh cột.
        // Nếu lấy cả dòng tổng kết cuối bảng ("2 upgrades available.") thì
        // ranh giới cột sẽ bị tính sai.
        var tableLines = dataLines
            .Where(line => LooksLikePackageId(CellAt(line, candidateColumns, 1)))
            .ToList();

        var refinedColumns = RefineColumnStarts(
            candidateColumns,
            tableLines.Count > 0 ? tableLines : dataLines);

        var results = ParseRows(headerLine, dataLines, refinedColumns);

        // Nếu bộ cột đã lọc không ra kết quả nào, thử lại với bộ cột gốc từ tiêu đề.
        if (results.Count == 0 && refinedColumns.Count != candidateColumns.Count)
        {
            results = ParseRows(headerLine, dataLines, candidateColumns);
        }

        return results;
    }

    /// <summary>
    /// Kiểm tra nhanh xem một Package Id có xuất hiện trong output của "winget list" hay không.
    /// So sánh không phân biệt hoa thường vì WinGet không phân biệt hoa thường với Id.
    /// </summary>
    public static bool ContainsPackageId(string? output, string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return false;
        }

        return ParseTable(output)
            .Any(p => string.Equals(p.PackageId, packageId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Loại bỏ các dòng chỉ chứa ký tự spinner/thanh tiến trình của winget.</summary>
    public static string StripProgressNoise(string? output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        var kept = output
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Where(line => line.Trim().Length > 0 && line.Trim().Any(c => !SpinnerChars.Contains(c)));

        return string.Join(Environment.NewLine, kept);
    }

    private static List<WingetPackageInfo> ParseRows(
        string headerLine,
        IReadOnlyList<string> dataLines,
        IReadOnlyList<int> columns)
    {
        var hasAvailableColumn = HeaderHasAvailableColumn(headerLine, columns);
        var results = new List<WingetPackageInfo>();

        foreach (var line in dataLines)
        {
            var cells = SliceColumns(line, columns);
            if (cells.Count < 2)
            {
                continue;
            }

            var name = cells[0];
            var id = cells[1];
            var version = cells.Count > 2 ? cells[2] : string.Empty;

            // Loại các dòng chú thích cuối bảng ("2 upgrades available.") bị cắt nhầm thành ô:
            // Package Id thật không chứa khoảng trắng và luôn bắt đầu bằng chữ/số
            // (hoặc '{' với các mục trong Apps & Features dùng MSI product code).
            if (!LooksLikePackageId(id))
            {
                continue;
            }

            string? available = null;
            string? source = null;

            if (hasAvailableColumn)
            {
                available = cells.Count > 3 ? NullIfEmpty(cells[3]) : null;
                source = cells.Count > 4 ? NullIfEmpty(cells[4]) : null;
            }
            else
            {
                // Cột thứ 4 của "search" là Match hoặc Source tuỳ ngữ cảnh;
                // ta chỉ lấy cột cuối cùng làm Source.
                source = cells.Count > 3 ? NullIfEmpty(cells[cells.Count - 1]) : null;
            }

            results.Add(new WingetPackageInfo(name, id, version, available, source));
        }

        return results;
    }

    /// <summary>Tìm dòng gạch ngang "-----" ngăn giữa tiêu đề và dữ liệu.</summary>
    private static int FindSeparatorLineIndex(IReadOnlyList<string> lines)
    {
        for (var i = 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();

            if (trimmed.Length >= 5 && trimmed.All(c => c == '-') && !string.IsNullOrWhiteSpace(lines[i - 1]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Lấy vị trí bắt đầu của từng cột dựa trên dòng tiêu đề.
    /// Cột được ngăn cách bởi ÍT NHẤT một khoảng trắng - output thật của winget
    /// có thể chỉ cách nhau đúng 1 khoảng trắng khi các cột hẹp.
    /// </summary>
    private static List<int> GetColumnStarts(string headerLine)
    {
        var starts = new List<int>();

        for (var i = 0; i < headerLine.Length; i++)
        {
            var isStartOfToken = headerLine[i] != ' ' && (i == 0 || headerLine[i - 1] == ' ');

            if (isStartOfToken)
            {
                starts.Add(i);
            }
        }

        return starts;
    }

    /// <summary>
    /// Loại bỏ ranh giới cột "giả".
    ///
    /// Tiêu đề cột theo ngôn ngữ khác tiếng Anh có thể gồm nhiều từ (ví dụ tiếng Việt:
    /// "Phiên bản"), khiến bước tách ở trên tạo ra ranh giới không có thật.
    /// Một ranh giới chỉ được giữ khi:
    ///   (a) có ít nhất một dòng dữ liệu thực sự có nội dung bắt đầu tại vị trí đó, và
    ///   (b) mọi dòng có nội dung tại đó đều có khoảng trắng ngay trước (do WinGet canh lề trái
    ///       và luôn chèn ít nhất một khoảng trắng giữa hai cột).
    /// </summary>
    private static List<int> RefineColumnStarts(IReadOnlyList<int> candidates, IReadOnlyList<string> dataLines)
    {
        if (dataLines.Count == 0)
        {
            return candidates.ToList();
        }

        var kept = new List<int> { candidates[0] };

        for (var i = 1; i < candidates.Count; i++)
        {
            var position = candidates[i];
            var hasContent = false;
            var violated = false;

            foreach (var line in dataLines)
            {
                if (position >= line.Length || line[position] == ' ')
                {
                    continue;
                }

                hasContent = true;

                if (line[position - 1] != ' ')
                {
                    violated = true;
                    break;
                }
            }

            if (hasContent && !violated)
            {
                kept.Add(position);
            }
        }

        return kept;
    }

    /// <summary>Lấy nội dung ô thứ <paramref name="index"/> của một dòng (rỗng nếu không có).</summary>
    private static string CellAt(string line, IReadOnlyList<int> columnStarts, int index)
    {
        var cells = SliceColumns(line, columnStarts);
        return index < cells.Count ? cells[index] : string.Empty;
    }

    /// <summary>Cắt một dòng dữ liệu thành các ô theo vị trí cột.</summary>
    private static List<string> SliceColumns(string line, IReadOnlyList<int> columnStarts)
    {
        var cells = new List<string>(columnStarts.Count);

        for (var i = 0; i < columnStarts.Count; i++)
        {
            var start = columnStarts[i];
            if (start >= line.Length)
            {
                cells.Add(string.Empty);
                continue;
            }

            var end = i + 1 < columnStarts.Count ? Math.Min(columnStarts[i + 1], line.Length) : line.Length;
            cells.Add(line.Substring(start, end - start).Trim());
        }

        return cells;
    }

    /// <summary>
    /// "winget list" có 5 cột: Name, Id, Version, Available, Source.
    /// "winget search" có 4-5 cột: Name, Id, Version, [Match], [Source].
    /// Ta phân biệt bằng số cột và tên cột thứ 4.
    /// </summary>
    private static bool HeaderHasAvailableColumn(string headerLine, IReadOnlyList<int> columnStarts)
    {
        if (columnStarts.Count < 5)
        {
            return false;
        }

        var fourthColumn = SliceColumns(headerLine, columnStarts)[3];

        // Chạy tiếng Anh thì nhận ra ngay; ngôn ngữ khác thì mặc định coi 5 cột là "list".
        return !fourthColumn.Equals("Match", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePackageId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return char.IsLetterOrDigit(value[0]) || value[0] == '{';
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
