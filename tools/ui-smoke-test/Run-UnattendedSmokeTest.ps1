param(
    [string]$AppExe = "..\..\publish\WindowsSetupAssistant.exe",
    [string]$FakeWingetDir = ".\fake-winget\bin\Release\net8.0"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$AE   = [System.Windows.Automation.AutomationElement]
$TS   = [System.Windows.Automation.TreeScope]
$root = $AE::RootElement

# Dem so cua so cap cao (top-level) cua mot tien trinh bang UI Automation.
# Dung de xac nhan that su khong co cua so nao loe len trong luc chay khong giam sat,
# thay vi chi suy doan tu viec tien trinh da thoat (xem Run-UiSmokeTest.ps1 ben canh).
function Get-TopWindowCount($processId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $processId)
    return @($root.FindAll($TS::Children, $cond)).Count
}

$appExe  = (Resolve-Path $AppExe).Path
$fakeBin = (Resolve-Path $FakeWingetDir).Path
$appDir  = Split-Path $appExe -Parent

# Resolve-Path chi xac nhan THU MUC ton tai, khong xac nhan winget.exe GIA nam trong do.
# Neu thieu file nay (vi du buoc build winget gia bi bo sot hoac loi am tham), ung dung se
# roi xuong PATH va co the goi trung winget THAT tren may - tuyet doi khong duoc de xay ra.
$fakeWingetExe = Join-Path $fakeBin "winget.exe"
if (-not (Test-Path $fakeWingetExe)) {
    throw ("Khong tim thay winget GIA tai '" + $fakeWingetExe + "'. " +
           "Hay build truoc: dotnet build tools/ui-smoke-test/fake-winget -c Release. " +
           "DUNG LAI de tranh roi xuong winget THAT va cai phan mem that len may.")
}

$pass = 0; $fail = 0
function Check($label, $ok) {
    if ($ok) { $script:pass++; Write-Host ("  [DAT]   " + $label) }
    else     { $script:fail++; Write-Host ("  [TRUOT] " + $label) }
}

# Dat winget GIA len dau PATH: khong co phan mem that nao duoc cai.
# Luu lai PATH cu de khoi phuc trong finally, tranh ro ri anh huong toi cac lenh winget sau.
$oldPath = $env:PATH
$env:PATH = $fakeBin + ";" + $env:PATH

# Winget gia mac dinh "nam cho" 5 phut moi lenh cai (phuc vu kich ban UI can tien trinh
# song lau de thu huy). Kich ban khong giam sat nay khong can dieu do, nen rut ngan lai
# de moi lan chay chi mat vai giay thay vi hang chuc phut.
# Luu lai gia tri cu cua bien nay de khoi phuc trong finally, tranh ro ri anh huong
# toi lan chay tiep theo trong cung cua so PowerShell.
$oldDelay = $env:WSA_FAKE_WINGET_DELAY_MS
$env:WSA_FAKE_WINGET_DELAY_MS = "300"

# Du lieu rieng cho lan chay nay, khong dung vao Data that cua nguoi dung.
$work = Join-Path $env:TEMP ("wsa-unattended-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
$dataDir = Join-Path $appDir "Data"
$backup  = $null
if (Test-Path $dataDir) {
    $backup = Join-Path $work "Data-backup"
    Copy-Item $dataDir $backup -Recurse
}

$catalog = @{
    schemaVersion = 1
    profiles = @(@{
        id = [guid]::NewGuid().ToString()
        name = "Smoke"
        packages = @(
            @{ id = [guid]::NewGuid().ToString(); name = "Alpha"; packageId = "Fake.Alpha"; category = "Utility"; source = "winget"; isSelected = $true;  sortOrder = 0 },
            @{ id = [guid]::NewGuid().ToString(); name = "Beta";  packageId = "Fake.Beta";  category = "Utility"; source = "winget"; isSelected = $true;  sortOrder = 1 },
            @{ id = [guid]::NewGuid().ToString(); name = "Gamma"; packageId = "Fake.Gamma"; category = "Utility"; source = "winget"; isSelected = $false; sortOrder = 2 }
        )
        manualSoftware = @()
    })
}
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$catalog | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $dataDir "software-list.json") -Encoding utf8

