# WSA Debug Launcher - Simple Version
param(
    [switch]$Development,
    [switch]$Help
)

if ($Help) {
    Write-Host "WSA Debug Launcher for WindowsLauncher" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\Debug-WSA-Simple.ps1 [OPTIONS]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -Development    Use Development environment" -ForegroundColor Gray
    Write-Host "  -Help           Show this help message" -ForegroundColor Gray
    Write-Host ""
    exit 0
}

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$LogsDir = "$ProjectRoot\Logs"
$ExecutablePath = "$ProjectRoot\WindowsLauncher.UI\bin\Debug\net8.0-windows\win-x64\WindowsLauncher.UI.exe"
$ProjectFile = "$ProjectRoot\WindowsLauncher.UI\WindowsLauncher.UI.csproj"

Write-Host "===== WSA Debug Launcher =====" -ForegroundColor Cyan
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Gray
Write-Host ""

# Check WSA
Write-Host "Checking WSA prerequisites..." -ForegroundColor Yellow
$wsaPackage = Get-AppxPackage -Name "*WindowsSubsystemForAndroid*" -ErrorAction SilentlyContinue
if ($wsaPackage) {
    Write-Host "WSA Package found: $($wsaPackage.Name)" -ForegroundColor Green
} else {
    Write-Host "WARNING: WSA not found!" -ForegroundColor Red
}

# Build
Write-Host "Building WindowsLauncher..." -ForegroundColor Yellow
$buildConfig = if ($Development) { "Debug" } else { "Debug" }
$buildResult = dotnet build $ProjectFile --configuration $buildConfig --verbosity minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build completed successfully" -ForegroundColor Green
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Set environment
$environment = if ($Development) { "Development" } else { "Production" }
$env:ASPNETCORE_ENVIRONMENT = $environment
$env:DOTNET_ENVIRONMENT = $environment

Write-Host ""
# Create logs directory and clear old logs
Write-Host "Preparing logs directory..." -ForegroundColor Yellow
$logPath = "C:\WindowsLauncher\Logs"
if (-not (Test-Path $logPath)) {
    New-Item -ItemType Directory -Path $logPath -Force | Out-Null
    Write-Host "Created logs directory: $logPath" -ForegroundColor Green
} else {
    Remove-Item "$logPath\*" -Force -Recurse -ErrorAction SilentlyContinue
    Write-Host "Cleared old logs from: $logPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "Environment: $environment" -ForegroundColor Gray
Write-Host "Executable: $ExecutablePath" -ForegroundColor Gray
Write-Host "Logs Directory: $logPath" -ForegroundColor Gray
Write-Host "Ready to launch. Check WSL monitoring first!" -ForegroundColor Yellow
Write-Host ""
Write-Host "To launch the application manually, run:" -ForegroundColor Cyan
Write-Host "  `$env:ASPNETCORE_ENVIRONMENT = '$environment'" -ForegroundColor Gray
Write-Host "  `$env:DOTNET_ENVIRONMENT = '$environment'" -ForegroundColor Gray
Write-Host "  .\WindowsLauncher.UI\bin\Debug\net8.0-windows\win-x64\WindowsLauncher.UI.exe" -ForegroundColor Gray