using System;
using System.Collections.Generic;
using System.Diagnostics;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models.Lifecycle
{
    /// <summary>
    /// Представляет экземпляр запущенного приложения в системе управления жизненным циклом
    /// Содержит всю информацию о состоянии, процессе и окне приложения
    /// </summary>
    public class ApplicationInstance
    {
        #region Основная информация
        
        /// <summary>
        /// Уникальный идентификатор экземпляра приложения
        /// Формат: "{ApplicationId}_{ProcessId}_{Timestamp}"
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Модель приложения из базы данных
        /// </summary>
        public Application Application { get; set; } = null!;
        
        /// <summary>
        /// Пользователь, запустивший приложение
        /// </summary>
        public string LaunchedBy { get; set; } = string.Empty;
        
        /// <summary>
        /// Время запуска экземпляра
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// Время последнего обновления информации об экземпляре
        /// </summary>
        public DateTime LastUpdate { get; set; }
        
        #endregion

        #region Информация о процессе
        
        /// <summary>
        /// ID процесса приложения
        /// </summary>
        public int ProcessId { get; set; }
        
        /// <summary>
        /// Имя процесса
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;
        
        /// <summary>
        /// Использование памяти процессом в МБ
        /// </summary>
        public long MemoryUsageMB { get; set; }
        
        /// <summary>
        /// Отвечает ли процесс на запросы
        /// </summary>
        public bool IsResponding { get; set; } = true;
        
        #endregion

        #region Информация об окне
        
        /// <summary>
        /// Информация о главном окне приложения
        /// </summary>
        public WindowInfo? MainWindow { get; set; }
        
        /// <summary>
        /// Приложение активно (окно в фокусе)
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Приложение свернуто
        /// </summary>
        public bool IsMinimized { get; set; }
        
        #endregion

        #region Состояние и статус
        
        /// <summary>
        /// Текущее состояние экземпляра приложения
        /// </summary>
        public ApplicationState State { get; set; } = ApplicationState.Starting;
        
        /// <summary>
        /// Сообщение об ошибке (если есть)
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Время завершения экземпляра (если завершен)
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Является ли экземпляр виртуальным (без реального процесса)
        /// </summary>
        public bool IsVirtual { get; set; } = false;
        
        #endregion

        #region Специфичные данные для разных типов приложений
        
        /// <summary>
        /// Дополнительные метаданные для конкретных типов приложений
        /// Например, для Chrome Apps: ожидаемый заголовок окна, ключ приложения
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        /// <summary>
        /// Специальный ключ для Chrome Apps (для поддержки множественных экземпляров)
        /// Формат: "{ProcessId}_{AppName}_{Timestamp}"
        /// </summary>
        public string? ChromeAppKey 
        { 
            get => Metadata.ContainsKey("ChromeAppKey") ? Metadata["ChromeAppKey"]?.ToString() : null;
            set => Metadata["ChromeAppKey"] = value ?? string.Empty;
        }
        
        /// <summary>
        /// Ожидаемый заголовок окна (для Chrome Apps и web приложений)
        /// </summary>
        public string? ExpectedWindowTitle 
        { 
            get => Metadata.ContainsKey("ExpectedWindowTitle") ? Metadata["ExpectedWindowTitle"]?.ToString() : null;
            set => Metadata["ExpectedWindowTitle"] = value ?? string.Empty;
        }
        
        /// <summary>
        /// URL для web приложений
        /// </summary>
        public string? WebUrl 
        { 
            get => Metadata.ContainsKey("WebUrl") ? Metadata["WebUrl"]?.ToString() : null;
            set => Metadata["WebUrl"] = value ?? string.Empty;
        }
        
        /// <summary>
        /// Путь к папке (для Folder типа)
        /// </summary>
        public string? FolderPath 
        { 
            get => Metadata.ContainsKey("FolderPath") ? Metadata["FolderPath"]?.ToString() : null;
            set => Metadata["FolderPath"] = value ?? string.Empty;
        }
        
        #endregion

        #region Методы
        
        /// <summary>
        /// Обновить состояние экземпляра из Process объекта
        /// </summary>
        /// <param name="process">Process объект</param>
        public void UpdateFromProcess(Process? process)
        {
            if (process == null)
            {
                State = ApplicationState.Terminated;
                IsResponding = false;
                EndTime = DateTime.Now;
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    State = ApplicationState.Terminated;
                    IsResponding = false;
                    EndTime = process.ExitTime;
                    return;
                }

                ProcessName = process.ProcessName ?? ProcessName;
                IsResponding = process.Responding;
                MemoryUsageMB = process.WorkingSet64 / 1024 / 1024;
                
                // Обновляем информацию об окне если доступна
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    if (MainWindow == null)
                        MainWindow = new WindowInfo();
                        
                    MainWindow.Handle = process.MainWindowHandle;
                    MainWindow.Title = process.MainWindowTitle ?? string.Empty;
                    MainWindow.LastUpdate = DateTime.Now;
                }
                
                LastUpdate = DateTime.Now;
                
                // Определяем состояние на основе отзывчивости
                if (!IsResponding && State == ApplicationState.Running)
                {
                    State = ApplicationState.NotResponding;
                }
                else if (IsResponding && State == ApplicationState.NotResponding)
                {
                    State = ApplicationState.Running;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                State = ApplicationState.NotResponding;
                LastUpdate = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Создать экземпляр приложения из Application и Process
        /// </summary>
        /// <param name="application">Модель приложения</param>
        /// <param name="process">Process объект</param>
        /// <param name="launchedBy">Пользователь</param>
        /// <returns>Новый экземпляр приложения</returns>
        public static ApplicationInstance CreateFromProcess(Application application, Process process, string launchedBy)
        {
            var timestamp = DateTime.Now.Ticks;
            var instanceId = $"{application.Id}_{process.Id}_{timestamp}";
            
            var instance = new ApplicationInstance
            {
                InstanceId = instanceId,
                Application = application,
                LaunchedBy = launchedBy,
                StartTime = DateTime.Now,
                LastUpdate = DateTime.Now,
                ProcessId = process.Id,
                State = ApplicationState.Starting
            };
            
            // Обновляем из процесса
            instance.UpdateFromProcess(process);
            
            // Специфичные данные для Chrome Apps
            if (application.Type == ApplicationType.ChromeApp)
            {
                instance.ChromeAppKey = $"{process.Id}_{application.Name}_{timestamp}";
                
                // Извлекаем ожидаемый заголовок из аргументов --app
                instance.ExpectedWindowTitle = ExtractExpectedTitleFromChromeArgs(application);
            }
            
            // Специфичные данные для Web приложений
            if (application.Type == ApplicationType.Web)
            {
                instance.WebUrl = application.ExecutablePath; // URL хранится в ExecutablePath для Web типа
            }
            
            // Специфичные данные для папок
            if (application.Type == ApplicationType.Folder)
            {
                instance.FolderPath = application.ExecutablePath;
            }
            
            return instance;
        }
        
        /// <summary>
        /// Извлечь ожидаемый заголовок окна из аргументов Chrome --app
        /// </summary>
        /// <param name="application">Приложение</param>
        /// <returns>Ожидаемый заголовок или имя приложения</returns>
        private static string ExtractExpectedTitleFromChromeArgs(Application application)
        {
            try
            {
                var allArgs = $"{application.ExecutablePath ?? ""} {application.Arguments ?? ""}";
                
                // Ищем --app=file:/// или --app=http(s)://
                var appArgMatch = System.Text.RegularExpressions.Regex.Match(
                    allArgs, @"--app=(?:file:///|https?://)([^/\s]+(?:/[^\s]*)?)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (appArgMatch.Success)
                {
                    var url = appArgMatch.Groups[1].Value;
                    
                    // Для файлов извлекаем имя файла без расширения
                    if (url.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    {
                        return System.IO.Path.GetFileNameWithoutExtension(url);
                    }
                    
                    // Для URL возвращаем домен
                    return url.Split('/')[0];
                }
                
                // Fallback на имя приложения
                return application.Name;
            }
            catch
            {
                return application.Name;
            }
        }
        
        /// <summary>
        /// Проверить, является ли экземпляр активным (не завершенным)
        /// </summary>
        /// <returns>true если экземпляр активен</returns>
        public bool IsActiveInstance()
        {
            return State != ApplicationState.Terminated && State != ApplicationState.Closing;
        }
        
        /// <summary>
        /// Получить отображаемое имя экземпляра для UI
        /// </summary>
        /// <returns>Имя для отображения в интерфейсе</returns>
        public string GetDisplayName()
        {
            // For Chrome Apps with multiple instances, add instance number
            if (Application.Type == ApplicationType.ChromeApp && ChromeAppKey != null)
            {
                var parts = ChromeAppKey.Split('_');
                if (parts.Length >= 3)
                {
                    // Можно добавить логику для определения номера экземпляра
                    return Application.Name;
                }
            }
            
            return Application.Name;
        }
        
        /// <summary>
        /// Получить краткую информацию об экземпляре для логирования
        /// </summary>
        /// <returns>Строка с основной информацией</returns>
        public override string ToString()
        {
            return $"{Application.Name} (PID: {ProcessId}, State: {State}, User: {LaunchedBy})";
        }

        /// <summary>
        /// Генерирует уникальный ID экземпляра
        /// </summary>
        /// <param name="applicationId">ID приложения</param>
        /// <param name="processId">ID процесса</param>
        /// <returns>Уникальный ID экземпляра</returns>
        public static string GenerateInstanceId(int applicationId, int processId)
        {
            var timestamp = DateTime.Now.Ticks;
            return $"{applicationId}_{processId}_{timestamp}";
        }
        
        #endregion
    }
}