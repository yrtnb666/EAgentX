# 开发期截图（不参与构建）：用 PrintWindow 打印窗口自身画面（被其它窗口遮挡也能截到）
param(
    [string]$Out = "docs\uishot.png",
    [string]$Theme = "light",
    [string]$Scale = "",
    [string]$Dialog = "",
    [string]$Scroll = ""
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WCap {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, ref RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    // 取该进程里"除主窗口以外"的第一个可见顶层窗口（弹窗）
    public static IntPtr FindOtherWindow(int processId, IntPtr main) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr p) {
            int pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid != processId || h == main || !IsWindowVisible(h)) return true;
            RECT r = new RECT();
            if (!GetWindowRect(h, ref r)) return true;
            if (r.Right - r.Left < 120 || r.Bottom - r.Top < 80) return true;
            found = h;
            return false;
        }, IntPtr.Zero);
        return found;
    }
}
"@
[WCap]::SetProcessDPIAware() | Out-Null

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = Join-Path $root "dist\EAgentX-UI.exe"
$cfgDir = Join-Path $env:APPDATA "EAgentX-UI"
if (-not (Test-Path $cfgDir)) { New-Item -ItemType Directory -Path $cfgDir | Out-Null }
$mode = if ($Theme -eq "dark") { "dark" } else { "light" }
Set-Content -Path (Join-Path $cfgDir "theme.json") -Value "{`"version`":1,`"mode`":`"$mode`"}" -Encoding UTF8

if ($Scale -ne "") { $env:EAGENTX_UI_SCALE = $Scale } else { Remove-Item Env:\EAGENTX_UI_SCALE -ErrorAction SilentlyContinue }
if ($Dialog -ne "") { $env:EAGENTX_UISHOT = $Dialog } else { Remove-Item Env:\EAGENTX_UISHOT -ErrorAction SilentlyContinue }

Get-Process EAgentX-UI -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

if ($Scroll -ne "") { $env:EAGENTX_UISCROLL = $Scroll } else { Remove-Item Env://EAGENTX_UISCROLL -ErrorAction SilentlyContinue }
$p = Start-Process -FilePath $exe -PassThru
for ($i = 0; $i -lt 80; $i++) { Start-Sleep -Milliseconds 250; $p.Refresh(); if ($p.MainWindowHandle -ne 0) { break } }
Start-Sleep -Milliseconds 2400
$p.Refresh()
$main = $p.MainWindowHandle
if ($main -eq 0) { Write-Output "no window"; exit 1 }

$target = $main
if ($Dialog -ne "") {
    $target = [WCap]::FindOtherWindow($p.Id, $main)
    if ($target -eq [IntPtr]::Zero) { Write-Output "dialog window not found"; Stop-Process -Id $p.Id -Force; exit 1 }
}

$r = New-Object WCap+RECT
[WCap]::GetWindowRect($target, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$hgt = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $hgt
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [WCap]::PrintWindow($target, $hdc, 2)
$g.ReleaseHdc($hdc)
$g.Dispose()
$path = Join-Path $root $Out
$dir = Split-Path -Parent $path
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved $path ($w x $hgt) print=$ok"

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
