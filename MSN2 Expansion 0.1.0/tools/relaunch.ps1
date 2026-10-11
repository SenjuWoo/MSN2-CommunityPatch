# Dev loop: quit running game, rebuild + install, launch, press Continue, wait for the Game scene.
param([int]$Settle = 15, [switch]$NoLoad, [hashtable]$Config)
. "$PSScriptRoot\common.ps1"
$agent = "$PSScriptRoot\agent.ps1"
if (Get-Process 'My Sexy Neighbour 2' -ErrorAction SilentlyContinue) {
    try { & $agent quit -Timeout 8 | Out-Null } catch { }
    Get-Process 'My Sexy Neighbour 2' -ErrorAction SilentlyContinue | Wait-Process -Timeout 40 -ErrorAction SilentlyContinue
    Get-Process 'My Sexy Neighbour 2' -ErrorAction SilentlyContinue | Stop-Process -Force
}
& "$PSScriptRoot\install.ps1" | Out-Null
if ($Config) {
    # dev-only overrides, e.g. -Config @{ VerboseLog = 'true' }
    $cfg = Join-Path $GameDir 'BepInEx\config\senjuwoo.msn2.communitypatch.cfg'
    if (Test-Path $cfg) {
        $text = Get-Content $cfg -Raw
        foreach ($k in $Config.Keys) { $text = $text -replace "(?m)^$k = .*$", "$k = $($Config[$k])" }
        Set-Content $cfg $text -Encoding utf8 -NoNewline
    }
}
& "$PSScriptRoot\launch.ps1" | Out-Null
if ($NoLoad) { return }
Start-Sleep 5
& $agent 'click Continue' | Out-Null
$sw = [Diagnostics.Stopwatch]::StartNew()
do { Start-Sleep 2; $scene = ((& $agent ping) | ConvertFrom-Json).scene } until ($scene -eq 'Game' -or $sw.Elapsed.TotalSeconds -gt 120)
if ($scene -ne 'Game') { throw "Game scene not reached (scene=$scene)" }
Start-Sleep $Settle
Write-Host "in Game scene after $([int]$sw.Elapsed.TotalSeconds)s (+$Settle s settle)"
