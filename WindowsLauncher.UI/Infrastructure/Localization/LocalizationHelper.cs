// ===== WindowsLauncher.UI/Infrastructure/Localization/LocalizationHelper.cs =====
using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;

namespace WindowsLauncher.UI.Infrastructure.Localization
{
    /// <summary>
    /// Помощник для работы с локализацией и динамической смены языка
    /// </summary>
    public class LocalizationHelper : INotifyPropertyChanged
    {
        private static LocalizationHelper? _instance;
        private static readonly object _lock = new object();
        private readonly ResourceManager _resourceManager;

        public static LocalizationHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new LocalizationHelper();
                    }
                }
                return _instance;
            }
        }

        private LocalizationHelper()
        {
            // ✅ ИСПРАВЛЕНО: Правильная ссылка на Resources с подпапкой
            _resourceManager = new ResourceManager(
                "WindowsLauncher.UI.Properties.Resources.Resources",
                typeof(LocalizationHelper).Assembly
            );
        }

        /// <summary>
        /// Текущая культура
        /// </summary>
        public CultureInfo CurrentCulture
        {
            get => Thread.CurrentThread.CurrentUICulture;
            set
            {
                if (!Equals(value, Thread.CurrentThread.CurrentUICulture))
                {
                    System.Diagnostics.Debug.WriteLine($"🌐 LocalizationHelper: Changing language from {Thread.CurrentThread.CurrentUICulture.Name} to {value.Name}");
                    
                    Thread.CurrentThread.CurrentUICulture = value;
                    Thread.CurrentThread.CurrentCulture = value;

                    // Обновляем культуру для всех потоков
                    CultureInfo.DefaultThreadCurrentUICulture = value;
                    CultureInfo.DefaultThreadCurrentCulture = value;

                    OnPropertyChanged(nameof(CurrentCulture));
                    NotifyAllPropertiesChanged();
                    OnLanguageChanged();
                }
            }
        }

        /// <summary>
        /// Текущий язык (для UI)
        /// </summary>
        public string CurrentLanguage
        {
            get => CurrentCulture.TwoLetterISOLanguageName;
            set => SetLanguage(value);
        }

        /// <summary>
        /// Доступные языки
        /// </summary>
        public static readonly string[] AvailableLanguages = { "en", "ru" };

        /// <summary>
        /// Событие изменения языка
        /// </summary>
        public event EventHandler? LanguageChanged;

        /// <summary>
        /// Событие изменения свойства
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Получение локализованной строки
        /// </summary>
        /// <param name="key">Ключ ресурса</param>
        /// <returns>Локализованная строка</returns>
        public string GetString(string key)
        {
            try
            {
                return _resourceManager.GetString(key, CurrentCulture) ?? key;
            }
            catch
            {
                return key; // Возвращаем ключ если ресурс не найден
            }
        }

        /// <summary>
        /// Получение локализованной строки с форматированием
        /// </summary>
        /// <param name="key">Ключ ресурса</param>
        /// <param name="args">Аргументы для форматирования</param>
        /// <returns>Форматированная локализованная строка</returns>
        public string GetFormattedString(string key, params object[] args)
        {
            try
            {
                var format = GetString(key);
                return string.Format(format, args);
            }
            catch
            {
                return key;
            }
        }

        /// <summary>
        /// Установка языка по коду
        /// </summary>
        /// <param name="languageCode">Код языка (en, ru, en-US, ru-RU)</param>
        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return;

            System.Diagnostics.Debug.WriteLine($"🌐 LocalizationHelper: SetLanguage called with '{languageCode}'");

            try
            {
                // Сначала пробуем использовать код как есть
                var culture = new CultureInfo(languageCode);
                CurrentCulture = culture;
                return;
            }
            catch (CultureNotFoundException)
            {
                // Если не удалось, пробуем извлечь двухбуквенный код
                try
                {
                    if (languageCode.Contains("-"))
                    {
                        var twoLetterCode = languageCode.Split('-')[0];
                        if (Array.Exists(AvailableLanguages, lang => lang == twoLetterCode))
                        {
                            var culture = new CultureInfo(twoLetterCode);
                            CurrentCulture = culture;
                            return;
                        }
                    }
                }
                catch
                {
                    // Игнорируем ошибки дополнительной обработки
                }

                // Если ничего не получилось, используем английский по умолчанию
                CurrentCulture = new CultureInfo("en");
            }
        }

        /// <summary>
        /// Автоматическое определение языка системы
        /// </summary>
        public void SetSystemLanguage()
        {
            var systemCulture = CultureInfo.CurrentUICulture;
            var systemLanguage = systemCulture.Name; // Полный код культуры (ru-RU, en-US)
            var twoLetterCode = systemCulture.TwoLetterISOLanguageName; // Двухбуквенный код (ru, en)

            // Проверяем полное совпадение культуры (приоритет)
            if (IsLanguageSupportedByFullCode(systemLanguage))
            {
                SetLanguage(systemLanguage);
                return;
            }

            // Проверяем совпадение по двухбуквенному коду
            if (Array.Exists(AvailableLanguages, lang => lang == twoLetterCode))
            {
                SetLanguage(twoLetterCode);
                return;
            }

            // Если язык не поддерживается, используем английский
            SetLanguage("en");
        }

        /// <summary>
        /// Проверка поддержки языка по полному коду культуры
        /// </summary>
        /// <param name="cultureCode">Код культуры (например, ru-RU)</param>
        /// <returns>True если поддерживается</returns>
        private bool IsLanguageSupportedByFullCode(string cultureCode)
        {
            if (string.IsNullOrEmpty(cultureCode)) return false;

            // Преобразуем в двухбуквенный код для сравнения с AvailableLanguages
            try
            {
                var culture = new CultureInfo(cultureCode);
                var twoLetterCode = culture.TwoLetterISOLanguageName;
                return Array.Exists(AvailableLanguages, lang => lang == twoLetterCode);
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Получение отображаемого имени языка
        /// </summary>
        /// <param name="languageCode">Код языка</param>
        /// <returns>Отображаемое имя</returns>
        public string GetLanguageDisplayName(string languageCode)
        {
            return languageCode switch
            {
                "en" => "English",
                "ru" => "Русский",
                _ => languageCode
            };
        }

        /// <summary>
        /// Сохранение выбранного языка в настройки
        /// ✅ ИСПРАВЛЕНО: Безопасное обращение к настройкам
        /// </summary>
        public void SaveLanguageSettings()
        {
            try
            {
                var settings = Properties.Settings.Default;

                // Проверяем, что свойство Language существует
                var languageProperty = settings.GetType().GetProperty("Language");
                if (languageProperty != null && languageProperty.CanWrite)
                {
                    languageProperty.SetValue(settings, CurrentLanguage);
                    settings.Save();
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем работу
                System.Diagnostics.Debug.WriteLine($"Failed to save language settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка языка из настроек
        /// ✅ ИСПРАВЛЕНО: Безопасное обращение к настройкам
        /// </summary>
        public void LoadLanguageSettings()
        {
            try
            {
                var settings = Properties.Settings.Default;

                // Проверяем, что свойство Language существует
                var languageProperty = settings.GetType().GetProperty("Language");
                if (languageProperty != null && languageProperty.CanRead)
                {
                    var savedLanguage = languageProperty.GetValue(settings) as string;
                    if (!string.IsNullOrEmpty(savedLanguage))
                    {
                        System.Diagnostics.Debug.WriteLine($"🌐 LocalizationHelper: Found saved language '{savedLanguage}' in settings");
                        SetLanguage(savedLanguage);
                        return;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("🌐 LocalizationHelper: No saved language found in settings");
                    }
                }

                // Если настройка не найдена или пустая, не устанавливаем язык
                // Язык будет установлен через LanguageConfigurationService
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load language settings: {ex.Message}");
                // Не устанавливаем fallback язык - это делает LanguageConfigurationService
            }
        }

        /// <summary>
        /// Проверка доступности конкретного языка
        /// </summary>
        /// <param name="languageCode">Код языка</param>
        /// <returns>True если язык поддерживается</returns>
        public bool IsLanguageSupported(string languageCode)
        {
            return Array.Exists(AvailableLanguages, lang =>
                string.Equals(lang, languageCode, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Получение всех доступных языков с их отображаемыми именами
        /// </summary>
        /// <returns>Словарь код_языка -> отображаемое_имя</returns>
        public System.Collections.Generic.Dictionary<string, string> GetAvailableLanguages()
        {
            var languages = new System.Collections.Generic.Dictionary<string, string>();

            foreach (var lang in AvailableLanguages)
            {
                languages[lang] = GetLanguageDisplayName(lang);
            }

            return languages;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnLanguageChanged()
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Свойства для привязки в XAML - AppSwitcher
        /// </summary>
        public string AppSwitcher_Title => GetString("AppSwitcher_Title");
        public string AppSwitcher_Navigation => GetString("AppSwitcher_Navigation");
        public string AppSwitcher_Switch => GetString("AppSwitcher_Switch");
        public string AppSwitcher_Cancel => GetString("AppSwitcher_Cancel");

        /// <summary>
        /// Уведомить об изменении всех свойств локализации
        /// </summary>
        private void NotifyAllPropertiesChanged()
        {
            OnPropertyChanged(nameof(AppSwitcher_Title));
            OnPropertyChanged(nameof(AppSwitcher_Navigation));
            OnPropertyChanged(nameof(AppSwitcher_Switch));
            OnPropertyChanged(nameof(AppSwitcher_Cancel));
        }
    }

    /// <summary>
    /// Markup Extension для использования локализации в XAML
    /// Использование: {loc:Localize LoginWindow_Title}
    /// </summary>
    public class LocalizeExtension : System.Windows.Markup.MarkupExtension
    {
        public LocalizeExtension() { }

        public LocalizeExtension(string key)
        {
            Key = key;
        }
        
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return string.Empty;

            return LocalizationHelper.Instance.GetString(Key);
        }
    }

    /// <summary>
    /// ✅ НОВЫЙ: Конвертер для привязки локализованных строк в XAML
    /// </summary>
    public class LocalizedStringConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string key)
            {
                return LocalizationHelper.Instance.GetString(key);
            }

            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}