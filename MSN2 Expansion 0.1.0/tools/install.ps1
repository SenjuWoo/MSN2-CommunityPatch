# Installs BepInEx 5.4.23.5 (if absent) and the MSN2 plugins into the game folder. Vanilla files are never modified.
param([switch]$SkipBuild)
. "$PSScriptRoot\common.ps1"
if (Get-Process 'My Sexy Neighbour 2' -ErrorAction SilentlyContinue) { throw 'Close the game first.' }
if (-not $SkipBuild) { & "$PSScriptRoot\build.ps1" }

$manifest = Join-Path $GameDir 'BepInEx\MSN2\installed-by-msn2.txt'
if (-not (Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))) {
    if (Test-Path (Join-Path $GameDir 'winhttp.dll')) { throw 'winhttp.dll exists but no BepInEx core: another loader/proxy is installed; resolve manually.' }
    Copy-Item (Join-Path $BepInExSrc 'winhttp.dll'), (Join-Path $BepInExSrc 'doorstop_config.ini'), (Join-Path $BepInExSrc '.doorstop_version') $GameDir
    Copy-Item (Join-Path $BepInExSrc 'BepInEx') $GameDir -Recurse
    New-Item -ItemType Directory -Force (Split-Path $manifest) | Out-Null
    "bepinex=5.4.23.5`nwinhttp.dll`ndoorstop_config.ini`n.doorstop_version`nBepInEx\" | Set-Content $manifest -Encoding utf8
    Write-Host 'BepInEx 5.4.23.5 installed'
}
New-Item -ItemType Directory -Force $PluginDir | Out-Null
foreach ($p in $Projects) { Copy-Item (Join-Path $VersionRoot "src\$p\bin\Release\$p.dll") $PluginDir -Force }
$packs = Join-Path $VersionRoot 'packs'
if (Test-Path $packs) { Copy-Item $packs (Join-Path $PluginDir 'packs') -Recurse -Force }
Get-ChildItem $PluginDir -Recurse -File | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.Substring(0, 12), $_.FullName.Substring($GameDir.Length + 1) }
