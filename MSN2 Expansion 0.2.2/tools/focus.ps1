# Brings the game window to the foreground. The Input System ignores injected keys while the game is unfocused, so key-based tests need this.
. "$PSScriptRoot\common.ps1"
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public class MSN2Focus {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, int flags, int extra);
}
'@
$p = Get-Process 'My Sexy Neighbour 2' -ErrorAction Stop | Select-Object -First 1
[MSN2Focus]::ShowWindow($p.MainWindowHandle, 9) | Out-Null          # restore if minimised
[MSN2Focus]::keybd_event(0x12, 0, 0, 0); [MSN2Focus]::keybd_event(0x12, 0, 2, 0)   # an Alt tap lets a background process take the foreground
[MSN2Focus]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 400
Write-Host "game window focused (pid $($p.Id))"
