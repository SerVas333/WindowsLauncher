using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WindowsLauncher.UI.Components.AppSwitcher
{
    /// <summary>
    /// Конвертер для преобразования bool в строку для Tag
    /// </summary>
    public class BooleanToStringConverter : IValueConverter
    {
        public static BooleanToStringConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter is string paramString)
            {
                return paramString;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для определения цвета статуса приложения
    /// </summary>
    public class StatusColorConverter : IValueConverter
    {
        public static StatusColorConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AppSwitcherItem item)
            {
                if (!item.IsResponding)
                {
                    return Colors.Red; // Не отвечает
                }
                else if (item.IsMinimized)
                {
                    return Colors.Orange; // Свернуто
                }
                else
                {
                    return Colors.Green; // Активно
                }
            }
            
            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для текста статуса приложения
    /// </summary>
    public class StatusTextConverter : IValueConverter
    {
        public static StatusTextConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMinimized)
            {
                return isMinimized ? "Свернуто" : "Активно";
            }
            
            return "Неизв.";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для преобразования bool в Visibility
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public static BooleanToVisibilityConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }
}