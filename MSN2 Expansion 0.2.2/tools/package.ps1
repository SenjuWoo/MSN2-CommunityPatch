# Builds release archives in <owner>\dist\<version>\ (payload only, game-folder layout):
#   MSN2_CommunityPatch_v<ver>.zip  BepInEx 5.4.23.5 + MSN2CommunityPatch.dll (extract into the game folder)
#   MSN2_Expansion_v<ver>.zip       MSN2Expansion.dll + packs (needs BepInEx; the patch zip provides it)
param([Parameter(Mandatory)][string]$Version)
. "$PSScriptRoot\common.ps1"
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
& "$PSScriptRoot\build.ps1"
$dist = Join-Path $OwnerRoot "dist\$Version"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force $dist | Out-Null
$docs = Join-Path $VersionRoot 'docs\release'

function Stage([string]$name, [scriptblock]$fill) {
    $stage = Join-Path $env:TEMP "msn2pkg-$name"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    $plug = Join-Path $stage 'BepInEx\plugins\MSN2'
    New-Item -ItemType Directory -Force (Join-Path $plug 'docs') | Out-Null
    & $fill $stage $plug
    $zip = Join-Path $dist "$name`_v$Version.zip"
    # ZipFile.CreateFromDirectory on Windows PowerShell 5.1 writes '\' separators; write '/' entries explicitly
    $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem $stage -Recurse -File -Force | Sort-Object FullName | ForEach-Object {
            $rel = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $rel, [IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $archive.Dispose() }
    Remove-Item $stage -Recurse -Force
    $zip
}

$patch = Stage 'MSN2_CommunityPatch' {
    param($stage, $plug)
    Copy-Item (Join-Path $BepInExSrc 'winhttp.dll'), (Join-Path $BepInExSrc 'doorstop_config.ini'), (Join-Path $BepInExSrc '.doorstop_version') $stage
    Copy-Item (Join-Path $BepInExSrc 'BepInEx\core') (Join-Path $stage 'BepInEx\core') -Recurse
    Copy-Item (Join-Path $BepInExSrc 'LICENSE.BepInEx.txt') (Join-Path $stage 'BepInEx\LICENSE.BepInEx.txt')
    Copy-Item (Join-Path $VersionRoot 'src\MSN2CommunityPatch\bin\Release\MSN2CommunityPatch.dll') $plug
    Copy-Item (Join-Path $docs 'CommunityPatch-README.txt'), (Join-Path $docs 'CHANGELOG.txt') (Join-Path $plug 'docs')
}
$exp = Stage 'MSN2_Expansion' {
    param($stage, $plug)
    Copy-Item (Join-Path $VersionRoot 'src\MSN2Expansion\bin\Release\MSN2Expansion.dll') $plug
    Copy-Item (Join-Path $VersionRoot 'packs') (Join-Path $plug 'packs') -Recurse
    Copy-Item (Join-Path $docs 'Expansion-README.txt'), (Join-Path $docs 'CHANGELOG.txt'), (Join-Path $VersionRoot 'docs\PACK_FORMAT.md') (Join-Path $plug 'docs')
}

foreach ($z in $patch, $exp) {
    $entries = [IO.Compression.ZipFile]::OpenRead($z)
    try { $bad = $entries.Entries | Where-Object { $_.FullName -match '\\' -or $_.FullName -match '\.(ps1|cs|csproj)$' } }
    finally { $entries.Dispose() }
    if ($bad) { throw "bad entries in $z : $($bad.FullName -join ', ')" }
    '{0}  {1,9:N0} bytes  {2}' -f (Get-FileHash $z -Algorithm SHA256).Hash, (Get-Item $z).Length, (Split-Path $z -Leaf)
}
