// ===== WindowsLauncher.UI/Infrastructure/Icons/FontAwesomeIconService.cs =====
using System;
using System.Collections.Generic;
using System.Windows;
using FontAwesome.WPF;

namespace WindowsLauncher.UI.Infrastructure.Icons
{
    /// <summary>
    /// Сервис для управления иконками FontAwesome и маппинга emoji на векторные иконки
    /// </summary>
    public class FontAwesomeIconService
    {
        private static FontAwesomeIconService? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Маппинг emoji иконок на FontAwesome иконки
        /// </summary>
        private static readonly Dictionary<string, FontAwesomeIcon> EmojiToFontAwesome = new()
        {
            // Основные системные иконки
            {"👤", FontAwesomeIcon.User},
            {"🛠️", FontAwesomeIcon.Wrench},
            {"⚙️", FontAwesomeIcon.Cog},
            {"🔄", FontAwesomeIcon.Refresh},
            {"🚪", FontAwesomeIcon.SignOut},

            // Приложения
            {"📱", FontAwesomeIcon.MobilePhone},
            {"🌐", FontAwesomeIcon.Globe},
            {"💻", FontAwesomeIcon.Terminal},
            {"📝", FontAwesomeIcon.Edit},
            {"📋", FontAwesomeIcon.Clipboard},
            {"🔑", FontAwesomeIcon.Key},

            // Действия
            {"✏️", FontAwesomeIcon.Pencil},
            {"🗑️", FontAwesomeIcon.Trash},
            {"💾", FontAwesomeIcon.Save},
            {"📁", FontAwesomeIcon.FolderOpen},
            {"🔍", FontAwesomeIcon.Search},
            {"🔧", FontAwesomeIcon.Wrench},
            {"⏳", FontAwesomeIcon.Refresh},

            // Клавиатура (контурная иконка клавиатуры)
            {"⌨️", FontAwesomeIcon.KeyboardOutline},

            // Статусы и предупреждения
            {"⚠️", FontAwesomeIcon.Warning},
            {"✅", FontAwesomeIcon.Check},
            {"❌", FontAwesomeIcon.Times},
            {"ℹ️", FontAwesomeIcon.InfoCircle},

            // Дополнительные
            {"🗄️", FontAwesomeIcon.Database},
            {"📊", FontAwesomeIcon.BarChart}
        };

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static FontAwesomeIconService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new FontAwesomeIconService();
                    }
                }
                return _instance;
            }
        }

        private FontAwesomeIconService() { }

        /// <summary>
        /// Получить FontAwesome иконку по emoji строке
        /// </summary>
        /// <param name="emojiText">Emoji строка</param>
        /// <returns>FontAwesome иконка или null если не найдена</returns>
        public FontAwesomeIcon? GetFontAwesomeIcon(string emojiText)
        {
            if (string.IsNullOrEmpty(emojiText))
                return null;

            return EmojiToFontAwesome.TryGetValue(emojiText, out var icon) ? icon : null;
        }

        /// <summary>
        /// Создать ImageSource для FontAwesome иконки
        /// </summary>
        /// <param name="icon">FontAwesome иконка</param>
        /// <param name="foreground">Цвет иконки</param>
        /// <param name="size">Размер иконки</param>
        /// <returns>ImageSource для использования в Image элементах</returns>
        public System.Windows.Media.ImageSource CreateImageSource(FontAwesomeIcon icon, System.Windows.Media.Brush? foreground = null, double size = 16)
        {
            return ImageAwesome.CreateImageSource(icon, foreground ?? System.Windows.Media.Brushes.Black, size);
        }

        /// <summary>
        /// Создать ImageSource для FontAwesome иконки по emoji строке
        /// </summary>
        /// <param name="emojiText">Emoji строка</param>
        /// <param name="foreground">Цвет иконки</param>
        /// <param name="size">Размер иконки</param>
        /// <returns>ImageSource или null если emoji не найден</returns>
        public System.Windows.Media.ImageSource? CreateImageSourceFromEmoji(string emojiText, System.Windows.Media.Brush? foreground = null, double size = 16)
        {
            var icon = GetFontAwesomeIcon(emojiText);
            return icon.HasValue ? CreateImageSource(icon.Value, foreground, size) : null;
        }

        /// <summary>
        /// Создать ImageAwesome элемент для прямого использования в XAML
        /// </summary>
        /// <param name="icon">FontAwesome иконка</param>
        /// <param name="foreground">Цвет иконки</param>
        /// <param name="size">Размер иконки</param>
        /// <returns>ImageAwesome элемент</returns>
        public ImageAwesome CreateImageAwesome(FontAwesomeIcon icon, System.Windows.Media.Brush? foreground = null, double size = 16)
        {
            return new ImageAwesome
            {
                Icon = icon,
                Foreground = foreground ?? System.Windows.Media.Brushes.Black,
                Width = size,
                Height = size
            };
        }

        /// <summary>
        /// Создать ImageAwesome элемент по emoji строке
        /// </summary>
        /// <param name="emojiText">Emoji строка</param>
        /// <param name="foreground">Цвет иконки</param>
        /// <param name="size">Размер иконки</param>
        /// <returns>ImageAwesome элемент или null если emoji не найден</returns>
        public ImageAwesome? CreateImageAwesomeFromEmoji(string emojiText, System.Windows.Media.Brush? foreground = null, double size = 16)
        {
            var icon = GetFontAwesomeIcon(emojiText);
            return icon.HasValue ? CreateImageAwesome(icon.Value, foreground, size) : null;
        }

        /// <summary>
        /// Проверить, доступен ли маппинг для emoji строки
        /// </summary>
        /// <param name="emojiText">Emoji строка</param>
        /// <returns>True если маппинг существует</returns>
        public bool HasMapping(string emojiText)
        {
            return !string.IsNullOrEmpty(emojiText) && EmojiToFontAwesome.ContainsKey(emojiText);
        }

        /// <summary>
        /// Получить все доступные маппинги emoji → FontAwesome
        /// </summary>
        /// <returns>Словарь маппингов</returns>
        public IReadOnlyDictionary<string, FontAwesomeIcon> GetAllMappings()
        {
            return EmojiToFontAwesome;
        }

        /// <summary>
        /// Добавить новый маппинг emoji → FontAwesome
        /// </summary>
        /// <param name="emojiText">Emoji строка</param>
        /// <param name="icon">FontAwesome иконка</param>
        public void AddMapping(string emojiText, FontAwesomeIcon icon)
        {
            if (!string.IsNullOrEmpty(emojiText))
            {
                EmojiToFontAwesome[emojiText] = icon;
            }
        }

        /// <summary>
        /// Удалить маппинг для emoji строки
        /// </summary>
        /// <param name="emojiText">Emoji строка</param>
        /// <returns>True если маппинг был удален</returns>
        public bool RemoveMapping(string emojiText)
        {
            return !string.IsNullOrEmpty(emojiText) && EmojiToFontAwesome.Remove(emojiText);
        }
    }
}