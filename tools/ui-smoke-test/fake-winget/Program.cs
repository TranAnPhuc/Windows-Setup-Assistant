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
        // Nam cho de app o trang thai "dang cai" du lau cho viec kiem thu.
        Thread.Sleep(TimeSpan.FromMinutes(5));
        Console.WriteLine("Successfully installed");
        return 0;

    default:
        return 0;
}
