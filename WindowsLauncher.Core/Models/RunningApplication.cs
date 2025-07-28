using System;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Представляет запущенное из лаунчера приложение
    /// </summary>
    public class RunningApplication
    {
        /// <summary>
        /// Идентификатор приложения в базе данных
        /// </summary>
        public int ApplicationId { get; set; }

        /// <summary>
        /// Название приложения
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Описание приложения
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Категория приложения
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Иконка приложения (emoji или путь к файлу)
        /// </summary>
        public string IconText { get; set; } = "📱";

        /// <summary>
        /// Тип приложения
        /// </summary>
        public ApplicationType Type { get; set; }

        /// <summary>
        /// ID процесса
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Имя процесса
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Handle главного окна процесса
        /// </summary>
        public IntPtr MainWindowHandle { get; set; }

        /// <summary>
        /// Заголовок главного окна
        /// </summary>
        public string MainWindowTitle { get; set; } = string.Empty;

        /// <summary>
        /// Время запуска приложения
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Пользователь, запустивший приложение
        /// </summary>
        public string LaunchedBy { get; set; } = string.Empty;

        /// <summary>
        /// Путь к исполняемому файлу
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Аргументы командной строки
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// Рабочая директория
        /// </summary>
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Приложение активно (окно видимо)
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Приложение свернуто
        /// </summary>
        public bool IsMinimized { get; set; }

        /// <summary>
        /// Приложение отвечает на запросы
        /// </summary>
        public bool IsResponding { get; set; } = true;

        /// <summary>
        /// Использование памяти (в МБ)
        /// </summary>
        public long MemoryUsageMB { get; set; }

        /// <summary>
        /// Время последнего обновления статуса
        /// </summary>
        public DateTime LastStatusUpdate { get; set; }

        public override string ToString()
        {
            return $"{Name} (PID: {ProcessId})";
        }

        /// <summary>
        /// Создать RunningApplication из Application и Process
        /// </summary>
        public static RunningApplication FromApplication(Application app, System.Diagnostics.Process process, string launchedBy)
        {
            return new RunningApplication
            {
                ApplicationId = app.Id,
                Name = app.Name,
                Description = app.Description,
                Category = app.Category,
                IconText = app.IconText,
                Type = app.Type,
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                MainWindowHandle = process.MainWindowHandle,
                MainWindowTitle = process.MainWindowTitle ?? string.Empty,
                StartTime = process.StartTime,
                LaunchedBy = launchedBy,
                ExecutablePath = app.ExecutablePath,
                Arguments = app.Arguments ?? string.Empty,
                WorkingDirectory = app.WorkingDirectory ?? string.Empty,
                IsActive = !process.HasExited && process.MainWindowHandle != IntPtr.Zero,
                IsMinimized = false, // Будет определено позже через Windows API
                IsResponding = process.Responding,
                MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
                LastStatusUpdate = DateTime.Now
            };
        }

        /// <summary>
        /// Обновить статус из Process
        /// </summary>
        public void UpdateFromProcess(System.Diagnostics.Process process)
        {
            ProcessName = process.ProcessName;
            MainWindowHandle = process.MainWindowHandle;
            MainWindowTitle = process.MainWindowTitle ?? string.Empty;
            IsActive = !process.HasExited && process.MainWindowHandle != IntPtr.Zero;
            IsResponding = process.Responding;
            MemoryUsageMB = process.WorkingSet64 / 1024 / 1024;
            LastStatusUpdate = DateTime.Now;
        }
    }
}