function Invoke-App([string[]]$AppArgs) {
    $out = Join-Path $work "stdout.txt"
    $err = Join-Path $work "stderr.txt"
    # KHONG dung -Wait: phai de tien trinh dang chay de do so cua so cap cao thuc su,
    # neu khong thi -Wait se chan toi khi tien trinh DA THOAT roi moi kiem tra, luc do
    # dung nhien khong con cua so nao - muc kiem tra se luon "dat" du ung dung co mo
    # cua so giua chung hay khong.
    $p = Start-Process -FilePath $appExe -ArgumentList $AppArgs -NoNewWindow -PassThru `
                       -RedirectStandardOutput $out -RedirectStandardError $err
    # Truy cap .Handle ngay sau khi tao tien trinh: day la thao tac bat buoc theo mot
    # quai tinh cua .NET Process - neu khong lam vay, .ExitCode co the tra ve rong sau
    # khi tien trinh thoat vi handle da bi giai phong truoc khi doc duoc ma thoat.
    $h = $p.Handle
    $maxWindows = 0
    while (-not $p.HasExited) {
        $count = Get-TopWindowCount $p.Id
        if ($count -gt $maxWindows) { $maxWindows = $count }
        Start-Sleep -Milliseconds 100
    }
    $p.WaitForExit()
    return [pscustomobject]@{
        ExitCode   = $p.ExitCode
        StdOut     = (Get-Content $out -Raw -ErrorAction SilentlyContinue)
        StdErr     = (Get-Content $err -Raw -ErrorAction SilentlyContinue)
        MaxWindows = $maxWindows
    }
}

try {
    Write-Host "1. --help"
    $r = Invoke-App @("--help")
    Check "ma thoat 0"                 ($r.ExitCode -eq 0)
    Check "in ra huong dan"            ($r.StdOut -match "--unattended")
    Check "nhac quyen Administrator"   ($r.StdOut -match "Administrator")

    Write-Host "2. Tham so sai"
    $r = Invoke-App @("--unattended", "--existing", "reinstall")
    Check "ma thoat 2"                 ($r.ExitCode -eq 2)

    Write-Host "3. Sai ten cau hinh"
    # Start-Process ghep ArgumentList bang khoang trang ma khong tu dong bao ten co dau cach,
    # nen phai tu boc ngoac kep de "Khong Ton Tai" duoc coi la MOT tham so duy nhat.
    $r = Invoke-App @("--unattended", "--profile", '"Khong Ton Tai"')
    Check "ma thoat 2"                 ($r.ExitCode -eq 2)
    Check "liet ke cau hinh dang co"   (($r.StdOut + $r.StdErr) -match "Smoke")

    Write-Host "4. Chay that voi winget gia"
    $report = Join-Path $work "report.json"
    $r = Invoke-App @("--unattended", "--profile", "Smoke", "--report", $report)
    Check "ma thoat 0"                 ($r.ExitCode -eq 0)
    Check "in tien trinh goi 1"        ($r.StdOut -match "\[1/2\]")
    Check "in tien trinh goi 2"        ($r.StdOut -match "\[2/2\]")
    Check "khong cua so nao mo ra"     ($r.MaxWindows -eq 0)
    Check "co file bao cao"            (Test-Path $report)

    if (Test-Path $report) {
        $json = Get-Content $report -Raw | ConvertFrom-Json
        Check "schemaVersion = 1"      ($json.schemaVersion -eq 1)
        Check "dung ten cau hinh"      ($json.profileName -eq "Smoke")
        Check "exitCode khop"          ($json.exitCode -eq 0)
        Check "chi 2 goi duoc tick"    ($json.counts.total -eq 2)
        Check "khong co goi Gamma"     (-not ($json.packages.packageId -contains "Fake.Gamma"))
    }

    Write-Host "5. Duong dan bao cao hong khong lam doi ma thoat"
    $r = Invoke-App @("--unattended", "--profile", "Smoke", "--report", "Z:\khong-ton-tai\a<b>.json")
    Check "van la ma thoat 0"          ($r.ExitCode -eq 0)
    Check "co canh bao"                (($r.StdOut + $r.StdErr) -match "WARNING")
}
finally {
    # Bo qua trong try/catch rieng: $ErrorActionPreference = 'Stop' co the khien
    # Remove-Item/Copy-Item nem loi terminating (vd. file bi khoa boi phan mem diet virus),
    # neu khong bat lai o day thi buoc don dep thu muc tam ben duoi se khong bao gio chay.
    $dataRestoreFailed = $false
    try {
        if ($backup) {
            # Da co Data that tu truoc: khoi phuc lai nguyen ven.
            Remove-Item $dataDir -Recurse -Force
            Copy-Item $backup $dataDir -Recurse
        }
        elseif (Test-Path $dataDir) {
            # Ban dau KHONG co Data (ban publish moi tinh): phai xoa het du lieu thu
            # nghiem da tao ra, tra lai dung trang thai ban dau - khong duoc de lai
            # danh sach gia canh file .exe.
            Remove-Item $dataDir -Recurse -Force
        }
    }
    catch {
        $dataRestoreFailed = $true
        Write-Warning ("Khong the khoi phuc/don dep thu muc Data mot cach day du: " + $_.Exception.Message)
        Write-Warning ("Ban sao luu Data nam tai: " + $backup)
    }

    # Chi xoa thu muc tam neu khoi phuc Data thanh cong (neu co). Neu khoi phuc that bai,
    # giu lai $work de nguoi dung co the khoi phuc bang tay.
    if (-not $dataRestoreFailed) {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
    else {
        Write-Warning ("Thu muc tam da duoc giu lai: " + $work)
    }

    # Khoi phuc cac bien moi truong: neu bien khong ton tai truoc day (oldDelay = $null),
    # phai xoa hoan toan. Neu ton tai, khoi phuc lai gia tri cu.
    if ($null -eq $oldDelay) {
        Remove-Item Env:\WSA_FAKE_WINGET_DELAY_MS -ErrorAction SilentlyContinue
    }
    else {
        $env:WSA_FAKE_WINGET_DELAY_MS = $oldDelay
    }
    $env:PATH = $oldPath
}

Write-Host ""
Write-Host ("Ket qua: " + $pass + " dat, " + $fail + " truot")
if ($fail -gt 0) { exit 1 }
