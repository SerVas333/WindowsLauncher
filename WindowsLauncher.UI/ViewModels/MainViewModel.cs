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

        #endregion

        #region Constructor

        public MainViewModel(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<MainViewModel> logger,
            IDialogService dialogService,
            OfficeToolsViewModel officeToolsViewModel,
            ApplicationManagementViewModel applicationManagementViewModel,
            WSAStatusViewModel wsaStatusViewModel,
            CategoryManagementViewModel categoryManagementViewModel,
            UserSessionViewModel userSessionViewModel)
            : base(logger, dialogService)
        {
            _serviceScopeFactory = serviceScopeFactory;
            OfficeTools = officeToolsViewModel;
            ApplicationManager = applicationManagementViewModel;
            WSAStatus = wsaStatusViewModel;
            CategoryManager = categoryManagementViewModel;
            UserSession = userSessionViewModel;

            // Настраиваем провайдер роли пользователя для офисных инструментов
            OfficeTools.SetCurrentUserRoleProvider(() => CurrentUser?.Role);

            // Подписываемся на события CategoryManager
            CategoryManager.FilteringRequested += OnFilteringRequested;
            
            // Подписываемся на события UserSession
            UserSession.UserChanged += OnUserChanged;

            // Инициализируем команды
            InitializeCommands();

            // Подписываемся на изменение языка
            LocalizationHelper.Instance.LanguageChanged += OnLanguageChanged;

            // Инициализация будет запущена вручную после установки CurrentUser
        }

        #endregion

        #region Properties

        /// <summary>
        /// Делегированное свойство к UserSession для обратной совместимости с XAML
        /// </summary>
        public User? CurrentUser
        {
            get => UserSession.CurrentUser;
            set
            {
                // Простое делегирование - вся логика теперь в OnUserChanged через событие
                if (UserSession.CurrentUser != value)
                {
                    UserSession.CurrentUser = value;
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

        // Делегированные свойства к CategoryManager для обратной совместимости с XAML
        public string SearchText
        {
            get => CategoryManager.SearchText;
            set => CategoryManager.SearchText = value;
        }

        public string SelectedCategory
        {
            get => CategoryManager.SelectedCategory;
            set => CategoryManager.SelectedCategory = value;
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

        // Делегированные коллекции к ApplicationManager и CategoryManager
        public ObservableCollection<ApplicationViewModel> Applications => ApplicationManager.Applications;
        public ObservableCollection<ApplicationViewModel> FilteredApplications => ApplicationManager.FilteredApplications;
        public ObservableCollection<CategoryViewModel> LocalizedCategories => CategoryManager.LocalizedCategories;

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

        /// <summary>
        /// Делегированное свойство к UserSession для обратной совместимости с XAML
        /// </summary>
        public string LocalizedRole => UserSession.LocalizedRole;

        public int TileSize => UserSettings?.TileSize ?? 150;
        public bool ShowCategories => UserSettings?.ShowCategories ?? true;
        public string Theme => UserSettings?.Theme ?? "Light";
        /// <summary>
        /// Делегированное свойство к UserSession для обратной совместимости с XAML
        /// </summary>
        public bool CanManageSettings => UserSession.CanManageSettings;
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
        public bool HasActiveFilter => CategoryManager.HasActiveFilter;

        // Делегированные свойства к WSAStatus для обратной совместимости с XAML
        public bool ShowWSAStatus => WSAStatus.ShowWSAStatus;
        public string WSAStatusText => WSAStatus.WSAStatusText;
        public string WSAStatusTooltip => WSAStatus.WSAStatusTooltip;
        public string WSAStatusColor => WSAStatus.WSAStatusColor;

        /// <summary>
        /// Офисные инструменты: email, адресная книга, справка
        /// </summary>
        public OfficeToolsViewModel OfficeTools { get; }

        /// <summary>
        /// Управление приложениями: загрузка, фильтрация, поиск, запуск
        /// </summary>
        public ApplicationManagementViewModel ApplicationManager { get; }

        /// <summary>
        /// Статус Android подсистемы (WSA)
        /// </summary>
        public WSAStatusViewModel WSAStatus { get; }

        /// <summary>
        /// Управление категориями и фильтрацией
        /// </summary>
        public CategoryManagementViewModel CategoryManager { get; }

        /// <summary>
        /// Управление пользовательскими сессиями
        /// </summary>
        public UserSessionViewModel UserSession { get; }

        #endregion

        #region Commands

        // Делегированные команды к ApplicationManager и CategoryManager для обратной совместимости с XAML
        public AsyncRelayCommand<ApplicationViewModel> LaunchApplicationCommand => ApplicationManager.LaunchApplicationCommand;
        public RelayCommand<string> SelectCategoryCommand => CategoryManager.SelectCategoryCommand;
        public AsyncRelayCommand RefreshCommand => ApplicationManager.RefreshCommand;
        /// <summary>
        /// Делегированная команда к UserSession для обратной совместимости с XAML
        /// </summary>
        public RelayCommand ExitApplicationCommand => UserSession.ExitApplicationCommand;
        public RelayCommand OpenSettingsCommand { get; private set; } = null!;
        public RelayCommand OpenAdminCommand { get; private set; } = null!;
        public AsyncRelayCommand ShowVirtualKeyboardCommand { get; private set; } = null!;
        public RelayCommand ToggleSidebarCommand { get; private set; } = null!;
        
        // Делегированные команды к OfficeToolsViewModel для обратной совместимости с XAML
        public RelayCommand ComposeEmailCommand => OfficeTools.ComposeEmailCommand;
        public RelayCommand OpenAddressBookCommand => OfficeTools.OpenAddressBookCommand;
        public RelayCommand OpenHelpCommand => OfficeTools.OpenHelpCommand;

        private void InitializeCommands()
        {
            // LaunchApplicationCommand, RefreshCommand и SelectCategoryCommand теперь делегируются

            OpenSettingsCommand = new RelayCommand(
                OpenSettings,
                () => CanManageSettings);

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
                    await UserSession.AuthenticateUserAsync();

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

                // Initialize WSA status через делегированный ViewModel
                await WSAStatus.InitializeWSAStatusAsync();

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

                // Load and localize categories через CategoryManager
                await CategoryManager.LoadLocalizedCategoriesAsync(appService);

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

        // LoadLocalizedCategoriesAsync и GetLocalizedCategoryName удалены - теперь обрабатываются в CategoryManagementViewModel

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

        private async void OnLanguageChanged(object? sender, EventArgs e)
        {
            Logger.LogInformation("Language changed, updating UI strings");

            // Обновляем все локализованные свойства
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(LocalizedRole));

            // Обновляем локализацию категорий через CategoryManager
            await CategoryManager.UpdateLocalizationAsync();

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

        /// <summary>
        /// Обработчик события обновления фильтрации от CategoryManagementViewModel
        /// </summary>
        /// <summary>
        /// Обработчик события изменения пользователя из UserSessionViewModel
        /// </summary>
        private void OnUserChanged(object? sender, User? user)
        {
            try
            {
                Logger.LogDebug("User changed event received: {Username}", user?.Username ?? "null");
                
                // Обновляем внутреннее поле для совместимости
                _currentUser = user;
                
                // Уведомляем об изменении свойств
                OnPropertyChanged(nameof(CurrentUser));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(LocalizedRole));
                OnPropertyChanged(nameof(CanManageSettings));

                // Передаем пользователя в ApplicationManager и CategoryManager
                ApplicationManager.CurrentUser = user;
                CategoryManager.CurrentUser = user;

                // Обновляем команды
                OpenSettingsCommand.RaiseCanExecuteChanged();
                OpenAdminCommand.RaiseCanExecuteChanged();

                // ИСПРАВЛЕНИЕ: Сбрасываем флаг инициализации при любой смене пользователя
                // Это позволяет новому пользователю инициализироваться заново в Singleton ViewModel
                _isInitialized = false;
                
                if (user != null)
                {
                    // Инициализация для любого нового пользователя - выполняем в UI потоке
                    _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await InitializeAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling user changed event");
            }
        }

        private void OnFilteringRequested(object? sender, EventArgs e)
        {
            try
            {
                Logger.LogDebug("Filtering requested by CategoryManagementViewModel");
                FilterApplications();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling filtering request");
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

                // Filter by checked categories через CategoryManager
                if (!CategoryManager.ShouldIncludeAllCategories())
                {
                    var checkedCategories = CategoryManager.GetCheckedCategories();
                    filtered = filtered.Where(a => checkedCategories.Contains(a.Category));
                }

                // Filter by search (дублируем логику поиска из ApplicationManager для консистентности)
                if (!string.IsNullOrWhiteSpace(CategoryManager.SearchText))
                {
                    var searchLower = CategoryManager.SearchText.ToLower();
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

        // OnCategoryPropertyChanged и UpdateActiveFilterStatus удалены - теперь обрабатываются в CategoryManagementViewModel

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


        private void OpenSettings()
        {
            DialogService.ShowInfo(
                LocalizationHelper.Instance.GetString("SettingsWindowMessage"),
                LocalizationHelper.Instance.GetString("Settings"));
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


        #endregion

        #region Security Methods



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

    // CategoryViewModel перенесен в CategoryManagementViewModel.cs
}