// WindowsLauncher.UI/ViewModels/MainViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Infrastructure.Commands;
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;
using System.Windows;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;

        private User? _currentUser;
        private UserSettings? _userSettings;
        private string _searchText = "";
        private string _selectedCategory = "All";
        private string _statusMessage = "Ready";
        private bool _isLoading = false;

        public MainViewModel(IServiceProvider serviceProvider, ILogger<MainViewModel> logger, IDialogService dialogService)
    : base(logger, dialogService)
        {
            _serviceProvider = serviceProvider;

            Applications = new ObservableCollection<ApplicationViewModel>(); // ИЗМЕНЕНО
            FilteredApplications = new ObservableCollection<ApplicationViewModel>(); // ИЗМЕНЕНО
            Categories = new ObservableCollection<string>();

            InitializeCommands();
            _ = InitializeAsync();
        }

        #region Properties

        public User? CurrentUser
        {
            get => _currentUser;
            set
            {
                SetProperty(ref _currentUser, value);
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(CanManageSettings));
            }
        }

        public UserSettings? UserSettings
        {
            get => _userSettings;
            set
            {
                SetProperty(ref _userSettings, value);
                OnPropertyChanged(nameof(TileSize));
                OnPropertyChanged(nameof(ShowCategories));
                OnPropertyChanged(nameof(Theme));
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                FilterApplications();
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                SetProperty(ref _selectedCategory, value);
                FilterApplications();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public new bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<ApplicationViewModel> Applications { get; }
        public ObservableCollection<ApplicationViewModel> FilteredApplications { get; }
        public ObservableCollection<string> Categories { get; }

        // Computed Properties
        public string WindowTitle => CurrentUser != null
            ? $"Company Launcher - {CurrentUser.DisplayName} ({CurrentUser.Role})"
            : "Company Launcher";

        public int TileSize => UserSettings?.TileSize ?? 150;
        public bool ShowCategories => UserSettings?.ShowCategories ?? true;
        public string Theme => UserSettings?.Theme ?? "Light";
        public bool CanManageSettings => CurrentUser?.Role >= Core.Enums.UserRole.PowerUser;
        public int ApplicationCount => FilteredApplications.Count;
        public bool HasNoApplications => !IsLoading && ApplicationCount == 0;

        #endregion

        #region Commands

        public AsyncRelayCommand<ApplicationViewModel> LaunchApplicationCommand { get; private set; } = null!;
        public RelayCommand<string> SelectCategoryCommand { get; private set; } = null!;
        public AsyncRelayCommand RefreshCommand { get; private set; } = null!;
        public RelayCommand LogoutCommand { get; private set; } = null!;
        public RelayCommand OpenSettingsCommand { get; private set; } = null!;
        public RelayCommand SwitchUserCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            LaunchApplicationCommand = new AsyncRelayCommand<ApplicationViewModel>(LaunchApplication, // ИЗМЕНЕНО тип
                app => app != null && !IsLoading);
            SelectCategoryCommand = new RelayCommand<string>(SelectCategory);
            RefreshCommand = new AsyncRelayCommand(RefreshApplications);
            LogoutCommand = new RelayCommand(Logout);
            OpenSettingsCommand = new RelayCommand(OpenSettings, () => CanManageSettings);
            SwitchUserCommand = new RelayCommand(SwitchUser);
        }


        #endregion

        #region Initialization

        private async Task InitializeAsync()
        {
            try
            {
                Logger.LogInformation("=== STARTING INITIALIZATION ===");
                IsLoading = true;
                StatusMessage = "Starting initialization...";

                using var scope = _serviceProvider.CreateScope();
                Logger.LogInformation("Created DI scope successfully");

                // STEP 1: Database initialization
                Logger.LogInformation("Step 1: Initializing database...");
                StatusMessage = "Initializing database...";

                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await dbInitializer.InitializeAsync();
                Logger.LogInformation("Database initialization completed");

                // STEP 2: Authentication
                Logger.LogInformation("Step 2: Starting authentication...");
                StatusMessage = "Authenticating user...";

                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
                var authResult = await authService.AuthenticateAsync();

                Logger.LogInformation("Authentication result: Success={Success}, User={User}, Error={Error}",
                    authResult.IsSuccess, authResult.User?.Username, authResult.ErrorMessage);

                if (!authResult.IsSuccess || authResult.User == null)
                {
                    var errorMessage = $"Authentication failed: {authResult.ErrorMessage ?? "Unknown error"}";
                    StatusMessage = errorMessage;
                    Logger.LogError("Authentication failed: {Error}", authResult.ErrorMessage);
                    DialogService.ShowError(errorMessage);
                    return;
                }

                CurrentUser = authResult.User;
                Logger.LogInformation("Current user set: {User} ({Role})", CurrentUser.Username, CurrentUser.Role);
                StatusMessage = $"Welcome, {CurrentUser.DisplayName}!";

                // STEP 3: Load user settings and applications
                Logger.LogInformation("Step 3: Loading user settings and applications...");
                StatusMessage = "Loading user data...";

                await LoadUserSettingsAndApplications();

                StatusMessage = "Ready";
                Logger.LogInformation("=== INITIALIZATION COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "=== INITIALIZATION FAILED ===");
                StatusMessage = $"Initialization failed: {ex.Message}";

                // Показываем детальную ошибку пользователю
                DialogService.ShowError($"Initialization failed:\n{ex.Message}\n\nSee logs for details.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadUserSettingsAndApplications()
        {
            if (CurrentUser == null)
            {
                Logger.LogWarning("Cannot load user data: CurrentUser is null");
                return;
            }

            try
            {
                Logger.LogInformation("Loading user settings and applications for user: {User}", CurrentUser.Username);

                using var scope = _serviceProvider.CreateScope();
                var authzService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
                var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                // Load user settings
                Logger.LogInformation("Loading user settings...");
                try
                {
                    UserSettings = await authzService.GetUserSettingsAsync(CurrentUser);
                    Logger.LogInformation("User settings loaded successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to load user settings, using defaults");
                    UserSettings = null; // Will use defaults
                }

                // Load applications
                Logger.LogInformation("Loading applications...");
                StatusMessage = "Loading applications...";

                var apps = await authzService.GetAuthorizedApplicationsAsync(CurrentUser);
                Logger.LogInformation("Found {Count} authorized applications", apps.Count());

                Applications.Clear();
                foreach (var app in apps)
                {
                    // ИЗМЕНЕНО: создаем ApplicationViewModel
                    var appViewModel = CreateApplicationViewModel(app);
                    Applications.Add(appViewModel);
                    Logger.LogDebug("Added application: {AppName} (Category: {Category})", app.Name, app.Category);
                }

                // Load categories
                Logger.LogInformation("Loading categories...");
                var categories = await appService.GetCategoriesAsync();
                Logger.LogInformation("Found {Count} categories", categories.Count());

                Categories.Clear();
                Categories.Add("All");
                foreach (var category in categories.Where(c => !(UserSettings?.HiddenCategories?.Contains(c) == true)))
                {
                    Categories.Add(category);
                    Logger.LogDebug("Added category: {Category}", category);
                }

                // Apply filters
                FilterApplications();

                StatusMessage = $"Loaded {Applications.Count} applications";
                Logger.LogInformation("User data loading completed: {AppCount} apps, {CatCount} categories",
                    Applications.Count, Categories.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load user settings and applications");
                StatusMessage = $"Error loading applications: {ex.Message}";

                // FALLBACK: Load test data if services fail
                Logger.LogInformation("Loading test data as fallback...");
                await LoadTestData();
            }
        }

        // ВРЕМЕННЫЙ метод для тестирования UI
        private async Task LoadTestData()
        {
            Logger.LogInformation("Loading TEST DATA for UI verification...");

            // Тестовые приложения
            Applications.Clear();

            var testApps = new[]
            {
        new CoreApplication
        {
            Id = 1,
            Name = "Calculator",
            Description = "Windows Calculator",
            Category = "Utilities",
            ExecutablePath = "calc.exe",
            IsEnabled = true
        },
        new CoreApplication
        {
            Id = 2,
            Name = "Notepad",
            Description = "Text Editor",
            Category = "Utilities",
            ExecutablePath = "notepad.exe",
            IsEnabled = true
        },
        new CoreApplication
        {
            Id = 3,
            Name = "Control Panel",
            Description = "Windows Control Panel",
            Category = "System",
            ExecutablePath = "control.exe",
            IsEnabled = true
        }
    };

            foreach (var app in testApps)
            {
                var appViewModel = CreateApplicationViewModel(app);
                Applications.Add(appViewModel);
            }

            // Тестовые категории
            Categories.Clear();
            Categories.Add("All");
            Categories.Add("Utilities");
            Categories.Add("System");
            Categories.Add("Development");

            FilterApplications();
            StatusMessage = $"Loaded {Applications.Count} TEST applications";
            Logger.LogInformation("Test data loaded: {Count} applications", Applications.Count);
        }


        #endregion

        #region Data Operations
        private ApplicationViewModel CreateApplicationViewModel(CoreApplication application)
        {
            // Теперь просто создаем ApplicationViewModel без зависимостей
            return new ApplicationViewModel(application);
        }
        private void FilterApplications()
        {
            try
            {
                Logger.LogDebug("Filtering applications: SelectedCategory={Category}, SearchText='{Search}'",
                    SelectedCategory, SearchText);

                var filtered = Applications.AsEnumerable();
                var originalCount = filtered.Count();

                // Filter by category
                if (SelectedCategory != "All")
                {
                    filtered = filtered.Where(a => a.Category == SelectedCategory);
                    Logger.LogDebug("After category filter: {Count} apps", filtered.Count());
                }

                // Filter by search
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var searchLower = SearchText.ToLower();
                    filtered = filtered.Where(a =>
                        a.Name.ToLower().Contains(searchLower) ||
                        a.Description.ToLower().Contains(searchLower));
                    Logger.LogDebug("After search filter: {Count} apps", filtered.Count());
                }

                FilteredApplications.Clear();
                foreach (var app in filtered)
                {
                    FilteredApplications.Add(app);
                }

                Logger.LogInformation("Filtered applications: {FilteredCount}/{TotalCount} apps displayed",
                    FilteredApplications.Count, originalCount);

                OnPropertyChanged(nameof(ApplicationCount));
                OnPropertyChanged(nameof(HasNoApplications));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error filtering applications");
            }
        }

        private async Task RefreshApplications()
        {
            await ExecuteSafelyAsync(async () =>
            {
                await LoadUserSettingsAndApplications();
                StatusMessage = "Applications refreshed";
            }, "refresh applications");
        }

        #endregion

        #region Commands Implementation

        private async Task LaunchApplication(ApplicationViewModel? appViewModel)
        {
            if (appViewModel == null || CurrentUser == null) return;

            await ExecuteSafelyAsync(async () =>
            {
                var app = appViewModel.GetApplication(); // Получаем исходную модель
                StatusMessage = $"Launching {app.Name}...";
                Logger.LogInformation("Launching application {App} for user {User}", app.Name, CurrentUser.Username);

                using var scope = _serviceProvider.CreateScope();
                var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                var result = await appService.LaunchApplicationAsync(app, CurrentUser);

                if (result.IsSuccess)
                {
                    StatusMessage = $"Successfully launched {app.Name}";
                    Logger.LogInformation("Successfully launched {App} for user {User}", app.Name, CurrentUser.Username);
                }
                else
                {
                    var errorMessage = $"Failed to launch {app.Name}: {result.ErrorMessage}";
                    StatusMessage = errorMessage;
                    Logger.LogWarning("Failed to launch {App} for user {User}: {Error}", app.Name, CurrentUser.Username, result.ErrorMessage);
                    DialogService.ShowWarning(errorMessage);
                }
            }, $"launch application {appViewModel.Name}");
        }

        private void SelectCategory(string? category)
        {
            if (category != null)
            {
                SelectedCategory = category;
                Logger.LogDebug("Category selected: {Category}", category);
            }
        }

        private void Logout()
        {
            try
            {
                if (!DialogService.ShowConfirmation("Are you sure you want to logout?"))
                    return;

                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                authService.Logout();
                Logger.LogInformation("User {User} logged out", CurrentUser?.Username);

                StatusMessage = "Logged out";
                WpfApplication.Current.Shutdown();
            }
            catch (Exception ex)
            {
                var errorMessage = $"Logout error: {ex.Message}";
                StatusMessage = errorMessage;
                Logger.LogError(ex, "Logout error for user {User}", CurrentUser?.Username);
                DialogService.ShowError(errorMessage);
            }
        }

        private void OpenSettings()
        {
            StatusMessage = "Settings functionality coming soon...";
            DialogService.ShowInfo("Settings window will be implemented in the next iteration.");
        }

        private void SwitchUser()
        {
            try
            {
                var loginWindow = new LoginWindow(_serviceProvider);
                loginWindow.Owner = System.Windows.Application.Current.MainWindow;

                if (loginWindow.ShowDialog() == true && loginWindow.AuthenticatedUser != null)
                {
                    Logger.LogInformation("Switching from user {OldUser} to {NewUser}",
                        CurrentUser?.Username, loginWindow.AuthenticatedUser.Username);

                    CurrentUser = loginWindow.AuthenticatedUser;
                    UserSettings = null; // Сбросим настройки для перезагрузки
                    _ = LoadUserSettingsAndApplications(); // Перезагружаем все для нового пользователя
                    StatusMessage = $"Switched to user: {CurrentUser.DisplayName}";
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Switch user error: {ex.Message}";
                StatusMessage = errorMessage;
                Logger.LogError(ex, "Failed to switch user");
                DialogService.ShowError(errorMessage);
            }
        }

        #endregion
    }
}