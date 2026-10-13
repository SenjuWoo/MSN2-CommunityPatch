# Backs up saves, starts the game and waits for the plugin to answer `ping`.
param([int]$Timeout = 120)
. "$PSScriptRoot\common.ps1"
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) { throw 'Start Steam first.' }
if (Test-Path $SaveDir) {
    $bk = Join-Path $OwnerRoot ("backups\saves\" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force $bk | Out-Null
    Get-ChildItem $SaveDir -File -Filter '*.dat' | Copy-Item -Destination $bk
    Write-Host "saves backed up -> $bk"
}
Remove-Item (Join-Path $AgentDir 'cmd.txt') -ErrorAction SilentlyContinue
Start-Process -FilePath $GameExe -WorkingDirectory $GameDir
$sw = [Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $Timeout) {
    Start-Sleep 3
    try { & "$PSScriptRoot\agent.ps1" ping -Timeout 5; return } catch { }
}
throw "plugin did not answer within $Timeout s; check $GameDir\BepInEx\LogOutput.log"
