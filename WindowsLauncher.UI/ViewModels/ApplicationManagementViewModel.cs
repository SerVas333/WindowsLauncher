using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.Infrastructure.Commands;
using WindowsLauncher.UI.Infrastructure.Extensions;
using WindowsLauncher.UI.Infrastructure.Localization;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.ViewModels.Base;
using WpfApplication = System.Windows.Application;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для управления приложениями: загрузка, фильтрация, поиск, запуск
    /// </summary>
    public class ApplicationManagementViewModel : ViewModelBase
    {
        #region Fields

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private string _searchText = "";
        private string _selectedCategory = "All";
        private User? _currentUser;
        private bool _isLoading = false;

        #endregion

        #region Constructor

        public ApplicationManagementViewModel(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ApplicationManagementViewModel> logger,
            IDialogService dialogService)
            : base(logger, dialogService)
        {
            _serviceScopeFactory = serviceScopeFactory;

            // Инициализируем коллекции
            Applications = new ObservableCollection<ApplicationViewModel>();
            FilteredApplications = new ObservableCollection<ApplicationViewModel>();

            // Инициализируем команды
            InitializeCommands();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Текущий пользователь для авторизации приложений
        /// </summary>
        public User? CurrentUser
        {
            get => _currentUser;
            set
            {
                if (SetProperty(ref _currentUser, value))
                {
                    // При смене пользователя нужно перезагрузить приложения
                    // Это будет вызываться из MainViewModel
                }
            }
        }

        /// <summary>
        /// Текст для поиска приложений
        /// </summary>
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

        /// <summary>
        /// Выбранная категория для фильтрации
        /// </summary>
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    FilterApplications();
                }
            }
        }

        /// <summary>
        /// Признак загрузки данных
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Все приложения пользователя
        /// </summary>
        public ObservableCollection<ApplicationViewModel> Applications { get; }

        /// <summary>
        /// Отфильтрованные приложения для отображения
        /// </summary>
        public ObservableCollection<ApplicationViewModel> FilteredApplications { get; }

        // Вычисляемые свойства
        public int ApplicationCount => FilteredApplications.Count;
        public bool HasNoApplications => !IsLoading && ApplicationCount == 0;

        #endregion

        #region Commands

        public AsyncRelayCommand<ApplicationViewModel> LaunchApplicationCommand { get; private set; } = null!;
        public AsyncRelayCommand RefreshCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            LaunchApplicationCommand = new AsyncRelayCommand<ApplicationViewModel>(
                LaunchApplication,
                app => app != null && !IsLoading,
                Logger);

            RefreshCommand = new AsyncRelayCommand(
                RefreshApplications,
                () => CurrentUser != null && !IsLoading,
                Logger);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Загрузить приложения для текущего пользователя
        /// </summary>
        public async Task LoadApplicationsAsync()
        {
            if (CurrentUser == null) return;

            try
            {
                Logger.LogInformation("Loading applications for user: {User}", CurrentUser.Username);
                IsLoading = true;

                using var scope = _serviceScopeFactory.CreateScope();
                var authzService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

                // Получаем авторизованные приложения
                var apps = await authzService.GetAuthorizedApplicationsAsync(CurrentUser);

                if (apps?.Count > 0)
                {
                    // Очистка и заполнение коллекций в UI потоке
                    await WpfApplication.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        Applications.Clear();
                        FilteredApplications.Clear();

                        // Создаем задачи для параллельной инициализации ApplicationViewModel
                        var initializationTasks = apps.Select(CreateAndInitializeApplicationViewModelAsync);
                        var appViewModels = await Task.WhenAll(initializationTasks);

                        // Добавляем в коллекцию Applications в UI потоке
                        foreach (var appViewModel in appViewModels.Where(vm => vm != null))
                        {
                            Applications.Add(appViewModel!);
                        }

                        // Применяем фильтры
                        FilterApplications();
                    });

                    var appCount = Applications.Count;
                    Logger.LogInformation("Applications loaded: {AppCount} apps", appCount);
                }
                else
                {
                    Logger.LogInformation("No applications found for user: {User}", CurrentUser.Username);
                    
                    await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Applications.Clear();
                        FilteredApplications.Clear();
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load applications");
                
                // Показываем ошибку пользователю
                DialogService.ShowError(
                    LocalizationHelper.Instance.GetFormattedString("ErrorLoadingApplications", ex.Message),
                    LocalizationHelper.Instance.GetString("Error"));

                // Приложения будут пустыми - это нормально для production системы
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Очистить все данные при смене пользователя
        /// </summary>
        public async Task ClearAsync()
        {
            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
            {
                Applications.Clear();
                FilteredApplications.Clear();
                SearchText = "";
                SelectedCategory = "All";
            });
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Запустить приложение
        /// </summary>
        private async Task LaunchApplication(ApplicationViewModel? appViewModel)
        {
            if (appViewModel == null || CurrentUser == null) return;

            appViewModel.IsLaunching = true;

            try
            {
                await ExecuteSafelyAsync(async () =>
                {
                    var app = appViewModel.GetApplication();

                    using var scope = _serviceScopeFactory.CreateScope();
                    var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                    var result = await appService.LaunchApplicationAsync(app, CurrentUser);

                    if (result.IsSuccess)
                    {
                        Logger.LogInformation("Application launched: {App}", app.Name);
                    }
                    else
                    {
                        var errorMessage = LocalizationHelper.Instance.GetFormattedString("FailedToLaunch", app.Name, result.ErrorMessage);
                        DialogService.ShowWarning(errorMessage, LocalizationHelper.Instance.GetString("LaunchError"));
                    }
                }, $"launch application {appViewModel.Name}");
            }
            finally
            {
                // Всегда сбрасываем состояние загрузки
                appViewModel.IsLaunching = false;
            }
        }

        /// <summary>
        /// Обновить приложения
        /// </summary>
        private async Task RefreshApplications()
        {
            await ExecuteSafelyAsync(async () =>
            {
                await LoadApplicationsAsync();
            }, "refresh applications");
        }

        /// <summary>
        /// Применить фильтры к коллекции приложений
        /// </summary>
        private void FilterApplications()
        {
            try
            {
                var filtered = Applications.AsEnumerable();

                // Фильтр по категории
                if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "All")
                {
                    filtered = filtered.Where(app => app.Category == SelectedCategory);
                }

                // Фильтр по поисковому тексту
                if (!string.IsNullOrEmpty(SearchText))
                {
                    var searchLower = SearchText.ToLower();
                    filtered = filtered.Where(app =>
                        app.Name.ToLower().Contains(searchLower) ||
                        (!string.IsNullOrEmpty(app.Description) && app.Description.ToLower().Contains(searchLower)));
                }

                // Обновляем отфильтрованную коллекцию в UI потоке
                WpfApplication.Current.Dispatcher.BeginInvoke(() =>
                {
                    FilteredApplications.Clear();
                    foreach (var app in filtered)
                    {
                        FilteredApplications.Add(app);
                    }

                    // Уведомляем об изменении вычисляемых свойств
                    OnPropertyChanged(nameof(ApplicationCount));
                    OnPropertyChanged(nameof(HasNoApplications));
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error filtering applications");
            }
        }

        /// <summary>
        /// Создать и асинхронно инициализировать ApplicationViewModel
        /// </summary>
        private async Task<ApplicationViewModel> CreateAndInitializeApplicationViewModelAsync(Core.Models.Application app)
        {
            // Создаем ApplicationViewModel с правильными параметрами
            using var scope = _serviceScopeFactory.CreateScope();
            var categoryService = scope.ServiceProvider.GetService<ICategoryManagementService>();
            
            var appViewModel = new ApplicationViewModel(app, categoryService);
            await appViewModel.InitializeAsync();
            return appViewModel;
        }

        #endregion
    }
}