using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.Infrastructure.Localization;
using WindowsLauncher.UI.Infrastructure.Commands;
using WpfApplication = System.Windows.Application;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для управления категориями, фильтрацией и поиском
    /// </summary>
    public class CategoryManagementViewModel : ViewModelBase
    {
        #region Fields

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private string _searchText = "";
        private string _selectedCategory = "All";
        private bool _hasActiveFilter = false;
        private User? _currentUser;

        #endregion

        #region Constructor

        public CategoryManagementViewModel(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<CategoryManagementViewModel> logger,
            IDialogService dialogService)
            : base(logger, dialogService)
        {
            _serviceScopeFactory = serviceScopeFactory;

            // Инициализируем коллекции
            LocalizedCategories = new ObservableCollection<CategoryViewModel>();

            // Инициализируем команды
            InitializeCommands();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Текущий пользователь для загрузки видимых категорий
        /// </summary>
        public User? CurrentUser
        {
            get => _currentUser;
            set
            {
                if (SetProperty(ref _currentUser, value))
                {
                    // При смене пользователя нужно перезагрузить категории
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
                    UpdateActiveFilterStatus();
                    // Фильтрация будет вызываться из MainViewModel через событие
                    OnSearchTextChanged();
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
                    UpdateCategorySelection();
                    // Фильтрация будет вызываться из MainViewModel через событие
                    OnSelectedCategoryChanged();
                }
            }
        }

        /// <summary>
        /// Индикатор активного фильтра для визуальной индикации
        /// </summary>
        public bool HasActiveFilter
        {
            get => _hasActiveFilter;
            set => SetProperty(ref _hasActiveFilter, value);
        }

        /// <summary>
        /// Локализованные категории с чекбоксами для фильтрации
        /// </summary>
        public ObservableCollection<CategoryViewModel> LocalizedCategories { get; }

        #endregion

        #region Commands

        public RelayCommand<string> SelectCategoryCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            SelectCategoryCommand = new RelayCommand<string>(SelectCategory);
        }

        #endregion

        #region Events

        /// <summary>
        /// Событие изменения текста поиска
        /// </summary>
        public event EventHandler? SearchTextChanged;

        /// <summary>
        /// Событие изменения выбранной категории
        /// </summary>
        public event EventHandler? SelectedCategoryChanged;

        /// <summary>
        /// Событие необходимости обновления фильтрации
        /// </summary>
        public event EventHandler? FilteringRequested;

        #endregion

        #region Public Methods

        /// <summary>
        /// Загрузить и локализовать категории
        /// </summary>
        public async Task LoadLocalizedCategoriesAsync(IApplicationService appService)
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
                    var hiddenCategories = new List<string>(); // TODO: Get from user settings if needed

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

        /// <summary>
        /// Получить коллекцию включенных категорий для фильтрации
        /// </summary>
        public string[] GetCheckedCategories()
        {
            var checkedCategories = LocalizedCategories
                .Where(c => c.IsChecked)
                .Select(c => c.Key)
                .ToArray();

            return checkedCategories;
        }

        /// <summary>
        /// Проверить должна ли фильтрация включать все категории
        /// </summary>
        public bool ShouldIncludeAllCategories()
        {
            var allCategory = LocalizedCategories.FirstOrDefault(c => c.Key == "All");
            return allCategory?.IsChecked == true;
        }

        /// <summary>
        /// Обновить локализацию категорий при смене языка
        /// </summary>
        public async Task UpdateLocalizationAsync()
        {
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
        }

        /// <summary>
        /// Очистить все данные при смене пользователя
        /// </summary>
        public async Task ClearAsync()
        {
            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
            {
                // Отписываемся от событий перед очисткой
                foreach (var category in LocalizedCategories)
                {
                    category.PropertyChanged -= OnCategoryPropertyChanged;
                }

                LocalizedCategories.Clear();
                SearchText = "";
                SelectedCategory = "All";
                HasActiveFilter = false;
            });
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Получить локализованное имя категории
        /// </summary>
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

        /// <summary>
        /// Обновить выбор категорий в UI
        /// </summary>
        private void UpdateCategorySelection()
        {
            foreach (var category in LocalizedCategories)
            {
                category.IsSelected = category.Key == SelectedCategory;
            }
        }

        /// <summary>
        /// Обновить статус активного фильтра
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

                // Обновляем индикатор активного фильтра
                UpdateActiveFilterStatus();

                // Уведомляем о необходимости обновления фильтрации
                OnFilteringRequested();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Ошибка при обработке изменения чекбокса категории");
            }
        }

        /// <summary>
        /// Выбрать категорию
        /// </summary>
        private void SelectCategory(string? category)
        {
            if (!string.IsNullOrEmpty(category))
            {
                SelectedCategory = category;
            }
        }

        /// <summary>
        /// Вызвать событие изменения текста поиска
        /// </summary>
        private void OnSearchTextChanged()
        {
            SearchTextChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Вызвать событие изменения выбранной категории
        /// </summary>
        private void OnSelectedCategoryChanged()
        {
            SelectedCategoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Вызвать событие необходимости обновления фильтрации
        /// </summary>
        private void OnFilteringRequested()
        {
            FilteringRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Отписываемся от всех событий
                foreach (var category in LocalizedCategories)
                {
                    category.PropertyChanged -= OnCategoryPropertyChanged;
                }

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