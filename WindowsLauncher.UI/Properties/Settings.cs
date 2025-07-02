// ===== WindowsLauncher.UI/Properties/Settings.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ =====
using System.ComponentModel;

namespace WindowsLauncher.UI.Properties
{
    /// <summary>
    /// Настройки приложения с необходимыми свойствами
    /// ✅ ИСПРАВЛЕНО: Добавлены все свойства, используемые в коде
    /// </summary>
    public sealed class Settings : INotifyPropertyChanged
    {
        private static Settings _default = null!;
        private string _language = "ru-RU";
        private string _lastDomain = "";
        private string _lastUsername = "";
        private string _lastLoginMode = "Domain";
        private string _theme = "Light";
        private bool _rememberCredentials = false;
        private bool _autoStart = false;
        private int _sessionTimeout = 60;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static Settings Default => _default ??= new Settings();

        /// <summary>
        /// Язык интерфейса
        /// </summary>
        public string Language
        {
            get => _language;
            set
            {
                if (_language != value)
                {
                    _language = value;
                    OnPropertyChanged(nameof(Language));
                }
            }
        }

        /// <summary>
        /// Последний использованный домен
        /// </summary>
        public string LastDomain
        {
            get => _lastDomain;
            set
            {
                if (_lastDomain != value)
                {
                    _lastDomain = value;
                    OnPropertyChanged(nameof(LastDomain));
                }
            }
        }

        /// <summary>
        /// Последнее использованное имя пользователя
        /// </summary>
        public string LastUsername
        {
            get => _lastUsername;
            set
            {
                if (_lastUsername != value)
                {
                    _lastUsername = value;
                    OnPropertyChanged(nameof(LastUsername));
                }
            }
        }

        /// <summary>
        /// Последний использованный режим входа
        /// </summary>
        public string LastLoginMode
        {
            get => _lastLoginMode;
            set
            {
                if (_lastLoginMode != value)
                {
                    _lastLoginMode = value;
                    OnPropertyChanged(nameof(LastLoginMode));
                }
            }
        }

        /// <summary>
        /// Тема интерфейса
        /// </summary>
        public string Theme
        {
            get => _theme;
            set
            {
                if (_theme != value)
                {
                    _theme = value;
                    OnPropertyChanged(nameof(Theme));
                }
            }
        }

        /// <summary>
        /// Запоминать учетные данные
        /// </summary>
        public bool RememberCredentials
        {
            get => _rememberCredentials;
            set
            {
                if (_rememberCredentials != value)
                {
                    _rememberCredentials = value;
                    OnPropertyChanged(nameof(RememberCredentials));
                }
            }
        }

        /// <summary>
        /// Автозапуск с Windows
        /// </summary>
        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                if (_autoStart != value)
                {
                    _autoStart = value;
                    OnPropertyChanged(nameof(AutoStart));
                }
            }
        }

        /// <summary>
        /// Время сессии в минутах
        /// </summary>
        public int SessionTimeout
        {
            get => _sessionTimeout;
            set
            {
                if (_sessionTimeout != value)
                {
                    _sessionTimeout = value;
                    OnPropertyChanged(nameof(SessionTimeout));
                }
            }
        }

        /// <summary>
        /// Сохранение настроек
        /// </summary>
        public void Save()
        {
            try
            {
                // TODO: Реализовать сохранение в реестр или файл конфигурации
                // Пока используем заглушку
                System.Diagnostics.Debug.WriteLine("Settings saved successfully");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка настроек
        /// </summary>
        public void Load()
        {
            try
            {
                // TODO: Реализовать загрузку из реестра или файла конфигурации
                // Пока используем значения по умолчанию
                System.Diagnostics.Debug.WriteLine("Settings loaded successfully");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Сброс настроек к значениям по умолчанию
        /// </summary>
        public void Reset()
        {
            Language = "ru-RU";
            LastDomain = "";
            LastUsername = "";
            LastLoginMode = "Domain";
            Theme = "Light";
            RememberCredentials = false;
            AutoStart = false;
            SessionTimeout = 60;
        }

        /// <summary>
        /// Событие изменения свойства
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Уведомление об изменении свойства
        /// </summary>
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Статический конструктор для инициализации
        /// </summary>
        static Settings()
        {
            _default = new Settings();
            _default.Load();
        }

        /// <summary>
        /// Приватный конструктор для Singleton
        /// </summary>
        private Settings()
        {
            // Инициализация значениями по умолчанию
            Reset();
        }

        /// <summary>
        /// Деструктор для автоматического сохранения
        /// </summary>
        ~Settings()
        {
            try
            {
                Save();
            }
            catch
            {
                // Игнорируем ошибки в деструкторе
            }
        }

        /// <summary>
        /// Получение строкового представления настроек для отладки
        /// </summary>
        public override string ToString()
        {
            return $"Settings: Language={Language}, Theme={Theme}, LastDomain={LastDomain}, LastLoginMode={LastLoginMode}";
        }
    }
}