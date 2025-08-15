# ===== WSA Debug Launcher for WindowsLauncher =====
# PowerShell script to launch WindowsLauncher with enhanced WSA debugging
# Usage: .\Debug-WSA.ps1 [-Development] [-NoLaunch] [-ClearLogs] [-ShowLogs]

[CmdletBinding()]
param(
    [switch]$Development,
    [switch]$NoLaunch,
    [switch]$ClearLogs,
    [switch]$ShowLogs,
    [switch]$Help
)

# Configuration
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$LogsDir = "$ProjectRoot\Logs"
$ExecutablePath = "$ProjectRoot\WindowsLauncher.UI\bin\Debug\net8.0-windows\WindowsLauncher.UI.exe"
$ProjectFile = "$ProjectRoot\WindowsLauncher.UI\WindowsLauncher.UI.csproj"

# Colors for output
$ColorRed = "Red"
$ColorGreen = "Green"
$ColorYellow = "Yellow"
$ColorCyan = "Cyan"
$ColorGray = "Gray"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Show-Help {
    Write-ColorOutput "WSA Debug Launcher for WindowsLauncher" $ColorCyan
    Write-Host ""
    Write-ColorOutput "Usage: .\Debug-WSA.ps1 [OPTIONS]" $ColorYellow
    Write-Host ""
    Write-ColorOutput "Options:" $ColorYellow
    Write-ColorOutput "  -Development    Use Development environment (more verbose logging)" $ColorGray
    Write-ColorOutput "  -NoLaunch       Build only, don't launch the application" $ColorGray
    Write-ColorOutput "  -ClearLogs      Clear all log files before starting" $ColorGray
    Write-ColorOutput "  -ShowLogs       Open logs directory after launch" $ColorGray
    Write-ColorOutput "  -Help           Show this help message" $ColorGray
    Write-Host ""
    Write-ColorOutput "Examples:" $ColorYellow
    Write-ColorOutput "  .\Debug-WSA.ps1                    # Build and launch in production mode" $ColorGray
    Write-ColorOutput "  .\Debug-WSA.ps1 -Development       # Build and launch in development mode" $ColorGray
    Write-ColorOutput "  .\Debug-WSA.ps1 -ClearLogs -ShowLogs # Clear logs, launch, and open logs folder" $ColorGray
    Write-Host ""
}

function Test-WSAPrerequisites {
    Write-ColorOutput "Checking WSA prerequisites..." $ColorYellow
    
    # Check if WSA is installed
    $wsaPackage = Get-AppxPackage -Name "*WindowsSubsystemForAndroid*" -ErrorAction SilentlyContinue
    if (-not $wsaPackage) {
        Write-ColorOutput "WARNING: Windows Subsystem for Android not found!" $ColorRed
        Write-ColorOutput "WSA integration will not work without WSA installed." $ColorYellow
        return $false
    }
    
    Write-ColorOutput "✓ WSA Package found: $($wsaPackage.Name)" $ColorGreen
    Write-ColorOutput "  Version: $($wsaPackage.Version)" $ColorGray
    Write-ColorOutput "  Install Location: $($wsaPackage.InstallLocation)" $ColorGray
    
    # Check if WSA is running
    $wsaProcesses = Get-Process -Name "*WSA*" -ErrorAction SilentlyContinue
    if ($wsaProcesses) {
        Write-ColorOutput "✓ WSA processes running:" $ColorGreen
        foreach ($proc in $wsaProcesses) {
            Write-ColorOutput "  - $($proc.ProcessName) (PID: $($proc.Id))" $ColorGray
        }
    } else {
        Write-ColorOutput "! WSA processes not currently running" $ColorYellow
        Write-ColorOutput "  WSA will be started when needed" $ColorGray
    }
    
    return $true
}

function Clear-LogFiles {
    Write-ColorOutput "Clearing log files..." $ColorYellow
    
    if (-not (Test-Path $LogsDir)) {
        Write-ColorOutput "Creating logs directory: $LogsDir" $ColorGray
        New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null
        return
    }
    
    $logFiles = Get-ChildItem -Path $LogsDir -Filter "*.log" -ErrorAction SilentlyContinue
    $jsonFiles = Get-ChildItem -Path $LogsDir -Filter "*.json" -ErrorAction SilentlyContinue
    
    $allFiles = @($logFiles) + @($jsonFiles)
    if ($allFiles.Count -eq 0) {
        Write-ColorOutput "No log files found to clear" $ColorGray
        return
    }
    
    foreach ($file in $allFiles) {
        try {
            Remove-Item $file.FullName -Force
            Write-ColorOutput "✓ Removed: $($file.Name)" $ColorGray
        }
        catch {
            Write-ColorOutput "! Failed to remove: $($file.Name) - $($_.Exception.Message)" $ColorRed
        }
    }
    
    Write-ColorOutput "✓ Log files cleared" $ColorGreen
}

function Build-Application {
    Write-ColorOutput "Building WindowsLauncher..." $ColorYellow
    
    if (-not (Test-Path $ProjectFile)) {
        Write-ColorOutput "ERROR: Project file not found: $ProjectFile" $ColorRed
        return $false
    }
    
    $buildConfig = if ($Development) { "Debug" } else { "Release" }
    Write-ColorOutput "Build configuration: $buildConfig" $ColorGray
    
    try {
        $buildResult = dotnet build $ProjectFile --configuration $buildConfig --verbosity minimal 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✓ Build completed successfully" $ColorGreen
            return $true
        } else {
            Write-ColorOutput "Build failed!" $ColorRed
            Write-ColorOutput $buildResult $ColorRed
            return $false
        }
    }
    catch {
        Write-ColorOutput "Build error: $($_.Exception.Message)" $ColorRed
        return $false
    }
}

