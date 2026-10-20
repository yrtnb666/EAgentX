<#
    一键编译 EAgentX（.NET Framework / WinForms，无 NuGet 运行时依赖）
    用法:
        powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
        powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -NoDownload
        powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Csc "C:\path\to\csc.exe"

    编译器查找顺序:
        1. -Csc 参数
        2. tools\.compiler\csc.exe（上次自动下载并缓存）
        3. Visual Studio / MSBuild 自带的 Roslyn csc.exe
        4. .NET SDK 里的 Roslyn（通过 dotnet exec 调用）
        5. Windows 自带的 %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe（C# 5）
        6. 从 nuget.org 下载 Microsoft.Net.Compilers.Toolset 并缓存到 tools\.compiler

    说明: 源码已整理为 C# 5 语法，Windows 自带的 csc.exe（C# 5）即可编译，
          不联网、不装 SDK 也能构建。代价是写法偏保守（无 ?.、无字符串插值、无 using 声明）。
#>

[CmdletBinding()]
param(
    [string]$Csc,
    [switch]$NoDownload,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root "src"
$distDir = Join-Path $root "dist"
$toolDir = Join-Path $root "tools\.compiler"
$exeOut = Join-Path $distDir "EAgentX-UI.exe"

function Write-Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "    $text" -ForegroundColor Green }

