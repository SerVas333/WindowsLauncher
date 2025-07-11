# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WindowsLauncher is a WPF-based enterprise application launcher built with .NET 8.0 that provides centralized application management with Active Directory authentication and role-based access control. The application features a corporate-style interface with Material Design themes and supports both Russian and English localization.

## Build Commands

### Development Build
```bash
dotnet build --configuration Debug
```

### Release Build
```bash
dotnet build --configuration Release
```

### Clean Build (Recommended)
```bash
# Use the PowerShell script for thorough cleaning
pwsh ./clean-build.ps1
# Or manually:
dotnet clean
dotnet restore
dotnet build --configuration Debug --no-incremental
```

### Database Migrations
```bash
# Create migration (from UI project directory)
cd WindowsLauncher.UI
dotnet ef migrations add MigrationName --project ../WindowsLauncher.Data --context LauncherDbContext

# Update database
dotnet ef database update --project ../WindowsLauncher.Data --context LauncherDbContext
```

### Running the Application
```bash
# From UI project directory
cd WindowsLauncher.UI
dotnet run
```

## Architecture

### Solution Structure
- **WindowsLauncher.Core** - Domain models, enums, and interfaces
- **WindowsLauncher.Data** - Entity Framework DbContext, repositories, and database configurations
- **WindowsLauncher.Services** - Business logic, authentication, and external service integrations
- **WindowsLauncher.UI** - WPF application with MVVM pattern, views, and view models

### Key Architectural Patterns
- **Repository Pattern** - Data access abstraction in WindowsLauncher.Data/Repositories
- **MVVM Pattern** - UI follows Model-View-ViewModel with ViewModels in WindowsLauncher.UI/ViewModels
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection configured in App.xaml.cs
- **Clean Architecture** - Core domain isolated from infrastructure concerns

### Database
- **SQLite** database (`launcher.db`) with Entity Framework Core
- **Migrations** located in WindowsLauncher.Data/Migrations
- **Seeding** handled by DatabaseSeeder.cs and DatabaseInitializer

### Authentication & Authorization
- **Active Directory** integration via System.DirectoryServices
- **Service Account** fallback for non-domain environments
- **Role-based access** with UserRole enum (Standard, PowerUser, Administrator)
- **Group-based permissions** stored as JSON in User.GroupsJson

### Key Components

#### Models (WindowsLauncher.Core/Models)
- **User** - User entity with AD integration and service account support
- **Application** - Launchable application with access control
- **AuditLog** - Action logging for security and compliance
- **UserSettings** - User preferences and configuration

#### Services (WindowsLauncher.Services)
- **ActiveDirectoryService** - AD authentication and user management
- **AuthenticationService** - Unified authentication with AD/service account fallback
- **ApplicationService** - Application management and access control
- **AuditService** - Security event logging

#### UI Architecture
- **Localization** - LocalizationHelper with resource files for Russian/English
- **Material Design** - MaterialDesignThemes package for corporate styling
- **Navigation** - WpfNavigationService for window management
- **Commands** - RelayCommand and AsyncRelayCommand for MVVM binding

### Configuration
- **appsettings.json** - Main configuration file with AD, logging, and application settings
- **Connection strings** - SQLite database configuration
- **Active Directory** - Domain, LDAP server, and group mappings
- **Logging** - Console, debug, and file logging configuration

### Security Features
- **Password hashing** with salt for service accounts
- **Failed login tracking** with account lockout
- **Audit logging** for all user actions
- **Role-based UI** with conditional visibility
- **Secure configuration** with sensitive data protection

## Development Notes

### Adding New Applications
1. Use ApplicationService.CreateAsync() method
2. Set RequiredGroups and MinimumRole for access control
3. Verify icon path and executable path exist
4. Test with different user roles

### Extending User Roles
- Modify UserRole enum in WindowsLauncher.Core/Enums
- Update role checking logic in User.HasMinimumRole()
- Adjust UI visibility in ViewModels

### Database Schema Changes
- Always create migrations for schema changes
- Test with both empty and populated databases
- Update DatabaseSeeder if needed for new required data

### Localization
- Add new strings to Resources.resx and Resources.ru-RU.resx
- Use LocalizationHelper.Instance.GetString() in ViewModels
- Update window titles through LocalizationHelper callbacks

### Testing Active Directory
- Use ADTestService for AD connectivity testing
- Configure test domain in appsettings.json
- Enable fallback mode for development without AD

## Common Issues

### Build Errors
- Run `clean-build.ps1` to resolve most build issues
- Check that all NuGet packages are restored
- Verify .NET 8.0 SDK is installed

### Database Issues
- Delete `launcher.db` to force database recreation
- Check connection string in appsettings.json
- Verify migrations are applied correctly

### Authentication Problems
- Check AD configuration in appsettings.json
- Verify service account credentials are set
- Enable fallback mode for development

### UI/Localization Issues
- Verify resource files are marked as embedded resources
- Check LocalizationHelper initialization in App.xaml.cs
- Test with both Russian and English cultures