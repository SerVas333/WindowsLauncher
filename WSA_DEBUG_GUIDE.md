# WSA Cross-Platform Debugging Guide

> **Comprehensive debugging solution for Windows Subsystem for Android (WSA) integration in WindowsLauncher**

## 🎯 Overview

This guide provides a complete cross-platform debugging infrastructure for WSA integration development. The solution addresses the challenge of developing WSA features in a WSL/Linux environment while building and testing the Windows-only WindowsLauncher application.

## 🔧 Architecture

### Problem Statement
- **Development Environment**: WSL/Linux for coding and git operations
- **Build Environment**: Windows-only for C# WPF application compilation
- **Testing Environment**: Windows-only for WSA integration testing
- **Challenge**: Need real-time debugging and log analysis from WSL environment

### Solution Components

```
Cross-Platform Debugging Infrastructure
├── Windows Side (Build & Run)
│   ├── Debug-WSA.ps1              # PowerShell launcher with WSA checks
│   ├── appsettings.json           # Production logging configuration
│   └── appsettings.Development.json # Development logging configuration
│
├── WSL Side (Monitor & Analyze)
│   ├── monitor-wsa-logs.sh        # Real-time log monitoring
│   └── analyze-wsa-events.sh      # WSA events analysis tool
│
└── Shared Mount Point
    └── /mnt/c/WindowsLauncher/Logs/ # Cross-platform log access
```

## 📁 Configuration Files

### appsettings.json (Production)
Enhanced logging configuration with WSA-specific log levels:

```json
{
  "Logging": {
    "LogLevel": {
      "WindowsLauncher.Services.Lifecycle.Launchers.WSAApplicationLauncher": "Trace",
      "WindowsLauncher.Services.Lifecycle.Windows.WindowManager": "Trace",
      "WindowsLauncher.Services.Android": "Debug"
    },
    "File": {
      "Path": "C:\\WindowsLauncher\\Logs\\app-{Date}.log"
    },
    "WSAJsonFile": {
      "Path": "C:\\WindowsLauncher\\Logs\\wsa-events-{Date}.json"
    }
  }
}
```

### appsettings.Development.json
Development-specific configuration with enhanced verbosity:

```json
{
  "Logging": {
    "LogLevel": {
      "WindowsLauncher": "Trace",
      "WindowsLauncher.Services.Lifecycle.Launchers.WSAApplicationLauncher": "Trace",
      "WindowsLauncher.Services.Lifecycle.Windows.WindowManager": "Trace",
      "WindowsLauncher.Services.Android": "Trace"
    },
    "File": {
      "Path": "C:\\WindowsLauncher\\Logs\\app-dev-{Date}.log"
    },
    "WSAJsonFile": {
      "Path": "C:\\WindowsLauncher\\Logs\\wsa-dev-{Date}.json"
    },
    "DebugFile": {
      "Path": "C:\\WindowsLauncher\\Logs\\debug-dev-{Date}.log",
      "Enabled": true
    }
  }
}
```

## 🛠️ Tools and Scripts

### 1. Debug-WSA.ps1 (Windows PowerShell)

**Purpose**: Build and launch WindowsLauncher with WSA debugging capabilities

**Features**:
- WSA prerequisites validation (checks if WSA is installed and running)
- Automated build process with proper configuration
- Environment variable setup for Development/Production modes
- Real-time log monitoring during application execution
- Comprehensive error handling and reporting

**Usage**:
```powershell
# Development mode with enhanced logging
.\Scripts\Debug-WSA.ps1 -Development -ShowLogs

# Production mode with log cleanup
.\Scripts\Debug-WSA.ps1 -ClearLogs

# Build only (no launch)
.\Scripts\Debug-WSA.ps1 -NoLaunch

# Show help
.\Scripts\Debug-WSA.ps1 -Help
```

**Key Functions**:
- `Test-WSAPrerequisites`: Validates WSA installation and running processes
- `Build-Application`: Compiles the project with proper configuration
- `Start-Application`: Launches with environment setup and monitoring
- `Show-RecentLogs`: Displays recent log entries with colorization

### 2. monitor-wsa-logs.sh (WSL Bash)

**Purpose**: Real-time monitoring of WSA logs from WSL environment

**Features**:
- Real-time log streaming with `tail -f`
- WSA-specific filtering (Windows, Android, Chrome_WidgetWin_*, etc.)
- Colored output for different log levels and WSA components
- Multiple monitoring modes: standard, JSON, debug
- Development/Production log file detection

