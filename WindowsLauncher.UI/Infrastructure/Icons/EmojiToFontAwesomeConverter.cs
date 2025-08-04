// ===== WindowsLauncher.UI/Infrastructure/Icons/EmojiToFontAwesomeConverter.cs =====
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FontAwesome.WPF;

namespace WindowsLauncher.UI.Infrastructure.Icons
{
    /// <summary>
    /// XAML конвертер для преобразования emoji строк в FontAwesome иконки
    /// Использование: {Binding IconText, Converter={StaticResource EmojiToFontAwesomeConverter}}
    /// </summary>
    public class EmojiToFontAwesomeConverter : IValueConverter
    {
        /// <summary>
        /// Конвертирует emoji строку в ImageAwesome элемент для отображения FontAwesome иконки
        /// </summary>
        /// <param name="value">Emoji строка</param>
        /// <param name="targetType">Целевой тип (должен быть FrameworkElement)</param>
        /// <param name="parameter">Дополнительные параметры в формате "size:color" или "size"</param>
        /// <param name="culture">Культура</param>
        /// <returns>ImageAwesome элемент или исходный emoji текст если маппинг не найден</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string emojiText || string.IsNullOrEmpty(emojiText))
                return DependencyProperty.UnsetValue;

            // Парсим параметр для размера и цвета
            var size = 16.0;
            Brush? foreground = null;

            if (parameter is string parameterString && !string.IsNullOrEmpty(parameterString))
            {
                var parts = parameterString.Split(':');
                
                // Первый параметр - размер
                if (parts.Length > 0 && double.TryParse(parts[0], out var parsedSize))
                {
                    size = parsedSize;
                }

                // Второй параметр - цвет
                if (parts.Length > 1)
                {
                    var colorName = parts[1];
                    try
                    {
                        // Пробуем найти цвет в ресурсах приложения
                        var resource = Application.Current?.FindResource(colorName);
                        if (resource is Brush brush)
                        {
                            foreground = brush;
                        }
                        else
                        {
                            // Пробуем парсить как системный цвет
                            var converter = new BrushConverter();
                            foreground = converter.ConvertFromString(colorName) as Brush;
                        }
                    }
                    catch
                    {
                        // Если не удалось парсить цвет, используем текущий foreground
                        foreground = null;
                    }
                }
            }

            // Пробуем получить FontAwesome иконку
            var iconService = FontAwesomeIconService.Instance;
            var fontAwesomeIcon = iconService.GetFontAwesomeIcon(emojiText);

            if (fontAwesomeIcon.HasValue)
            {
                // Создаем ImageAwesome элемент
                var imageAwesome = new ImageAwesome
                {
                    Icon = fontAwesomeIcon.Value,
                    Width = size,
                    Height = size,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Устанавливаем цвет если указан
                if (foreground != null)
                {
                    imageAwesome.Foreground = foreground;
                }

                return imageAwesome;
            }

            // Если маппинг не найден, возвращаем исходный emoji как TextBlock
            return new System.Windows.Controls.TextBlock
            {
                Text = emojiText,
                FontSize = size,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = System.Windows.TextAlignment.Center
            };
        }

        /// <summary>
        /// Обратное преобразование не поддерживается
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("Обратное преобразование FontAwesome иконки в emoji не поддерживается");
        }
    }

    /// <summary>
    /// Конвертер для получения только FontAwesome иконки (enum значения) без создания UI элемента
    /// Использование для привязки к свойству Icon элемента ImageAwesome
    /// </summary>
    public class EmojiToFontAwesomeIconConverter : IValueConverter
    {
        /// <summary>
        /// Конвертирует emoji строку в значение FontAwesome иконки
        /// </summary>
        /// <param name="value">Emoji строка</param>
        /// <param name="targetType">Целевой тип (должен быть FontAwesomeIcon)</param>
        /// <param name="parameter">Не используется</param>
        /// <param name="culture">Культура</param>
        /// <returns>Значение FontAwesome иконки или FontAwesome.Question если не найден</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string emojiText || string.IsNullOrEmpty(emojiText))
                return FontAwesomeIcon.Question;

            var iconService = FontAwesomeIconService.Instance;
            var fontAwesomeIcon = iconService.GetFontAwesomeIcon(emojiText);

            return fontAwesomeIcon ?? FontAwesomeIcon.Question;
        }

        /// <summary>
        /// Обратное преобразование не поддерживается
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("Обратное преобразование FontAwesome иконки в emoji не поддерживается");
        }
    }

    /// <summary>
    /// Конвертер для создания ImageSource из emoji строки
    /// Полезен для элементов Image
    /// </summary>
    public class EmojiToImageSourceConverter : IValueConverter
    {
        /// <summary>
        /// Конвертирует emoji строку в ImageSource для FontAwesome иконки
        /// </summary>
        /// <param name="value">Emoji строка</param>
        /// <param name="targetType">Целевой тип (должен быть ImageSource)</param>
        /// <param name="parameter">Размер иконки (по умолчанию 16)</param>
        /// <param name="culture">Культура</param>
        /// <returns>ImageSource или null если маппинг не найден</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string emojiText || string.IsNullOrEmpty(emojiText))
                return DependencyProperty.UnsetValue;

            var size = 16.0;
            if (parameter is string parameterString && double.TryParse(parameterString, out var parsedSize))
            {
                size = parsedSize;
            }

            var iconService = FontAwesomeIconService.Instance;
            var imageSource = iconService.CreateImageSourceFromEmoji(emojiText, Brushes.Black, size);

            return imageSource ?? DependencyProperty.UnsetValue;
        }

        /// <summary>
        /// Обратное преобразование не поддерживается
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("Обратное преобразование ImageSource в emoji не поддерживается");
        }
    }

    /// <summary>
    /// Специальный конвертер для иконок размером 32px
    /// Используется в MainWindow для кнопок управления
    /// </summary>
    public class EmojiToFontAwesome32Converter : IValueConverter
    {
        /// <summary>
        /// Конвертирует emoji строку в ImageAwesome элемент размером 32px
        /// </summary>
        /// <param name="value">Emoji строка</param>
        /// <param name="targetType">Целевой тип</param>
        /// <param name="parameter">Не используется</param>
        /// <param name="culture">Культура</param>
        /// <returns>ImageAwesome элемент 32px или исходный emoji текст</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string emojiText || string.IsNullOrEmpty(emojiText))
                return DependencyProperty.UnsetValue;

            // Используем размер 16px для 32px кнопок (пропорционально)
            var size = 16.0;

            var iconService = FontAwesomeIconService.Instance;
            var fontAwesomeIcon = iconService.GetFontAwesomeIcon(emojiText);

            if (fontAwesomeIcon.HasValue)
            {
                return new ImageAwesome
                {
                    Icon = fontAwesomeIcon.Value,
                    Width = size,
                    Height = size,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Brushes.Gray // Цвет по умолчанию для кнопок
                };
            }

            // Возвращаем исходный emoji как fallback
            return new System.Windows.Controls.TextBlock
            {
                Text = emojiText,
                FontSize = size,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = System.Windows.TextAlignment.Center
            };
        }

        /// <summary>
        /// Обратное преобразование не поддерживается
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("Обратное преобразование FontAwesome иконки в emoji не поддерживается");
        }
    }
}