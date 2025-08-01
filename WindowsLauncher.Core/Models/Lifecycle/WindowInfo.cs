using System;

namespace WindowsLauncher.Core.Models.Lifecycle
{
    /// <summary>
    /// Информация об окне приложения, полученная через Windows API
    /// Содержит все необходимые данные для управления окном
    /// </summary>
    public class WindowInfo
    {
        #region Основная информация об окне
        
        /// <summary>
        /// Handle окна (уникальный идентификатор окна в Windows)
        /// </summary>
        public IntPtr Handle { get; set; } = IntPtr.Zero;
        
        /// <summary>
        /// Заголовок окна
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Класс окна (Window Class Name)
        /// </summary>
        public string ClassName { get; set; } = string.Empty;
        
        /// <summary>
        /// ID процесса, которому принадлежит окно
        /// </summary>
        public uint ProcessId { get; set; }
        
        /// <summary>
        /// ID потока, создавшего окно
        /// </summary>
        public uint ThreadId { get; set; }
        
        #endregion

        #region Состояние окна
        
        /// <summary>
        /// Окно видимо пользователю
        /// </summary>
        public bool IsVisible { get; set; }
        
        /// <summary>
        /// Окно свернуто
        /// </summary>
        public bool IsMinimized { get; set; }
        
        /// <summary>
        /// Окно максимизировано
        /// </summary>
        public bool IsMaximized { get; set; }
        
        /// <summary>
        /// Окно активно (в фокусе)
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Окно включено (может получать ввод пользователя)
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Окно отвечает на запросы
        /// </summary>
        public bool IsResponding { get; set; } = true;
        
        #endregion

        #region Геометрия окна
        
        /// <summary>
        /// Позиция окна по X
        /// </summary>
        public int X { get; set; }
        
        /// <summary>
        /// Позиция окна по Y
        /// </summary>
        public int Y { get; set; }
        
        /// <summary>
        /// Ширина окна
        /// </summary>
        public int Width { get; set; }
        
        /// <summary>
        /// Высота окна
        /// </summary>
        public int Height { get; set; }
        
        #endregion

        #region Метаданные
        
        /// <summary>
        /// Время создания/обнаружения окна
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Время последнего обновления информации об окне
        /// </summary>
        public DateTime LastUpdate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Дополнительная информация об окне (для отладки)
        /// </summary>
        public string? AdditionalInfo { get; set; }
        
        #endregion

        #region Статические методы создания
        
        /// <summary>
        /// Создать WindowInfo с минимальной информацией
        /// </summary>
        /// <param name="handle">Handle окна</param>
        /// <param name="title">Заголовок окна</param>
        /// <param name="processId">ID процесса</param>
        /// <returns>Новый объект WindowInfo</returns>
        public static WindowInfo Create(IntPtr handle, string title = "", uint processId = 0)
        {
            return new WindowInfo
            {
                Handle = handle,
                Title = title,
                ProcessId = processId,
                CreatedAt = DateTime.Now,
                LastUpdate = DateTime.Now
            };
        }
        
        /// <summary>
        /// Создать WindowInfo с полной информацией
        /// </summary>
        /// <param name="handle">Handle окна</param>
        /// <param name="title">Заголовок окна</param>
        /// <param name="className">Класс окна</param>
        /// <param name="processId">ID процесса</param>
        /// <param name="threadId">ID потока</param>
        /// <param name="isVisible">Видимость окна</param>
        /// <returns>Новый объект WindowInfo</returns>
        public static WindowInfo CreateDetailed(
            IntPtr handle, 
            string title, 
            string className, 
            uint processId, 
            uint threadId, 
            bool isVisible)
        {
            return new WindowInfo
            {
                Handle = handle,
                Title = title,
                ClassName = className,
                ProcessId = processId,
                ThreadId = threadId,
                IsVisible = isVisible,
                CreatedAt = DateTime.Now,
                LastUpdate = DateTime.Now
            };
        }
        
