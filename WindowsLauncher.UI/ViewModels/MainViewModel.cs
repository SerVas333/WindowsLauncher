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
using WindowsLauncher.UI.Infrastructure.Extensions;
using System.ComponentModel;
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;
using WindowsLauncher.UI.Views;
using System.Windows;
using System.Windows.Input;
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

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private User? _currentUser;
        private UserSettings? _userSettings;
        private string _statusMessage = "";
        private bool _isLoading = false;
        private bool _isInitialized = false;
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
            IServiceScopeFactory serviceScopeFactory,
            ILogger<MainViewModel> logger,
            IDialogService dialogService,
            OfficeToolsViewModel officeToolsViewModel,
            ApplicationManagementViewModel applicationManagementViewModel)
            : base(logger, dialogService)
        {
            _serviceScopeFactory = serviceScopeFactory;
            OfficeTools = officeToolsViewModel;
            ApplicationManager = applicationManagementViewModel;

            // Настраиваем провайдер роли пользователя для офисных инструментов
            OfficeTools.SetCurrentUserRoleProvider(() => CurrentUser?.Role);

            // Инициализируем коллекции (только те что не делегируются)
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

                    // Передаем пользователя в ApplicationManager
                    ApplicationManager.CurrentUser = value;

                    // Обновляем команды
                    OpenSettingsCommand.RaiseCanExecuteChanged();
                    OpenAdminCommand.RaiseCanExecuteChanged();

                    // Упрощенная логика смены пользователя с обеспечением безопасности
                    if (value != null)
                    {
                        var oldUser = _currentUser; // Сохраняем ссылку на старого пользователя
                        
                        // Проверяем смену пользователя (сравниваем ID, не имена)
                        bool isUserChanged = oldUser != null && oldUser.Id != value.Id;
                        
                        if (isUserChanged && _isInitialized)
                        {
                            Logger.LogWarning("SECURITY: User changed from {OldUser} (ID: {OldId}) to {NewUser} (ID: {NewId})", 
                                oldUser.Username, oldUser.Id, value.Username, value.Id);
                            
                            // КРИТИЧНО: Закрываем все приложения старого пользователя для безопасности
                            _ = Task.Run(async () =>
                            {
                                await CloseUserApplicationsAsync(oldUser);
                                await ClearUserStateAsync();
                                
                                // Сбрасываем инициализацию для перезагрузки данных нового пользователя
                                _isInitialized = false;
                                
                                // Запускаем инициализацию для нового пользователя в UI потоке
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                                {
                                    await InitializeAsync();
                                });
                            });
                        }
                        else if (!_isInitialized)
                        {
                            // Первая инициализация или пользователь не изменился - выполняем в UI потоке
                            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                await InitializeAsync();
                            });
                        }
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

        // Делегированные свойства к ApplicationManager для обратной совместимости с XAML
        public string SearchText
        {
            get => ApplicationManager.SearchText;
            set 
            { 
                ApplicationManager.SearchText = value;
                UpdateActiveFilterStatus(); // Обновляем индикатор при изменении поиска
            }
        }

        public string SelectedCategory
        {
            get => ApplicationManager.SelectedCategory;
            set
            {
                ApplicationManager.SelectedCategory = value;
                UpdateCategorySelection();
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

        // Делегированные коллекции к ApplicationManager
        public ObservableCollection<ApplicationViewModel> Applications => ApplicationManager.Applications;
        public ObservableCollection<ApplicationViewModel> FilteredApplications => ApplicationManager.FilteredApplications;
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
        public int ApplicationCount => ApplicationManager.ApplicationCount;
        public bool HasNoApplications => ApplicationManager.HasNoApplications;
        
        // Вычисляемые свойства для отфильтрованных приложений (UI-специфичные)
        public int FilteredApplicationCount => FilteredApplications.Count;
        public bool HasNoFilteredApplications => !ApplicationManager.IsLoading && FilteredApplicationCount == 0;
        
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

        /// <summary>
        /// Офисные инструменты: email, адресная книга, справка
        /// </summary>
        public OfficeToolsViewModel OfficeTools { get; }

        /// <summary>
        /// Управление приложениями: загрузка, фильтрация, поиск, запуск
        /// </summary>
        public ApplicationManagementViewModel ApplicationManager { get; }

        #endregion

        #region Commands

        // Делегированные команды к ApplicationManager для обратной совместимости с XAML
        public AsyncRelayCommand<ApplicationViewModel> LaunchApplicationCommand => ApplicationManager.LaunchApplicationCommand;
        public RelayCommand<string> SelectCategoryCommand { get; private set; } = null!;
        public AsyncRelayCommand RefreshCommand => ApplicationManager.RefreshCommand;
        public RelayCommand LogoutCommand { get; private set; } = null!;
        public RelayCommand OpenSettingsCommand { get; private set; } = null!;
        public RelayCommand SwitchUserCommand { get; private set; } = null!;
        public RelayCommand OpenAdminCommand { get; private set; } = null!;
        public AsyncRelayCommand ShowVirtualKeyboardCommand { get; private set; } = null!;
        public RelayCommand ToggleSidebarCommand { get; private set; } = null!;
        
        // Делегированные команды к OfficeToolsViewModel для обратной совместимости с XAML
        public RelayCommand ComposeEmailCommand => OfficeTools.ComposeEmailCommand;
        public RelayCommand OpenAddressBookCommand => OfficeTools.OpenAddressBookCommand;
        public RelayCommand OpenHelpCommand => OfficeTools.OpenHelpCommand;

        private void InitializeCommands()
        {
            // LaunchApplicationCommand и RefreshCommand теперь делегируются к ApplicationManager

            SelectCategoryCommand = new RelayCommand<string>(SelectCategory);

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

            // Офисные команды теперь делегируются к OfficeToolsViewModel
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

                using var scope = _serviceScopeFactory.CreateScope();
                Logger.LogInformation("Created DI scope successfully");

                // БД уже должна быть инициализирована на этапе запуска приложения

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
                
                // ИСПРАВЛЕНИЕ: Принудительно обновляем UI и команды после смены пользователя
                OnPropertyChanged(nameof(ApplicationCount));
                OnPropertyChanged(nameof(HasNoApplications));
                OnPropertyChanged(nameof(FilteredApplicationCount));
                OnPropertyChanged(nameof(HasNoFilteredApplications));
                
                // Обновляем команды
                CommandManager.InvalidateRequerySuggested();
                
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

                using var scope = _serviceScopeFactory.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                var authResult = await authService.AuthenticateAsync();

                if (authResult.IsSuccess && authResult.User != null)
                {
                    CurrentUser = authResult.User;
                    Logger.LogInformation("User authenticated: {User} ({Role})",
                        CurrentUser.Username, CurrentUser.Role);
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

                using var scope = _serviceScopeFactory.CreateScope();
                var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                // Load user settings
                try
                {
                    var authzService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
                    UserSettings = await authzService.GetUserSettingsAsync(CurrentUser);
                    Logger.LogInformation("User settings loaded");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to load user settings, using defaults");
                    UserSettings = CreateDefaultSettings();
                }

                // Load applications через ApplicationManager
                await ApplicationManager.LoadApplicationsAsync();

                // Load and localize categories
                await LoadLocalizedCategoriesAsync(appService);

                var appCount = ApplicationManager.ApplicationCount;
                Logger.LogInformation("User data loaded: {AppCount} apps", appCount);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load user data");
                StatusMessage = LocalizationHelper.Instance.GetFormattedString("ErrorLoadingApplications", ex.Message);

                // Приложения будут пустыми - это нормально для production системы
            }
        }

        private async Task LoadLocalizedCategoriesAsync(IApplicationService appService)
        {
            try
            {
                // Используем CategoryManagementService для получения видимых категорий с полными метаданными
                using var scope = _serviceScopeFactory.CreateScope();
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
                    Icon = "ThLarge" // Grid icon for all items view (FontAwesome)
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
                        var localizedName = GetLocalizedCategoryName(category);
                        var categoryViewModel = new CategoryViewModel
                        {
                            Key = category,
                            DisplayName = localizedName,
                            IsSelected = SelectedCategory == category,
                            IsChecked = true, // По умолчанию все категории включены
                            Color = "#666666", // Default gray
                            Icon = "FolderOpen" // Default folder icon (FontAwesome)
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
                        Icon = "ThLarge" // Grid icon (FontAwesome)
                    };
                    
                    // Подписываемся на изменения чекбокса "Все"
                    allCategory.PropertyChanged += OnCategoryPropertyChanged;
                    LocalizedCategories.Add(allCategory);
                }
            }
        }

        private string GetLocalizedCategoryName(string localizationKey, string fallbackName = null)
        {
            if (string.IsNullOrEmpty(localizationKey)) 
                return fallbackName ?? "";

            try
            {
                // Если ключ не содержит подчеркивания, считаем его категорией и формируем ключ
                var actualKey = localizationKey.Contains("_") ? localizationKey : $"Category_{localizationKey}";
                var actualFallback = fallbackName ?? localizationKey;
                
                var localized = LocalizationHelper.Instance.GetString(actualKey);
                
                // Если локализация найдена и отличается от ключа, используем её
                return !string.IsNullOrEmpty(localized) && localized != actualKey 
                    ? localized 
                    : actualFallback;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error getting localized name for key {Key}, using fallback", localizationKey);
                return fallbackName ?? localizationKey;
            }
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
        // CreateAndInitializeApplicationViewModelAsync удален - теперь используется ApplicationManager

        // LoadTestDataAsync удален - fallback на тестовые данные не нужен в production системе

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
                            using var scope = _serviceScopeFactory.CreateScope();
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
                // Используем базовую фильтрацию ApplicationManager (по SearchText)
                // Но добавляем дополнительную фильтрацию по категориям на уровне MainViewModel
                
                var filtered = ApplicationManager.Applications.AsEnumerable();

                // Filter by checked categories (UI-специфичная логика)
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

                // Filter by search (дублируем логику поиска из ApplicationManager для консистентности)
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var searchLower = SearchText.ToLower();
                    filtered = filtered.Where(a =>
                        a.Name.ToLower().Contains(searchLower) ||
                        (!string.IsNullOrEmpty(a.Description) && a.Description.ToLower().Contains(searchLower)));
                }

                FilteredApplications.Clear();
                foreach (var app in filtered)
                {
                    FilteredApplications.Add(app);
                }

                // Обновляем вычисляемые свойства (используют FilteredApplications.Count)
                OnPropertyChanged(nameof(FilteredApplicationCount));
                OnPropertyChanged(nameof(HasNoFilteredApplications));

                Logger.LogDebug("Filtered applications: {Count}/{Total}",
                    FilteredApplications.Count, ApplicationManager.Applications.Count);
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

        // RefreshApplications удален - теперь используется ApplicationManager.RefreshCommand

        /// <summary>
        /// Обновление приложений после изменений в AdminWindow (сохраняя фильтры пользователя)
        /// </summary>
        public async Task RefreshApplicationsFromAdmin()
        {
            await ExecuteSafelyAsync(async () =>
            {
                Logger.LogInformation("Refreshing applications after AdminWindow changes");

                // Используем ApplicationManager для перезагрузки
                await ApplicationManager.LoadApplicationsAsync();
                
                Logger.LogInformation("Applications refreshed successfully after AdminWindow changes");
            }, "refresh applications from admin");
        }

        #endregion

        #region Commands Implementation

        // LaunchApplication удален - теперь используется ApplicationManager.LaunchApplicationCommand

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
                using var scope = _serviceScopeFactory.CreateScope();
                
                // Используем SessionManagementService для обработки выхода
                var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManagementService>();
                var success = await sessionManager.HandleLogoutRequestAsync().ConfigureAwait(false);
                
                if (success)
                {
                    Logger.LogInformation("User logged out successfully: {User}", CurrentUser?.Username);
                    StatusMessage = LocalizationHelper.Instance.GetString("LoggedOut"); // Оставляем финальное состояние
                    
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
                    StatusMessage = LocalizationHelper.Instance.GetString("LogoutFailed"); // Оставляем для ошибки
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
                    "👤", // Эмодзи пользователя для смены пользователя
                    WpfApplication.Current.MainWindow);

                if (!confirmed)
                {
                    Logger.LogInformation("User switch cancelled by user");
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

                // Используем ту же логику что и в Logout - делегируем SessionManagementService
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var sessionService = scope.ServiceProvider.GetRequiredService<ISessionManagementService>();
                    
                    // Завершаем сессию через тот же механизм что и Logout
                    var success = await sessionService.HandleLogoutRequestAsync();
                    
                    if (success)
                    {
                        Logger.LogInformation("User switch successful for {Username}", CurrentUser?.Username);
                        
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
                        StatusMessage = LocalizationHelper.Instance.GetString("UserSwitchFailed"); // Оставляем для ошибки
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



        private async void OpenAdminWindow()
        {
            try
            {
                // AdminWindow создается с ServiceScopeFactory для работы с Scoped сервисами  
                using var scope = _serviceScopeFactory.CreateScope();
                var adminWindow = new AdminWindow(scope.ServiceProvider);
                adminWindow.Owner = WpfApplication.Current.MainWindow;
                adminWindow.ShowDialog();

                // После закрытия окна администрирования обновляем список приложений
                // ИСПРАВЛЕНО: используем специальный метод с принудительной очисткой и await
                await RefreshApplicationsFromAdmin();
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
                using var scope = _serviceScopeFactory.CreateScope();
                var virtualKeyboardService = scope.ServiceProvider.GetRequiredService<IVirtualKeyboardService>();

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
                    Logger.LogInformation("Virtual keyboard shown successfully from MainWindow button");
                }
                else
                {
                    // Выполняем диагностику для понимания проблемы
                    var diagnosis = await virtualKeyboardService.DiagnoseVirtualKeyboardAsync();
                    Logger.LogWarning("Failed to show virtual keyboard from MainWindow button. Diagnosis:\n{Diagnosis}", diagnosis);
                    
                    StatusMessage = LocalizationHelper.Instance.GetString("VirtualKeyboardToggleFailed"); // Оставляем для ошибки
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
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error toggling sidebar");
                DialogService.ShowError($"Ошибка переключения sidebar: {ex.Message}");
            }
        }

        // Офисные команды (ComposeEmail, OpenAddressBook, OpenHelp) 
        // перенесены в OfficeToolsViewModel для лучшей архитектуры

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
                Logger.LogInformation("Starting WSA status initialization in MainViewModel");
                
                using var scope = _serviceScopeFactory.CreateScope();
                var androidSubsystem = scope.ServiceProvider.GetService<IAndroidSubsystemService>();

                if (androidSubsystem == null)
                {
                    Logger.LogWarning("Android subsystem service not available in MainViewModel");
                    ShowWSAStatus = false;
                    return;
                }

                var mode = androidSubsystem.CurrentMode;
                var currentStatus = androidSubsystem.WSAStatus;
                Logger.LogInformation("MainViewModel: AndroidSubsystemService mode: {Mode}, current status: {Status}", mode, currentStatus);

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

        #region Security Methods

        /// <summary>
        /// Безопасно закрыть все приложения пользователя при смене пользователя
        /// Критично для предотвращения утечки персональной информации
        /// </summary>
        private async Task CloseUserApplicationsAsync(User user)
        {
            if (user == null) return;

            try
            {
                Logger.LogWarning("SECURITY: Closing all applications for user {Username} during user switch", user.Username);
                
                await ExecuteInScopeAsync(async (serviceProvider) =>
                {
                    var lifecycleService = serviceProvider.GetRequiredService<IApplicationLifecycleService>();
                    
                    // Получаем все приложения пользователя
                    var userApplications = await lifecycleService.GetByUserAsync(user.Username);
                    
                    if (userApplications.Count > 0)
                    {
                        Logger.LogWarning("SECURITY: Found {Count} applications to close for user {Username}", 
                            userApplications.Count, user.Username);
                        
                        // Закрываем все приложения пользователя
                        var result = await lifecycleService.CloseAllAsync(timeoutMs: 10000);
                        
                        if (result.Success)
                        {
                            Logger.LogInformation("SECURITY: Successfully closed all applications for user {Username}", user.Username);
                        }
                        else
                        {
                            Logger.LogError("SECURITY: Failed to close some applications for user {Username}: {Errors}", 
                                user.Username, string.Join(", ", result.Errors));
                        }
                    }
                    else
                    {
                        Logger.LogInformation("SECURITY: No applications to close for user {Username}", user.Username);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SECURITY: Critical error closing applications for user {Username}", user.Username);
                // Не выбрасываем исключение - смена пользователя должна продолжиться даже при ошибке
            }
        }

        /// <summary>
        /// Полная очистка состояния пользователя в UI
        /// </summary>
        private async Task ClearUserStateAsync()
        {
            Logger.LogInformation("Clearing user state for user switch");
            
            try
            {
                // Очищаем ApplicationManager
                await ApplicationManager.ClearAsync();
                
                // Очистка остального состояния в UI потоке
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LocalizedCategories.Clear();
                    UserSettings = null;
                    HasActiveFilter = false;
                    
                    // Принудительно обновляем связанные свойства UI
                    OnPropertyChanged(nameof(ApplicationCount));
                    OnPropertyChanged(nameof(HasNoApplications));
                    OnPropertyChanged(nameof(FilteredApplicationCount));
                    OnPropertyChanged(nameof(HasNoFilteredApplications));
                    OnPropertyChanged(nameof(TileSize));
                    OnPropertyChanged(nameof(ShowCategories));
                    OnPropertyChanged(nameof(Theme));
                });
                
                Logger.LogInformation("User state cleared successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error clearing user state");
                throw;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Выполнить операцию в DI scope
        /// </summary>
        private async Task<T> ExecuteInScopeAsync<T>(Func<IServiceProvider, Task<T>> operation)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            return await operation(scope.ServiceProvider);
        }

        /// <summary>
        /// Выполнить операцию в DI scope (без возвращаемого значения)
        /// </summary>
        private async Task ExecuteInScopeAsync(Func<IServiceProvider, Task> operation)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await operation(scope.ServiceProvider);
        }

        /// <summary>
        /// Выполнить синхронную операцию в DI scope
        /// </summary>
        private T ExecuteInScope<T>(Func<IServiceProvider, T> operation)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            return operation(scope.ServiceProvider);
        }

        /// <summary>
        /// Выполнить синхронную операцию в DI scope (без возвращаемого значения)
        /// </summary>
        private void ExecuteInScope(Action<IServiceProvider> operation)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            operation(scope.ServiceProvider);
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