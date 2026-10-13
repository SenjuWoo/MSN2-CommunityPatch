# Shared paths. Override the game folder with $env:MSN2_GAME_DIR.
$ErrorActionPreference = 'Stop'
$Script:VersionRoot = Split-Path $PSScriptRoot -Parent
$Script:OwnerRoot   = Split-Path $VersionRoot -Parent

# Finds the game: $env:MSN2_GAME_DIR, else the first Steam library (Steam's own list) that holds it.
function Find-GameDir {
    if ($env:MSN2_GAME_DIR) { return $env:MSN2_GAME_DIR }
    $libs = @()
    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if ($steam) {
        $libs += $steam
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            # VDF escapes backslashes as \\ inside quotes
            foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $libs += ($m.Groups[1].Value -replace '\\\\', '\')
            }
        }
    }
    foreach ($lib in $libs) {
        $dir = Join-Path $lib 'steamapps\common\My sexy Neighbour 2'
        if (Test-Path -LiteralPath (Join-Path $dir 'My Sexy Neighbour 2.exe')) { return $dir }
    }
    return 'S:\SteamLibrary\steamapps\common\My sexy Neighbour 2'   # last resort; the throw below names it
}
$Script:GameDir     = Find-GameDir
$Script:GameExe     = Join-Path $GameDir 'My Sexy Neighbour 2.exe'
$Script:BepInExSrc  = Join-Path $OwnerRoot 'vendor\BepInEx_5.4.23.5'
$Script:PluginDir   = Join-Path $GameDir 'BepInEx\plugins\MSN2'
$Script:AgentDir    = Join-Path $GameDir 'BepInEx\MSN2\agent'
$Script:SaveDir     = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow\DefaultCompany\My Sexy Neighbour 2'
$Script:Projects    = @('MSN2CommunityPatch', 'MSN2Expansion')
if (-not (Test-Path -LiteralPath $GameExe)) { throw "Game not found: $GameExe (set MSN2_GAME_DIR)" }
