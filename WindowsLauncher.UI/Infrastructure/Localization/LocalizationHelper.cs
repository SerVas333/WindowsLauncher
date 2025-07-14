// ===== WindowsLauncher.UI/Infrastructure/Localization/LocalizationHelper.cs =====
using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace WindowsLauncher.UI.Infrastructure.Localization
{
    /// <summary>
    /// Помощник для работы с локализацией и динамической смены языка
    /// </summary>
    public class LocalizationHelper : INotifyPropertyChanged
    {
        private static LocalizationHelper _instance;
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
                    Thread.CurrentThread.CurrentUICulture = value;
                    Thread.CurrentThread.CurrentCulture = value;

                    // Обновляем культуру для всех потоков
                    CultureInfo.DefaultThreadCurrentUICulture = value;
                    CultureInfo.DefaultThreadCurrentCulture = value;

                    OnPropertyChanged(nameof(CurrentCulture));
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
        public event EventHandler LanguageChanged;

        /// <summary>
        /// Событие изменения свойства
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

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
        /// <param name="languageCode">Код языка (en, ru)</param>
        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return;

            try
            {
                var culture = new CultureInfo(languageCode);
                CurrentCulture = culture;
            }
            catch (CultureNotFoundException)
            {
                // Если культура не найдена, используем английский по умолчанию
                CurrentCulture = new CultureInfo("en");
            }
        }

        /// <summary>
        /// Автоматическое определение языка системы
        /// </summary>
        public void SetSystemLanguage()
        {
            var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            // Проверяем, поддерживается ли язык системы
            if (Array.Exists(AvailableLanguages, lang => lang == systemLanguage))
            {
                SetLanguage(systemLanguage);
            }
            else
            {
                // Если язык не поддерживается, используем английский
                SetLanguage("en");
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
                        SetLanguage(savedLanguage);
                        return;
                    }
                }

                // Если настройка не найдена или пустая, используем системный язык
                SetSystemLanguage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load language settings: {ex.Message}");
                SetSystemLanguage();
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

        private void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnLanguageChanged()
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
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

        public string Key { get; set; }

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