param(
    # Duong dan file .exe da publish (phai co thu muc Data ben canh)
    [string]$AppExe = "..\..\publish\WindowsSetupAssistant.exe",
    # Thu muc chua winget.exe GIA
    [string]$FakeWingetDir = ".\fake-winget\bin\Release\net8.0"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [DllImport("user32.dll")]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
"@

$AE  = [System.Windows.Automation.AutomationElement]
$TS  = [System.Windows.Automation.TreeScope]
$INV = [System.Windows.Automation.InvokePattern]
$WIN = [System.Windows.Automation.WindowPattern]
$CT  = [System.Windows.Automation.ControlType]
$root = $AE::RootElement

$WM_COMMAND = 0x0111
$IDYES = 6
$IDNO  = 7

$appExe  = (Resolve-Path $AppExe).Path
$fakeBin = (Resolve-Path $FakeWingetDir).Path

$pass = 0; $fail = 0
function Check($label, $ok) {
    if ($ok) { $script:pass++; Write-Host ("  [DAT]   " + $label) }
    else     { $script:fail++; Write-Host ("  [TRUOT] " + $label) }
}

function Get-TopWindows($processId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $processId)
    return @($root.FindAll($TS::Children, $cond))
}

# UI Automation long cua so con (co Owner) vao ben trong cay cua cua so cha.
function Get-ProcessWindows($processId) {
    $result = @()
    $winCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Window)
    foreach ($top in (Get-TopWindows $processId)) {
        $result += $top
        try { $result += @($top.FindAll($TS::Descendants, $winCond)) } catch { }
    }
    return $result
}

function Get-MainWindow($processId) {
    foreach ($w in (Get-TopWindows $processId)) {
        if ($w.Current.Name -eq "Windows Setup Assistant") { return $w }
    }
    return $null
}

function Wait-Window($processId, $namePattern, $timeoutSec) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
        foreach ($w in (Get-ProcessWindows $processId)) {
            try { if ($w.Current.Name -like $namePattern) { return $w } } catch { }
        }
        Start-Sleep -Milliseconds 250
    }
    return $null
}

function Find-Button($window, $name) {
    $c1 = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    $c2 = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Button)
    $cond = New-Object System.Windows.Automation.AndCondition($c1, $c2)
    return $window.FindFirst($TS::Descendants, $cond)
}

function Invoke-Button($button) {
    $button.GetCurrentPattern($INV::Pattern).Invoke()
}

# MessageBox cua Win32 khong bao cac nut duoi dang Button trong moi truong nay,
# nen gui thang WM_COMMAND voi ma nut chuan (IDYES / IDNO) cho chac chan.
function Click-MessageBoxButton($box, $buttonId) {
    $hwnd = New-Object IntPtr($box.Current.NativeWindowHandle)
    [Win32]::PostMessage($hwnd, $WM_COMMAND, [IntPtr]$buttonId, [IntPtr]::Zero) | Out-Null
}

function Get-MessageBoxText($box) {
    $texts = @()
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $child = $walker.GetFirstChild($box)
    while ($null -ne $child) {
        $n = $child.Current.Name
        if ($n -and $n.Length -gt 10) { $texts += $n }
        $child = $walker.GetNextSibling($child)
    }
    return ($texts -join " ")
}

# Nhan biet "dang cai dat" bang chinh giao dien: nut Huy chi hien khi dang cai.
function Test-Installing($processId) {
    $main = Get-MainWindow $processId
    if ($null -eq $main) { return $false }
    return ($null -ne (Find-Button $main "Huỷ cài đặt"))
}

# Chi dem winget GIA (theo duong dan file), khong dung Win32_Process cho don gian va nhanh.
function Get-FakeWingetProcesses($parentId) {
    return @(Get-Process -Name winget -ErrorAction SilentlyContinue |
             Where-Object { try { $_.Path -like ($fakeBin + "*") } catch { $false } })
}

function Wait-Until($condition, $timeoutSec) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
        if (& $condition) { return $true }
        Start-Sleep -Milliseconds 250
    }
    return $false
}

function Cleanup-FakeWinget {
    Get-Process winget -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -like ($fakeBin + "*") } catch { $false } } |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

Cleanup-FakeWinget

Write-Host "=== CHUAN BI ==="
$env:PATH = $fakeBin + ";" + $env:PATH
$resolved = (Get-Command winget).Source
Write-Host ("winget dang tro toi: " + $resolved)
Check "Dung winget GIA - khong co phan mem that nao duoc cai" ($resolved -like ($fakeBin + "*"))

