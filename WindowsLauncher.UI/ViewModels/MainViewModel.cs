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
using WindowsLauncher.UI.Infrastructure.Localization;
using System.ComponentModel;
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;
using WindowsLauncher.UI.Views;
using System.Windows;
using WindowsLauncher.UI.Components.Dialogs;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Enums;

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
        private bool _isVirtualKeyboardVisible = false;
        private bool _isSidebarVisible = false;
        private bool _hasActiveFilter = false;

        // WSA Status Fields
        private bool _showWSAStatus = false;
        private string _wsaStatusText = "";
        private string _wsaStatusTooltip = "";
        private string _wsaStatusColor = "#666666";

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
            LocalizationHelper.Instance.LanguageChanged += OnLanguageChanged;

            // Инициализация будет запущена вручную после установки CurrentUser
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

                    // Запускаем инициализацию при первой установке пользователя
                    if (value != null && !_isInitialized)
                    {
                        _ = InitializeAsync();
                    }
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
                    UpdateActiveFilterStatus(); // Обновляем индикатор при изменении поиска
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
                    return LocalizationHelper.Instance.GetString("AppTitle");

                return LocalizationHelper.Instance.GetFormattedString("WindowTitle", CurrentUser.DisplayName, LocalizedRole);
            }
        }

        public string LocalizedRole
        {
            get
            {
                if (CurrentUser == null) return "";

                return CurrentUser.Role switch
                {
                    Core.Enums.UserRole.Administrator => LocalizationHelper.Instance.GetString("RoleAdministrator"),
                    Core.Enums.UserRole.PowerUser => LocalizationHelper.Instance.GetString("RolePowerUser"),
                    Core.Enums.UserRole.Standard => LocalizationHelper.Instance.GetString("RoleStandard"),
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
        
        public bool IsVirtualKeyboardVisible
        {
            get => _isVirtualKeyboardVisible;
            set => SetProperty(ref _isVirtualKeyboardVisible, value);
        }

        public bool IsSidebarVisible
        {
            get => _isSidebarVisible;
            set => SetProperty(ref _isSidebarVisible, value);
        }

        /// <summary>
        /// Индикатор активного фильтра для визуальной индикации на кнопке Sidebar
        /// </summary>
        public bool HasActiveFilter
        {
            get => _hasActiveFilter;
            set => SetProperty(ref _hasActiveFilter, value);
        }

        /// <summary>
        /// Показывать ли индикатор статуса WSA в UI
        /// </summary>
        public bool ShowWSAStatus
        {
            get => _showWSAStatus;
            set => SetProperty(ref _showWSAStatus, value);
        }

        /// <summary>
        /// Текст статуса WSA
        /// </summary>
        public string WSAStatusText
        {
            get => _wsaStatusText;
            set => SetProperty(ref _wsaStatusText, value);
        }

        /// <summary>
        /// Подсказка для статуса WSA
        /// </summary>
        public string WSAStatusTooltip
        {
            get => _wsaStatusTooltip;
            set => SetProperty(ref _wsaStatusTooltip, value);
        }

        /// <summary>
        /// Цвет текста статуса WSA
        /// </summary>
        public string WSAStatusColor
        {
            get => _wsaStatusColor;
            set => SetProperty(ref _wsaStatusColor, value);
        }

        #endregion

        #region Commands

        public AsyncRelayCommand<ApplicationViewModel> LaunchApplicationCommand { get; private set; } = null!;
        public RelayCommand<string> SelectCategoryCommand { get; private set; } = null!;
        public AsyncRelayCommand RefreshCommand { get; private set; } = null!;
        public RelayCommand LogoutCommand { get; private set; } = null!;
        public RelayCommand OpenSettingsCommand { get; private set; } = null!;
        public RelayCommand SwitchUserCommand { get; private set; } = null!;
        public RelayCommand OpenAdminCommand { get; private set; } = null!;
        public AsyncRelayCommand ShowVirtualKeyboardCommand { get; private set; } = null!;
        public RelayCommand ToggleSidebarCommand { get; private set; } = null!;
        public RelayCommand ComposeEmailCommand { get; private set; } = null!;
        public RelayCommand OpenAddressBookCommand { get; private set; } = null!;
        public RelayCommand OpenHelpCommand { get; private set; } = null!;

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

            ShowVirtualKeyboardCommand = new AsyncRelayCommand(
                ToggleVirtualKeyboard, // Оставляем старое имя метода для совместимости
                () => !IsLoading,
                Logger);

            ToggleSidebarCommand = new RelayCommand(
                ToggleSidebar,
                () => !IsLoading);

            ComposeEmailCommand = new RelayCommand(ComposeEmail);
            OpenAddressBookCommand = new RelayCommand(OpenAddressBook);
            OpenHelpCommand = new RelayCommand(OpenHelp);
        }

        #endregion

        #region Initialization

        public override async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                Logger.LogInformation("Initializing MainViewModel");
                IsLoading = true;
                StatusMessage = LocalizationHelper.Instance.GetString("Initializing");

                using var scope = _serviceProvider.CreateScope();
                Logger.LogInformation("Created DI scope successfully");

                // БД уже должна быть инициализирована на этапе запуска приложения
                StatusMessage = LocalizationHelper.Instance.GetString("DatabaseInitializing");

                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                var isReady = await dbInitializer.IsDatabaseReadyAsync();
                if (!isReady)
                {
                    throw new InvalidOperationException("Database is not ready. This should have been resolved during application startup.");
                }
                Logger.LogDebug("Database verification completed");

                // Authentication
                if (CurrentUser == null)
                {
                    Logger.LogInformation("Starting user authentication");
                    await AuthenticateUserAsync();

                    if (CurrentUser == null)
                    {
                        Logger.LogWarning("Authentication failed, user is null");
                        return;
                    }
                }
                else
                {
                    Logger.LogDebug("Using existing authenticated user: {Username}", CurrentUser.Username);
                }

                // Load user data
                await LoadUserDataAsync();

                // Initialize WSA status
                await InitializeWSAStatusAsync();

                StatusMessage = LocalizationHelper.Instance.GetString("Ready");
                _isInitialized = true;
                Logger.LogInformation("MainViewModel initialization completed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "MainViewModel initialization failed");
                StatusMessage = LocalizationHelper.Instance.GetFormattedString("InitializationError", ex.Message);
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
                StatusMessage = LocalizationHelper.Instance.GetString("Authenticating");

                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                var authResult = await authService.AuthenticateAsync();

                if (authResult.IsSuccess && authResult.User != null)
                {
                    CurrentUser = authResult.User;
                    Logger.LogInformation("User authenticated: {User} ({Role})",
                        CurrentUser.Username, CurrentUser.Role);

                    StatusMessage = LocalizationHelper.Instance.GetFormattedString("Welcome", CurrentUser.DisplayName);
                }
                else
                {
                    var errorMessage = LocalizationHelper.Instance.GetFormattedString("AuthenticationFailed",
                        authResult.ErrorMessage ?? "Unknown error");

                    Logger.LogError("Authentication failed: {Error}", authResult.ErrorMessage);
                    StatusMessage = errorMessage;
                    DialogService.ShowError(errorMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Authentication error");
                var errorMessage = LocalizationHelper.Instance.GetFormattedString("AuthenticationFailed", ex.Message);
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
                StatusMessage = LocalizationHelper.Instance.GetString("LoadingApplications");

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
                
                // Создаем задачи для параллельной инициализации ApplicationViewModel
                var initializationTasks = new List<Task<ApplicationViewModel>>();
                
                foreach (var app in apps)
                {
                    initializationTasks.Add(CreateAndInitializeApplicationViewModelAsync(app));
                }
                
                // Ожидаем завершения всех инициализаций параллельно
                var initializedViewModels = await Task.WhenAll(initializationTasks);
                
                foreach (var appViewModel in initializedViewModels)
                {
                    Applications.Add(appViewModel);
                }

                // Load and localize categories
                await LoadLocalizedCategoriesAsync(appService);

                // Apply filters
                FilterApplications();

                var appCount = Applications.Count;
                StatusMessage = LocalizationHelper.Instance.GetFormattedString("LoadedApps", appCount);
                Logger.LogInformation("User data loaded: {AppCount} apps", appCount);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load user data");
                StatusMessage = LocalizationHelper.Instance.GetFormattedString("ErrorLoadingApplications", ex.Message);

                // Load test data as fallback
                await LoadTestDataAsync();
            }
        }

        private async Task LoadLocalizedCategoriesAsync(IApplicationService appService)
        {
            try
            {
                // Используем CategoryManagementService для получения видимых категорий с полными метаданными
                using var scope = _serviceProvider.CreateScope();
                var categoryService = scope.ServiceProvider.GetService<ICategoryManagementService>();
                
                LocalizedCategories.Clear();

                // Add "All" category first
                var allCategory = new CategoryViewModel
                {
                    Key = "All",
                    DisplayName = LocalizationHelper.Instance.GetString("CategoryAll"),
                    IsSelected = SelectedCategory == "All",
                    IsChecked = true, // "Все" по умолчанию включено
                    Color = "#2196F3", // Default blue for "All" category
                    Icon = "ViewGridOutline" // Grid icon for all items view
                };
                
                // Подписываемся на изменения чекбокса "Все"
                allCategory.PropertyChanged += OnCategoryPropertyChanged;
                LocalizedCategories.Add(allCategory);

                if (categoryService != null && CurrentUser != null)
                {
                    // Получаем видимые категории с метаданными через CategoryManagementService
                    var categoryDefinitions = await categoryService.GetVisibleCategoriesAsync(CurrentUser);
                    
                    foreach (var categoryDef in categoryDefinitions)
                    {
                        // Используем CategoryManagementService для получения базового имени
                        var baseName = categoryService.GetLocalizedCategoryName(categoryDef);
                        
                        // Применяем UI-слойную локализацию через LocalizationHelper
                        var localizedName = GetLocalizedCategoryName(categoryDef.LocalizationKey, baseName);
                        
                        var categoryViewModel = new CategoryViewModel
                        {
                            Key = categoryDef.Key,
                            DisplayName = localizedName,
                            IsSelected = SelectedCategory == categoryDef.Key,
                            IsChecked = true, // По умолчанию все категории включены
                            Color = categoryDef.Color,
                            Icon = categoryDef.Icon
                        };
                        
                        // Подписываемся на изменения чекбоксов категорий
                        categoryViewModel.PropertyChanged += OnCategoryPropertyChanged;
                        LocalizedCategories.Add(categoryViewModel);
                    }
                    
                    Logger.LogInformation("Loaded {Count} categories via CategoryManagementService (predefined + dynamic)", LocalizedCategories.Count - 1);
                }
                else
                {
                    // Fallback: используем старый метод через ApplicationService
                    Logger.LogWarning("CategoryManagementService not available, falling back to ApplicationService");
                    
                    var categories = await appService.GetCategoriesAsync();
                    var hiddenCategories = UserSettings?.HiddenCategories ?? new List<string>();

                    // Add other categories with defaults
                    foreach (var category in categories.Where(c => !hiddenCategories.Contains(c)))
                    {
                        var localizedName = GetLocalizedCategoryName($"Category_{category}", category);
                        var categoryViewModel = new CategoryViewModel
                        {
                            Key = category,
                            DisplayName = localizedName,
                            IsSelected = SelectedCategory == category,
                            IsChecked = true, // По умолчанию все категории включены
                            Color = "#666666", // Default gray
                            Icon = "FolderOpen" // Default folder icon
                        };
                        
                        // Подписываемся на изменения чекбоксов категорий
                        categoryViewModel.PropertyChanged += OnCategoryPropertyChanged;
                        LocalizedCategories.Add(categoryViewModel);
                    }
                    
                    Logger.LogInformation("Loaded {Count} categories via ApplicationService fallback", LocalizedCategories.Count - 1);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load categories");
                
                // Добавляем минимальную категорию "All" для стабильности UI
                if (LocalizedCategories.Count == 0)
                {
                    var allCategory = new CategoryViewModel
                    {
                        Key = "All",
                        DisplayName = LocalizationHelper.Instance.GetString("CategoryAll") ?? "All",
                        IsSelected = true,
                        IsChecked = true,
                        Color = "#2196F3",
                        Icon = "ViewGridOutline"
                    };
                    
                    // Подписываемся на изменения чекбокса "Все"
                    allCategory.PropertyChanged += OnCategoryPropertyChanged;
                    LocalizedCategories.Add(allCategory);
                }
            }
        }

        private string GetLocalizedCategoryName(string localizationKey, string fallbackName)
        {
            if (string.IsNullOrEmpty(localizationKey)) 
                return fallbackName ?? "";

            try
            {
                var localized = LocalizationHelper.Instance.GetString(localizationKey);
                
                // Если локализация найдена и отличается от ключа, используем её
                if (!string.IsNullOrEmpty(localized) && localized != localizationKey)
                {
                    return localized;
                }
                
                // Fallback на оригинальное название
                return fallbackName ?? localizationKey;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error getting localized name for key {Key}, using fallback", localizationKey);
                return fallbackName ?? localizationKey;
            }
        }

        // Перегрузка для обратной совместимости
        private string GetLocalizedCategoryName(string category)
        {
            if (string.IsNullOrEmpty(category)) return category;

            var key = $"Category_{category}";
            return GetLocalizedCategoryName(key, category);
        }

        private UserSettings CreateDefaultSettings()
        {
            return new UserSettings
            {
                UserId = CurrentUser?.Id ?? 0,
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

        /// <summary>
        /// Создать и асинхронно инициализировать ApplicationViewModel
        /// </summary>
        private async Task<ApplicationViewModel> CreateAndInitializeApplicationViewModelAsync(CoreApplication application)
        {
            // Создаем отдельный scope для каждой операции инициализации
            using var scope = _serviceProvider.CreateScope();
            var categoryService = scope.ServiceProvider.GetService<ICategoryManagementService>();
            
            // Создаем ApplicationViewModel
            var appViewModel = new ApplicationViewModel(application, categoryService);
            
            // Асинхронно инициализируем данные категории
            await appViewModel.InitializeAsync();
            
            return appViewModel;
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
                    Description = LocalizationHelper.Instance.GetString("CalculatorDescription"),
                    Category = "Utilities",
                    ExecutablePath = "calc.exe",
                    IsEnabled = true
                },
                new CoreApplication
                {
                    Id = 2,
                    Name = "Notepad",
                    Description = LocalizationHelper.Instance.GetString("NotepadDescription"),
                    Category = "Utilities",
                    ExecutablePath = "notepad.exe",
                    IsEnabled = true
                },
                new CoreApplication
                {
                    Id = 3,
                    Name = "Control Panel",
                    Description = LocalizationHelper.Instance.GetString("ControlPanelDescription"),
                    Category = "System",
                    ExecutablePath = "control.exe",
                    IsEnabled = true
                },
                new CoreApplication
                {
                    Id = 4,
                    Name = "Command Prompt",
                    Description = LocalizationHelper.Instance.GetString("CommandPromptDescription"),
                    Category = "System",
                    ExecutablePath = "cmd.exe",
                    IsEnabled = true
                },
                new CoreApplication
                {
                    Id = 5,
                    Name = "Google",
                    Description = LocalizationHelper.Instance.GetString("GoogleDescription"),
                    Category = "Web",
                    ExecutablePath = "https://www.google.com",
                    IsEnabled = true,
                    Type = Core.Enums.ApplicationType.Web
                }
            };

            // Создаем задачи для параллельной инициализации тестовых ApplicationViewModel
            var testInitializationTasks = new List<Task<ApplicationViewModel>>();
            
            foreach (var app in testApps)
            {
                testInitializationTasks.Add(CreateAndInitializeApplicationViewModelAsync(app));
            }
            
            // Ожидаем завершения всех инициализаций параллельно
            var testViewModels = await Task.WhenAll(testInitializationTasks);
            
            foreach (var appViewModel in testViewModels)
            {
                Applications.Add(appViewModel);
            }

            // Load test categories with icons and colors
            LocalizedCategories.Clear();
            LocalizedCategories.Add(new CategoryViewModel
            {
                Key = "All",
                DisplayName = LocalizationHelper.Instance.GetString("CategoryAll"),
                IsSelected = true,
                Color = "#2196F3",
                Icon = "ViewGridOutline"
            });
            LocalizedCategories.Add(new CategoryViewModel
            {
                Key = "Utilities",
                DisplayName = LocalizationHelper.Instance.GetString("Category_Utilities"),
                IsSelected = false,
                Color = "#4CAF50",
                Icon = "Wrench"
            });
            LocalizedCategories.Add(new CategoryViewModel
            {
                Key = "System",
                DisplayName = LocalizationHelper.Instance.GetString("CategorySystem"),
                IsSelected = false,
                Color = "#2196F3",
                Icon = "Cogs"
            });
            LocalizedCategories.Add(new CategoryViewModel
            {
                Key = "Web",
                DisplayName = LocalizationHelper.Instance.GetString("CategoryWeb"),
                IsSelected = false,
                Color = "#9C27B0",
                Icon = "Globe"
            });

            FilterApplications();
            StatusMessage = LocalizationHelper.Instance.GetFormattedString("LoadedApps", Applications.Count);
        }

        #endregion

        #region Localization

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            Logger.LogInformation("Language changed, updating UI strings");

            // Обновляем все локализованные свойства
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(LocalizedRole));

            // Обновляем категории с учетом новой системы CategoryManagementService
            foreach (var category in LocalizedCategories)
            {
                if (category.Key == "All")
                {
                    category.DisplayName = LocalizationHelper.Instance.GetString("CategoryAll");
                }
                else
                {
                    // Используем CategoryManagementService для получения правильного ключа локализации
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var categoryService = scope.ServiceProvider.GetService<ICategoryManagementService>();
                            
                            if (categoryService != null)
                            {
                                var categoryDef = await categoryService.GetCategoryByKeyAsync(category.Key);
                                if (categoryDef != null)
                                {
                                    var baseName = categoryService.GetLocalizedCategoryName(categoryDef);
                                    var localizedName = GetLocalizedCategoryName(categoryDef.LocalizationKey, baseName);
                                    
                                    // Обновляем в UI потоке
                                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                                    {
                                        category.DisplayName = localizedName;
                                    });
                                }
                            }
                            else
                            {
                                // Fallback на старую логику
                                var localizedName = GetLocalizedCategoryName(category.Key);
                                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                                {
                                    category.DisplayName = localizedName;
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error updating category localization for {Key}", category.Key);
                        }
                    });
                }
            }

            // Обновляем статус если это стандартное сообщение
            if (StatusMessage == "Ready")
            {
                StatusMessage = LocalizationHelper.Instance.GetString("Ready");
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

                // Filter by checked categories (NEW LOGIC)
                var checkedCategories = LocalizedCategories
                    .Where(c => c.IsChecked)
                    .Select(c => c.Key)
                    .ToHashSet();
                
                // Если чекбокс "Все" не включен, фильтруем по выбранным категориям
                if (!checkedCategories.Contains("All"))
                {
                    filtered = filtered.Where(a => checkedCategories.Contains(a.Category));
                }
                // Если "Все" включено, показываем все приложения

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

        /// <summary>
        /// Обработчик изменений чекбоксов категорий
        /// </summary>
        private void OnCategoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CategoryViewModel.IsChecked) || sender is not CategoryViewModel changedCategory)
                return;

            try
            {
                // Логика чекбокса "Все"
                if (changedCategory.Key == "All")
                {
                    // При установке "Все" в True → все остальные чекбоксы становятся True
                    if (changedCategory.IsChecked)
                    {
                        foreach (var category in LocalizedCategories.Where(c => c.Key != "All"))
                        {
                            category.PropertyChanged -= OnCategoryPropertyChanged; // Отключаем обработчик для избежания рекурсии
                            category.IsChecked = true;
                            category.PropertyChanged += OnCategoryPropertyChanged; // Включаем обратно
                        }
                        Logger.LogDebug("Чекбокс 'Все' установлен в True - все категории включены");
                    }
                    // При установке "Все" в False → все остальные чекбоксы становятся False
                    else
                    {
                        foreach (var category in LocalizedCategories.Where(c => c.Key != "All"))
                        {
                            category.PropertyChanged -= OnCategoryPropertyChanged; // Отключаем обработчик для избежания рекурсии
                            category.IsChecked = false;
                            category.PropertyChanged += OnCategoryPropertyChanged; // Включаем обратно
                        }
                        Logger.LogDebug("Чекбокс 'Все' установлен в False - все категории отключены");
                    }
                }
                else
                {
                    // При установке любого чекбокса в False → "Все" становится False
                    if (!changedCategory.IsChecked)
                    {
                        var allCategory = LocalizedCategories.FirstOrDefault(c => c.Key == "All");
                        if (allCategory != null && allCategory.IsChecked)
                        {
                            allCategory.PropertyChanged -= OnCategoryPropertyChanged; // Отключаем обработчик
                            allCategory.IsChecked = false;
                            allCategory.PropertyChanged += OnCategoryPropertyChanged; // Включаем обратно
                            Logger.LogDebug("Категория '{CategoryName}' отключена - чекбокс 'Все' сброшен", changedCategory.DisplayName);
                        }
                    }
                    // Проверяем, если все категории (кроме "Все") включены, то ставим "Все" в True
                    else if (changedCategory.IsChecked)
                    {
                        var allNonAllCategoriesChecked = LocalizedCategories.Where(c => c.Key != "All").All(c => c.IsChecked);
                        if (allNonAllCategoriesChecked)
                        {
                            var allCategory = LocalizedCategories.FirstOrDefault(c => c.Key == "All");
                            if (allCategory != null && !allCategory.IsChecked)
                            {
                                allCategory.PropertyChanged -= OnCategoryPropertyChanged;
                                allCategory.IsChecked = true;
                                allCategory.PropertyChanged += OnCategoryPropertyChanged;
                                Logger.LogDebug("Все категории включены - чекбокс 'Все' установлен в True");
                            }
                        }
                    }
                }

                // Обновляем фильтрацию приложений
                FilterApplications();
                
                // Обновляем индикатор активного фильтра
                UpdateActiveFilterStatus();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Ошибка при обработке изменения чекбокса категории");
            }
        }

        /// <summary>
        /// Обновляет статус активного фильтра для визуальной индикации
        /// </summary>
        private void UpdateActiveFilterStatus()
        {
            try
            {
                // Фильтр активен, если:
                // 1. Не все категории включены (чекбокс "Все" отключен или не все категории включены)
                // 2. Есть активный поиск
                
                var allCategory = LocalizedCategories.FirstOrDefault(c => c.Key == "All");
                var hasSearchFilter = !string.IsNullOrWhiteSpace(SearchText);
                var allCategoriesSelected = allCategory?.IsChecked == true;
                
                // Фильтр активен, если есть поиск или не все категории выбраны
                HasActiveFilter = hasSearchFilter || !allCategoriesSelected;
                
                Logger.LogDebug("Статус фильтра: HasActiveFilter={HasActiveFilter}, Поиск: '{SearchText}', Все категории: {AllSelected}", 
                    HasActiveFilter, SearchText, allCategoriesSelected);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Ошибка при обновлении статуса активного фильтра");
            }
        }

        private async Task RefreshApplications()
        {
            await ExecuteSafelyAsync(async () =>
            {
                await LoadUserDataAsync();
                StatusMessage = LocalizationHelper.Instance.GetString("ApplicationsRefreshed");
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
                StatusMessage = LocalizationHelper.Instance.GetFormattedString("LaunchingApp", app.Name);

                using var scope = _serviceProvider.CreateScope();
                var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                var result = await appService.LaunchApplicationAsync(app, CurrentUser);

                if (result.IsSuccess)
                {
                    StatusMessage = LocalizationHelper.Instance.GetFormattedString("SuccessfullyLaunched", app.Name);
                    Logger.LogInformation("Application launched: {App}", app.Name);
                }
                else
                {
                    var errorMessage = LocalizationHelper.Instance.GetFormattedString("FailedToLaunch", app.Name, result.ErrorMessage);
                    StatusMessage = errorMessage;
                    DialogService.ShowWarning(errorMessage, LocalizationHelper.Instance.GetString("LaunchError"));
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

        private async void Logout()
        {
            try
            {
                Logger.LogInformation("Logout process started");
                // Показываем диалог подтверждения выхода
                var confirmed = CorporateConfirmationDialog.ShowLogoutConfirmation(
                    WpfApplication.Current.MainWindow);
                
                if (!confirmed)
                {
                    Logger.LogInformation("Logout cancelled by user");
                    return;
                }

                Logger.LogInformation("User confirmed logout, processing...");
                using var scope = _serviceProvider.CreateScope();
                StatusMessage = LocalizationHelper.Instance.GetString("LoggingOut");
                
                // Используем SessionManagementService для обработки выхода
                var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManagementService>();
                var success = await sessionManager.HandleLogoutRequestAsync().ConfigureAwait(false);
                
                if (success)
                {
                    Logger.LogInformation("User logged out successfully: {User}", CurrentUser?.Username);
                    StatusMessage = LocalizationHelper.Instance.GetString("LoggedOut");
                    
                    // Закрываем MainWindow - это запустит HandleMainWindowClosedAsync в App.xaml.cs
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        mainWindow?.Close();
                    });
                }
                else
                {
                    Logger.LogWarning("Logout failed");
                    StatusMessage = LocalizationHelper.Instance.GetString("LogoutFailed");
                }
                
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Logout process failed");
                var errorMessage = LocalizationHelper.Instance.GetFormattedString("LogoutError", ex.Message);
                StatusMessage = errorMessage;
                DialogService.ShowError(errorMessage);
                Logger.LogError(ex, "Error during logout");
            }
        }

        private void OpenSettings()
        {
            StatusMessage = LocalizationHelper.Instance.GetString("SettingsComingSoon");
            DialogService.ShowInfo(
                LocalizationHelper.Instance.GetString("SettingsWindowMessage"),
                LocalizationHelper.Instance.GetString("Settings"));
        }

        private async void SwitchUser()
        {
            try
            {
                // ИСПРАВЛЕНИЕ: Показываем диалог подтверждения смены пользователя
                Logger.LogInformation("User switch requested by {Username}", CurrentUser?.Username);

                bool confirmed = Components.Dialogs.CorporateConfirmationDialog.ShowConfirmation(
                    LocalizationHelper.Instance.GetString("Dialog_SwitchUserTitle"),
                    LocalizationHelper.Instance.GetString("Dialog_SwitchUserMessage"),
                    LocalizationHelper.Instance.GetString("Dialog_SwitchUserDetails"),
                    LocalizationHelper.Instance.GetString("Dialog_Confirm"),
                    LocalizationHelper.Instance.GetString("Common_Cancel"),
                    MaterialDesignThemes.Wpf.PackIconKind.AccountSwitch,
                    WpfApplication.Current.MainWindow);

                if (!confirmed)
                {
                    Logger.LogInformation("User switch cancelled by user");
                    StatusMessage = LocalizationHelper.Instance.GetString("UserSwitchCancelled");
                    return;
                }

                // Завершаем сессию текущего пользователя (НЕ перезапускаем процесс)
                await HandleUserSwitchAsync();
            }
            catch (Exception ex)
            {
                var errorMessage = LocalizationHelper.Instance.GetFormattedString("SwitchUserError", ex.Message);
                StatusMessage = errorMessage;
                DialogService.ShowError(errorMessage);
                Logger.LogError(ex, "Error during user switch");
            }
        }

        private async Task HandleUserSwitchAsync()
        {
            try
            {
                Logger.LogInformation("Handling user switch for {Username}", CurrentUser?.Username);
                StatusMessage = LocalizationHelper.Instance.GetString("SwitchingUser");

                // Используем ту же логику что и в Logout - делегируем SessionManagementService
                using (var scope = _serviceProvider.CreateScope())
                {
                    var sessionService = scope.ServiceProvider.GetRequiredService<ISessionManagementService>();
                    
                    // Завершаем сессию через тот же механизм что и Logout
                    var success = await sessionService.HandleLogoutRequestAsync();
                    
                    if (success)
                    {
                        Logger.LogInformation("User switch successful for {Username}", CurrentUser?.Username);
                        StatusMessage = LocalizationHelper.Instance.GetString("UserSwitchComplete");
                        
                        // Закрываем MainWindow - это запустит HandleMainWindowClosedAsync в App.xaml.cs
                        // который покажет LoginWindow автоматически
                        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
                        {
                            var mainWindow = WpfApplication.Current.MainWindow;
                            mainWindow?.Close();
                        });
                    }
                    else
                    {
                        Logger.LogWarning("User switch failed for {Username}", CurrentUser?.Username);
                        StatusMessage = LocalizationHelper.Instance.GetString("UserSwitchFailed");
                        DialogService.ShowError("Не удалось выполнить смену пользователя");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling user switch for {Username}", CurrentUser?.Username);
                var errorMessage = LocalizationHelper.Instance.GetFormattedString("SwitchUserError", ex.Message);
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

        private async Task ToggleVirtualKeyboard()
        {
            await ExecuteSafelyAsync(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var virtualKeyboardService = scope.ServiceProvider.GetRequiredService<IVirtualKeyboardService>();

                StatusMessage = LocalizationHelper.Instance.GetString("TogglingVirtualKeyboard");

                // Изменяем логику: всегда показываем клавиатуру и позиционируем её
                bool success = await virtualKeyboardService.ShowVirtualKeyboardAsync();
                
                if (!success)
                {
                    // Если первая попытка не удалась, пробуем принудительное позиционирование
                    Logger.LogInformation("Первая попытка показа не удалась, пытаемся принудительное позиционирование");
                    success = await virtualKeyboardService.RepositionKeyboardAsync();
                }

                if (success)
                {
                    IsVirtualKeyboardVisible = true;
                    StatusMessage = LocalizationHelper.Instance.GetString("VirtualKeyboardShown");
                    Logger.LogInformation("Virtual keyboard shown successfully from MainWindow button");
                }
                else
                {
                    // Выполняем диагностику для понимания проблемы
                    var diagnosis = await virtualKeyboardService.DiagnoseVirtualKeyboardAsync();
                    Logger.LogWarning("Failed to show virtual keyboard from MainWindow button. Diagnosis:\n{Diagnosis}", diagnosis);
                    
                    StatusMessage = LocalizationHelper.Instance.GetString("VirtualKeyboardToggleFailed");
                    DialogService.ShowWarning(
                        LocalizationHelper.Instance.GetString("VirtualKeyboardError"), 
                        LocalizationHelper.Instance.GetString("Error"));
                }
            }, "show virtual keyboard");
        }

        private void ToggleSidebar()
        {
            try
            {
                IsSidebarVisible = !IsSidebarVisible;
                
                Logger.LogInformation("Sidebar toggled: {IsVisible}", IsSidebarVisible);
                
                // Обновляем статус
                StatusMessage = IsSidebarVisible 
                    ? LocalizationHelper.Instance.GetString("SidebarShown") 
                    : LocalizationHelper.Instance.GetString("SidebarHidden");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error toggling sidebar");
                DialogService.ShowError($"Ошибка переключения sidebar: {ex.Message}");
            }
        }

        /// <summary>
        /// Открыть окно создания email сообщения
        /// </summary>
        private void ComposeEmail()
        {
            try
            {
                var composeViewModel = _serviceProvider.GetService<ComposeEmailViewModel>();
                if (composeViewModel == null)
                {
                    Logger.LogError("Failed to resolve ComposeEmailViewModel from DI container");
                    MessageBox.Show(LocalizationHelper.Instance.GetString("Error_EmailServiceNotAvailable"), 
                        LocalizationHelper.Instance.GetString("Error"), 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var composeWindow = new ComposeEmailWindow(composeViewModel)
                {
                    Owner = WpfApplication.Current.MainWindow,
                    Title = LocalizationHelper.Instance.GetString("ComposeEmail_WindowTitle")
                };

                composeWindow.Show();
                Logger.LogInformation("Opened compose email window");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening compose email window");
                MessageBox.Show($"{LocalizationHelper.Instance.GetString("Error_EmailServiceUnavailable")}: {ex.Message}", 
                    LocalizationHelper.Instance.GetString("Error"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Открыть адресную книгу
        /// </summary>
        private void OpenAddressBook()
        {
            try
            {
                var addressBookViewModel = _serviceProvider.GetService<AddressBookViewModel>();
                if (addressBookViewModel == null)
                {
                    Logger.LogError("Failed to resolve AddressBookViewModel from DI container");
                    MessageBox.Show(LocalizationHelper.Instance.GetString("Error_AddressBookNotAvailable"), 
                        LocalizationHelper.Instance.GetString("Error"), 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Настраиваем режим просмотра (не выбор контактов)
                addressBookViewModel.IsSelectionMode = false;
                addressBookViewModel.IsAdminMode = CurrentUser?.Role >= Core.Enums.UserRole.Administrator;

                var addressBookWindow = new AddressBookWindow(addressBookViewModel)
                {
                    Owner = WpfApplication.Current.MainWindow,
                    Title = LocalizationHelper.Instance.GetString("AddressBook_WindowTitle")
                };

                addressBookWindow.Show();
                Logger.LogInformation("Opened address book window in admin mode: {IsAdminMode}", addressBookViewModel.IsAdminMode);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening address book window");
                MessageBox.Show($"{LocalizationHelper.Instance.GetString("Error_AddressBookUnavailable")}: {ex.Message}", 
                    LocalizationHelper.Instance.GetString("Error"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Открыть справочную систему
        /// </summary>
        private void OpenHelp()
        {
            try
            {
                var helpWindow = new HelpWindow()
                {
                    Owner = WpfApplication.Current.MainWindow
                };

                helpWindow.Show();
                Logger.LogInformation("Opened help system window");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening help window");
                MessageBox.Show($"{LocalizationHelper.Instance.GetString("Help_LoadingError")}: {ex.Message}", 
                    LocalizationHelper.Instance.GetString("Error"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LocalizationHelper.Instance.LanguageChanged -= OnLanguageChanged;

                // Очищаем коллекции
                Applications.Clear();
                FilteredApplications.Clear();
                LocalizedCategories.Clear();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Инициализация статуса Android подсистемы
        /// </summary>
        private async Task InitializeWSAStatusAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var androidSubsystem = scope.ServiceProvider.GetService<IAndroidSubsystemService>();

                if (androidSubsystem == null)
                {
                    Logger.LogDebug("Android subsystem service not available");
                    ShowWSAStatus = false;
                    return;
                }

                // Показываем статус только если включено в конфигурации
                ShowWSAStatus = androidSubsystem.CurrentMode != AndroidMode.Disabled;
                
                if (!ShowWSAStatus)
                {
                    Logger.LogDebug("WSA status hidden - Android subsystem disabled");
                    return;
                }

                // Подписываемся на изменения статуса
                androidSubsystem.StatusChanged += OnWSAStatusChanged;

                // Устанавливаем начальный статус
                await UpdateWSAStatusDisplayAsync(androidSubsystem);

                Logger.LogInformation("WSA status initialized for {Mode} mode", androidSubsystem.CurrentMode);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize WSA status");
                ShowWSAStatus = false;
            }
        }

        /// <summary>
        /// Обработчик изменения статуса WSA
        /// </summary>
        private async void OnWSAStatusChanged(object? sender, string status)
        {
            try
            {
                if (sender is IAndroidSubsystemService androidSubsystem)
                {
                    await UpdateWSAStatusDisplayAsync(androidSubsystem);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling WSA status change");
            }
        }

        /// <summary>
        /// Обновление отображения статуса WSA
        /// </summary>
        private async Task UpdateWSAStatusDisplayAsync(IAndroidSubsystemService androidSubsystem)
        {
            try
            {
                var status = androidSubsystem.WSAStatus;
                var mode = androidSubsystem.CurrentMode;

                // Обновляем UI в главном потоке
                await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                {
                    WSAStatusText = GetLocalizedStatusText(status);
                    WSAStatusColor = GetStatusColor(status);
                    WSAStatusTooltip = GetStatusTooltip(status, mode);
                });

                Logger.LogDebug("WSA status updated: {Status} in {Mode} mode", status, mode);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to update WSA status display");
            }
        }

        /// <summary>
        /// Получить локализованный текст статуса
        /// </summary>
        private string GetLocalizedStatusText(string status)
        {
            return status switch
            {
                "Ready" => "Готов",
                "Starting" => "Запуск",
                "Stopping" => "Остановка",
                "Available" => "Доступен",
                "Unavailable" => "Недоступен",
                "Disabled" => "Отключен",
                "Error" => "Ошибка",
                "Initializing" => "Инициализация",
                "Suspended (Low Memory)" => "Приостановлен",
                _ => status
            };
        }

        /// <summary>
        /// Получить цвет для статуса
        /// </summary>
        private string GetStatusColor(string status)
        {
            return status switch
            {
                "Ready" => "#4CAF50",           // Зеленый
                "Available" => "#2196F3",       // Синий
                "Starting" or "Initializing" => "#FF9800",  // Оранжевый
                "Stopping" => "#FF9800",        // Оранжевый
                "Error" => "#F44336",           // Красный
                "Unavailable" or "Disabled" => "#757575",  // Серый
                "Suspended (Low Memory)" => "#FF5722",     // Темно-оранжевый
                _ => "#666666"                  // Серый по умолчанию
            };
        }

        /// <summary>
        /// Получить подсказку для статуса
        /// </summary>
        private string GetStatusTooltip(string status, AndroidMode mode)
        {
            var modeText = mode switch
            {
                AndroidMode.Disabled => "отключен",
                AndroidMode.OnDemand => "по требованию", 
                AndroidMode.Preload => "предзагрузка",
                _ => mode.ToString()
            };

            return status switch
            {
                "Ready" => $"Android подсистема готова к работе\nРежим: {modeText}",
                "Starting" => $"Запуск Android подсистемы...\nРежим: {modeText}",
                "Available" => $"Android подсистема доступна\nРежим: {modeText}",
                "Unavailable" => $"Android подсистема недоступна\nПроверьте установку WSA",
                "Error" => $"Ошибка Android подсистемы\nПроверьте логи для подробностей",
                "Disabled" => "Android функции отключены в настройках",
                "Suspended (Low Memory)" => "Android подсистема приостановлена\nНедостаточно свободной памяти",
                _ => $"Android подсистема: {status}\nРежим: {modeText}"
            };
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
        private bool _isChecked = true; // По умолчанию все категории видимы
        private string _color = "#666666";
        private string _icon = "FolderOpen";

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

        /// <summary>
        /// Включена ли категория в фильтрацию (для чекбоксов в Sidebar)
        /// </summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Цвет категории в hex формате
        /// </summary>
        public string Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Иконка FontAwesome
        /// </summary>
        public string Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    _icon = value;
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