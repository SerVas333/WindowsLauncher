# Android Setup Guide for WindowsLauncher

This guide explains how to set up Android application support in WindowsLauncher with WSA (Windows Subsystem for Android), including the new configurable Android subsystem management, automatic APK/XAPK installation, and troubleshooting.

## New in v1.3.0: Microservices Architecture & Enhanced Android Support

WindowsLauncher now features a completely refactored Android subsystem with microservices-based architecture and comprehensive Android solution:

### 🏗️ **Architectural Improvements (v1.3.0)**
- **🔧 Microservices Architecture** - Decomposed monolithic service into specialized components
- **⚡ Intelligent Caching** - TTL-based caching with automatic refresh strategies
- **🔄 Event-Driven Updates** - Real-time notifications for connection and app changes
- **🛠️ Retry Mechanisms** - Exponential backoff patterns for robust operation
- **📊 Progress Reporting** - Live progress tracking for long-running operations

### ✨ **Core Features**
- **🔄 Automatic APK/XAPK Installation** - Apps install automatically on first launch
- **📦 XAPK Support** - Full support for XAPK packages with split APK files
- **🚀 Multiple Installation Methods** - Fallback strategies for complex APK formats
- **⚙️ Configurable Subsystem** - Three operational modes for different needs
- **📊 Real-time Monitoring** - Live WSA status and diagnostics

### **Operational Modes**
- **Disabled** - Android functionality completely disabled, saves system resources
- **OnDemand** - WSA starts when needed (balanced approach, default)
- **Preload** - WSA preloads in background for maximum performance

### Android Configuration

Configure in `appsettings.json`:

```json
{
  "AndroidSubsystem": {
    "Mode": "OnDemand",
    "PreloadDelaySeconds": 30,
    "AutoStartWSA": true,
    "ShowStatusInUI": true,
    "EnableDiagnostics": true,
    "ResourceOptimization": {
      "MaxMemoryMB": 2048,
      "StopWSAOnIdle": false,
      "IdleTimeoutMinutes": 30
    },
    "Fallback": {
      "DisableOnLowMemory": true,
      "MemoryThresholdMB": 4096
    }
  }
}
```

**UI Integration:**
- WSA status indicator in main window status bar (🤖)
- Real-time status updates (Ready, Starting, Error, etc.)
- Admin panel hides Android features when Mode = "Disabled"

## Quick Start

### Automatic Installation (Recommended)

Run the automated installer script as Administrator:

```powershell
# Install both AAPT and ADB
.\Scripts\Install-AndroidTools.ps1

# Install only ADB (if AAPT already available)
.\Scripts\Install-AndroidTools.ps1 -BuildToolsOnly

# Install only AAPT (if ADB already available) 
.\Scripts\Install-AndroidTools.ps1 -PlatformToolsOnly

# Force reinstall existing tools
.\Scripts\Install-AndroidTools.ps1 -Force
```

### Manual Verification

After installation, verify tools are available:

```powershell
# Check if ADB is available
adb version

# Check if AAPT is available
aapt version

# Check WSA status
Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid
```

## Prerequisites

### Windows Compatibility - WSA Support

**Windows 11 (Officially Supported):**
- ✅ **Full WSA support** via Microsoft Store
- ✅ **Automatic updates** and official support
- ✅ **All WindowsLauncher Android features** work seamlessly

**Windows 10 (Community Supported):**
- ⚠️ **WSA available via unofficial installers** (WSABuilds, MagiskOnWSA, etc.)
- ⚠️ **Manual installation required** - not available in Microsoft Store
- ⚠️ **No official Microsoft support** - community maintained
- ⚠️ **Potential stability issues** compared to Windows 11
- ✅ **WindowsLauncher Android features work** if WSA properly installed

### Windows 10 WSA Installation Options

**Unofficial WSA installers for Windows 10:**
1. **WSABuilds** - https://github.com/MustardChef/WSABuilds
2. **MagiskOnWSA** - https://github.com/LSPosed/MagiskOnWSA
3. **WSA-Script** - Automated installation scripts

**Requirements for Windows 10:**
- Windows 10 Build 19041 (20H1) or higher
- Virtualization enabled in BIOS/UEFI
- At least 8GB RAM (16GB recommended)
- Administrative privileges for installation

### Windows Subsystem for Android (WSA) - Official Installation