Write-Host ""
Write-Host "=== 1. DONG UNG DUNG KHI KHONG CAI DAT: khong duoc hoi gi ==="
$proc = Start-Process $appExe -PassThru
$main = Wait-Window $proc.Id "Windows Setup Assistant" 30
Check "Cua so chinh mo duoc" ($null -ne $main)
if ($null -eq $main) { exit 1 }
Start-Sleep -Seconds 5

$main.GetCurrentPattern($WIN::Pattern).Close()
Check "Dong ngay lap tuc, khong hien hop thoai nao" (Wait-Until { $proc.HasExited } 15)
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }

Write-Host ""
Write-Host "=== 2. BAT DAU CAI DAT ==="
$proc = Start-Process $appExe -PassThru
$main = Wait-Window $proc.Id "Windows Setup Assistant" 30
Check "Cua so chinh mo lai duoc" ($null -ne $main)
Start-Sleep -Seconds 5

Invoke-Button (Find-Button $main "Bắt đầu cài đặt")
$confirm = Wait-Window $proc.Id "Xác nhận cài đặt" 20
Check "Hop XAC NHAN truoc khi cai hang loat da hien" ($null -ne $confirm)
if ($null -eq $confirm) { Stop-Process -Id $proc.Id -Force; exit 1 }
Start-Sleep -Milliseconds 800

Invoke-Button (Find-Button $confirm "Bắt đầu cài đặt")
Check "Giao dien chuyen sang trang thai DANG CAI (nut Huy hien ra)" (Wait-Until { Test-Installing $proc.Id } 25)

# Nut Huy hien ra NGAY khi hang doi bat dau, som hon luc tien trinh con kip sinh ra,
# nen phai cho them roi moi do.
$hasChild = Wait-Until { (Get-FakeWingetProcesses $proc.Id).Count -gt 0 } 20
Check "Da sinh tien trinh winget con" $hasChild
$childPid = $null
if ($hasChild) {
    $childPid = (Get-FakeWingetProcesses $proc.Id)[0].Id
    Write-Host ("     pid tien trinh winget con = " + $childPid)
}

Write-Host ""
Write-Host "=== 3. BAM X LUC DANG CAI -> TRA LOI 'No' ==="
$main = Get-MainWindow $proc.Id
$main.GetCurrentPattern($WIN::Pattern).Close()

$box = Wait-Window $proc.Id "Đang cài đặt" 15
Check "Hop XAC NHAN khi dong luc dang cai da hien" ($null -ne $box)
if ($null -ne $box) {
    Start-Sleep -Milliseconds 800
    $msg = Get-MessageBoxText $box
    Write-Host ("     noi dung: " + ($msg -replace "`r`n", " "))
    Check "Noi dung canh bao dung y nghia" ($msg -match "hu(ỷ|y) các gói còn lại")
    Click-MessageBoxButton $box $IDNO
}

Start-Sleep -Seconds 3
Check "Ung dung VAN CHAY sau khi tra loi No" (-not $proc.HasExited)
Check "Cua so chinh van con do" ($null -ne (Get-MainWindow $proc.Id))
Check "Hang doi VAN DANG CHAY (chua bi huy)" (Test-Installing $proc.Id)
Check "Tien trinh winget con van song (dung pid da ghi nhan)" ($null -ne $childPid -and $null -ne (Get-Process -Id $childPid -ErrorAction SilentlyContinue))

Write-Host ""
Write-Host "=== 4. BAM X LAN NUA -> TRA LOI 'Yes' ==="
$main = Get-MainWindow $proc.Id
$main.GetCurrentPattern($WIN::Pattern).Close()

$box2 = Wait-Window $proc.Id "Đang cài đặt" 15
Check "Hop xac nhan hien lai lan hai" ($null -ne $box2)
if ($null -ne $box2) {
    Start-Sleep -Milliseconds 800
    Click-MessageBoxButton $box2 $IDYES
}

$exited = Wait-Until { $proc.HasExited } 30
Check "Ung dung DA THOAT sau khi tra loi Yes" $exited
if (-not $exited) {
    Write-Host "     --- app chua thoat, cac cua so dang mo ---"
    foreach ($w in (Get-ProcessWindows $proc.Id)) {
        Write-Host ("       '" + $w.Current.Name + "' class=" + $w.Current.ClassName)
        if ($w.Current.ClassName -eq "#32770") { Write-Host ("          noi dung: " + (Get-MessageBoxText $w)) }
    }
}
Check "Tien trinh winget con da bi dung theo" ($null -ne $childPid -and (Wait-Until { $null -eq (Get-Process -Id $childPid -ErrorAction SilentlyContinue) } 15))

if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
Cleanup-FakeWinget

Write-Host ""
Write-Host "=== KET QUA ==="
Write-Host ("Dat: " + $pass + " | Truot: " + $fail)
if ($fail -gt 0) { exit 1 } else { exit 0 }
