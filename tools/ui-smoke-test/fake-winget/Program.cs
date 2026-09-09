// winget.exe GIA - chi dung de kiem thu giao dien.
// Khong tai, khong cai, khong dong vao he thong.
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var verb = args.Length > 0 ? args[0] : "";

switch (verb)
{
    case "--version":
        Console.WriteLine("v1.29.290");
        return 0;

    case "list":
        Console.WriteLine("No installed package found matching input criteria.");
        return 0;

    case "search":
        Console.WriteLine("Name          Id                Version  Match");
        Console.WriteLine("-----------------------------------------------------");
        Console.WriteLine("Fake Package  Fake.Package      1.0.0    ");
        return 0;

    case "install":
    case "upgrade":
        Console.WriteLine("Found Fake Package [Fake.Package] Version 1.0.0");
        Console.WriteLine("Downloading (gia lap - khong tai gi that)...");
        Console.Out.Flush();
        // Thoi gian "nam cho" co the chinh qua bien moi truong WSA_FAKE_WINGET_DELAY_MS.
        // KHONG dat bien nay thi giu nguyen hanh vi cu (5 phut) de Run-UiSmokeTest.ps1
        // (can tien trinh song lau de thu huy giua chung) khong bi anh huong.
        // Run-UnattendedSmokeTest.ps1 dat bien nay rat ngan vi no khong can tien trinh
        // song lau, chi can kiem chung AttachConsole/ShutdownMode/ma thoat.
        var delayMs = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;
        var envDelay = Environment.GetEnvironmentVariable("WSA_FAKE_WINGET_DELAY_MS");
        if (!string.IsNullOrEmpty(envDelay) && int.TryParse(envDelay, out var parsedDelayMs) && parsedDelayMs >= 0)
        {
            delayMs = parsedDelayMs;
        }
        Thread.Sleep(delayMs);
        Console.WriteLine("Successfully installed");
        return 0;

    default:
        return 0;
}