        #endregion

        #region Методы обновления
        
        /// <summary>
        /// Обновить заголовок окна
        /// </summary>
        /// <param name="newTitle">Новый заголовок</param>
        public void UpdateTitle(string newTitle)
        {
            if (Title != newTitle)
            {
                Title = newTitle;
                LastUpdate = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Обновить состояние видимости окна
        /// </summary>
        /// <param name="isVisible">Новое состояние видимости</param>
        /// <param name="isMinimized">Свернуто ли окно</param>
        /// <param name="isMaximized">Максимизировано ли окно</param>
        public void UpdateVisibility(bool isVisible, bool isMinimized = false, bool isMaximized = false)
        {
            if (IsVisible != isVisible || IsMinimized != isMinimized || IsMaximized != isMaximized)
            {
                IsVisible = isVisible;
                IsMinimized = isMinimized;
                IsMaximized = isMaximized;
                LastUpdate = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Обновить позицию и размер окна
        /// </summary>
        /// <param name="x">Позиция X</param>
        /// <param name="y">Позиция Y</param>
        /// <param name="width">Ширина</param>
        /// <param name="height">Высота</param>
        public void UpdateGeometry(int x, int y, int width, int height)
        {
            if (X != x || Y != y || Width != width || Height != height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                LastUpdate = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Обновить активность окна
        /// </summary>
        /// <param name="isActive">Активно ли окно</param>
        public void UpdateActivity(bool isActive)
        {
            if (IsActive != isActive)
            {
                IsActive = isActive;
                LastUpdate = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Обновить отзывчивость окна
        /// </summary>
        /// <param name="isResponding">Отвечает ли окно</param>
        public void UpdateResponding(bool isResponding)
        {
            if (IsResponding != isResponding)
            {
                IsResponding = isResponding;
                LastUpdate = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Отметить время обновления информации об окне
        /// </summary>
        public void MarkUpdated()
        {
            LastUpdate = DateTime.Now;
        }
        
        #endregion

        #region Методы проверки
        
        /// <summary>
        /// Проверить, валиден ли handle окна
        /// </summary>
        /// <returns>true если handle не равен IntPtr.Zero</returns>
        public bool IsValidHandle()
        {
            return Handle != IntPtr.Zero;
        }
        
        /// <summary>
        /// Проверить, устарела ли информация об окне
        /// </summary>
        /// <param name="maxAge">Максимальный возраст информации</param>
        /// <returns>true если информация устарела</returns>
        public bool IsStale(TimeSpan maxAge)
        {
            return DateTime.Now - LastUpdate > maxAge;
        }
        
        /// <summary>
        /// Проверить, является ли окно главным окном приложения
        /// </summary>
        /// <returns>true если окно может быть главным</returns>
        public bool IsLikelyMainWindow()
        {
            return IsVisible && 
                   !string.IsNullOrEmpty(Title) && 
                   !IsMinimized &&
                   Width > 0 && 
                   Height > 0;
        }
        
        #endregion

        #region Переопределенные методы
        
        /// <summary>
        /// Строковое представление окна для отладки
        /// </summary>
        /// <returns>Строка с информацией об окне</returns>
        public override string ToString()
        {
            return $"Window(Handle={Handle:X}, Title='{Title}', Class='{ClassName}', PID={ProcessId}, Visible={IsVisible})";
        }
        
        /// <summary>
        /// Проверка равенства окон по handle
        /// </summary>
        /// <param name="obj">Объект для сравнения</param>
        /// <returns>true если окна равны</returns>
        public override bool Equals(object? obj)
        {
            if (obj is WindowInfo other)
            {
                return Handle == other.Handle;
            }
            return false;
        }
        
        /// <summary>
        /// Хэш-код окна на основе handle
        /// </summary>
        /// <returns>Хэш-код</returns>
        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }
        
        #endregion
    }
}