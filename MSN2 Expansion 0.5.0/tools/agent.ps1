# Sends one command to the in-game AgentBridge and prints the JSON result.
# Examples: .\agent.ps1 ping | .\agent.ps1 'fps 10' -Timeout 30 | .\agent.ps1 'console addMoney 100'
param([Parameter(Mandatory)][string]$Command, [int]$Timeout = 20)
. "$PSScriptRoot\common.ps1"
$id = 'c' + [DateTime]::Now.ToString('HHmmssfff')
New-Item -ItemType Directory -Force $AgentDir | Out-Null
$out = Join-Path $AgentDir "out\$id.json"
Add-Content (Join-Path $AgentDir 'cmd.txt') "$id|$Command" -Encoding utf8
$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not (Test-Path $out)) {
    if ($sw.Elapsed.TotalSeconds -gt $Timeout) { throw "no reply to '$Command' within $Timeout s (game running? plugin loaded?)" }
    Start-Sleep -Milliseconds 200
}
Start-Sleep -Milliseconds 100
Get-Content $out -Raw