**Usage**:
```bash
# Monitor production WSA logs
./Scripts/monitor-wsa-logs.sh

# Monitor development logs (more verbose)
./Scripts/monitor-wsa-logs.sh --dev

# Monitor WSA JSON events
./Scripts/monitor-wsa-logs.sh --json --dev

# Monitor all debug logs
./Scripts/monitor-wsa-logs.sh --debug --all

# Show help
./Scripts/monitor-wsa-logs.sh --help
```

**Color Coding**:
- 🔴 **ERROR**: Critical errors requiring attention
- 🟡 **WARN**: Warnings and potential issues
- 🟢 **INFO**: General information messages
- 🔵 **DEBUG**: Debugging information
- ⚪ **TRACE**: Detailed trace information
- 🟦 **WSA Components**: WSA-related classes and methods
- 🟨 **Window Handles**: Window handle references
- 🟩 **Android**: Android-specific operations

### 3. analyze-wsa-events.sh (WSL Bash)

**Purpose**: Comprehensive analysis of WSA integration events and performance

**Features**:
- **Operations Analysis**: WSA launches, window detection, cache hits/misses
- **Performance Metrics**: Launch times, search performance, response times
- **Cache Efficiency**: Hit rates, cache utilization, performance impact
- **Window Lifecycle**: Window creation, activation, closure tracking
- **Report Generation**: Summary and JSON output formats
- **Historical Analysis**: Date-specific log analysis

**Usage**:
```bash
# Analyze today's WSA events
./Scripts/analyze-wsa-events.sh

# Analyze specific date
./Scripts/analyze-wsa-events.sh --date=2025-08-12

# Generate JSON report
./Scripts/analyze-wsa-events.sh --format=json --output=wsa-report.json

# Detailed verbose analysis
./Scripts/analyze-wsa-events.sh --verbose

# Show help
./Scripts/analyze-wsa-events.sh --help
```

**Report Sections**:
- **WSA Operations**: Launch counts, window detection statistics, errors
- **Performance Metrics**: Average launch times, search performance
- **Cache Efficiency**: Hit rates, cache utilization metrics
- **Window Lifecycle**: Window management statistics
- **Recent Events**: Last WSA operations for troubleshooting

## 🔄 Complete Workflow

### 1. Development Setup (One-time)
```bash
# Ensure scripts are executable
chmod +x Scripts/*.sh

# Verify log directory structure
ls -la Scripts/
```

### 2. Daily Development Workflow

**Step 1: Start Windows Debugging Session**
```powershell
# In Windows PowerShell from project root
.\Scripts\Debug-WSA.ps1 -Development -ShowLogs
```

**Step 2: Monitor from WSL (Separate Terminal)**
```bash
# In WSL from project root
./Scripts/monitor-wsa-logs.sh --dev
```

**Step 3: Test WSA Functionality**
- Launch Android applications through WindowsLauncher
- Test window detection and AppSwitcher integration
- Observe real-time logs in WSL terminal

**Step 4: Analyze Performance**
```bash
# After testing session
./Scripts/analyze-wsa-events.sh --verbose
```

### 3. Production Deployment Testing

**Windows Side:**
```powershell
# Production build and test
.\Scripts\Debug-WSA.ps1 -ClearLogs
```

**WSL Side:**
```bash
# Monitor production logs
./Scripts/monitor-wsa-logs.sh

# Generate deployment report
./Scripts/analyze-wsa-events.sh --format=json --output=production-report.json
```

## 📊 Log Analysis and Metrics

### WSA Operations Tracking
- **Application Launches**: Android app launch attempts and success rates
- **Window Detection**: Chrome_WidgetWin_* and ApplicationFrameWindow detection
- **Cache Operations**: WSA window cache hits, misses, and efficiency
- **Error Tracking**: WSA-related errors and failure patterns

### Performance Metrics
- **Launch Performance**: Average, min, max launch times for Android applications
- **Search Performance**: Window detection and correlation timing
- **Cache Efficiency**: Cache hit rates and performance impact
- **Memory Usage**: Resource consumption patterns

### Sample Analysis Output
```
===== WSA Events Analysis Report =====
Date: 2025-08-12
Generated: Tue Aug 12 15:30:00 +07 2025

Log Files Analyzed:
  Application Logs: 2 files
  WSA JSON Logs: 1 files
  Debug Logs: 1 files

=== WSA Operations ===
WSA Operations Summary:
  Application Launches: 15
  Windows Found: 12
  Windows from Cache: 8
  Windows Closed: 10
  Cache Entries Created: 4
  Errors: 2

=== Performance Metrics ===
Launch Performance:
  Average: 450.2ms
  Min: 125ms
  Max: 1200ms

Search Performance:
  Average Window Search: 85.5ms

=== Cache Efficiency ===
Cache Efficiency:
  Cache Hits: 8
  Cache Misses: 4
  Hit Rate: 66.7%
  Entries Created: 4
  Entries Cleared: 2
```

