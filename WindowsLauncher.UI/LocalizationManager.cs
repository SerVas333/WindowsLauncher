using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using WindowsLauncher.UI.Properties.Resources;

namespace WindowsLauncher.UI
{
    public static class LocalizationManager
    {
        public static event EventHandler? LanguageChanged;

        public static void SetLanguage(string culture)
        {
            try
            {
                var cultureInfo = new CultureInfo(culture);

                // Устанавливаем культуру для текущего потока
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                Thread.CurrentThread.CurrentUICulture = cultureInfo;

                // Устанавливаем культуру по умолчанию для новых потоков
                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

                // Обновляем ресурсы
                Resources.Culture = cultureInfo;

                LanguageChanged?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting language: {ex.Message}", "Language Error");
            }
        }

        public static void InitializeLanguage()
        {
            // По умолчанию русский язык
            SetLanguage("ru-RU");
        }

        // Удобные методы для получения локализованных строк
        public static string GetString(string key, params object[] args)
        {
            try
            {
                var value = Resources.ResourceManager.GetString(key, Resources.Culture);
                return args.Length > 0 && !string.IsNullOrEmpty(value)
                    ? string.Format(value, args)
                    : value ?? key;
            }
            catch
            {
                return key;
            }
        }
    }
}