# ---------- 1. 找编译器 ----------
function Find-VsCsc {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $installs = & $vswhere -products * -requires Microsoft.Component.MSBuild -property installationPath 2>$null
        foreach ($i in $installs) {
            $hit = Get-ChildItem -Path (Join-Path $i "MSBuild") -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
                   Sort-Object FullName -Descending | Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }
    $globs = @(
        "$env:ProgramFiles\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\Roslyn\csc.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\Roslyn\csc.exe",
        "${env:ProgramFiles(x86)}\MSBuild\*\Bin\Roslyn\csc.exe",
        "$env:ProgramFiles\MSBuild\*\Bin\Roslyn\csc.exe"
    )
    foreach ($g in $globs) {
        $hit = Get-ChildItem -Path $g -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

function Install-RoslynToolset {
    if ($NoDownload) { throw "没找到 Roslyn 编译器，且指定了 -NoDownload。" }
    $version = "4.11.0"
    $url = "https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/$version/microsoft.net.compilers.toolset.$version.nupkg"
    $nupkg = Join-Path $env:TEMP "microsoft.net.compilers.toolset.$version.nupkg"
    $zip = Join-Path $env:TEMP "microsoft.net.compilers.toolset.$version.zip"

    Write-Step "下载 Roslyn 编译器 Microsoft.Net.Compilers.Toolset $version（约 7 MB，只需一次）"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $url -OutFile $nupkg -UseBasicParsing
    Copy-Item $nupkg $zip -Force
    if (Test-Path $toolDir) { Remove-Item $toolDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $toolDir | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $toolDir)
    $found = Get-ChildItem -Path $toolDir -Recurse -Filter csc.exe | Select-Object -First 1
    if (-not $found) { throw "下载的编译器包里没有 csc.exe" }
    # 把 net472 目录的内容平铺到 tools\.compiler，方便下次直接调用
    $flat = Split-Path $found.FullName -Parent
    Get-ChildItem $flat -File | ForEach-Object {
        $target = Join-Path $toolDir $_.Name
        if ($_.FullName -ne $target) { Copy-Item $_.FullName $target -Force }
    }
    return (Join-Path $toolDir "csc.exe")
}

# Windows 自带的 C# 5 编译器；源码已降级到 C# 5，所以它可以直接用（无需联网）
function Find-FrameworkCsc {
    $candidates = @(
        (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
        (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

function Find-SdkCsc {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { return $null }
    $sdkRoot = Join-Path $env:ProgramFiles "dotnet\sdk"
    if (-not (Test-Path $sdkRoot)) { return $null }
    $hit = Get-ChildItem -Path $sdkRoot -Recurse -Filter csc.dll -ErrorAction SilentlyContinue |
           Where-Object { $_.FullName -match 'Roslyn\\bincore\\csc\.dll$' } |
           Sort-Object FullName -Descending | Select-Object -First 1
    if ($hit) { return $hit.FullName }
    return $null
}

$cscExe = $null
$cscPrefix = @()

if ($Csc) {
    if (-not (Test-Path $Csc)) { throw "指定的编译器不存在: $Csc" }
    $cscExe = $Csc
}
elseif (Test-Path (Join-Path $toolDir "csc.exe")) {
    $cscExe = Join-Path $toolDir "csc.exe"
}
else {
    $cscExe = Find-VsCsc
    if (-not $cscExe) {
        $dll = Find-SdkCsc
        if ($dll) { $cscPrefix = @("exec", $dll) ; $cscExe = (Get-Command dotnet).Source }
    }
    if (-not $cscExe) { $cscExe = Find-FrameworkCsc }
    if (-not $cscExe) { $cscExe = Install-RoslynToolset }
}

Write-Step "编译器: $cscExe"
if ($cscPrefix.Count -gt 0) { Write-Ok ($cscPrefix -join " ") }

# ---------- 2. 准备输出目录 ----------
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }

$fw = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path (Join-Path $fw "System.dll"))) {
    $fw = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319"
}

$refs = @("System.dll", "System.Core.dll", "System.Drawing.dll", "System.Windows.Forms.dll",
          "System.Web.Extensions.dll", "System.Net.Http.dll", "System.IO.Compression.dll") |
        ForEach-Object { "/r:" + (Join-Path $fw $_) }

$skillsZip = Join-Path $root "assets\skill_zip.zip"
if (-not (Test-Path $skillsZip)) { throw "缺少 assets\skill_zip.zip（26 个技能的内嵌包）" }
$promptFile = Join-Path $root "assets\eagent_prompt.txt"
if (-not (Test-Path $promptFile)) { throw "缺少 assets\eagent_prompt.txt（EAgent 内置提示词）" }
$resources = @(
    "/resource:" + $skillsZip + ",skills.zip"
    "/resource:" + $promptFile + ",eagent_prompt.txt"
)

$files = Get-ChildItem -Path $srcDir -Recurse -Filter *.cs |
         Sort-Object FullName | Select-Object -ExpandProperty FullName
if (-not $files) { throw "src\ 下没有找到 .cs 文件" }

$ico = Join-Path $srcDir "app.ico"

# ---------- 3. 编译 ----------
$cscArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/codepage:65001",
    "/langversion:5",
    "/define:TRACE",
    "/optimize+",
    "/out:$exeOut",
    "/win32icon:$ico"
) + $resources + $refs + $files

if ($Configuration -eq "Debug") {
    $cscArgs = $cscArgs | Where-Object { $_ -ne "/optimize+" }
    $cscArgs += @("/debug:pdbonly", "/define:DEBUG")
}

Write-Step "编译 $($files.Count) 个源文件"
if ($cscPrefix.Count -gt 0) {
    $exe = $cscPrefix[0]
    $argv = @($cscPrefix[1]) + $cscArgs
    & $exe @argv
}
else {
    & $cscExe @cscArgs
}
if ($LASTEXITCODE -ne 0) { throw "编译失败（exit $LASTEXITCODE）" }

# ---------- 4. 结果 ----------
$item = Get-Item $exeOut
$hash = (Get-FileHash $exeOut -Algorithm SHA256).Hash
Write-Ok ("产物: {0}  ({1:N0} 字节, {2})" -f $item.FullName, $item.Length, $Configuration)
Write-Ok "SHA256: $hash"

if ((Get-Command Get-AuthenticodeSignature -ErrorAction SilentlyContinue) -and
    (Get-AuthenticodeSignature $exeOut).Status -ne "Valid") {
    Write-Host "    提示: exe 未签名，杀软可能启发式误报（原作同样未签名）" -ForegroundColor DarkYellow
}
