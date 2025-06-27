// WindowsLauncher.UI/ViewModels/AdminViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.UI.Infrastructure.Commands;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.ViewModels.Base;

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
        private readonly IServiceProvider _serviceProvider;

        private ApplicationEditViewModel? _selectedApplication;
        private ApplicationEditViewModel? _editingApplication;
        private string _searchText = "";
        private string _statusMessage = "";
        private bool _isEditMode;
        private bool _hasUnsavedChanges;

        #endregion

        #region Constructor

        public AdminViewModel(
            IApplicationService applicationService,
            IAuthorizationService authorizationService,
            IServiceProvider serviceProvider,
            ILogger<AdminViewModel> logger,
            IDialogService dialogService)
            : base(logger, dialogService)
        {
            _applicationService = applicationService;
            _authorizationService = authorizationService;
            _serviceProvider = serviceProvider;

            Applications = new ObservableCollection<ApplicationEditViewModel>();
            AvailableCategories = new ObservableCollection<string>();
            AvailableGroups = new ObservableCollection<string>();
            AvailableTypes = Enum.GetValues<ApplicationType>().ToList();
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
        public List<ApplicationType> AvailableTypes { get; }
        public List<UserRole> AvailableRoles { get; }
        public ICollectionView ApplicationsView { get; }

        public ApplicationEditViewModel? SelectedApplication
        {
            get => _selectedApplication;
            set
            {
                if (SetProperty(ref _selectedApplication, value))
                {
                    if (value != null && !IsEditMode)
                    {
                        StartEdit(value);
                    }
                    UpdateCommandStates();
                }
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

            // Обновляем список категорий
            var categories = Applications.Select(a => a.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c);

            AvailableCategories.Clear();
            foreach (var category in categories)
            {
                AvailableCategories.Add(category);
            }

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
                "Finance Department",
                "HR Department"
            };

            AvailableGroups.Clear();
            foreach (var group in testGroups.OrderBy(g => g))
            {
                AvailableGroups.Add(group);
            }
        }

        #endregion

        #region Edit Operations

        private void AddNewApplication()
        {
            var newApp = new Application
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
            IsEditMode = true;
            HasUnsavedChanges = true;

            StatusMessage = "Создание нового приложения";
        }

        private void StartEdit(ApplicationEditViewModel? app)
        {
            if (app == null || !CanStartEdit()) return;

            // Создаем копию для редактирования
            EditingApplication = app.CreateCopy();
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

            EditingApplication = null;
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
                    EditingApplication = null;
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

            await ExecuteSafelyAsync(async () =>
            {
                var duplicate = app.CreateCopy();
                duplicate.Id = 0;
                duplicate.Name = $"{app.Name} - Копия";
                duplicate.IsNew = true;
                duplicate.CreatedDate = DateTime.Now;
                duplicate.ModifiedDate = DateTime.Now;

                EditingApplication = duplicate;
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
                // TODO: Реализовать импорт из CSV/JSON
                DialogService.ShowInfo(
                    "Функция импорта будет реализована в следующей версии",
                    "В разработке");
            }, "import applications");
        }

        private async Task ExportApplicationsAsync()
        {
            await ExecuteSafelyAsync(async () =>
            {
                // TODO: Реализовать экспорт в CSV/JSON
                DialogService.ShowInfo(
                    "Функция экспорта будет реализована в следующей версии",
                    "В разработке");
            }, "export applications");
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
                HasUnsavedChanges = true;
            }
        }

        private void RemoveRequiredGroup(string? group)
        {
            if (EditingApplication == null || string.IsNullOrEmpty(group)) return;

            EditingApplication.RequiredGroups.Remove(group);
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
            if (sender == EditingApplication && IsEditMode)
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

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var app in Applications)
                {
                    app.PropertyChanged -= OnApplicationPropertyChanged;
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
        private Application _application;
        private bool _isNew;

        public ApplicationEditViewModel(Application application)
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

        // UI helpers
        public string TypeDisplay => Type switch
        {
            ApplicationType.Desktop => "Приложение",
            ApplicationType.Web => "Веб-ссылка",
            ApplicationType.Folder => "Папка",
            _ => Type.ToString()
        };

        public string RoleDisplay => MinimumRole switch
        {
            UserRole.Standard => "Стандартный",
            UserRole.PowerUser => "Опытный",
            UserRole.Administrator => "Администратор",
            _ => MinimumRole.ToString()
        };

        public Application ToApplication()
        {
            _application.RequiredGroups = RequiredGroups.ToList();
            return _application;
        }

        public ApplicationEditViewModel CreateCopy()
        {
            var copy = new Application
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
                CreatedBy = CreatedBy
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

            RequiredGroups.Clear();
            foreach (var group in other.RequiredGroups)
            {
                RequiredGroups.Add(group);
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
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
}