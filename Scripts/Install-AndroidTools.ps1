# Install-AndroidTools.ps1
# Автоматическая установка Android Build Tools и Platform Tools для WindowsLauncher APK интеграции

[CmdletBinding()]
param(
    [string]$InstallPath = "C:\WindowsLauncher\Tools\Android",
    [switch]$Force = $false,
    [switch]$AddToPath = $true,
    [switch]$BuildToolsOnly = $false,
    [switch]$PlatformToolsOnly = $false
)

# Настройки
$BuildToolsVersion = "34"
$AndroidSdkUrl = "https://dl.google.com/android/repository/build-tools_r$BuildToolsVersion-windows.zip"
$PlatformToolsUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip"
$TempPath = "$env:TEMP\WindowsLauncher\AndroidTools"

Write-Host "WindowsLauncher Android Tools Installer" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green

function Test-AdminRights {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-AndroidBuildTools {
    param(
        [string]$InstallPath,
        [string]$TempPath,
        [string]$DownloadUrl
    )
    
    Write-Host "Installing Android Build Tools..." -ForegroundColor Yellow
    
    try {
        # Создаем временную папку
        if (!(Test-Path $TempPath)) {
            New-Item -ItemType Directory -Path $TempPath -Force | Out-Null
        }
        
        $zipFile = "$TempPath\build-tools.zip"
        
        # Скачиваем Build Tools
        Write-Host "Downloading Android Build Tools $BuildToolsVersion..." -ForegroundColor Cyan
        
        $webClient = New-Object System.Net.WebClient
        $webClient.DownloadFile($DownloadUrl, $zipFile)
        
        Write-Host "Downloaded: $([math]::Round((Get-Item $zipFile).Length / 1MB, 2)) MB" -ForegroundColor Green
        
        # Создаем папку установки
        if (!(Test-Path $InstallPath)) {
            New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        }
        
        # Распаковываем
        Write-Host "Extracting to $InstallPath..." -ForegroundColor Cyan
        
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $InstallPath)
        
        # Находим исполняемые файлы
        $aaptPath = Get-ChildItem -Path $InstallPath -Name "aapt.exe" -Recurse | Select-Object -First 1
        
        if ($aaptPath) {
            $fullAaptPath = Join-Path $InstallPath $aaptPath
            Write-Host "AAPT found: $fullAaptPath" -ForegroundColor Green
            
            # Тестируем AAPT
            try {
                $aaptVersion = & $fullAaptPath version 2>&1
                Write-Host "AAPT version: $aaptVersion" -ForegroundColor Green
            } catch {
                Write-Warning "Could not test AAPT: $_"
            }
            
            return $fullAaptPath
        } else {
            throw "AAPT not found in extracted archive"
        }
        
    } catch {
        Write-Error "Error installing Android Build Tools: $_"
        return $null
    } finally {
        # Очистка временных файлов
        if (Test-Path $zipFile) {
            Remove-Item $zipFile -Force -ErrorAction SilentlyContinue
        }
    }
}

function Install-AndroidPlatformTools {
    param(
        [string]$InstallPath,
        [string]$TempPath,
        [string]$DownloadUrl
    )
    
    Write-Host "Installing Android Platform Tools (ADB)..." -ForegroundColor Yellow
    
    try {
        # Создаем временную папку
        if (!(Test-Path $TempPath)) {
            New-Item -ItemType Directory -Path $TempPath -Force | Out-Null
        }
        
        $zipFile = "$TempPath\platform-tools.zip"
        $platformToolsPath = "$InstallPath\platform-tools"
        
        # Скачиваем Platform Tools
        Write-Host "Downloading Android Platform Tools..." -ForegroundColor Cyan
        
        $webClient = New-Object System.Net.WebClient
        $webClient.DownloadFile($DownloadUrl, $zipFile)
        
        Write-Host "Downloaded: $([math]::Round((Get-Item $zipFile).Length / 1MB, 2)) MB" -ForegroundColor Green
        
        # Создаем папку установки
        if (!(Test-Path $InstallPath)) {
            New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        }
        
        # Удаляем старую версию platform-tools если есть
        if (Test-Path $platformToolsPath) {
            Remove-Item $platformToolsPath -Recurse -Force
        }
        
        # Распаковываем
        Write-Host "Extracting to $InstallPath..." -ForegroundColor Cyan
        
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $InstallPath)
        
        # Находим исполняемые файлы
        $adbPath = "$platformToolsPath\adb.exe"
        $fastbootPath = "$platformToolsPath\fastboot.exe"
        
        if (Test-Path $adbPath) {
            Write-Host "ADB found: $adbPath" -ForegroundColor Green
            
            # Тестируем ADB
            try {
                $adbVersion = & $adbPath version 2>&1
                Write-Host "ADB version: $adbVersion" -ForegroundColor Green
            } catch {
                Write-Warning "Could not test ADB: $_"
            }
            
            if (Test-Path $fastbootPath) {
                Write-Host "Fastboot found: $fastbootPath" -ForegroundColor Green
            }
            
            return $adbPath
        } else {
            throw "ADB not found in extracted platform-tools"
        }
        
    } catch {
        Write-Error "Error installing Android Platform Tools: $_"
        return $null
    } finally {
        # Очистка временных файлов
        if (Test-Path $zipFile) {
            Remove-Item $zipFile -Force -ErrorAction SilentlyContinue
        }
    }
}

