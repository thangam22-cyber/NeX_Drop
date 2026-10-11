# NeX_Drop — Uninstall (Integrator only, Admin required)
param([string]$InstallDir = "C:\Program Files\NeX_Drop", [string]$ServiceName = "NeX_Drop_KillSwitch")
$ErrorActionPreference = "Stop"
Write-Host "NeX_Drop Uninstall" -ForegroundColor Red

if (Get-Service $ServiceName -EA SilentlyContinue) { Stop-Service $ServiceName -Force; sc.exe delete $ServiceName | Out-Null }
$reg = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
$bk  = (Get-ItemProperty $reg -Name "Shell_NeX_Drop_Backup" -EA SilentlyContinue)?.Shell_NeX_Drop_Backup
Set-ItemProperty $reg -Name "Shell" -Value ($bk ?? "explorer.exe") -Type String
$p   = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System"
if (Test-Path $p) { Remove-ItemProperty $p -Name "DisableTaskMgr" -EA SilentlyContinue }
$c   = Read-Host "Remove '$InstallDir'? (y/N)"; if ($c -eq 'y') { Remove-Item -Recurse -Force $InstallDir -EA SilentlyContinue }
Write-Host "Done. Restart device." -ForegroundColor Green
