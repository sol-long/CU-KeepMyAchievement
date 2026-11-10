param(
    # 游戏根目录（包含 CasualtiesUnknown.exe / BepInEx / CasualtiesUnknown_Data 的那一层）
    [string]$GameDir = '',
    # 编译完直接把 dll 复制到 <GameDir>\BepInEx\plugins\CU-KeepMyAchievement\
    [switch]$Deploy
)

# 只用 Windows 自带的 .NET Framework 4.x 编译器，不联网、不需要 SDK / NuGet。
$ErrorActionPreference = 'Stop'

$src    = Join-Path $PSScriptRoot 'src\KeepMyAchievement.cs'
$binDir = Join-Path $PSScriptRoot 'bin'
$out    = Join-Path $binDir 'CU-KeepMyAchievement.dll'
$csc    = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not $GameDir) {
    # 依次尝试：仓库内的游戏副本 -> 与仓库同级 -> 报错要求 -GameDir
    $candidates = @(
        (Join-Path $PSScriptRoot 'Casualties Unknown'),
        (Join-Path (Split-Path -Parent $PSScriptRoot) 'Casualties Unknown')
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $c 'CasualtiesUnknown_Data\Managed')) { $GameDir = $c; break }
    }
}

$managed = if ($GameDir) { Join-Path $GameDir 'CasualtiesUnknown_Data\Managed' } else { '' }
$core    = if ($GameDir) { Join-Path $GameDir 'BepInEx\core' } else { '' }

if (-not (Test-Path -LiteralPath $src)) { throw "missing: $src" }
if (-not (Test-Path -LiteralPath $csc)) { throw "missing: $csc" }
foreach ($p in @($managed, $core)) {
    if (-not (Test-Path -LiteralPath $p)) {
        throw @"
找不到游戏程序集：$p
请用 -GameDir 指定游戏根目录，例如：
  pwsh -File build.ps1 -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\Casualties Unknown'
"@
    }
}

New-Item -ItemType Directory -Force -Path $binDir | Out-Null

$refs = @(
    (Join-Path $core    'BepInEx.dll'),
    (Join-Path $core    '0Harmony.dll'),
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'netstandard.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'Newtonsoft.Json.dll')
)

$cscArgs = @('/nologo', '/target:library', '/platform:x64', '/optimize+', '/langversion:5', '/codepage:65001', "/out:$out")
$cscArgs += $src
foreach ($r in $refs) { $cscArgs += "/r:$r" }

Write-Host "csc -> $out"
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "compile failed (exit $LASTEXITCODE)" }

$fi = Get-Item -LiteralPath $out
Write-Host ("OK  {0}  ({1} bytes)" -f $fi.FullName, $fi.Length)

if ($Deploy) {
    $dest = Join-Path $GameDir 'BepInEx\plugins\CU-KeepMyAchievement'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item -LiteralPath $out -Destination $dest -Force
    Write-Host "已部署到 $dest"
}