function Start-Application {
    Write-ColorOutput "Starting WindowsLauncher with WSA debugging..." $ColorYellow
    
    # Determine environment
    $environment = if ($Development) { "Development" } else { "Production" }
    Write-ColorOutput "Environment: $environment" $ColorGray
    
    # Set environment variables
    $env:ASPNETCORE_ENVIRONMENT = $environment
    $env:DOTNET_ENVIRONMENT = $environment
    
    # Check if executable exists
    if (-not (Test-Path $ExecutablePath)) {
        Write-ColorOutput "ERROR: Executable not found: $ExecutablePath" $ColorRed
        Write-ColorOutput "Please build the project first" $ColorYellow
        return $false
    }
    
    # Create logs directory
    if (-not (Test-Path $LogsDir)) {
        New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null
        Write-ColorOutput "Created logs directory: $LogsDir" $ColorGray
    }
    
    Write-ColorOutput "Executable: $ExecutablePath" $ColorGray
    Write-ColorOutput "Logs directory: $LogsDir" $ColorGray
    Write-ColorOutput "Environment variables set:" $ColorGray
    Write-ColorOutput "  ASPNETCORE_ENVIRONMENT = $environment" $ColorGray
    Write-ColorOutput "  DOTNET_ENVIRONMENT = $environment" $ColorGray
    Write-Host ""
    
    # Launch application
    try {
        Write-ColorOutput "🚀 Launching WindowsLauncher..." $ColorGreen
        Write-ColorOutput "Press Ctrl+C to stop monitoring or close the application window" $ColorYellow
        Write-Host ""
        
        # Start the process
        $processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
        $processStartInfo.FileName = $ExecutablePath
        $processStartInfo.WorkingDirectory = Split-Path $ExecutablePath
        $processStartInfo.UseShellExecute = $false
        $processStartInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Normal
        
        $process = [System.Diagnostics.Process]::Start($processStartInfo)
        
        if ($process) {
            Write-ColorOutput "✓ Application started (PID: $($process.Id))" $ColorGreen
            
            # Wait a bit for initial startup
            Start-Sleep -Seconds 3
            
            # Show recent log entries if logs exist
            Show-RecentLogs
            
            # Keep script running until process exits
            Write-ColorOutput "Monitoring application... (PID: $($process.Id))" $ColorCyan
            $process.WaitForExit()
            
            Write-ColorOutput "Application exited with code: $($process.ExitCode)" $ColorYellow
        } else {
            Write-ColorOutput "Failed to start application" $ColorRed
            return $false
        }
    }
    catch {
        Write-ColorOutput "Launch error: $($_.Exception.Message)" $ColorRed
        return $false
    }
    
    return $true
}

function Show-RecentLogs {
    Write-ColorOutput "Recent log entries:" $ColorCyan
    
    # Find the most recent log file
    $logPattern = if ($Development) { "app-dev-*.log" } else { "app-*.log" }
    $recentLog = Get-ChildItem -Path $LogsDir -Filter $logPattern -ErrorAction SilentlyContinue | 
                 Sort-Object LastWriteTime -Descending | 
                 Select-Object -First 1
    
    if ($recentLog) {
        Write-ColorOutput "Showing last 10 lines from: $($recentLog.Name)" $ColorGray
        Write-Host ""
        
        try {
            $content = Get-Content $recentLog.FullName -Tail 10 -ErrorAction SilentlyContinue
            foreach ($line in $content) {
                # Simple colorization
                if ($line -match "\[ERROR\]") {
                    Write-ColorOutput $line $ColorRed
                } elseif ($line -match "\[WARN\]") {
                    Write-ColorOutput $line $ColorYellow
                } elseif ($line -match "(WSA|Android|Window)") {
                    Write-ColorOutput $line $ColorCyan
                } else {
                    Write-ColorOutput $line $ColorGray
                }
            }
        }
        catch {
            Write-ColorOutput "Could not read log file: $($_.Exception.Message)" $ColorRed
        }
    } else {
        Write-ColorOutput "No recent log files found" $ColorGray
    }
    
    Write-Host ""
}

function Open-LogsDirectory {
    if (Test-Path $LogsDir) {
        Write-ColorOutput "Opening logs directory..." $ColorYellow
        explorer.exe $LogsDir
    } else {
        Write-ColorOutput "Logs directory not found: $LogsDir" $ColorYellow
    }
}

# Main execution
if ($Help) {
    Show-Help
    exit 0
}

Write-ColorOutput "===== WSA Debug Launcher =====" $ColorCyan
Write-ColorOutput "Project Root: $ProjectRoot" $ColorGray
Write-Host ""

# Check WSA prerequisites
$wsaOk = Test-WSAPrerequisites
if (-not $wsaOk) {
    Write-Host ""
    Write-ColorOutput "⚠️  WSA prerequisites check failed, but continuing anyway..." $ColorYellow
    Write-Host ""
}

# Clear logs if requested
if ($ClearLogs) {
    Clear-LogFiles
    Write-Host ""
}

# Build application
if (-not (Build-Application)) {
    Write-ColorOutput "Build failed. Exiting." $ColorRed
    exit 1
}

Write-Host ""

# Launch application unless -NoLaunch specified
if (-not $NoLaunch) {
    $launched = Start-Application
    
    if (-not $launched) {
        Write-ColorOutput "Launch failed. Exiting." $ColorRed
        exit 1
    }
}

# Show logs directory if requested
if ($ShowLogs) {
    Open-LogsDirectory
}

Write-ColorOutput "✅ Debug session completed" $ColorGreen