function Add-ToSystemPath {
    param([string]$PathToAdd)
    
    if (!(Test-AdminRights)) {
        Write-Warning "Administrator rights required to add to system PATH"
        Write-Host "Run script as Administrator or add PATH manually:"
        Write-Host "   $PathToAdd" -ForegroundColor Yellow
        return
    }
    
    try {
        $currentPath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
        
        if ($currentPath -notlike "*$PathToAdd*") {
            $newPath = "$currentPath;$PathToAdd"
            [Environment]::SetEnvironmentVariable("PATH", $newPath, "Machine")
            Write-Host "Added to system PATH: $PathToAdd" -ForegroundColor Green
            Write-Host "Restart console to apply PATH changes" -ForegroundColor Yellow
        } else {
            Write-Host "Path already exists in system PATH" -ForegroundColor Cyan
        }
    } catch {
        Write-Error "Error adding to PATH: $_"
    }
}

function Test-AaptAvailable {
    try {
        $aaptVersion = aapt version 2>&1
        Write-Host "AAPT already available: $aaptVersion" -ForegroundColor Green
        return $true
    } catch {
        return $false
    }
}

function Test-AdbAvailable {
    try {
        $adbVersion = adb version 2>&1
        Write-Host "ADB already available: $adbVersion" -ForegroundColor Green
        return $true
    } catch {
        return $false
    }
}

# Основной процесс установки
try {
    $installBuildTools = !$PlatformToolsOnly
    $installPlatformTools = !$BuildToolsOnly
    
    # Определяем что нужно устанавливать
    $aaptAlreadyAvailable = Test-AaptAvailable
    $adbAlreadyAvailable = Test-AdbAvailable
    
    if ($installBuildTools -and $aaptAlreadyAvailable -and !$Force) {
        Write-Host "AAPT is already available in system" -ForegroundColor Cyan
        $installBuildTools = $false
    }
    
    if ($installPlatformTools -and $adbAlreadyAvailable -and !$Force) {
        Write-Host "ADB is already available in system" -ForegroundColor Cyan
        $installPlatformTools = $false
    }
    
    # Проверяем существующие установки
    if ($installBuildTools -and (Test-Path "$InstallPath\*aapt.exe") -and !$Force) {
        Write-Host "Android Build Tools already installed in $InstallPath" -ForegroundColor Cyan
        $installBuildTools = $false
    }
    
    if ($installPlatformTools -and (Test-Path "$InstallPath\platform-tools\adb.exe") -and !$Force) {
        Write-Host "Android Platform Tools already installed in $InstallPath" -ForegroundColor Cyan
        $installPlatformTools = $false
    }
    
    # Если ничего не нужно устанавливать
    if (!$installBuildTools -and !$installPlatformTools) {
        Write-Host "All requested tools are already installed" -ForegroundColor Cyan
        Write-Host "Use -Force to reinstall" -ForegroundColor Gray
        exit 0
    }
    
    Write-Host "Starting Android Tools installation..." -ForegroundColor Yellow
    Write-Host "Install path: $InstallPath" -ForegroundColor Cyan
    
    $installedPaths = @()
    
    # Установка Build Tools (AAPT)
    if ($installBuildTools) {
        Write-Host "Build Tools URL: $AndroidSdkUrl" -ForegroundColor Cyan
        $aaptPath = Install-AndroidBuildTools -InstallPath $InstallPath -TempPath $TempPath -DownloadUrl $AndroidSdkUrl
        if ($aaptPath) {
            $installedPaths += Split-Path $aaptPath -Parent
            Write-Host "✓ AAPT installed at: $aaptPath" -ForegroundColor Green
        } else {
            throw "Failed to install Android Build Tools"
        }
    }
    
    # Установка Platform Tools (ADB)
    if ($installPlatformTools) {
        Write-Host "Platform Tools URL: $PlatformToolsUrl" -ForegroundColor Cyan
        $adbPath = Install-AndroidPlatformTools -InstallPath $InstallPath -TempPath $TempPath -DownloadUrl $PlatformToolsUrl
        if ($adbPath) {
            $installedPaths += Split-Path $adbPath -Parent
            Write-Host "✓ ADB installed at: $adbPath" -ForegroundColor Green
        } else {
            throw "Failed to install Android Platform Tools"
        }
    }
    
    # Итоги установки
    if ($installedPaths.Count -gt 0) {
        Write-Host ""
        Write-Host "Installation completed successfully!" -ForegroundColor Green
        
        # Добавляем в PATH
        if ($AddToPath) {
            foreach ($binPath in ($installedPaths | Sort-Object -Unique)) {
                Add-ToSystemPath -PathToAdd $binPath
            }
        }
        
        Write-Host ""
        if ($installBuildTools) {
            Write-Host "✓ WindowsLauncher can now extract APK metadata!" -ForegroundColor Green
        }
        if ($installPlatformTools) {
            Write-Host "✓ WindowsLauncher can now install and launch Android apps!" -ForegroundColor Green
        }
        Write-Host "Restart WindowsLauncher to apply changes." -ForegroundColor Yellow
        
    } else {
        Write-Error "No tools were installed"
        exit 1
    }
    
} catch {
    Write-Error "Critical error: $_"
    exit 1
}

Write-Host ""
Write-Host "Android Tools installation completed" -ForegroundColor Green
Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Yellow
Write-Host "  Install both AAPT and ADB:     .\Install-AndroidTools.ps1" -ForegroundColor Gray
Write-Host "  Install only AAPT:             .\Install-AndroidTools.ps1 -PlatformToolsOnly" -ForegroundColor Gray
Write-Host "  Install only ADB:              .\Install-AndroidTools.ps1 -BuildToolsOnly" -ForegroundColor Gray
Write-Host "  Force reinstall:               .\Install-AndroidTools.ps1 -Force" -ForegroundColor Gray