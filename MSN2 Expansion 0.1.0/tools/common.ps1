# Shared paths. Override the game folder with $env:MSN2_GAME_DIR.
$ErrorActionPreference = 'Stop'
$Script:VersionRoot = Split-Path $PSScriptRoot -Parent
$Script:OwnerRoot   = Split-Path $VersionRoot -Parent
$Script:GameDir     = if ($env:MSN2_GAME_DIR) { $env:MSN2_GAME_DIR } else { 'S:\SteamLibrary\steamapps\common\My sexy Neighbour 2' }
$Script:GameExe     = Join-Path $GameDir 'My Sexy Neighbour 2.exe'
$Script:BepInExSrc  = Join-Path $OwnerRoot 'vendor\BepInEx_5.4.23.5'
$Script:PluginDir   = Join-Path $GameDir 'BepInEx\plugins\MSN2'
$Script:AgentDir    = Join-Path $GameDir 'BepInEx\MSN2\agent'
$Script:SaveDir     = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow\DefaultCompany\My Sexy Neighbour 2'
$Script:Projects    = @('MSN2CommunityPatch', 'MSN2Expansion')
if (-not (Test-Path -LiteralPath $GameExe)) { throw "Game not found: $GameExe (set MSN2_GAME_DIR)" }
