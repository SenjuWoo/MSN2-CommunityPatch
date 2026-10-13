param([string]$Configuration = 'Release')
. "$PSScriptRoot\common.ps1"
foreach ($p in $Projects) {
    dotnet build (Join-Path $VersionRoot "src\$p\$p.csproj") -c $Configuration -nologo -v q "-p:GameDir=$GameDir"
    if ($LASTEXITCODE) { throw "build failed: $p" }
}
Write-Host "built: $($Projects -join ', ')"
