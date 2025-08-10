// WindowsLauncher.UI/ViewModels/AdminViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Data;
using Microsoft.Win32;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Email;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Configuration;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.UI.Infrastructure.Commands;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Views;
using WindowsLauncher.Services;
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для окна администрирования приложений
    /// </summary>
    public class AdminViewModel : ViewModelBase
    {
        #region Fields

        private readonly IApplicationService _applicationService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILocalUserService _localUserService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ApplicationDataManager _applicationDataManager;

        private ApplicationEditViewModel? _selectedApplication;
        private ApplicationEditViewModel? _editingApplication;
        private string _searchText = "";
        private bool _isEditMode;
        private bool _hasUnsavedChanges;

        // Поля для управления локальными пользователями
        private User? _selectedUser;
        private bool _isUserManagementMode = false;
        private bool _isSystemManagementMode = false;
        private string _userSearchText = "";
        private string _statusMessage = "";
        
        // Поля для системной панели
        private ApplicationDataInfo? _applicationDataInfo;
        private string _applicationVersion = "";
        private string _buildDate = "";
        private string _databaseVersion = "";
        private string _totalDataSize = "";
        
        // Поля для SMTP статуса
        private string _primarySmtpStatus = "❌ Не настроен";
        private string _backupSmtpStatus = "❌ Не настроен";

        #endregion

        #region Constructor

        public AdminViewModel(
            IApplicationService applicationService,
            IAuthorizationService authorizationService,
            ILocalUserService localUserService,
            IServiceProvider serviceProvider,
            ApplicationDataManager applicationDataManager,
            ILogger<AdminViewModel> logger,
            IDialogService dialogService)
            : base(logger, dialogService)
        {
            _applicationService = applicationService;
            _authorizationService = authorizationService;
            _localUserService = localUserService;
            _serviceProvider = serviceProvider;
            _applicationDataManager = applicationDataManager;

            Applications = new ObservableCollection<ApplicationEditViewModel>();
            AvailableCategories = new ObservableCollection<string>();
            AvailableGroups = new ObservableCollection<string>();
            
            // Инициализация коллекции локальных пользователей
            LocalUsers = new ObservableCollection<User>();
            AvailableRoles = Enum.GetValues<UserRole>().ToList();

            // Настройка фильтрации
            ApplicationsView = CollectionViewSource.GetDefaultView(Applications);
            ApplicationsView.Filter = FilterApplications;

            InitializeCommands();
            _ = InitializeAsync();
        }


        #endregion

        #region Properties

        public ObservableCollection<ApplicationEditViewModel> Applications { get; }
        public ObservableCollection<string> AvailableCategories { get; }
        public ObservableCollection<string> AvailableGroups { get; }
        /// <summary>
        /// Доступные типы приложений (исключая Android когда он отключен)
        /// </summary>
        public List<ApplicationType> AvailableTypes
        {
            get
            {
                var allTypes = Enum.GetValues<ApplicationType>().ToList();
                
                // Исключаем Android тип когда Android подсистема отключена
                if (!IsAndroidEnabled)
                {
                    allTypes.Remove(ApplicationType.Android);
                }
                
                return allTypes;
            }
        }
        public List<UserRole> AvailableRoles { get; }
        public ICollectionView ApplicationsView { get; }

        // Свойства для управления локальными пользователями
        public ObservableCollection<User> LocalUsers { get; }

        #endregion


        public ApplicationEditViewModel? SelectedApplication
        {
            get => _selectedApplication;
            set
            {
                // Проверяем несохраненные изменения перед сменой выбора
                if (_selectedApplication != value && HasUnsavedChanges)
                {
                    var result = DialogService.ShowConfirmation(
                        "У вас есть несохраненные изменения. Сохранить их перед переходом к другому приложению?",
                        "Несохраненные изменения");

                    if (result)
                    {
                        // Сохраняем изменения асинхронно
                        _ = SaveChangesAsync().ContinueWith(t =>
                        {
                            if (t.IsCompletedSuccessfully)
                            {
                                // После успешного сохранения переходим к новому приложению
                                WpfApplication.Current.Dispatcher.Invoke(() =>
                                {
                                    SetSelectedApplicationInternal(value);
                                });
                            }
                        });
                        return;
                    }
                    else
                    {
                        // Спрашиваем, хочет ли пользователь отменить изменения
                        var cancelResult = DialogService.ShowConfirmation(
                            "Отменить все несохраненные изменения?",
                            "Подтверждение отмены");

                        if (!cancelResult)
                        {
                            // Пользователь не хочет терять изменения - остаемся на текущем элементе
                            OnPropertyChanged(nameof(SelectedApplication));
                            return;
                        }

                        // Отменяем изменения
                        CancelEditWithoutConfirmation();
                    }
                }

                SetSelectedApplicationInternal(value);
            }
        }

        private void SetSelectedApplicationInternal(ApplicationEditViewModel? value)
        {
            if (SetProperty(ref _selectedApplication, value))
            {
                if (value != null && !IsEditMode)
                {
                    // Просто показываем детали для просмотра
                    EditingApplication = value;
                }
                UpdateCommandStates();
            }
        }

        public ApplicationEditViewModel? EditingApplication
        {
            get => _editingApplication;
            set => SetProperty(ref _editingApplication, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplicationsView.Refresh();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(IsViewMode));
                    UpdateCommandStates();
                }
            }
        }

        public bool IsViewMode => !IsEditMode;

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                if (SetProperty(ref _hasUnsavedChanges, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        public int TotalApplications => Applications.Count;
        public int EnabledApplications => Applications.Count(a => a.IsEnabled);
        public int DisabledApplications => Applications.Count(a => !a.IsEnabled);

        // Свойства для управления локальными пользователями
        public User? SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public bool IsUserManagementMode
        {
            get => _isUserManagementMode;
            set 
            { 
                if (SetProperty(ref _isUserManagementMode, value))
                {
                    OnPropertyChanged(nameof(IsApplicationManagementMode));
                }
            }
        }

        public bool IsSystemManagementMode
        {
            get => _isSystemManagementMode;
            set 
            { 
                if (SetProperty(ref _isSystemManagementMode, value))
                {
                    OnPropertyChanged(nameof(IsApplicationManagementMode));
                }
            }
        }

        /// <summary>
        /// Показать панель управления приложениями (когда не выбраны другие панели)
        /// </summary>
        public bool IsApplicationManagementMode => !IsUserManagementMode && !IsSystemManagementMode;

        // Свойства для системной панели
        public ApplicationDataInfo? DatabaseInfo
        {
            get => _applicationDataInfo;
            set => SetProperty(ref _applicationDataInfo, value);
        }

        public string ApplicationVersion
        {
            get => _applicationVersion;
            set => SetProperty(ref _applicationVersion, value);
        }

        public string BuildDate
        {
            get => _buildDate;
            set => SetProperty(ref _buildDate, value);
        }

        public string DatabaseVersion
        {
            get => _databaseVersion;
            set => SetProperty(ref _databaseVersion, value);
        }

        public string TotalDataSize
        {
            get => _totalDataSize;
            set => SetProperty(ref _totalDataSize, value);
        }

        // SMTP статус свойства
        public string PrimarySmtpStatus
        {
            get => _primarySmtpStatus;
            set => SetProperty(ref _primarySmtpStatus, value);
        }

        public string BackupSmtpStatus
        {
            get => _backupSmtpStatus;
            set => SetProperty(ref _backupSmtpStatus, value);
        }

        public string UserSearchText
        {
            get => _userSearchText;
            set
            {
                if (SetProperty(ref _userSearchText, value))
                {
                    // TODO: Добавить фильтрацию пользователей
                }
            }
        }

        /// <summary>
        /// Проверяет, доступна ли Android функциональность (AndroidMode != Disabled)
        /// </summary>
        public bool IsAndroidEnabled
        {
            get
            {
                try
                {
                    var androidSubsystem = _serviceProvider.GetService<WindowsLauncher.Core.Interfaces.Android.IAndroidSubsystemService>();
                    return androidSubsystem?.CurrentMode != AndroidMode.Disabled;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to check Android subsystem availability");
                    return false; // По умолчанию скрываем Android функциональность при ошибках
                }
            }
        }

     

        #region Helper Methods

        /// <summary>
        /// Установить состояние загрузки с сообщением
        /// </summary>
        private void SetLoading(bool isLoading, string? message = null)
        {
            IsLoading = isLoading;
            if (!string.IsNullOrEmpty(message))
            {
                StatusMessage = message;
            }
        }

        /// <summary>
        /// Установить статусное сообщение с флагом ошибки
        /// </summary>
        private void SetStatusMessage(string message, bool isError = false)
        {
            StatusMessage = message;
            if (isError)
            {
                Logger.LogWarning("Status error: {Message}", message);
            }
        }

        /// <summary>
        /// Получить ID текущего пользователя
        /// </summary>
        private int GetCurrentUserId()
        {
            // TODO: Реализовать получение ID текущего пользователя из контекста/сессии
            // Временное значение для тестирования
            return 1;
        }

        #endregion

        #region Commands

        public AsyncRelayCommand RefreshCommand { get; private set; } = null!;
        public RelayCommand AddApplicationCommand { get; private set; } = null!;
        public RelayCommand<ApplicationEditViewModel> EditApplicationCommand { get; private set; } = null!;
        public AsyncRelayCommand<ApplicationEditViewModel> DeleteApplicationCommand { get; private set; } = null!;
        public AsyncRelayCommand SaveCommand { get; private set; } = null!;
        public RelayCommand CancelEditCommand { get; private set; } = null!;
        public AsyncRelayCommand<ApplicationEditViewModel> DuplicateApplicationCommand { get; private set; } = null!;
        public AsyncRelayCommand<ApplicationEditViewModel> ToggleEnabledCommand { get; private set; } = null!;
        public AsyncRelayCommand ImportCommand { get; private set; } = null!;
        public AsyncRelayCommand ExportCommand { get; private set; } = null!;
        public RelayCommand AddGroupCommand { get; private set; } = null!;
        public RelayCommand<string> RemoveGroupCommand { get; private set; } = null!;
        public AsyncRelayCommand TestApplicationCommand { get; private set; } = null!;

        // Команды для управления локальными пользователями
        public AsyncRelayCommand LoadLocalUsersCommand { get; private set; } = null!;
        public RelayCommand AddLocalUserCommand { get; private set; } = null!;
        public RelayCommand<User> EditLocalUserCommand { get; private set; } = null!;
        public AsyncRelayCommand<User> DeleteLocalUserCommand { get; private set; } = null!;
        public AsyncRelayCommand<User> ToggleUserActiveCommand { get; private set; } = null!;
        public AsyncRelayCommand<User> ToggleUserStatusCommand { get; private set; } = null!;
        public AsyncRelayCommand<User> ResetUserPasswordCommand { get; private set; } = null!;
        public RelayCommand ToggleUserManagementModeCommand { get; private set; } = null!;
        
        // Команды переключения между разделами
        public RelayCommand SwitchToApplicationsCommand { get; private set; } = null!;
        public RelayCommand SwitchToUsersCommand { get; private set; } = null!;
        public RelayCommand SwitchToSystemCommand { get; private set; } = null!;
        
        // Команды системной панели
        public AsyncRelayCommand RefreshDatabaseInfoCommand { get; private set; } = null!;
        public AsyncRelayCommand ClearDatabaseCommand { get; private set; } = null!;
        public AsyncRelayCommand ClearAllDataCommand { get; private set; } = null!;
        public RelayCommand ExportConfigurationCommand { get; private set; } = null!;
        public RelayCommand OpenDataFolderCommand { get; private set; } = null!;
        public AsyncRelayCommand RunDiagnosticsCommand { get; private set; } = null!;
        public RelayCommand OpenSmtpSettingsCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            RefreshCommand = new AsyncRelayCommand(LoadApplicationsAsync, () => !IsLoading, Logger);

            AddApplicationCommand = new RelayCommand(AddNewApplication, () => !IsEditMode);

            EditApplicationCommand = new RelayCommand<ApplicationEditViewModel>(
                StartEdit,
                app => app != null && !IsEditMode);

            DeleteApplicationCommand = new AsyncRelayCommand<ApplicationEditViewModel>(
                DeleteApplicationAsync,
                app => app != null && !IsEditMode,
                Logger);

            SaveCommand = new AsyncRelayCommand(SaveChangesAsync, () => IsEditMode && HasUnsavedChanges, Logger);

            CancelEditCommand = new RelayCommand(CancelEdit, () => IsEditMode);

            DuplicateApplicationCommand = new AsyncRelayCommand<ApplicationEditViewModel>(
                DuplicateApplicationAsync,
                app => app != null && !IsEditMode,
                Logger);

            ToggleEnabledCommand = new AsyncRelayCommand<ApplicationEditViewModel>(
                ToggleApplicationEnabledAsync,
                app => app != null,
                Logger);

            ImportCommand = new AsyncRelayCommand(ImportApplicationsAsync, () => !IsEditMode, Logger);

            ExportCommand = new AsyncRelayCommand(ExportApplicationsAsync, () => Applications.Any(), Logger);

            AddGroupCommand = new RelayCommand(AddRequiredGroup, () => IsEditMode);

            RemoveGroupCommand = new RelayCommand<string>(
                RemoveRequiredGroup,
                group => !string.IsNullOrEmpty(group) && IsEditMode);

            TestApplicationCommand = new AsyncRelayCommand(
                TestApplicationAsync,
                () => EditingApplication != null && IsEditMode,
                Logger);

            // Инициализация команд для управления пользователями
            LoadLocalUsersCommand = new AsyncRelayCommand(LoadLocalUsersAsync, () => !IsLoading, Logger);
            AddLocalUserCommand = new RelayCommand(ShowAddUserDialog, () => !IsLoading);
            EditLocalUserCommand = new RelayCommand<User>(ShowEditUserDialog, user => user != null && !IsLoading);
            DeleteLocalUserCommand = new AsyncRelayCommand<User>(DeleteUserAsync, user => user != null && !IsLoading, Logger);
            ToggleUserActiveCommand = new AsyncRelayCommand<User>(ToggleUserActiveAsync, user => user != null && !IsLoading, Logger);
            ToggleUserStatusCommand = new AsyncRelayCommand<User>(ToggleUserActiveAsync, user => user != null && !IsLoading, Logger);
            ResetUserPasswordCommand = new AsyncRelayCommand<User>(ResetUserPasswordAsync, user => user != null && !IsLoading, Logger);
            ToggleUserManagementModeCommand = new RelayCommand(() => IsUserManagementMode = !IsUserManagementMode);
            
            // Инициализация команд переключения между разделами
            SwitchToApplicationsCommand = new RelayCommand(() => 
            {
                IsUserManagementMode = false;
                IsSystemManagementMode = false;
            });
            SwitchToUsersCommand = new RelayCommand(() => 
            {
                IsUserManagementMode = true;
                IsSystemManagementMode = false;
                _ = LoadLocalUsersAsync(); // Загружаем пользователей при переключении
            });
            SwitchToSystemCommand = new RelayCommand(() => 
            {
                Logger.LogInformation("Switching to System panel");
                IsUserManagementMode = false;
                IsSystemManagementMode = true;
                Logger.LogInformation("System panel state: IsSystemManagementMode={IsSystemManagementMode}, IsUserManagementMode={IsUserManagementMode}, IsApplicationManagementMode={IsApplicationManagementMode}", 
                    IsSystemManagementMode, IsUserManagementMode, IsApplicationManagementMode);
                _ = LoadSystemInfoAsync(); // Загружаем системную информацию при переключении
            });
            
            // Инициализация команд системной панели
            RefreshDatabaseInfoCommand = new AsyncRelayCommand(LoadSystemInfoAsync, () => !IsLoading, Logger);
            ClearDatabaseCommand = new AsyncRelayCommand(ClearDatabaseOnlyAsync, () => !IsLoading, Logger);
            ClearAllDataCommand = new AsyncRelayCommand(ClearAllApplicationDataAsync, () => !IsLoading, Logger);
            ExportConfigurationCommand = new RelayCommand(ExportConfiguration);
            OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
            RunDiagnosticsCommand = new AsyncRelayCommand(RunSystemDiagnosticsAsync, () => !IsLoading, Logger);
            OpenSmtpSettingsCommand = new RelayCommand(OpenSmtpSettings);
        }

        private void UpdateCommandStates()
        {
            AddApplicationCommand.RaiseCanExecuteChanged();
            EditApplicationCommand.RaiseCanExecuteChanged();
            DeleteApplicationCommand.RaiseCanExecuteChanged();
            SaveCommand.RaiseCanExecuteChanged();
            CancelEditCommand.RaiseCanExecuteChanged();
            DuplicateApplicationCommand.RaiseCanExecuteChanged();
            ImportCommand.RaiseCanExecuteChanged();
            TestApplicationCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region Initialization

        public override async Task InitializeAsync()
        {
            await ExecuteSafelyAsync(async () =>
            {
                StatusMessage = "Загрузка приложений...";

                await LoadApplicationsAsync();
                await LoadAvailableGroupsAsync();

                StatusMessage = $"Загружено {TotalApplications} приложений";
            }, "initialization");
        }

        private async Task LoadApplicationsAsync()
        {
            var applications = await _applicationService.GetAllApplicationsAsync();

            Applications.Clear();
            foreach (var app in applications.OrderBy(a => a.Category).ThenBy(a => a.Name))
            {
                var editVm = new ApplicationEditViewModel(app);
                editVm.PropertyChanged += OnApplicationPropertyChanged;
                Applications.Add(editVm);
            }

            // Обновляем список категорий через CategoryManagementService
            await LoadAvailableCategoriesAsync();

            OnPropertyChanged(nameof(TotalApplications));
            OnPropertyChanged(nameof(EnabledApplications));
            OnPropertyChanged(nameof(DisabledApplications));
        }

        private async Task LoadAvailableGroupsAsync()
        {
            // TODO: Загрузить группы из AD
            // Пока используем тестовые данные
            var testGroups = new[]
            {
                "LauncherUsers",
                "LauncherPowerUsers",
                "LauncherAdmins",
                "Domain Users",
                "Domain Admins",
                "IT Department",                
                "HR Department"
            };

            AvailableGroups.Clear();
            foreach (var group in testGroups.OrderBy(g => g))
            {
                AvailableGroups.Add(group);
            }
        }

        private async Task LoadAvailableCategoriesAsync()
        {
            try
            {
                AvailableCategories.Clear();
                
                using var scope = _serviceProvider.CreateScope();
                var categoryService = scope.ServiceProvider.GetService<ICategoryManagementService>();
                
                if (categoryService != null)
                {
                    // Получаем все категории (для админа показываем все)
                    var categories = await categoryService.GetAllCategoriesAsync();
                    
                    foreach (var category in categories.OrderBy(c => c.SortOrder).ThenBy(c => c.DefaultName))
                    {
                        AvailableCategories.Add(category.Key);
                    }
                    
                    Logger.LogInformation("Loaded {Count} categories for AdminWindow", AvailableCategories.Count);
                }
                else
                {
                    // Fallback - берем категории из существующих приложений
                    Logger.LogWarning("CategoryManagementService not available, using fallback category loading");
                    
                    var categoriesFromApps = Applications.Select(a => a.Category)
                        .Where(c => !string.IsNullOrEmpty(c))
                        .Distinct()
                        .OrderBy(c => c);

                    foreach (var category in categoriesFromApps)
                    {
                        AvailableCategories.Add(category);
                    }
                    
                    // Добавляем предустановленные категории из конфигурации
                    try
                    {
                        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                        var predefinedCategories = configuration.GetSection("Categories:PredefinedCategories").Get<List<CategoryDefinition>>();
                        
                        if (predefinedCategories != null)
                        {
                            foreach (var predefinedCategory in predefinedCategories)
                            {
                                if (!AvailableCategories.Contains(predefinedCategory.Key))
                                {
                                    AvailableCategories.Add(predefinedCategory.Key);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Could not load predefined categories from configuration");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading available categories");
                
                // Minimal fallback
                if (AvailableCategories.Count == 0)
                {
                    AvailableCategories.Add("General");
                }
            }
        }

        #endregion

        #region Edit Operations

        private void AddNewApplication()
        {
            // Проверяем несохраненные изменения
            if (HasUnsavedChanges)
            {
                var result = DialogService.ShowConfirmation(
                    "У вас есть несохраненные изменения. Сохранить их перед созданием нового приложения?",
                    "Несохраненные изменения");

                if (result)
                {
                    // Сохраняем изменения асинхронно
                    _ = SaveChangesAsync().ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            // После успешного сохранения создаем новое приложение
                            WpfApplication.Current.Dispatcher.Invoke(() =>
                            {
                                CreateNewApplication();
                            });
                        }
                    });
                    return;
                }
                else
                {
                    // Спрашиваем, хочет ли пользователь отменить изменения
                    var cancelResult = DialogService.ShowConfirmation(
                        "Отменить все несохраненные изменения?",
                        "Подтверждение отмены");

                    if (!cancelResult)
                    {
                        // Пользователь не хочет терять изменения
                        return;
                    }

                    // Отменяем изменения
                    CancelEditWithoutConfirmation();
                }
            }

            CreateNewApplication();
        }

        private void CreateNewApplication()
        {
            var newApp = new CoreApplication
            {
                Id = 0,
                Name = "Новое приложение",
                Description = "",
                Category = AvailableCategories.FirstOrDefault() ?? "General",
                Type = ApplicationType.Desktop,
                ExecutablePath = "",
                Arguments = "",
                IconPath = "",
                MinimumRole = UserRole.Standard,
                RequiredGroups = new List<string>(),
                IsEnabled = true,
                SortOrder = Applications.Count + 1,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                CreatedBy = Environment.UserName
            };

            var editVm = new ApplicationEditViewModel(newApp) { IsNew = true };
            EditingApplication = editVm;
            EditingApplication.PropertyChanged += OnEditingApplicationPropertyChanged;
            SelectedApplication = null; // Снимаем выделение в списке
            IsEditMode = true;
            HasUnsavedChanges = true;

            StatusMessage = "Создание нового приложения";
        }

        private void StartEdit(ApplicationEditViewModel? app)
        {
            if (app == null || !CanStartEdit()) return;

            // Создаем копию для редактирования
            EditingApplication = app.CreateCopy();
            EditingApplication.PropertyChanged += OnEditingApplicationPropertyChanged;
            SelectedApplication = app;
            IsEditMode = true;
            HasUnsavedChanges = false;

            StatusMessage = $"Редактирование: {app.Name}";
        }

        private bool CanStartEdit()
        {
            if (HasUnsavedChanges)
            {
                var result = DialogService.ShowConfirmation(
                    "У вас есть несохраненные изменения. Продолжить без сохранения?",
                    "Несохраненные изменения");

                if (!result) return false;
            }

            return true;
        }

        private void CancelEdit()
        {
            if (HasUnsavedChanges)
            {
                var result = DialogService.ShowConfirmation(
                    "Отменить все изменения?",
                    "Подтверждение отмены");

                if (!result) return;
            }

            CancelEditWithoutConfirmation();
        }

        private void CancelEditWithoutConfirmation()
        {
            if (EditingApplication != null)
            {
                EditingApplication.PropertyChanged -= OnEditingApplicationPropertyChanged;
            }

            // Возвращаемся к просмотру выбранного элемента
            if (SelectedApplication != null)
            {
                EditingApplication = SelectedApplication;
            }

            IsEditMode = false;
            HasUnsavedChanges = false;

            StatusMessage = "Редактирование отменено";
        }

        #endregion

        #region Save Operations

        private async Task SaveChangesAsync()
        {
            if (EditingApplication == null) return;

            await ExecuteSafelyAsync(async () =>
            {
                // Валидация
                var validationErrors = ValidateApplication(EditingApplication);
                if (validationErrors.Any())
                {
                    DialogService.ShowWarning(
                        "Исправьте следующие ошибки:\n" + string.Join("\n", validationErrors),
                        "Ошибки валидации");
                    return;
                }

                StatusMessage = "Сохранение изменений...";

                var app = EditingApplication.ToApplication();
                app.ModifiedDate = DateTime.Now;

                bool success;
                if (EditingApplication.IsNew)
                {
                    // Добавление нового приложения
                    var currentUser = await GetCurrentUserAsync();
                    success = await _applicationService.AddApplicationAsync(app, currentUser);

                    if (success)
                    {
                        EditingApplication.Id = app.Id;
                        EditingApplication.IsNew = false;
                        Applications.Add(EditingApplication);
                    }
                }
                else
                {
                    // Обновление существующего
                    var currentUser = await GetCurrentUserAsync();
                    success = await _applicationService.UpdateApplicationAsync(app, currentUser);

                    if (success)
                    {
                        // Обновляем в коллекции
                        var original = Applications.FirstOrDefault(a => a.Id == app.Id);
                        if (original != null)
                        {
                            original.UpdateFrom(EditingApplication);
                        }
                    }
                }

                if (success)
                {
                    if (EditingApplication != null)
                    {
                        EditingApplication.PropertyChanged -= OnEditingApplicationPropertyChanged;
                    }

                    EditingApplication = SelectedApplication;
                    IsEditMode = false;
                    HasUnsavedChanges = false;

                    StatusMessage = "Изменения сохранены успешно";
                    await LoadApplicationsAsync(); // Перезагружаем для актуализации
                }
                else
                {
                    StatusMessage = "Ошибка при сохранении изменений";
                }
            }, "save changes");
        }

        private List<string> ValidateApplication(ApplicationEditViewModel app)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(app.Name))
                errors.Add("• Название приложения обязательно");

            if (string.IsNullOrWhiteSpace(app.ExecutablePath))
                errors.Add("• Путь к приложению обязателен");

            if (string.IsNullOrWhiteSpace(app.Category))
                errors.Add("• Категория обязательна");

            // Проверка уникальности имени
            var duplicate = Applications.FirstOrDefault(a =>
                a.Id != app.Id &&
                a.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
                errors.Add($"• Приложение с именем '{app.Name}' уже существует");

            return errors;
        }

        #endregion

        #region Delete Operations

        private async Task DeleteApplicationAsync(ApplicationEditViewModel? app)
        {
            if (app == null) return;

            // Проверяем несохраненные изменения
            if (HasUnsavedChanges)
            {
                DialogService.ShowWarning(
                    "Сохраните или отмените текущие изменения перед удалением другого приложения.",
                    "Несохраненные изменения");
                return;
            }

            var result = DialogService.ShowConfirmation(
                $"Вы действительно хотите удалить приложение '{app.Name}'?\n\n" +
                "Это действие нельзя отменить.",
                "Подтверждение удаления");

            if (!result) return;

            await ExecuteSafelyAsync(async () =>
            {
                StatusMessage = $"Удаление {app.Name}...";

                var currentUser = await GetCurrentUserAsync();
                var success = await _applicationService.DeleteApplicationAsync(app.Id, currentUser);

                if (success)
                {
                    Applications.Remove(app);

                    // Если удаляем выбранное приложение, очищаем выбор
                    if (SelectedApplication == app)
                    {
                        SelectedApplication = null;
                        EditingApplication = null;
                    }

                    StatusMessage = $"Приложение '{app.Name}' удалено";

                    OnPropertyChanged(nameof(TotalApplications));
                    OnPropertyChanged(nameof(EnabledApplications));
                    OnPropertyChanged(nameof(DisabledApplications));
                }
                else
                {
                    StatusMessage = "Ошибка при удалении приложения";
                }
            }, "delete application");
        }

        #endregion

        #region Other Operations

        private async Task DuplicateApplicationAsync(ApplicationEditViewModel? app)
        {
            if (app == null) return;

            // Проверяем несохраненные изменения
            if (HasUnsavedChanges)
            {
                DialogService.ShowWarning(
                    "Сохраните или отмените текущие изменения перед дублированием приложения.",
                    "Несохраненные изменения");
                return;
            }

            await ExecuteSafelyAsync(async () =>
            {
                var duplicate = app.CreateCopy();
                duplicate.Id = 0;
                duplicate.Name = $"{app.Name} - Копия";
                duplicate.IsNew = true;
                duplicate.CreatedDate = DateTime.Now;
                duplicate.ModifiedDate = DateTime.Now;

                EditingApplication = duplicate;
                EditingApplication.PropertyChanged += OnEditingApplicationPropertyChanged;
                SelectedApplication = null; // Снимаем выделение в списке
                IsEditMode = true;
                HasUnsavedChanges = true;

                StatusMessage = $"Создана копия приложения '{app.Name}'";
            }, "duplicate application");
        }

        private async Task ToggleApplicationEnabledAsync(ApplicationEditViewModel? app)
        {
            if (app == null) return;

            await ExecuteSafelyAsync(async () =>
            {
                app.IsEnabled = !app.IsEnabled;

                var currentUser = await GetCurrentUserAsync();
                var success = await _applicationService.UpdateApplicationAsync(app.ToApplication(), currentUser);

                if (success)
                {
                    StatusMessage = app.IsEnabled
                        ? $"Приложение '{app.Name}' включено"
                        : $"Приложение '{app.Name}' отключено";

                    OnPropertyChanged(nameof(EnabledApplications));
                    OnPropertyChanged(nameof(DisabledApplications));
                }
                else
                {
                    // Откатываем изменение
                    app.IsEnabled = !app.IsEnabled;
                    StatusMessage = "Ошибка при изменении статуса приложения";
                }
            }, "toggle application");
        }

        private async Task TestApplicationAsync()
        {
            if (EditingApplication == null) return;

            await ExecuteSafelyAsync(async () =>
            {
                StatusMessage = $"Тестирование запуска {EditingApplication.Name}...";

                var app = EditingApplication.ToApplication();
                var currentUser = await GetCurrentUserAsync();

                var result = await _applicationService.LaunchApplicationAsync(app, currentUser);

                if (result.IsSuccess)
                {
                    StatusMessage = $"Приложение запущено успешно (PID: {result.ProcessId})";
                    DialogService.ShowInfo(
                        $"Приложение '{app.Name}' запущено успешно.\nPID процесса: {result.ProcessId}",
                        "Тест успешен");
                }
                else
                {
                    StatusMessage = $"Ошибка запуска: {result.ErrorMessage}";
                    DialogService.ShowError(
                        $"Не удалось запустить приложение:\n{result.ErrorMessage}",
                        "Ошибка запуска");
                }
            }, "test application");
        }

        #endregion

        #region Import/Export

        private async Task ImportApplicationsAsync()
        {
            await ExecuteSafelyAsync(async () =>
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Импорт данных лаунчера",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() != true)
                    return;

                try
                {
                    StatusMessage = "Импорт данных...";
                    
                    var json = await File.ReadAllTextAsync(openFileDialog.FileName);
                    var importData = JsonSerializer.Deserialize<LauncherExportData>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    });

                    if (importData == null)
                    {
                        DialogService.ShowError("Невозможно прочитать файл импорта", "Ошибка импорта");
                        return;
                    }

                    // Показываем диалог с опциями импорта
                    var importOptionsResult = ShowImportOptionsDialog(importData);
                    if (importOptionsResult == null) return;

                    var clearDatabase = importOptionsResult.Value;

                    var currentUser = await GetCurrentUserAsync();
                    int importedApps = 0, importedUsers = 0, deletedApps = 0, deletedUsers = 0;

                    // Очистка базы если выбрана опция
                    if (clearDatabase)
                    {
                        StatusMessage = "Очистка существующих данных...";
                        
                        // Удаляем все существующие приложения
                        var existingApps = await _applicationService.GetAllApplicationsAsync();
                        foreach (var app in existingApps)
                        {
                            await _applicationService.DeleteApplicationAsync(app.Id, currentUser);
                            deletedApps++;
                        }

                        // Удаляем всех локальных пользователей
                        var existingUsers = await _localUserService.GetLocalUsersAsync();
                        foreach (var user in existingUsers)
                        {
                            await _localUserService.DeleteLocalUserAsync(user.Id);
                            deletedUsers++;
                        }
                        
                        Logger.LogInformation("Database cleared: {DeletedApps} applications, {DeletedUsers} users", deletedApps, deletedUsers);
                    }

                    StatusMessage = "Импорт приложений...";

                    // Импорт приложений
                    foreach (var appData in importData.Applications)
                    {
                        var app = new CoreApplication
                        {
                            Name = appData.Name,
                            Description = appData.Description,
                            ExecutablePath = appData.ExecutablePath,
                            Arguments = appData.Arguments,
                            IconPath = appData.IconPath,
                            Category = appData.Category,
                            Type = appData.Type,
                            RequiredGroups = appData.RequiredGroups,
                            MinimumRole = appData.MinimumRole,
                            IsEnabled = appData.IsEnabled,
                            SortOrder = appData.SortOrder,
                            CreatedDate = DateTime.Now,
                            CreatedBy = currentUser.Username
                        };

                        await _applicationService.AddApplicationAsync(app, currentUser);
                        importedApps++;
                    }

                    StatusMessage = "Импорт пользователей...";

                    // Импорт локальных пользователей
                    foreach (var userData in importData.LocalUsers)
                    {
                        // Генерируем безопасный временный пароль
                        var tempPassword = GenerateSecurePassword();
                        
                        // Используем CreateLocalUserAsync с паролем и затем обновляем хеш
                        var user = await _localUserService.CreateLocalUserAsync(
                            userData.Username, 
                            tempPassword, // Безопасный временный пароль
                            userData.DisplayName,
                            userData.Email,
                            userData.Role);
                            
                        // Обновляем с правильным хешем пароля
                        user.PasswordHash = userData.PasswordHash;
                        user.Salt = userData.PasswordSalt;
                        user.CreatedAt = userData.CreatedAt;
                        user.LastLoginAt = userData.LastLoginAt;
                        user.IsActive = userData.IsActive;

                        await _localUserService.UpdateLocalUserAsync(user);
                        importedUsers++;
                    }

                    // Обновляем UI
                    await LoadApplicationsAsync();
                    await LoadLocalUsersAsync();

                    var resultMessage = clearDatabase 
                        ? $"Импорт завершен успешно!\n\n" +
                          $"Очистка базы:\n" +
                          $"• Удалено приложений: {deletedApps}\n" +
                          $"• Удалено пользователей: {deletedUsers}\n\n" +
                          $"Импорт данных:\n" +
                          $"• Импортировано приложений: {importedApps}\n" +
                          $"• Импортировано пользователей: {importedUsers}"
                        : $"Импорт завершен успешно!\n" +
                          $"• Импортировано приложений: {importedApps}\n" +
                          $"• Импортировано пользователей: {importedUsers}";

                    DialogService.ShowInfo(resultMessage, "Импорт завершен");

                    Logger.LogInformation("Import completed: {Apps} applications, {Users} users. Cleared: {DeletedApps} applications, {DeletedUsers} users", 
                        importedApps, importedUsers, deletedApps, deletedUsers);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error during import");
                    DialogService.ShowError($"Ошибка при импорте: {ex.Message}", "Ошибка импорта");
                }
                finally
                {
                    StatusMessage = string.Empty;
                }
            }, "import applications");
        }

        private async Task ExportApplicationsAsync()
        {
            await ExecuteSafelyAsync(async () =>
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Экспорт данных лаунчера",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    FilterIndex = 1,
                    DefaultExt = "json",
                    FileName = $"WindowsLauncher_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                try
                {
                    StatusMessage = "Экспорт данных...";
                    
                    var currentUser = await GetCurrentUserAsync();
                    
                    // Получаем все приложения из базы
                    var allApplications = await _applicationService.GetAllApplicationsAsync();
                    var allLocalUsers = await _localUserService.GetLocalUsersAsync();

                    var exportData = new LauncherExportData
                    {
                        Version = "1.0",
                        ExportedAt = DateTime.Now,
                        ExportedBy = currentUser.Username,
                        Applications = allApplications.Select(app => new ExportApplication
                        {
                            Name = app.Name,
                            Description = app.Description,
                            ExecutablePath = app.ExecutablePath,
                            Arguments = app.Arguments,
                            IconPath = app.IconPath,
                            Category = app.Category,
                            Type = app.Type,
                            RequiredGroups = app.RequiredGroups,
                            MinimumRole = app.MinimumRole,
                            IsEnabled = app.IsEnabled,
                            SortOrder = app.SortOrder
                        }).ToList(),
                        LocalUsers = allLocalUsers.Select(user => new ExportUser
                        {
                            Username = user.Username,
                            DisplayName = user.DisplayName,
                            Email = user.Email,
                            Role = user.Role,
                            IsActive = user.IsActive,
                            PasswordHash = user.PasswordHash,
                            PasswordSalt = user.Salt,
                            CreatedAt = user.CreatedAt,
                            LastLoginAt = user.LastLoginAt
                        }).ToList()
                    };

                    var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });

                    await File.WriteAllTextAsync(saveFileDialog.FileName, json);

                    DialogService.ShowInfo(
                        $"Экспорт завершен успешно!\n" +
                        $"• Экспортировано приложений: {exportData.Applications.Count}\n" +
                        $"• Экспортировано пользователей: {exportData.LocalUsers.Count}\n" +
                        $"• Файл: {saveFileDialog.FileName}",
                        "Экспорт завершен");

                    Logger.LogInformation("Export completed: {Apps} applications, {Users} users to {File}", 
                        exportData.Applications.Count, exportData.LocalUsers.Count, saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error during export");
                    DialogService.ShowError($"Ошибка при экспорте: {ex.Message}", "Ошибка экспорта");
                }
                finally
                {
                    StatusMessage = string.Empty;
                }
            }, "export applications");
        }

        /// <summary>
        /// Показать диалог опций импорта
        /// </summary>
        private bool? ShowImportOptionsDialog(LauncherExportData importData)
        {
            var message = $"Импорт данных лаунчера:\n" +
                         $"• Приложений: {importData.Applications.Count}\n" +
                         $"• Локальных пользователей: {importData.LocalUsers.Count}\n" +
                         $"• Версия: {importData.Version}\n" +
                         $"• Экспортировано: {importData.ExportedAt:dd.MM.yyyy HH:mm}\n\n" +
                         $"Выберите режим импорта:\n\n" +
                         $"Да - Полная замена (удалить все существующие данные и импортировать новые)\n" +
                         $"Нет - Добавление (импортировать данные к существующим)\n" +
                         $"Отмена - Отменить импорт";

            var result = System.Windows.MessageBox.Show(
                message,
                "Настройки импорта",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.Cancel);

            return result switch
            {
                System.Windows.MessageBoxResult.Yes => true,  // Очистить базу перед импортом
                System.Windows.MessageBoxResult.No => false, // Добавить к существующим данным
                _ => null // Отмена
            };
        }

        /// <summary>
        /// Генерировать безопасный пароль для импорта
        /// </summary>
        private string GenerateSecurePassword()
        {
            const string upperCase = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // Исключаем I, O для избежания путаницы
            const string lowerCase = "abcdefghjkmnpqrstuvwxyz"; // Исключаем i, l, o для избежания путаницы
            const string digits = "23456789"; // Исключаем 0, 1 для избежания путаницы и последовательностей
            const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var random = new Random();
            var password = new char[16]; // Длина 16 символов

            // Обеспечиваем наличие всех типов символов
            password[0] = upperCase[random.Next(upperCase.Length)];
            password[1] = lowerCase[random.Next(lowerCase.Length)];
            password[2] = digits[random.Next(digits.Length)];
            password[3] = specialChars[random.Next(specialChars.Length)];

            // Заполняем остальные позиции случайными символами
            var allChars = upperCase + lowerCase + digits + specialChars;
            for (int i = 4; i < password.Length; i++)
            {
                char nextChar;
                do
                {
                    nextChar = allChars[random.Next(allChars.Length)];
                    
                    // Избегаем повторяющихся символов подряд
                } while (i > 0 && nextChar == password[i - 1]);
                
                password[i] = nextChar;
            }

            // Перемешиваем массив для случайного порядка
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            var result = new string(password);
            
            // Проверяем, что пароль не содержит запрещенных последовательностей
            if (ContainsProhibitedSequences(result))
            {
                // Если содержит, генерируем новый (рекурсивно, но с низкой вероятностью)
                return GenerateSecurePassword();
            }

            return result;
        }

        /// <summary>
        /// Проверить пароль на запрещенные последовательности
        /// </summary>
        private bool ContainsProhibitedSequences(string password)
        {
            var lower = password.ToLowerInvariant();
            
            // Проверяем на простые последовательности
            var prohibitedSequences = new[]
            {
                "123", "234", "345", "456", "567", "678", "789",
                "abc", "bcd", "cde", "def", "efg", "fgh", "ghi", "hij", "ijk", "jkl", "klm", "lmn", "mno", "nop", "opq", "pqr", "qrs", "rst", "stu", "tuv", "uvw", "vwx", "wxy", "xyz",
                "qwerty", "qwert", "werty", "asdf", "sdfg", "zxcv", "xcvb"
            };

            return prohibitedSequences.Any(seq => lower.Contains(seq));
        }

        #endregion

        #region Group Management

        private void AddRequiredGroup()
        {
            if (EditingApplication == null) return;

            var input = DialogService.ShowInputDialog(
                "Введите название группы AD:",
                "Добавить группу",
                "");

            if (!string.IsNullOrWhiteSpace(input) && !EditingApplication.RequiredGroups.Contains(input))
            {
                EditingApplication.RequiredGroups.Add(input);
                EditingApplication.OnPropertyChanged(nameof(EditingApplication.RequiredGroups));
                HasUnsavedChanges = true;
            }
        }

        private void RemoveRequiredGroup(string? group)
        {
            if (EditingApplication == null || string.IsNullOrEmpty(group)) return;

            EditingApplication.RequiredGroups.Remove(group);
            EditingApplication.OnPropertyChanged(nameof(EditingApplication.RequiredGroups));
            HasUnsavedChanges = true;
        }

        #endregion

        #region Helper Methods

        private bool FilterApplications(object obj)
        {
            if (obj is not ApplicationEditViewModel app) return false;

            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var search = SearchText.ToLower();
            return app.Name.ToLower().Contains(search) ||
                   app.Description.ToLower().Contains(search) ||
                   app.Category.ToLower().Contains(search) ||
                   app.ExecutablePath.ToLower().Contains(search);
        }

        private void OnApplicationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Не нужно в режиме просмотра
        }

        private void OnEditingApplicationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsEditMode && !HasUnsavedChanges)
            {
                HasUnsavedChanges = true;
            }
        }

        private async Task<User> GetCurrentUserAsync()
        {
            // TODO: Получить текущего пользователя из AuthenticationService
            // Пока используем заглушку
            return new User
            {
                Id = 1,
                Username = Environment.UserName,
                DisplayName = Environment.UserName,
                Role = UserRole.Administrator
            };
        }

        #endregion

        #region User Management

        private async Task LoadLocalUsersAsync()
        {
            await ExecuteSafelyAsync(async () =>
            {
                var users = await _localUserService.GetLocalUsersAsync();
                
                LocalUsers.Clear();
                foreach (var user in users.OrderBy(u => u.Username))
                {
                    LocalUsers.Add(user);
                }

                StatusMessage = $"Загружено {users.Count} локальных пользователей";
                Logger.LogInformation("Loaded {Count} local users", users.Count);
            }, "load local users");
        }

        private void ShowAddUserDialog()
        {
            try
            {
                var auditService = _serviceProvider.GetService(typeof(IAuditService)) as IAuditService;
                if (auditService == null)
                {
                    DialogService.ShowError("Не удалось получить сервис аудита", "Ошибка");
                    return;
                }

                var dialogViewModel = new LocalUserDialogViewModel(_localUserService, auditService, GetCurrentUserId());
                var dialog = new LocalUserDialog(dialogViewModel);
                
                var result = dialog.ShowDialog();
                if (result == true)
                {
                    // Обновляем список пользователей
                    _ = LoadLocalUsersAsync();
                    SetStatusMessage("Пользователь успешно создан", false);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error showing add user dialog");
                DialogService.ShowError($"Ошибка при создании пользователя: {ex.Message}", "Ошибка");
            }
        }

        private void ShowEditUserDialog(User user)
        {
            if (user == null) return;

            try
            {
                var auditService = _serviceProvider.GetService(typeof(IAuditService)) as IAuditService;
                if (auditService == null)
                {
                    DialogService.ShowError("Не удалось получить сервис аудита", "Ошибка");
                    return;
                }

                var dialogViewModel = new LocalUserDialogViewModel(_localUserService, auditService, user, GetCurrentUserId());
                var dialog = new LocalUserDialog(dialogViewModel);
                
                var result = dialog.ShowDialog();
                if (result == true)
                {
                    // Обновляем список пользователей
                    _ = LoadLocalUsersAsync();
                    SetStatusMessage($"Пользователь '{user.Username}' успешно обновлен", false);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error showing edit user dialog for user {Username}", user.Username);
                DialogService.ShowError($"Ошибка при редактировании пользователя: {ex.Message}", "Ошибка");
            }
        }

        private async Task DeleteUserAsync(User user)
        {
            if (user == null) return;

            // Проверяем, можно ли удалить пользователя
            if (user.Username == "serviceadmin")
            {
                DialogService.ShowWarning("Нельзя удалить системного администратора", "Предупреждение");
                return;
            }

            var result = DialogService.ShowConfirmation(
                $"Вы уверены, что хотите удалить пользователя '{user.Username}'?\n\nЭто действие нельзя будет отменить.",
                "Подтверждение удаления");

            if (result != true) return;

            try
            {
                SetLoading(true, $"Удаление пользователя {user.Username}...");

                var deleteResult = await _localUserService.DeleteLocalUserAsync(user.Id);
                if (deleteResult)
                {
                    LocalUsers.Remove(user);
                    SetStatusMessage($"Пользователь '{user.Username}' успешно удален", false);
                    Logger.LogInformation("Deleted local user: {Username}", user.Username);
                }
                else
                {
                    SetStatusMessage($"Не удалось удалить пользователя '{user.Username}'", true);
                    DialogService.ShowError("Ошибка удаления пользователя", "Ошибка");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to delete user {Username}", user.Username);
                SetStatusMessage($"Ошибка удаления пользователя: {ex.Message}", true);
                DialogService.ShowError($"Ошибка удаления пользователя: {ex.Message}", "Ошибка");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task ToggleUserActiveAsync(User user)
        {
            try
            {
                if (user == null) return;

                SetLoading(true, $"Изменение статуса пользователя {user.Username}...");

                // Проверяем, можно ли изменить статус пользователя
                if (user.Username == "serviceadmin")
                {
                    DialogService.ShowWarning("Нельзя изменить статус системного администратора", "Предупреждение");
                    return;
                }

                bool success;
                if (user.IsActive)
                {
                    success = await _localUserService.DeactivateLocalUserAsync(user.Id, GetCurrentUserId());
                }
                else
                {
                    success = await _localUserService.ActivateLocalUserAsync(user.Id, GetCurrentUserId());
                }

                if (success)
                {
                    user.IsActive = !user.IsActive;
                    SetStatusMessage($"Статус пользователя '{user.Username}' изменен", false);
                    Logger.LogInformation("Toggled user active status: {Username} -> {IsActive}", user.Username, user.IsActive);
                }
                else
                {
                    SetStatusMessage($"Не удалось изменить статус пользователя '{user.Username}'", true);
                    DialogService.ShowError("Ошибка изменения статуса", "Ошибка");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to toggle user active status for {Username}", user?.Username);
                SetStatusMessage($"Ошибка изменения статуса: {ex.Message}", true);
                DialogService.ShowError($"Ошибка изменения статуса: {ex.Message}", "Ошибка");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task ResetUserPasswordAsync(User user)
        {
            try
            {
                if (user == null) return;

                // Проверяем, можно ли сбросить пароль пользователя
                if (user.Username == "serviceadmin")
                {
                    DialogService.ShowWarning("Для сброса пароля системного администратора используйте функцию редактирования пользователя", "Предупреждение");
                    return;
                }

                var result = DialogService.ShowConfirmation(
                    $"Вы уверены, что хотите сбросить пароль пользователя '{user.Username}'?\n\nБудет сгенерирован новый временный пароль.",
                    "Подтверждение сброса пароля");

                if (result != true) return;

                SetLoading(true, $"Сброс пароля для {user.Username}...");

                // Генерируем временный пароль
                var tempPassword = GenerateTemporaryPassword();
                var resetResult = await _localUserService.ResetLocalUserPasswordAsync(user.Id, tempPassword, GetCurrentUserId());
                
                if (resetResult)
                {
                    SetStatusMessage($"Пароль пользователя '{user.Username}' успешно сброшен", false);
                    Logger.LogInformation("Reset password for user: {Username}", user.Username);
                    
                    // Показываем новый пароль администратору с возможностью копирования
                    var passwordDialog = new WindowsLauncher.UI.Views.PasswordDisplayDialog(tempPassword);
                    passwordDialog.Owner = System.Windows.Application.Current.MainWindow;
                    passwordDialog.ShowDialog();
                }
                else
                {
                    SetStatusMessage($"Не удалось сбросить пароль пользователя '{user.Username}'", true);
                    DialogService.ShowError("Ошибка сброса пароля", "Ошибка");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to reset password for user {Username}", user?.Username);
                SetStatusMessage($"Ошибка сброса пароля: {ex.Message}", true);
                DialogService.ShowError($"Ошибка сброса пароля: {ex.Message}", "Ошибка");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private string GenerateTemporaryPassword()
        {
            var random = new Random();
            
            // Определяем наборы символов для обеспечения требований безопасности
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "!@#$%^&*()_+-=[]{}|;:,.<>?";
            
            // Начинаем с пустого пароля
            var password = new List<char>();
            
            // Гарантируем наличие хотя бы одного символа каждого типа
            password.Add(uppercase[random.Next(uppercase.Length)]);
            password.Add(lowercase[random.Next(lowercase.Length)]);
            password.Add(digits[random.Next(digits.Length)]);
            password.Add(special[random.Next(special.Length)]);
            
            // Создаем объединенный набор символов
            const string allChars = uppercase + lowercase + digits + special;
            
            // Добавляем оставшиеся символы до длины 12
            for (int i = 4; i < 12; i++)
            {
                password.Add(allChars[random.Next(allChars.Length)]);
            }
            
            // Перемешиваем символы для случайного порядка
            for (int i = password.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = password[i];
                password[i] = password[j];
                password[j] = temp;
            }
            
            return new string(password.ToArray());
        }

        #endregion

        #region System Management

        /// <summary>
        /// Загрузить системную информацию
        /// </summary>
        private async Task LoadSystemInfoAsync()
        {
            await ExecuteSafelyAsync(async () =>
            {
                Logger.LogInformation("Loading system information");
                
                // Загружаем информацию о данных приложения
                DatabaseInfo = await _applicationDataManager.GetDataInfoAsync();
                TotalDataSize = DatabaseInfo.FormattedDataSize;
                Logger.LogInformation("DatabaseInfo loaded: AppDataPath={AppDataPath}, FormattedDataSize={FormattedDataSize}, ConfigurationExists={ConfigurationExists}", 
                    DatabaseInfo.AppDataPath, DatabaseInfo.FormattedDataSize, DatabaseInfo.ConfigurationExists);
                
                // Загружаем версионную информацию
                using var scope = _serviceProvider.CreateScope();
                var versionService = scope.ServiceProvider.GetRequiredService<WindowsLauncher.Core.Services.IVersionService>();
                var appVersionService = scope.ServiceProvider.GetRequiredService<WindowsLauncher.Core.Interfaces.IApplicationVersionService>();
                
                var versionInfo = versionService.GetVersionInfo();
                ApplicationVersion = appVersionService.GetApplicationVersion();
                BuildDate = versionInfo.BuildDate.ToString("yyyy-MM-dd HH:mm");
                DatabaseVersion = await appVersionService.GetDatabaseVersionAsync() ?? "Неизвестно";
                
                Logger.LogInformation("Version info loaded: ApplicationVersion={ApplicationVersion}, BuildDate={BuildDate}, DatabaseVersion={DatabaseVersion}, TotalDataSize={TotalDataSize}", 
                    ApplicationVersion, BuildDate, DatabaseVersion, TotalDataSize);
                
                // Загружаем статус SMTP серверов
                await UpdateSmtpStatusAsync();
                
                StatusMessage = "Системная информация загружена";
                Logger.LogInformation("System information loaded successfully");
            }, "load system information");
        }

        /// <summary>
        /// Очистить только базу данных
        /// </summary>
        private async Task ClearDatabaseOnlyAsync()
        {
            var confirmMessage = "Вы уверены, что хотите удалить все данные из базы данных?\n\n" +
                                "⚠️ Это действие нельзя отменить!\n" +
                                "Конфигурация БД будет сохранена.";
                                
            if (!DialogService.ShowConfirmation(confirmMessage, "Подтвердите очистку БД"))
                return;

            await ExecuteSafelyAsync(async () =>
            {
                Logger.LogWarning("Starting database cleanup (keeping configuration)");
                await _applicationDataManager.ClearDatabaseOnlyAsync();
                
                // Обновляем информацию
                await LoadSystemInfoAsync();
                
                StatusMessage = "База данных очищена (конфигурация сохранена)";
                DialogService.ShowInfo("База данных успешно очищена.\nКонфигурация подключения сохранена.", "Операция завершена");
            }, "clear database only");
        }

        /// <summary>
        /// Полная очистка всех данных приложения
        /// </summary>
        private async Task ClearAllApplicationDataAsync()
        {
            var confirmMessage = "Вы уверены, что хотите удалить ВСЕ данные приложения?\n\n" +
                                "⚠️ Это действие нельзя отменить!\n" +
                                "Будут удалены:\n" +
                                "• База данных\n" +
                                "• Конфигурационные файлы\n" +
                                "• Логи и кэш\n\n" +
                                "После этого потребуется повторная настройка приложения.";
                                
            if (!DialogService.ShowConfirmation(confirmMessage, "Подтвердите полную очистку"))
                return;
                
            // Дополнительное подтверждение
            var finalConfirm = DialogService.ShowConfirmation(
                "Последнее предупреждение!\n\nВсе данные приложения будут безвозвратно удалены.\nПродолжить?", 
                "Финальное подтверждение");
                
            if (!finalConfirm)
                return;

            await ExecuteSafelyAsync(async () =>
            {
                Logger.LogWarning("Starting complete application data cleanup");
                await _applicationDataManager.ClearAllDataAsync();
                
                StatusMessage = "Все данные приложения удалены";
                DialogService.ShowInfo(
                    "Все данные приложения успешно удалены.\n\nПриложение будет закрыто для повторной настройки.", 
                    "Операция завершена");
                    
                // Закрываем приложение для повторной настройки
                WpfApplication.Current.Shutdown();
            }, "clear all application data");
        }

        /// <summary>
        /// Экспорт конфигурации
        /// </summary>
        private void ExportConfiguration()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "Экспорт конфигурации",
                    Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = $"WindowsLauncher_Config_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var configData = new
                    {
                        ExportDate = DateTime.Now,
                        ApplicationVersion = ApplicationVersion,
                        DatabaseVersion = DatabaseVersion,
                        DatabaseInfo = DatabaseInfo,
                        ConfigurationPath = DatabaseInfo?.ConfigurationPath
                    };

                    var json = JsonSerializer.Serialize(configData, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    
                    File.WriteAllText(saveDialog.FileName, json);
                    
                    StatusMessage = $"Конфигурация экспортирована: {Path.GetFileName(saveDialog.FileName)}";
                    DialogService.ShowInfo($"Конфигурация успешно экспортирована в:\n{saveDialog.FileName}", "Экспорт завершен");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to export configuration");
                DialogService.ShowError($"Ошибка экспорта конфигурации: {ex.Message}", "Ошибка экспорта");
            }
        }

        /// <summary>
        /// Открыть папку с данными приложения
        /// </summary>
        private void OpenDataFolder()
        {
            try
            {
                var dataPath = DatabaseInfo?.AppDataPath ?? 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsLauncher");
                    
                if (Directory.Exists(dataPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", dataPath);
                    StatusMessage = "Папка данных открыта в проводнике";
                }
                else
                {
                    DialogService.ShowWarning("Папка данных не найдена", "Папка не существует");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open data folder");
                DialogService.ShowError($"Ошибка открытия папки: {ex.Message}", "Ошибка");
            }
        }

        /// <summary>
        /// Запуск диагностики системы
        /// </summary>
        private async Task RunSystemDiagnosticsAsync()
        {
            await ExecuteSafelyAsync(async () =>
            {
                Logger.LogInformation("Running system diagnostics");
                StatusMessage = "Запуск диагностики системы...";
                
                var diagnostics = new List<string>();
                diagnostics.Add($"=== Диагностика системы - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                diagnostics.Add("");
                
                // Проверка версий
                diagnostics.Add($"Версия приложения: {ApplicationVersion}");
                diagnostics.Add($"Дата сборки: {BuildDate}");
                diagnostics.Add($"Версия БД: {DatabaseVersion}");
                diagnostics.Add("");
                
                // Проверка данных
                if (DatabaseInfo != null)
                {
                    diagnostics.Add("=== Информация о данных ===");
                    diagnostics.Add($"Тип БД: {DatabaseInfo.ConfigurationExists}");
                    diagnostics.Add($"Размер данных: {DatabaseInfo.FormattedDataSize}");
                    diagnostics.Add($"Файлы БД: {DatabaseInfo.DatabaseFiles.Length}");
                    diagnostics.Add($"Путь к данным: {DatabaseInfo.AppDataPath}");
                    diagnostics.Add("");
                }
                
                // Проверка сервисов
                diagnostics.Add("=== Проверка сервисов ===");
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbConfigService = scope.ServiceProvider.GetRequiredService<IDatabaseConfigurationService>();
                    var isConfigured = await dbConfigService.IsConfiguredAsync();
                    diagnostics.Add($"БД сконфигурирована: {(isConfigured ? "✓" : "✗")}");
                }
                catch (Exception ex)
                {
                    diagnostics.Add($"Ошибка проверки БД: {ex.Message}");
                }
                
                diagnostics.Add("");
                diagnostics.Add("=== Диагностика завершена ===");
                
                var result = string.Join(Environment.NewLine, diagnostics);
                
                // Показываем результат
                var diagWindow = new Views.DiagnosticsWindow(result);
                diagWindow.ShowDialog();
                
                StatusMessage = "Диагностика системы завершена";
            }, "run system diagnostics");
        }
        
        /// <summary>
        /// Открыть окно настроек SMTP серверов
        /// </summary>
        private void OpenSmtpSettings()
        {
            try
            {
                Logger.LogInformation("Opening SMTP settings window");
                
                // Получаем SmtpSettingsViewModel из DI
                var smtpSettingsViewModel = _serviceProvider.GetService(typeof(SmtpSettingsViewModel)) as SmtpSettingsViewModel;
                if (smtpSettingsViewModel == null)
                {
                    Logger.LogError("Failed to resolve SmtpSettingsViewModel from DI container");
                    StatusMessage = "Ошибка: не удалось открыть настройки SMTP";
                    return;
                }
                
                // Создаем и показываем окно настроек SMTP
                var smtpSettingsWindow = new Views.SmtpSettingsWindow(smtpSettingsViewModel)
                {
                    Owner = WpfApplication.Current.MainWindow
                };
                
                var result = smtpSettingsWindow.ShowDialog();
                
                if (result == true)
                {
                    // Обновляем статус SMTP серверов после закрытия окна
                    _ = UpdateSmtpStatusAsync();
                    StatusMessage = "Настройки SMTP обновлены";
                    Logger.LogInformation("SMTP settings updated successfully");
                }
                else
                {
                    StatusMessage = "Настройки SMTP не изменены";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening SMTP settings window");
                StatusMessage = $"Ошибка открытия настроек SMTP: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Обновить статус SMTP серверов
        /// </summary>
        private async Task UpdateSmtpStatusAsync()
        {
            try
            {
                // Получаем ISmtpSettingsRepository из DI
                var smtpRepository = _serviceProvider.GetService(typeof(ISmtpSettingsRepository)) as ISmtpSettingsRepository;
                if (smtpRepository == null)
                {
                    Logger.LogWarning("SmtpSettingsRepository not available in DI container");
                    PrimarySmtpStatus = "⚠️ Сервис недоступен";
                    BackupSmtpStatus = "⚠️ Сервис недоступен";
                    return;
                }
                
                // Загружаем серверы
                var servers = await smtpRepository.GetActiveSettingsAsync();
                
                var primaryServer = servers.FirstOrDefault(s => s.ServerType == Core.Models.Email.SmtpServerType.Primary);
                var backupServer = servers.FirstOrDefault(s => s.ServerType == Core.Models.Email.SmtpServerType.Backup);
                
                // Обновляем статус основного сервера
                if (primaryServer != null)
                {
                    var status = primaryServer.IsActive ? "✅" : "❌";
                    var errors = primaryServer.ConsecutiveErrors > 0 ? $" ({primaryServer.ConsecutiveErrors} ошибок)" : "";
                    PrimarySmtpStatus = $"{status} {primaryServer.Host}:{primaryServer.Port}{errors}";
                }
                else
                {
                    PrimarySmtpStatus = "❌ Не настроен";
                }
                
                // Обновляем статус резервного сервера
                if (backupServer != null)
                {
                    var status = backupServer.IsActive ? "✅" : "❌";
                    var errors = backupServer.ConsecutiveErrors > 0 ? $" ({backupServer.ConsecutiveErrors} ошибок)" : "";
                    BackupSmtpStatus = $"{status} {backupServer.Host}:{backupServer.Port}{errors}";
                }
                else
                {
                    BackupSmtpStatus = "⚠️ Не настроен";
                }
                
                Logger.LogDebug("Updated SMTP status - Primary: {Primary}, Backup: {Backup}", 
                    PrimarySmtpStatus, BackupSmtpStatus);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating SMTP status");
                PrimarySmtpStatus = "❌ Ошибка загрузки";
                BackupSmtpStatus = "❌ Ошибка загрузки";
            }
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var app in Applications)
                {
                    app.PropertyChanged -= OnApplicationPropertyChanged;
                }

                if (EditingApplication != null)
                {
                    EditingApplication.PropertyChanged -= OnEditingApplicationPropertyChanged;
                }
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// ViewModel для редактирования приложения
    /// </summary>
    public class ApplicationEditViewModel : INotifyPropertyChanged
    {
        private CoreApplication _application;
        private bool _isNew;

        public ApplicationEditViewModel(CoreApplication application)
        {
            _application = application;
            RequiredGroups = new ObservableCollection<string>(application.RequiredGroups);
        }

        public int Id
        {
            get => _application.Id;
            set => SetProperty(() => _application.Id = value);
        }

        public string Name
        {
            get => _application.Name;
            set => SetProperty(() => _application.Name = value);
        }

        public string Description
        {
            get => _application.Description;
            set => SetProperty(() => _application.Description = value);
        }

        public string ExecutablePath
        {
            get => _application.ExecutablePath;
            set => SetProperty(() => _application.ExecutablePath = value);
        }

        public string Arguments
        {
            get => _application.Arguments;
            set => SetProperty(() => _application.Arguments = value);
        }

        public string IconPath
        {
            get => _application.IconPath;
            set => SetProperty(() => _application.IconPath = value);
        }

        public string Category
        {
            get => _application.Category;
            set => SetProperty(() => _application.Category = value);
        }

        public ApplicationType Type
        {
            get => _application.Type;
            set => SetProperty(() => _application.Type = value);
        }

        public ObservableCollection<string> RequiredGroups { get; }

        public UserRole MinimumRole
        {
            get => _application.MinimumRole;
            set => SetProperty(() => _application.MinimumRole = value);
        }

        public bool IsEnabled
        {
            get => _application.IsEnabled;
            set => SetProperty(() => _application.IsEnabled = value);
        }

        public int SortOrder
        {
            get => _application.SortOrder;
            set => SetProperty(() => _application.SortOrder = value);
        }

        public DateTime CreatedDate
        {
            get => _application.CreatedDate;
            set => SetProperty(() => _application.CreatedDate = value);
        }

        public DateTime ModifiedDate
        {
            get => _application.ModifiedDate;
            set => SetProperty(() => _application.ModifiedDate = value);
        }

        public string CreatedBy
        {
            get => _application.CreatedBy;
            set => SetProperty(() => _application.CreatedBy = value);
        }

        public bool IsNew
        {
            get => _isNew;
            set => SetProperty(ref _isNew, value);
        }

        // APK метаданные (только для Android приложений)
        public string? ApkPackageName
        {
            get => _application.ApkPackageName;
            set => SetProperty(() => _application.ApkPackageName = value);
        }

        public string? ApkVersionCode
        {
            get => _application.ApkVersionCode?.ToString();
            set => SetProperty(() => _application.ApkVersionCode = int.TryParse(value, out var code) ? code : null);
        }

        public string? ApkVersionName
        {
            get => _application.ApkVersionName;
            set => SetProperty(() => _application.ApkVersionName = value);
        }

        public string? ApkMinSdk
        {
            get => _application.ApkMinSdk?.ToString();
            set => SetProperty(() => _application.ApkMinSdk = int.TryParse(value, out var sdk) ? sdk : null);
        }

        public string? ApkTargetSdk
        {
            get => _application.ApkTargetSdk?.ToString();
            set => SetProperty(() => _application.ApkTargetSdk = int.TryParse(value, out var sdk) ? sdk : null);
        }

        public string? ApkFilePath
        {
            get => _application.ApkFilePath;
            set => SetProperty(() => _application.ApkFilePath = value);
        }

        public string? ApkFileHash
        {
            get => _application.ApkFileHash;
            set => SetProperty(() => _application.ApkFileHash = value);
        }

        public string? ApkInstallStatus
        {
            get => _application.ApkInstallStatus;
            set => SetProperty(() => _application.ApkInstallStatus = value);
        }

        // UI helpers
        public string TypeDisplay => Type switch
        {
            ApplicationType.Desktop => "Приложение",
            ApplicationType.Web => "Веб-ссылка",
            ApplicationType.Folder => "Папка",
            ApplicationType.ChromeApp => "Chrome App",
            ApplicationType.Android => "Android APK",
            _ => Type.ToString()
        };

        public string RoleDisplay => MinimumRole switch
        {
            UserRole.Guest => "Гостевой",
            UserRole.Standard => "Стандартный",
            UserRole.PowerUser => "Опытный",
            UserRole.Administrator => "Администратор",
            _ => MinimumRole.ToString()
        };

        public CoreApplication ToApplication()
        {
            _application.RequiredGroups = RequiredGroups.ToList();
            return _application;
        }

        public ApplicationEditViewModel CreateCopy()
        {
            var copy = new CoreApplication
            {
                Id = Id,
                Name = Name,
                Description = Description,
                ExecutablePath = ExecutablePath,
                Arguments = Arguments,
                IconPath = IconPath,
                Category = Category,
                Type = Type,
                RequiredGroups = RequiredGroups.ToList(),
                MinimumRole = MinimumRole,
                IsEnabled = IsEnabled,
                SortOrder = SortOrder,
                CreatedDate = CreatedDate,
                ModifiedDate = ModifiedDate,
                CreatedBy = CreatedBy,
                // APK метаданные
                ApkPackageName = ApkPackageName,
                ApkVersionCode = _application.ApkVersionCode,
                ApkVersionName = ApkVersionName,
                ApkMinSdk = _application.ApkMinSdk,
                ApkTargetSdk = _application.ApkTargetSdk,
                ApkFilePath = ApkFilePath,
                ApkFileHash = ApkFileHash,
                ApkInstallStatus = ApkInstallStatus
            };

            return new ApplicationEditViewModel(copy) { IsNew = IsNew };
        }

        public void UpdateFrom(ApplicationEditViewModel other)
        {
            Name = other.Name;
            Description = other.Description;
            ExecutablePath = other.ExecutablePath;
            Arguments = other.Arguments;
            IconPath = other.IconPath;
            Category = other.Category;
            Type = other.Type;
            MinimumRole = other.MinimumRole;
            IsEnabled = other.IsEnabled;
            SortOrder = other.SortOrder;
            ModifiedDate = other.ModifiedDate;

            // APK метаданные
            ApkPackageName = other.ApkPackageName;
            ApkVersionCode = other.ApkVersionCode;
            ApkVersionName = other.ApkVersionName;
            ApkMinSdk = other.ApkMinSdk;
            ApkTargetSdk = other.ApkTargetSdk;
            ApkFilePath = other.ApkFilePath;
            ApkFileHash = other.ApkFileHash;
            ApkInstallStatus = other.ApkInstallStatus;

            RequiredGroups.Clear();
            foreach (var group in other.RequiredGroups)
            {
                RequiredGroups.Add(group);
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetProperty(Action setter, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            setter();
            OnPropertyChanged(propertyName);
        }

        private bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    #region Export/Import Models

    /// <summary>
    /// Модель для экспорта/импорта данных лаунчера
    /// </summary>
    public class LauncherExportData
    {
        public string Version { get; set; } = "1.0";
        public DateTime ExportedAt { get; set; } = DateTime.Now;
        public string ExportedBy { get; set; } = string.Empty;
        public List<ExportApplication> Applications { get; set; } = new();
        public List<ExportUser> LocalUsers { get; set; } = new();
    }

    /// <summary>
    /// Модель приложения для экспорта
    /// </summary>
    public class ExportApplication
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public ApplicationType Type { get; set; }
        public List<string> RequiredGroups { get; set; } = new();
        public UserRole MinimumRole { get; set; }
        public bool IsEnabled { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Модель пользователя для экспорта
    /// </summary>
    public class ExportUser
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    #endregion
}