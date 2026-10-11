# NeX_Drop — Install Script (Run as Administrator)
param(
    [string]$InstallDir  = "C:\Program Files\NeX_Drop",
    [string]$ServiceName = "NeX_Drop_KillSwitch",
    [string]$BuildDir    = "$PSScriptRoot\publish"
)
$ErrorActionPreference = "Stop"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole("Administrator")
if (-not $isAdmin) { Write-Error "Run as Administrator"; exit 1 }

Write-Host "=== NeX_Drop v1.0 Install ===" -ForegroundColor Cyan

# Build
Write-Host "[1/5] Building..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\NeX_Drop.sln" -c Release -r win-x64 --self-contained false -o $BuildDir
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

# Copy files
Write-Host "[2/5] Installing files..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item "$BuildDir\*" -Destination $InstallDir -Recurse -Force

# Register service
Write-Host "[3/5] Registering service..." -ForegroundColor Yellow
if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null; Start-Sleep 2
}
New-Service -Name $ServiceName -BinaryPathName "$InstallDir\NeX_Drop.Service.exe" `
    -DisplayName "NeX Drop Enforcement" -StartupType Automatic

# Registry shell replacement
Write-Host "[4/5] Setting shell..." -ForegroundColor Yellow
$reg = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
$old = (Get-ItemProperty $reg -Name Shell).Shell
Set-ItemProperty $reg -Name "Shell_NeX_Drop_Backup" -Value $old -Type String
Set-ItemProperty $reg -Name "Shell" -Value "$InstallDir\NeX_Drop.Shell.exe" -Type String

# Start service
Write-Host "[5/5] Starting service..." -ForegroundColor Yellow
Start-Service $ServiceName

Write-Host "=== Done! Restart device to activate ===" -ForegroundColor Green