## 🚨 Troubleshooting

### Common Issues and Solutions

**1. WSA Not Found**
```
WARNING: Windows Subsystem for Android not found!
```
**Solution**: Install WSA from Microsoft Store or check WSA installation

**2. Log Directory Not Accessible**
```
Error: Log directory not found: /mnt/c/WindowsLauncher/Logs
```
**Solution**: Run WindowsLauncher first to create log directory, or create manually

**3. PowerShell Execution Policy**
```
Execution of scripts is disabled on this system
```
**Solution**: 
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**4. No WSA Events in Logs**
**Check**:
- WSA applications are launched through WindowsLauncher
- Logging levels are set to Trace for WSA components
- Application is running in correct environment (Development/Production)

**5. Cross-Platform Path Issues**
**Windows Paths**: `C:\WindowsLauncher\Logs\`
**WSL Paths**: `/mnt/c/WindowsLauncher/Logs/`
**Ensure**: Consistent use of absolute paths in configurations

### Debug Checklist

**Before Debugging:**
- [ ] WSA is installed and can run Android applications
- [ ] WindowsLauncher compiles without errors
- [ ] Scripts are executable (`chmod +x Scripts/*.sh`)
- [ ] Log directory exists and is accessible

**During Debugging:**
- [ ] Monitor real-time logs for WSA operations
- [ ] Check WSA window detection algorithms
- [ ] Verify AppSwitcher integration
- [ ] Monitor performance metrics

**After Debugging:**
- [ ] Analyze WSA events and performance
- [ ] Document any issues or improvements
- [ ] Generate reports for team review

## 📈 Performance Optimization

### Log File Management
- **Rolling Intervals**: Daily (production) vs Hourly (development)
- **File Sizes**: 50MB (production) vs 100MB+ (development)
- **Retention**: 7 days (production) vs 24 hours (development)
- **Formats**: Structured JSON for WSA events, plain text for application logs

### Monitoring Efficiency
- **Filtering**: WSA-specific filtering reduces noise by ~80%
- **Caching**: WSL file system caching improves monitoring performance
- **Buffering**: Real-time monitoring uses efficient buffering strategies

### Analysis Performance
- **Batch Processing**: Analyze logs in date-specific batches
- **Indexing**: Use grep and awk for efficient pattern matching
- **Report Generation**: JSON format for automated processing, summary for human review

## 🔗 Integration with Development Tools

### Visual Studio Integration
The debugging infrastructure integrates seamlessly with Visual Studio development:

1. **Build Events**: Can be integrated with VS build events
2. **Output Window**: Logs appear in VS Output window during debug
3. **Breakpoints**: Standard C# debugging works alongside log monitoring
4. **IntelliSense**: No impact on IDE performance or functionality

### Git Workflow Integration
- Scripts and configurations are version controlled
- Log files are excluded via `.gitignore`
- Cross-platform development supports multiple developers
- Configuration changes are tracked and reviewable

## 📝 Best Practices

### Development Practices
1. **Always use Development mode** for WSA feature development
2. **Monitor logs in real-time** during testing sessions
3. **Generate analysis reports** after significant testing
4. **Document WSA-specific issues** in commit messages

### Performance Practices
1. **Clear logs regularly** to prevent disk space issues
2. **Use filtering** to focus on relevant WSA events
3. **Monitor cache efficiency** to optimize WSA performance
4. **Analyze performance trends** over development cycles

### Security Practices
1. **Log files may contain sensitive information** - handle appropriately
2. **WSA integration logs** may include package names and window titles
3. **Cross-platform access** should be limited to development machines
4. **Production logs** should be collected securely for analysis

---

## 🎉 Success Indicators

Your cross-platform WSA debugging setup is working correctly when:

- ✅ **Real-time monitoring**: WSL terminal shows colored WSA events as they occur
- ✅ **Performance analysis**: Analysis reports show meaningful WSA metrics
- ✅ **Error detection**: WSA issues are immediately visible in monitoring
- ✅ **Development efficiency**: Can develop in WSL while testing WSA in Windows
- ✅ **Report generation**: Automated analysis provides actionable insights

This infrastructure enables efficient cross-platform development of WSA integration features while maintaining full debugging capabilities and performance monitoring. 🚀