1. **Windows 11 Official Installation:**
   - Windows 11 Build 22000.0 or higher
   - At least 8GB RAM (16GB recommended)
   - Virtualization enabled in BIOS/UEFI
   - TPM 2.0 and Secure Boot enabled

2. **Install WSA:**
   ```powershell
   # Via Microsoft Store
   ms-windows-store://pdp/?ProductId=9P3395VX91NR
   
   # Via PowerShell (if Store not available)
   Add-AppxPackage -RegisterByFamilyName -MainPackage MicrosoftCorporationII.WindowsSubsystemForAndroid_8wekyb3d8bbwe
   ```

3. **Enable Developer Mode:**
   - Open Windows Subsystem for Android Settings
   - Turn on "Developer mode"
   - Note the IP address for ADB connection (usually 127.0.0.1:58526)

### Android Tools

WindowsLauncher requires two essential Android SDK tools:

1. **AAPT (Android Asset Packaging Tool)** - for APK metadata extraction
2. **ADB (Android Debug Bridge)** - for app installation and management

## Installation Methods

### Method 1: Automated Script (Recommended)

The `Install-AndroidTools.ps1` script automatically downloads and installs required tools:

```powershell
# Run as Administrator for PATH modification
PowerShell -ExecutionPolicy Bypass -File .\Scripts\Install-AndroidTools.ps1
```

