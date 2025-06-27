// ===== WindowsLauncher.UI/ViewModels/MainViewModel.cs - ПОЛНАЯ ВЕРСИЯ =====
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
using System.ComponentModel;
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// Главная ViewModel с полной функциональностью
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        #region Fields

        private readonly IServiceProvider _serviceProvider;
        private User? _currentUser;
        private UserSettings? _userSettings;
        private string _searchText = "";
        private string _selectedCategory = "All";
        private string _statusMessage = "";
        private bool _isLoading = false;
        private bool _isInitialized = false;

        #endregion

        #region Constructor

        public MainViewModel(
            IServiceProvider serviceProvider,
            ILogger<MainViewModel> logger,
            IDialogService dialogService)
            : base(logger, dialogService)
        {
            _serviceProvider = serviceProvider;

            // Инициализируем коллекции
            Applications = new ObservableCollection<ApplicationViewModel>();
            FilteredApplications = new ObservableCollection<ApplicationViewModel>();
            LocalizedCategories = new ObservableCollection<CategoryViewModel>();

            // Инициализируем команды
            InitializeCommands();

            // Подписываемся на изменение языка
            LocalizationManager.LanguageChanged += OnLanguageChanged;

            // Запускаем инициализацию асинхронно
            _ = InitializeAsync();
        }

        #endregion

        #region Properties

        public User? CurrentUser
        {
            get => _currentUser;
            set
            {
                if (SetProperty(ref _currentUser, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(LocalizedRole));
                    OnPropertyChanged(nameof(CanManageSettings));

                    // Обновляем команды
                    OpenSettingsCommand.RaiseCanExecuteChanged();
                    OpenAdminCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public UserSettings? UserSettings
        {
            get => _userSettings;
            set
            {
                if (SetProperty(ref _userSettings, value))
                {
                    OnPropertyChanged(nameof(TileSize));
                    OnPropertyChanged(nameof(ShowCategories));
                    OnPropertyChanged(nameof(Theme));
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterApplications();
                }
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    UpdateCategorySelection();
                    FilterApplications();
                }
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

        // Collections
        public ObservableCollection<ApplicationViewModel> Applications { get; }
        public ObservableCollection<ApplicationViewModel> FilteredApplications { get; }
        public ObservableCollection<CategoryViewModel> LocalizedCategories { get; }

        // Computed Properties
        public string WindowTitle
        {
            get
            {
                if (CurrentUser == null)
                    return LocalizationManager.GetString("AppTitle");

                return LocalizationManager.GetString("WindowTitle", CurrentUser.DisplayName, LocalizedRole);
            }
        }

        public string LocalizedRole
        {
            get
            {
                if (CurrentUser == null) return "";

                return CurrentUser.Role switch
                {
                    Core.Enums.UserRole.Administrator => LocalizationManager.GetString("RoleAdministrator"),
                    Core.Enums.UserRole.PowerUser => LocalizationManager.GetString("RolePowerUser"),
                    Core.Enums.UserRole.Standard => LocalizationManager.GetString("RoleStandard"),
                    _ => CurrentUser.Role.ToString()
                };
            }
        }

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
        public RelayCommand OpenAdminCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            LaunchApplicationCommand = new AsyncRelayCommand<ApplicationViewModel>(
                LaunchApplication,
                app => app != null && !IsLoading,
                Logger);

            SelectCategoryCommand = new RelayCommand<string>(SelectCategory);

            RefreshCommand = new AsyncRelayCommand(
                RefreshApplications,
                () => !IsLoading,
                Logger);

            LogoutCommand = new RelayCommand(Logout);

            OpenSettingsCommand = new RelayCommand(
                OpenSettings,
                () => CanManageSettings);

            SwitchUserCommand = new RelayCommand(SwitchUser);

            OpenAdminCommand = new RelayCommand(
                OpenAdminWindow,
                () => CurrentUser?.Role >= Core.Enums.UserRole.Administrator);
        }

        #endregion

        #region Initialization

        public override async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                Logger.LogInformation("=== STARTING MAINVIEWMODEL INITIALIZATION ===");
                IsLoading = true;
                StatusMessage = LocalizationManager.GetString("Initializing");

                using var scope = _serviceProvider.CreateScope();
                Logger.LogInformation("Created DI scope successfully");

                // STEP 1: Database initialization
                Logger.LogInformation("Step 1: Initializing database...");
                StatusMessage = LocalizationManager.GetString("DatabaseInitializing");

                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await dbInitializer.InitializeAsync();
                Logger.LogInformation("Database initialization completed");

                // STEP 2: Authentication
                Logger.LogInformation("Step 2: Starting authentication...");
                await AuthenticateUserAsync();

                if (CurrentUser == null)
                {
                    Logger.LogWarning("Authentication failed, user is null");
                    return;
                }

                // STEP 3: Load user data
                Logger.LogInformation("Step 3: Loading user data...");
                await LoadUserDataAsync();

                StatusMessage = LocalizationManager.GetString("Ready");
                _isInitialized = true;
                Logger.LogInformation("=== MAINVIEWMODEL INITIALIZATION COMPLETED ===");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "=== MAINVIEWMODEL INITIALIZATION FAILED ===");
                StatusMessage = LocalizationManager.GetString("InitializationError", ex.Message);
                await HandleErrorAsync(ex, "initialization");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AuthenticateUserAsync()
        {
            try
            {
                StatusMessage = LocalizationManager.GetString("Authenticating");

                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                var authResult = await authService.AuthenticateAsync();

                if (authResult.IsSuccess && authResult.User != null)
                {
                    CurrentUser = authResult.User;
                    Logger.LogInformation("User authenticated: {User} ({Role})",
                        CurrentUser.Username, CurrentUser.Role);

                    StatusMessage = LocalizationManager.GetString("Welcome", CurrentUser.DisplayName);
                }
                else
                {
                    var errorMessage = LocalizationManager.GetString("AuthenticationFailed",
                        authResult.ErrorMessage ?? "Unknown error");

                    Logger.LogError("Authentication failed: {Error}", authResult.ErrorMessage);
                    StatusMessage = errorMessage;
                    DialogService.ShowError(errorMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Authentication error");
                var errorMessage = LocalizationManager.GetString("AuthenticationFailed", ex.Message);
                StatusMessage = errorMessage;
                DialogService.ShowError(errorMessage);
            }
        }

        private async Task LoadUserDataAsync()
        {
            if (CurrentUser == null) return;

            try
            {
                Logger.LogInformation("Loading user data for: {User}", CurrentUser.Username);
                StatusMessage = LocalizationManager.GetString("LoadingApplications");

                using var scope = _serviceProvider.CreateScope();
                var authzService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
                var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                // Load user settings
                try
                {
                    UserSettings = await authzService.GetUserSettingsAsync(CurrentUser);
                    Logger.LogInformation("User settings loaded");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to load user settings, using defaults");
                    UserSettings = CreateDefaultSettings();
                }

                // Load applications
                var apps = await authzService.GetAuthorizedApplicationsAsync(CurrentUser);
                Logger.LogInformation("Found {Count} authorized applications", apps.Count);

                Applications.Clear();
                foreach (var app in apps)
                {
                    var appViewModel = new ApplicationViewModel(app);
                    Applications.Add(appViewModel);
                }

                // Load and localize categories
                await LoadLocalizedCategoriesAsync(appService);

                // Apply filters
                FilterApplications();

                var appCount = Applications.Count;
                StatusMessage = LocalizationManager.GetString("LoadedApps", appCount);
                Logger.LogInformation("User data loaded: {AppCount} apps", appCount);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load user data");
                StatusMessage = LocalizationManager.GetString("ErrorLoadingApplications", ex.Message);

                // Load test data as fallback
                await LoadTestDataAsync();
            }
        }

        private async Task LoadLocalizedCategoriesAsync(IApplicationService appService)
        {
            try
            {
                var categories = await appService.GetCategoriesAsync();
                var hiddenCategories = UserSettings?.HiddenCategories ?? new List<string>();

                LocalizedCategories.Clear();

                // Add "All" category
                LocalizedCategories.Add(new CategoryViewModel
                {
                    Key = "All",
                    DisplayName = LocalizationManager.GetString("CategoryAll"),
                    IsSelected = SelectedCategory == "All"
                });

                // Add other categories
                foreach (var category in categories.Where(c => !hiddenCategories.Contains(c)))
                {
                    var localizedName = GetLocalizedCategoryName(category);
                    LocalizedCategories.Add(new CategoryViewModel
                    {
                        Key = category,
                        DisplayName = localizedName,
                        IsSelected = SelectedCategory == category
                    });
                }

                Logger.LogInformation("Loaded {Count} localized categories", LocalizedCategories.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load categories");
            }
        }

        private string GetLocalizedCategoryName(string category)
        {
            if (string.IsNullOrEmpty(category)) return category;

            var key = $"Category{category}";
            var localized = LocalizationManager.GetString(key);

            // Если локализация не найдена, возвращаем оригинальное название
            return !string.IsNullOrEmpty(localized) && localized != key ? localized : category;
        }

        private UserSettings CreateDefaultSettings()
        {
            return new UserSettings
            {
                Username = CurrentUser?.Username ?? "",
                Theme = "Light",
                AccentColor = "Blue",
                TileSize = 150,
                ShowCategories = true,
                DefaultCategory = "All",
                AutoRefresh = true,
                RefreshIntervalMinutes = 30,
                ShowDescriptions = true,
                LastModified = DateTime.Now
            };
        }

        private async Task LoadTestDataAsync()
        {
            Logger.LogInformation("Loading test data as fallback...");

            Applications.Clear();

            var testApps = new[]
            {
                new CoreApplication
                {
                    Id = 1,
                    Name = "Calculator",
                    Description = LocalizationManager.GetString("CalculatorDescription"),
                    Category = "Utilities",
                    ExecutablePath = "calc.exe",
                    IsEnabled = true
                },
                new CoreApplication
                {
                    Id = 2,
                    Name = "Notepad",
                    Description = LocalizationManager.GetString("NotepadDescription"),
                    Category = "Utilities",
                    ExecutablePath = "notepad.exe",
                    IsEnabled = true
                },
                new CoreApplication
                {
                    Id = 3,
                    Name = "Control Panel",
                    Description = LocalizationManager.GetString("ControlPanelDescription"),
                    Category = "System",
                    ExecutablePath = "control.exe",
                    IsEnabled = true
                },
                new CoreApplication
                {
                    Id = 4,
                    Name = "Command Prompt",
                    Description = LocalizationManager.GetString("CommandPromptDescription"),
                    Category = "System",
                    ExecutablePath = "cmd.exe",
                    IsEnabled = true
                },
                new CoreApplication
                {
                    Id = 5,
                    Name = "Google",
                    Description = LocalizationManager.GetString("GoogleDescription"),
                    Category = "Web",
                    ExecutablePath = "https://www.google.com",
                    IsEnabled = true,
                    Type = Core.Enums.ApplicationType.Web
                }
            };

            foreach (var app in testApps)
            {
                Applications.Add(new ApplicationViewModel(app));
            }

            // Load test categories
            LocalizedCategories.Clear();
            LocalizedCategories.Add(new CategoryViewModel
            {
                Key = "All",
                DisplayName = LocalizationManager.GetString("CategoryAll"),
                IsSelected = true
            });
            LocalizedCategories.Add(new CategoryViewModel
            {
                Key = "Utilities",
                DisplayName = LocalizationManager.GetString("Category_Utilities"),
                IsSelected = false
            });
            LocalizedCategories.Add(new CategoryViewModel
            {
                Key = "System",
                DisplayName = LocalizationManager.GetString("CategorySystem"),
                IsSelected = false
            });
            LocalizedCategories.Add(new CategoryViewModel
            {
                Key = "Web",
                DisplayName = LocalizationManager.GetString("CategoryWeb"),
                IsSelected = false
            });

            FilterApplications();
            StatusMessage = LocalizationManager.GetString("LoadedApps", Applications.Count);
        }

        #endregion

        #region Localization

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            Logger.LogInformation("Language changed, updating UI strings");

            // Обновляем все локализованные свойства
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(LocalizedRole));

            // Обновляем категории
            foreach (var category in LocalizedCategories)
            {
                if (category.Key == "All")
                {
                    category.DisplayName = LocalizationManager.GetString("CategoryAll");
                }
                else
                {
                    category.DisplayName = GetLocalizedCategoryName(category.Key);
                }
            }

            // Обновляем статус если это стандартное сообщение
            if (StatusMessage == "Ready")
            {
                StatusMessage = LocalizationManager.GetString("Ready");
            }
        }

        private void UpdateCategorySelection()
        {
            foreach (var category in LocalizedCategories)
            {
                category.IsSelected = category.Key == SelectedCategory;
            }
        }

        #endregion

        #region Data Operations

        private void FilterApplications()
        {
            try
            {
                var filtered = Applications.AsEnumerable();

                // Filter by category
                if (SelectedCategory != "All")
                {
                    filtered = filtered.Where(a => a.Category == SelectedCategory);
                }

                // Filter by search
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var searchLower = SearchText.ToLower();
                    filtered = filtered.Where(a =>
                        a.Name.ToLower().Contains(searchLower) ||
                        a.Description.ToLower().Contains(searchLower));
                }

                FilteredApplications.Clear();
                foreach (var app in filtered)
                {
                    FilteredApplications.Add(app);
                }

                OnPropertyChanged(nameof(ApplicationCount));
                OnPropertyChanged(nameof(HasNoApplications));

                Logger.LogDebug("Filtered applications: {Count}/{Total}",
                    FilteredApplications.Count, Applications.Count);
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
                await LoadUserDataAsync();
                StatusMessage = LocalizationManager.GetString("ApplicationsRefreshed");
            }, "refresh applications");
        }

        #endregion

        #region Commands Implementation

        private async Task LaunchApplication(ApplicationViewModel? appViewModel)
        {
            if (appViewModel == null || CurrentUser == null) return;

            await ExecuteSafelyAsync(async () =>
            {
                var app = appViewModel.GetApplication();
                StatusMessage = LocalizationManager.GetString("LaunchingApp", app.Name);

                using var scope = _serviceProvider.CreateScope();
                var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                var result = await appService.LaunchApplicationAsync(app, CurrentUser);

                if (result.IsSuccess)
                {
                    StatusMessage = LocalizationManager.GetString("SuccessfullyLaunched", app.Name);
                    Logger.LogInformation("Application launched: {App}", app.Name);
                }
                else
                {
                    var errorMessage = LocalizationManager.GetString("FailedToLaunch", app.Name, result.ErrorMessage);
                    StatusMessage = errorMessage;
                    DialogService.ShowWarning(errorMessage, LocalizationManager.GetString("LaunchError"));
                }
            }, $"launch application {appViewModel.Name}");
        }

        private void SelectCategory(string? category)
        {
            if (!string.IsNullOrEmpty(category))
            {
                SelectedCategory = category;
            }
        }

        private void Logout()
        {
            try
            {
                var confirmMessage = LocalizationManager.GetString("ConfirmLogout");
                var confirmTitle = LocalizationManager.GetString("Confirmation");

                if (!DialogService.ShowConfirmation(confirmMessage, confirmTitle))
                    return;

                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                authService.Logout();
                Logger.LogInformation("User logged out: {User}", CurrentUser?.Username);

                StatusMessage = LocalizationManager.GetString("LoggedOut");
                WpfApplication.Current.Shutdown();
            }
            catch (Exception ex)
            {
                var errorMessage = LocalizationManager.GetString("LogoutError", ex.Message);
                StatusMessage = errorMessage;
                DialogService.ShowError(errorMessage);
            }
        }

        private void OpenSettings()
        {
            StatusMessage = LocalizationManager.GetString("SettingsComingSoon");
            DialogService.ShowInfo(
                LocalizationManager.GetString("SettingsWindowMessage"),
                LocalizationManager.GetString("Settings"));
        }

        private void SwitchUser()
        {
            try
            {
                var loginWindow = new LoginWindow(_serviceProvider);
                loginWindow.Owner = WpfApplication.Current.MainWindow;

                if (loginWindow.ShowDialog() == true && loginWindow.AuthenticatedUser != null)
                {
                    Logger.LogInformation("Switching from user {OldUser} to {NewUser}",
                        CurrentUser?.Username, loginWindow.AuthenticatedUser.Username);

                    CurrentUser = loginWindow.AuthenticatedUser;
                    UserSettings = null;

                    _ = LoadUserDataAsync();
                    StatusMessage = LocalizationManager.GetString("SwitchedToUser", CurrentUser.DisplayName);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = LocalizationManager.GetString("SwitchUserError", ex.Message);
                StatusMessage = errorMessage;
                DialogService.ShowError(errorMessage);
            }
        }

        private void OpenAdminWindow()
        {
            try
            {
                var adminWindow = new AdminWindow(_serviceProvider);
                adminWindow.Owner = WpfApplication.Current.MainWindow;
                adminWindow.ShowDialog();

                // После закрытия окна администрирования обновляем список приложений
                _ = RefreshApplications();
            }
            catch (Exception ex)
            {
                var errorMessage = "Ошибка при открытии окна администрирования";
                Logger.LogError(ex, errorMessage);
                DialogService.ShowError($"{errorMessage}: {ex.Message}");
            }
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LocalizationManager.LanguageChanged -= OnLanguageChanged;

                // Очищаем коллекции
                Applications.Clear();
                FilteredApplications.Clear();
                LocalizedCategories.Clear();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// ViewModel для категорий с локализацией
    /// </summary>
    public class CategoryViewModel : INotifyPropertyChanged
    {
        private string _displayName = "";
        private bool _isSelected;

        public string Key { get; set; } = "";

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Key}: {DisplayName} ({IsSelected})";
        }
    }
}