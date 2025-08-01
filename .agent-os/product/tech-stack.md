# Technical Stack

## Core Platform
- **Application Framework:** WPF (Windows Presentation Foundation)
- **Language:** C# 12
- **Target Framework:** .NET 8.0-windows
- **Architecture Pattern:** MVVM (Model-View-ViewModel)
- **Dependency Injection:** Microsoft.Extensions.DependencyInjection 9.0.6

## Database Systems
- **Primary Database:** SQLite (автономные установки)
- **Enterprise Database:** Firebird 4.0+ (централизованное управление)
- **ORM:** Entity Framework Core 9.0.6
- **Database Providers:** 
  - Microsoft.EntityFrameworkCore.Sqlite 9.0.6
  - FirebirdSql.EntityFrameworkCore.Firebird 11.1.2
- **Migration System:** Custom версионирование с поддержкой обеих БД

## UI Framework & Design
- **UI Components:** MaterialDesignThemes 5.2.1
- **Design System:** Material Design с корпоративным брендингом KDV
- **Styling:** XAML ResourceDictionaries (MaterialColors.xaml, CorporateStyles.xaml)
- **Font Provider:** Segoe UI (системный шрифт Windows)
- **Icons:** Emoji + Material Design Icons
- **Touch Support:** Встроенная виртуальная клавиатура Windows

## Authentication & Authorization
- **Primary Auth:** Active Directory через System.DirectoryServices 9.0.6
- **Fallback Auth:** Локальные пользователи с хешированием паролей
- **LDAP Integration:** System.DirectoryServices.AccountManagement 9.0.6
- **Session Management:** Custom session управление
- **Encryption:** Встроенный EncryptionService для паролей

## Web Integration
- **Web Engine:** Microsoft.Web.WebView2 1.0.3351.48
- **Browser Apps:** Chrome в режиме --app для веб-приложений
- **JSON Processing:** System.Text.Json 9.0.6 + Newtonsoft.Json 13.0.3

## Development Environment
- **IDE:** Visual Studio 2022 (Windows-only builds)
- **Source Control:** Git (операции через WSL допустимы)
- **Build Platform:** Windows исключительно для C#/WPF
- **Cross-Environment:** Общее хранилище /mnt/c/WindowsLauncher (WSL + Windows)

## Deployment & Distribution
- **Target OS:** Windows 10/11
- **Installation:** ClickOnce или MSI
- **Runtime:** .NET 8 Desktop Runtime
- **Native Dependencies:** 
  - Firebird embedded библиотеки (fbclient.dll)
  - WebView2 Runtime (автоматическая установка)

## Logging & Monitoring
- **Logging Framework:** Microsoft.Extensions.Logging 9.0.6
- **Console Logging:** Microsoft.Extensions.Logging.Console 9.0.6
- **Debug Logging:** Microsoft.Extensions.Logging.Debug 9.0.6
- **Audit System:** Custom AuditService с БД логированием

## Configuration Management
- **Configuration:** Microsoft.Extensions.Configuration 9.0.6
- **Settings Storage:** appsettings.json + Properties/Settings.settings
- **Database Config:** Отдельный database-config.json для БД настроек

## Testing Framework
- **Unit Testing:** xUnit.net (планируется)
- **Test Coverage:** ViewModel и бизнес-логика
- **Test Environment:** Windows-only execution

## Additional Components
- **Memory Caching:** Microsoft.Extensions.Caching.Memory 9.0.6
- **System Management:** System.Management 9.0.6 (процессы и окна)
- **Visual Basic Runtime:** Microsoft.VisualBasic 10.3.0 (системные функции)

## Build & Publishing
- **Build System:** MSBuild
- **Package Manager:** NuGet
- **Runtime Identifier:** win-x64
- **Single File:** false (multi-file deployment)
- **Self Contained:** false (требует .NET Runtime)
- **Ready to Run:** false (JIT compilation)