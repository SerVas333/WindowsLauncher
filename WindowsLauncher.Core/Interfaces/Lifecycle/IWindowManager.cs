using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models.Lifecycle;

namespace WindowsLauncher.Core.Interfaces.Lifecycle
{
    /// <summary>
    /// Интерфейс для управления окнами приложений через Windows API
    /// Абстрагирует работу с нативными функциями Windows для управления окнами
    /// </summary>
    public interface IWindowManager
    {
        #region Поиск окон
        
        /// <summary>
        /// Найти главное окно процесса
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="expectedTitle">Ожидаемый заголовок окна (опционально)</param>
        /// <param name="expectedClassName">Ожидаемый класс окна (опционально)</param>
        /// <returns>Информация о найденном окне или null</returns>
        Task<WindowInfo?> FindMainWindowAsync(int processId, string? expectedTitle = null, string? expectedClassName = null);
        
        /// <summary>
        /// Получить все окна процесса
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Массив всех окон процесса</returns>
        Task<WindowInfo[]> GetAllWindowsForProcessAsync(int processId);
        
        /// <summary>
        /// Найти окно по заголовку среди всех окон системы
        /// </summary>
        /// <param name="windowTitle">Заголовок окна</param>
        /// <param name="exactMatch">Точное совпадение или частичное</param>
        /// <returns>Информация о найденном окне или null</returns>
        Task<WindowInfo?> FindWindowByTitleAsync(string windowTitle, bool exactMatch = false);
        
        /// <summary>
        /// Найти окна по классу
        /// </summary>
        /// <param name="className">Класс окна</param>
        /// <returns>Список найденных окон</returns>
        Task<IReadOnlyList<WindowInfo>> FindWindowsByClassAsync(string className);
        
        #endregion

        #region Управление состоянием окон
        
        /// <summary>
        /// Переключиться на окно (активировать)
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если переключение успешно</returns>
        Task<bool> SwitchToWindowAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Свернуть окно
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если операция успешна</returns>
        Task<bool> MinimizeWindowAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Развернуть окно
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если операция успешна</returns>
        Task<bool> RestoreWindowAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Максимизировать окно
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если операция успешна</returns>
        Task<bool> MaximizeWindowAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Закрыть окно корректно (отправить WM_CLOSE)
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если сообщение отправлено</returns>
        Task<bool> CloseWindowAsync(IntPtr windowHandle);
        
        #endregion

        #region Получение информации об окне
        
        /// <summary>
        /// Обновить информацию об окне
        /// </summary>
        /// <param name="windowInfo">Существующая информация об окне</param>
        /// <returns>Обновленная информация или null если окно не найдено</returns>
        Task<WindowInfo?> RefreshWindowInfoAsync(WindowInfo windowInfo);
        
        /// <summary>
        /// Проверить, существует ли окно
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если окно существует</returns>
        Task<bool> IsWindowValidAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Проверить, видимо ли окно
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если окно видимо</returns>
        Task<bool> IsWindowVisibleAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Проверить, свернуто ли окно
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если окно свернуто</returns>
        Task<bool> IsWindowMinimizedAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Проверить, находится ли окно в фокусе
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если окно активно</returns>
        Task<bool> IsWindowActiveAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Получить заголовок окна
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>Заголовок окна или пустая строка</returns>
        Task<string> GetWindowTitleAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Получить класс окна
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>Класс окна или пустая строка</returns>
        Task<string> GetWindowClassAsync(IntPtr windowHandle);
        
        #endregion

        #region Специальные операции
        
        /// <summary>
        /// Принудительно переместить окно на передний план с обходом ограничений Windows
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если операция успешна</returns>
        Task<bool> ForceToForegroundAsync(IntPtr windowHandle);
        
        /// <summary>
        /// Найти окно через енумерацию всех окон системы (медленно, но надежно)
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="titleFilter">Фильтр по заголовку (опционально)</param>
        /// <returns>Первое найденное окно или null</returns>
        Task<WindowInfo?> EnumerateAndFindWindowAsync(int processId, string? titleFilter = null);
        
        #endregion
    }
}