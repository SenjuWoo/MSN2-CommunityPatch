# Removes the MSN2 plugins. -RemoveBepInEx also removes the loader if this project installed it.
param([switch]$RemoveBepInEx)
. "$PSScriptRoot\common.ps1"
if (Get-Process 'My Sexy Neighbour 2' -ErrorAction SilentlyContinue) { throw 'Close the game first.' }
if (Test-Path $PluginDir) { Remove-Item $PluginDir -Recurse -Force; Write-Host "removed $PluginDir" }
$manifest = Join-Path $GameDir 'BepInEx\MSN2\installed-by-msn2.txt'
if ($RemoveBepInEx) {
    if (-not (Test-Path $manifest)) { throw 'BepInEx was not installed by this project; leaving it.' }
    foreach ($f in 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version') { Remove-Item (Join-Path $GameDir $f) -Force -ErrorAction SilentlyContinue }
    Remove-Item (Join-Path $GameDir 'BepInEx') -Recurse -Force
    Write-Host 'BepInEx removed'
}
