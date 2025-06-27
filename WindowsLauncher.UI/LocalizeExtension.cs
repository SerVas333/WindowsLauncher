// ===== WindowsLauncher.UI/LocalizeExtension.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ =====
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using WindowsLauncher.UI.Properties.Resources;

namespace WindowsLauncher.UI
{
    /// <summary>
    /// Markup extension для локализации в XAML
    /// Использование: {local:Localize KeyName}
    /// </summary>
    [MarkupExtensionReturnType(typeof(BindingExpression))]
    public class LocalizeExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocalizeExtension()
        {
            Key = string.Empty;
        }

        public LocalizeExtension(string key)
        {
            Key = key ?? string.Empty;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return "[Missing Key]";

            // Создаем binding к локализованной строке
            var binding = new Binding("Value")
            {
                Source = new LocalizedString(Key),
                Mode = BindingMode.OneWay
            };

            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target)
            {
                // Если мы можем создать binding, делаем это
                if (target.TargetObject is DependencyObject && target.TargetProperty is DependencyProperty)
                {
                    return binding.ProvideValue(serviceProvider);
                }
            }

            // Fallback - возвращаем статическое значение
            return GetLocalizedValue(Key);
        }

        private static string GetLocalizedValue(string key)
        {
            try
            {
                var value = Resources.ResourceManager.GetString(key, Resources.Culture);
                return value ?? $"[{key}]";
            }
            catch
            {
                return $"[{key}]";
            }
        }
    }

    /// <summary>
    /// Объект для привязки к локализованным строкам с автообновлением
    /// </summary>
    public class LocalizedString : INotifyPropertyChanged
    {
        private readonly string _key;

        public LocalizedString(string key)
        {
            _key = key ?? string.Empty;

            // Подписываемся на изменения языка
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        public string Value
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(_key))
                        return "[Empty Key]";

                    var value = Resources.ResourceManager.GetString(_key, Resources.Culture);
                    return value ?? $"[{_key}]";
                }
                catch (Exception ex)
                {
                    return $"[Error: {ex.Message}]";
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // Уведомляем UI об изменении значения
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }

        // Важно отписаться от события для предотвращения утечек памяти
        ~LocalizedString()
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }
    }

    /// <summary>
    /// Альтернативное решение - статический converter
    /// </summary>
    public class LocalizationConverter : IValueConverter
    {
        public static readonly LocalizationConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (parameter is string key && !string.IsNullOrEmpty(key))
            {
                try
                {
                    var localizedValue = Resources.ResourceManager.GetString(key, Resources.Culture);
                    return localizedValue ?? $"[{key}]";
                }
                catch
                {
                    return $"[{key}]";
                }
            }

            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

// ===== ДОПОЛНИТЕЛЬНО: Простой статический помощник =====
namespace WindowsLauncher.UI
{
    /// <summary>
    /// Статический помощник для получения локализованных строк
    /// </summary>
    public static class Localization
    {
        public static string Get(string key)
        {
            try
            {
                var value = Resources.ResourceManager.GetString(key, Resources.Culture);
                return value ?? key;
            }
            catch
            {
                return key;
            }
        }

        public static string Get(string key, params object[] args)
        {
            try
            {
                var value = Resources.ResourceManager.GetString(key, Resources.Culture);
                if (value != null && args.Length > 0)
                {
                    return string.Format(value, args);
                }
                return value ?? key;
            }
            catch
            {
                return key;
            }
        }
    }
}