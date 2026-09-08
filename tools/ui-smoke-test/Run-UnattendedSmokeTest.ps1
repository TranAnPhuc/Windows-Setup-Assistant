param(
    [string]$AppExe = "..\..\publish\WindowsSetupAssistant.exe",
    [string]$FakeWingetDir = ".\fake-winget\bin\Release\net8.0"
)
$ErrorActionPreference = 'Stop'

$appExe  = (Resolve-Path $AppExe).Path
$fakeBin = (Resolve-Path $FakeWingetDir).Path
$appDir  = Split-Path $appExe -Parent

$pass = 0; $fail = 0
function Check($label, $ok) {
    if ($ok) { $script:pass++; Write-Host ("  [DAT]   " + $label) }
    else     { $script:fail++; Write-Host ("  [TRUOT] " + $label) }
}

# Dat winget GIA len dau PATH: khong co phan mem that nao duoc cai.
$env:PATH = $fakeBin + ";" + $env:PATH

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
    $p = Start-Process -FilePath $appExe -ArgumentList $AppArgs -NoNewWindow -Wait -PassThru `
                       -RedirectStandardOutput $out -RedirectStandardError $err
    return [pscustomobject]@{
        ExitCode = $p.ExitCode
        StdOut   = (Get-Content $out -Raw -ErrorAction SilentlyContinue)
        StdErr   = (Get-Content $err -Raw -ErrorAction SilentlyContinue)
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
    Check "khong cua so nao mo ra"     (-not (Get-Process -Name "WindowsSetupAssistant" -ErrorAction SilentlyContinue))
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
    if ($backup) {
        Remove-Item $dataDir -Recurse -Force
        Copy-Item $backup $dataDir -Recurse
    }
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host ("Ket qua: " + $pass + " dat, " + $fail + " truot")
if ($fail -gt 0) { exit 1 }