**What it does:**
- Downloads Android Build Tools (includes AAPT)
- Downloads Android Platform Tools (includes ADB and Fastboot)
- Installs to `C:\WindowsLauncher\Tools\Android\`
- Adds tool directories to system PATH
- Verifies installations by testing tool versions

**Installation locations:**
- AAPT: `C:\WindowsLauncher\Tools\Android\android-14\aapt.exe`
- ADB: `C:\WindowsLauncher\Tools\Android\platform-tools\adb.exe`
- Fastboot: `C:\WindowsLauncher\Tools\Android\platform-tools\fastboot.exe`

### Method 2: Android Studio (Advanced Users)

If you have Android Studio installed:

1. **Find existing tools:**
   ```
   %LOCALAPPDATA%\Android\Sdk\build-tools\{version}\aapt.exe
   %LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe
   ```

2. **Add to PATH:**
   - Add the build-tools and platform-tools directories to your system PATH
   - Restart WindowsLauncher to detect the tools

### Method 3: Manual Download

For advanced users or corporate environments:

1. **Download Build Tools:**
   - URL: https://dl.google.com/android/repository/build-tools_r34-windows.zip
   - Extract to `C:\WindowsLauncher\Tools\Android\`

2. **Download Platform Tools:**
   - URL: https://dl.google.com/android/repository/platform-tools-latest-windows.zip
   - Extract to `C:\WindowsLauncher\Tools\Android\`

3. **Update PATH manually via System Properties**

## Usage in WindowsLauncher

### Android Subsystem Modes

**Disabled Mode:**
- Android functionality completely hidden in UI
- No resource consumption by WSA
- Ideal for installations that don't need Android apps

**OnDemand Mode (Default):**
- WSA starts automatically when Android app is launched
- Balanced resource usage and performance
- Recommended for most users

**Preload Mode:**
- WSA starts in background with configurable delay
- Maximum performance for frequent Android app usage
- Higher memory consumption

### Adding Android Applications

**Prerequisites:** AndroidMode must be "OnDemand" or "Preload"

1. **Open Admin Panel** (🛠️ Admin button)
2. **Add New Application** (➕ Добавить приложение)
3. **Set Application Type:** Android APK (only visible when Android enabled)
4. **Browse APK/XAPK File:** Select your Android application file
   - **📱 APK files** - Standard Android package files
   - **📦 XAPK files** - Enhanced packages with split APK support (recommended for complex apps)
5. **Automatic Metadata Extraction:** WindowsLauncher automatically extracts:
   - Package name (com.example.app)
   - Version code and name
   - Minimum and target SDK versions
   - App display name
   - Split APK information (for XAPK files)
6. **Smart Installation:** Apps are automatically installed when first launched

**💡 **Tip:** Use XAPK files for apps that previously failed with "INSTALL_FAILED_MISSING_SPLIT" errors.

### Launching Android Apps

**🚀 Enhanced Launch Process:**

**All Modes Feature:**
- **🔍 Intelligent Detection:** Automatically detects APK vs XAPK files
- **⚡ Smart Installation:** Apps install automatically on first launch if not already installed
- **🛠️ Multiple Install Methods:** 
  - Standard APK installation
  - Split APK installation for complex apps
  - XAPK unpacking and multi-APK installation
  - Fallback methods for problematic packages

**OnDemand Mode:**
1. **WSA Check:** Verifies WSA availability
2. **Automatic Startup:** Starts WSA if not running
3. **ADB Connection:** Connects to WSA (127.0.0.1:58526)  
4. **Smart Installation:** 
   - Checks if app is installed in WSA
   - If not installed: automatically finds APK/XAPK file in database
   - Installs using the most appropriate method (standard, split, or XAPK)
5. **Launch:** Opens app in WSA environment

**Preload Mode:**
1. **Pre-warmed WSA:** Uses already running WSA instance
2. **Immediate Connection:** Fast ADB connection
3. **Instant Installation:** If needed, uses cached APK/XAPK files for fastest installation
4. **Quick Launch:** Minimal startup delay

**Installation Methods (Automatic Fallback):**
1. **Standard Install:** `adb install "app.apk"`
2. **Test APK Install:** `adb install -t -r "app.apk"` (for split APK compatibility)
3. **Force Install:** `adb install -g -t -r "app.apk"` (grants all permissions)
4. **XAPK Install:** Unpacks and installs all APK files using `adb install-multiple`
5. **Individual Install:** Falls back to installing each split APK separately

## Diagnostics and Troubleshooting

### Built-in Diagnostics Tool

WindowsLauncher includes comprehensive Android diagnostics:

1. **Open Admin Panel**
2. **Click "🤖 Диагностика Android"** (only visible when Android enabled)
3. **Review results:**
   - Current AndroidMode configuration (Disabled/OnDemand/Preload)
   - WSA availability and status
   - ADB availability and version
   - Android version in WSA
   - Tool installation paths (system PATH vs WindowsLauncher directories)
   - Installed Android apps
   - Memory usage and optimization settings
   - AndroidSubsystemService status

### WSA Status Indicator

The main window status bar shows real-time WSA status:
- 🤖 **Ready** - WSA running and available (green background)
- 🤖 **Starting** - WSA is starting up (orange background)  
- 🤖 **Error** - WSA unavailable or error (red background)
- 🤖 **Disabled** - Android subsystem disabled (gray)

### XAPK File Support

**What is XAPK?**
XAPK is an enhanced APK format that contains:
- Main APK file(s)
- Split APK files for different device configurations
- OBB expansion files (if needed)
- JSON manifest with installation metadata

**When to use XAPK:**
- ✅ Apps that fail with "INSTALL_FAILED_MISSING_SPLIT" error
- ✅ Large games and apps with multiple architecture support
- ✅ Apps downloaded from APKCombo, APKPure, or similar stores
- ✅ Modern Android apps using Android App Bundle format

**XAPK Installation Process:**
1. **Automatic Detection:** WindowsLauncher recognizes .xapk extension
2. **Extraction:** Unpacks XAPK to temporary directory
3. **Manifest Parsing:** Reads metadata from manifest.json
4. **Smart Installation:** 
   - Single APK: Uses standard installation methods
   - Multiple APKs: Uses `adb install-multiple` or installs individually
5. **Cleanup:** Removes temporary files after installation

**Getting XAPK Files:**
- [APKCombo](https://apkcombo.com/) - Download XAPK files directly
- [APKPure](https://apkpure.com/) - Alternative source for XAPK files
- [APKMirror](https://www.apkmirror.com/) - Some apps available as APK bundles

### Common Issues and Solutions

#### Issue: "Android functions are hidden/disabled"

**Symptoms:**
```
Android APK option missing from app types
🤖 Диагностика Android button not visible
WSA status indicator shows "Disabled"
```

**Solutions:**
1. **Check AndroidSubsystem configuration in appsettings.json:**
   ```json
   "AndroidSubsystem": {
     "Mode": "OnDemand"  // Change from "Disabled"
   }
   ```

2. **Restart WindowsLauncher after configuration changes**

3. **Verify configuration loading:**
   - Check debug logs for "Android subsystem configured in {Mode} mode"
   - Mode should be "OnDemand" or "Preload", not "Disabled"

#### Issue: "Android subsystem disabled by configuration"

**Symptoms:**
```
[DEBUG] Android support disabled by configuration
AndroidApplicationManager returns false for IsAndroidSupportAvailableAsync
```

**Solutions:**
1. **Enable Android in configuration:**
   ```json
   "AndroidSubsystem": {
     "Mode": "OnDemand", // or "Preload"
     "AutoStartWSA": true
   }
   ```

2. **Check memory threshold settings if using fallback:**
   ```json
   "Fallback": {
     "DisableOnLowMemory": false, // or increase MemoryThresholdMB
     "MemoryThresholdMB": 2048
   }
   ```

#### Issue: "ADB command not found in PATH"

**Symptoms:**
```
ERROR: ADB command not found in PATH
Android environment initialization failed
```

**Solutions:**
1. Run the installation script as Administrator:
   ```powershell
   .\Scripts\Install-AndroidTools.ps1 -Force
   ```

2. Manually verify ADB installation:
   ```powershell
   # Check if file exists
   Test-Path "C:\WindowsLauncher\Tools\Android\platform-tools\adb.exe"
   
   # Add to current session PATH
   $env:PATH += ";C:\WindowsLauncher\Tools\Android\platform-tools"
   ```

3. Restart WindowsLauncher after PATH changes

#### Issue: "WSA not available"

**Symptoms:**
```
WSA Available: False
WSA Running: False
```

**Solutions:**
1. **Install WSA from Microsoft Store**
2. **Enable Developer Mode:**
   - Start → Windows Subsystem for Android
   - Turn on "Developer mode"
   - Allow firewall exceptions

3. **Check Windows version:**
   ```powershell
   Get-ComputerInfo | Select WindowsProductName, WindowsVersion, WindowsBuildLabEx
   ```
   - Requires Windows 11 Build 22000+

#### Issue: "AAPT validation failed"

**Symptoms:**
```
APK validation failed: Invalid APK format
Could not extract APK metadata
```

**Solutions:**
1. **Verify AAPT installation:**
   ```powershell
   aapt version
   # Should output: Android Asset Packaging Tool, v0.2-...
   ```

2. **Reinstall build tools:**
   ```powershell
   .\Scripts\Install-AndroidTools.ps1 -PlatformToolsOnly -Force
   ```

3. **Check APK file integrity:**
   - Ensure APK file is not corrupted
   - Try with a different APK file
   - Check file permissions

#### Issue: "Connection to WSA failed"

**Symptoms:**
```
ADB Available: True
WSA Running: True
Failed to connect to WSA via ADB
```

**Solutions:**
1. **Reset ADB connection:**
   ```powershell
   adb kill-server
   adb start-server
   adb connect 127.0.0.1:58526
   ```

2. **Check WSA Developer Mode:**
   - Windows Subsystem for Android → Advanced settings
   - Ensure "Developer mode" is ON
   - Note the correct IP:Port (usually 127.0.0.1:58526)

3. **Firewall and Network:**
   - Allow ADB through Windows Firewall
   - Check if antivirus is blocking connections
   - Restart WSA: Settings → Apps → Windows Subsystem for Android → Advanced options → Reset

#### Issue: "PowerShell encoding problems"

**Symptoms:**
```
Garbled Russian text in logs
Кракозябры вместо русского текста
```

**Solutions:**
1. **Set PowerShell encoding:**
   ```powershell
   [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
   chcp 65001
   ```

2. **WindowsLauncher automatically handles encoding** - restart the application

#### Issue: "INSTALL_FAILED_MISSING_SPLIT with APK files"

**Symptoms:**
```
adb.ex: failed to install app.apk: Failure [INSTALL_FAILED_MISSING_SPLIT: Missing split for com.example.app]
Split APK installation failed: All installation methods failed
```

**Solutions:**
1. **Use XAPK instead of APK:**
   - Download the XAPK version of the app from APKCombo or APKPure
   - XAPK files contain all required split APK files
   - WindowsLauncher automatically handles XAPK installation

2. **Find Universal APK:**
   - Some developers provide "Universal APK" which doesn't require splits
   - Look for APK files labeled "universal", "fat", or "all-architectures"

3. **Try older app versions:**
   - Older versions of apps often don't use Android App Bundle format
   - Check APKMirror for older APK versions

#### Issue: "XAPK installation failed"

**Symptoms:**
```
XAPK installation exception: Invalid XAPK: manifest.json not found
Multiple APK installation exception: install-multiple failed
```

**Solutions:**
1. **Verify XAPK file integrity:**
   - Re-download XAPK file from a trusted source
   - Check file size matches the expected download size
   - Try opening XAPK with a ZIP tool to verify contents

2. **Check XAPK contents:**
   ```powershell
   # XAPK is a ZIP file, you can inspect it:
   Rename-Item "app.xapk" "app.zip"
   # Extract and check for manifest.json and APK files
   ```

3. **Manual installation:**
   - Extract XAPK file manually
   - Install APK files individually using ADB:
   ```powershell
   adb install "base.apk"
   adb install "config.arm64_v8a.apk"  
   adb install "config.en.apk"
   # Install all APK files found in extracted XAPK
   ```

#### Issue: "Automatic APK installation not working"

**Symptoms:**
```
Application is not installed and APK file not found for automatic installation
Failed to automatically install APK: APK file not found in database
```

**Solutions:**
1. **Verify APK file path:**
   - Ensure the APK/XAPK file still exists at the recorded path
   - Check if the file was moved or deleted after adding to database

2. **Re-add the application:**
   - Remove the application from WindowsLauncher
   - Add it again with the correct APK/XAPK file path

3. **Check file permissions:**
   - Ensure WindowsLauncher has read access to the APK/XAPK file
   - Move APK files to a location accessible by the application

### Performance Optimization

#### WSA Performance Settings

1. **Allocate more resources:**
   - Settings → Windows Subsystem for Android → Advanced
   - Increase memory allocation if available
   - Enable hardware acceleration

2. **Close unused apps:**
   - WSA runs multiple apps simultaneously
   - Close unused Android apps to free resources

#### WindowsLauncher Settings

1. **Enable APK metadata caching** (automatic)
2. **Batch install multiple APKs** instead of one-by-one
3. **Regular cleanup of temporary files**

## Architecture & Technical Details

### New Microservices Architecture (v1.3.0)

The Android subsystem has been completely refactored from a monolithic service into specialized microservices:

#### **Core Services**

**WSAConnectionService:**
- Intelligent WSA connection management with TTL-based caching
- Automatic retry mechanisms with exponential backoff
- Real-time connection status monitoring and events
- Optimized ADB path resolution and reuse

**ApkManagementService:**
- APK/XAPK file validation and metadata extraction
- Progress reporting for installation operations
- Multiple installation strategies with smart fallbacks
- Support for split APK and Android App Bundle formats

**InstalledAppsService:**
- Cached app inventory with automatic refresh
- Real-time app installation/uninstallation detection
- Usage statistics and performance monitoring
- Event-driven notifications for app lifecycle changes

**WSAIntegrationService (Composite):**
- Orchestrates all specialized services
- Maintains backward compatibility with existing interfaces
- Provides enhanced methods for progress tracking and cancellation
- Implements event subscription patterns for real-time updates

#### **Performance Benefits**
- **3-minute TTL caching** reduces system calls by up to 80%
- **Parallel operations** for faster APK processing
- **Smart resource management** prevents memory leaks
- **Event-driven updates** eliminate polling overhead

#### **Reliability Improvements**
- **Retry patterns** handle temporary WSA/ADB connection issues
- **Graceful degradation** when tools are unavailable
- **Comprehensive error handling** with detailed diagnostics
- **Race condition prevention** using semaphores and concurrent collections

#### **New Capabilities**
- **Cancellable operations** with CancellationToken support
- **Progress callbacks** for long-running installations
- **Event subscriptions** for real-time status updates
- **Enhanced diagnostics** with per-service metrics

### Integration Usage Examples

```csharp
// Enhanced APK installation with progress
var progress = new Progress<ApkInstallProgress>(p => 
    Console.WriteLine($"Installing: {p.ProgressPercentage}% - {p.CurrentStep}"));
    
var result = await wsaService.InstallApkWithProgressAsync(
    apkPath, progress, cancellationToken);

// Event-driven app monitoring
wsaService.SubscribeToAppsEvents((sender, e) => {
    if (e.ChangeType == ChangeType.AppInstalled) {
        Console.WriteLine($"New app installed: {e.PackageName}");
    }
});

// Real-time connection status
wsaService.SubscribeToConnectionEvents((sender, e) => {
    Console.WriteLine($"WSA Status: {e.Status} - {e.Message}");
});
```

## Advanced Configuration

### Corporate Environment Setup

For deployment in corporate environments:

1. **Pre-install Android tools:**
   ```powershell
   # Silent installation without user prompts
   .\Scripts\Install-AndroidTools.ps1 -Force -Confirm:$false
   ```

2. **Group Policy for WSA:**
   - Deploy WSA via WSUS or SCCM
   - Pre-configure developer mode via registry:
     ```reg
     [HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Lxss]
     "EnableDeveloperMode"=dword:00000001
     ```

3. **Network considerations:**
   - Allow ADB port 58526 through corporate firewall
   - Configure proxy settings if needed

### Custom Tool Locations

WindowsLauncher searches for tools in this order:

1. **System PATH** (standard installation)
2. **WindowsLauncher directories:**
   - `C:\WindowsLauncher\Tools\Android\platform-tools\`
   - `C:\WindowsLauncher\Tools\Android\android-14\`
3. **Android Studio locations:**
   - `%LOCALAPPDATA%\Android\Sdk\platform-tools\`
   - `%LOCALAPPDATA%\Android\Sdk\build-tools\{latest}\`

To use custom locations, add them to system PATH or place tools in WindowsLauncher directories.

## Development and Testing

### Testing Android Integration

1. **Use APK samples:**
   - Download sample APKs from APKMirror or F-Droid
   - Start with simple apps (calculators, notes)
   - Avoid apps requiring Google Play Services initially

2. **Debugging:**
   - Enable verbose logging in WindowsLauncher
   - Use `adb logcat` for Android system logs
   - Check Windows Event Viewer for WSA issues

3. **Development workflow:**
   - Install Android Studio for APK building
   - Use `adb install -r` for app updates
   - Test both installation and launching

### Logging and Monitoring

WindowsLauncher provides detailed logging:

```csharp
// Check logs in Output window (Visual Studio) or console
[INFO] Android support is fully available (WSA + ADB)
[INFO] Installing Android APK: com.example.app v1.2.3
[INFO] Successfully installed Android app: com.example.app
[INFO] Launching Android app: com.example.app
```

## Security Considerations

### APK Security

1. **Source validation:**
   - Only install APKs from trusted sources
   - Verify APK signatures when possible
   - Use antivirus scanning for APK files

2. **Permissions management:**
   - Review app permissions before installation
   - WSA provides Android permission system
   - Monitor app network access

### Network Security

1. **ADB connection security:**
   - ADB connection is local (127.0.0.1)
   - No external network exposure by default
   - Monitor firewall rules for ADB port

2. **Corporate policies:**
   - Implement APK allowlist/blocklist
   - Monitor installed Android applications
   - Regular security audits of WSA environment

## Support and Resources

### Getting Help

1. **Built-in diagnostics:** Use "🤖 Диагностика Android" button
2. **Log files:** Check WindowsLauncher debug output
3. **Community forums:** Android WSA community discussions

### Useful Commands

```powershell
# Check WSA status
Get-AppxPackage *WindowsSubsystemForAndroid*

# ADB debugging
adb devices
adb shell pm list packages
adb shell am start -n com.example.app/.MainActivity

# Tool versions
adb --version
aapt version

# Path management
echo $env:PATH
[Environment]::GetEnvironmentVariable("PATH", "Machine")
```

### Additional Resources

- [Windows Subsystem for Android Documentation](https://learn.microsoft.com/en-us/windows/android/wsa/)
- [Android Debug Bridge (ADB) Guide](https://developer.android.com/studio/command-line/adb)
- [Android Asset Packaging Tool (AAPT)](https://developer.android.com/studio/command-line/aapt2)
- [WindowsLauncher Project Documentation](README.md)

---

**Last Updated:** January 2025
**WindowsLauncher Version:** 1.3.0+
**Architecture:** 
- 🏗️ Microservices-based Android subsystem (WSAConnectionService, ApkManagementService, InstalledAppsService)
- ⚡ Intelligent caching with TTL strategies and automatic refresh
- 🔄 Event-driven architecture with real-time notifications
- 🛠️ Retry mechanisms with exponential backoff patterns
- 📊 Progress reporting and cancellation support
**Core Features:** 
- 🔄 Automatic APK/XAPK installation on first launch
- 📦 Full XAPK support with split APK handling  
- 🚀 Multiple installation methods with smart fallback
- ⚙️ Configurable Android Subsystem modes
- 📊 Enhanced WSA lifecycle management and monitoring
**Minimum Requirements:** 
- **Android features:** Windows 10 Build 19041+ or Windows 11, WSA, Android SDK Tools
- **Full support:** Windows 11 recommended for official WSA support

*This document is part of WindowsLauncher project. For technical support, please refer to the project documentation or contact the development team.*