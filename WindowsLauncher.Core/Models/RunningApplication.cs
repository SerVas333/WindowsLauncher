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
        /// Создать RunningApplication из Application и Process с проверкой состояния процесса
        /// </summary>
        public static RunningApplication FromApplication(Application app, System.Diagnostics.Process process, string launchedBy)
        {
            // Безопасное получение информации о процессе с проверкой состояния
            string processName = app.Name; // Fallback на имя приложения
            IntPtr mainWindowHandle = IntPtr.Zero;
            string mainWindowTitle = string.Empty;
            DateTime startTime = DateTime.Now;
            bool isActive = false;
            bool isResponding = true;
            long memoryUsageMB = 0;

            try
            {
                // Проверяем что процесс еще не завершился
                if (!process.HasExited)
                {
                    processName = process.ProcessName;
                    mainWindowHandle = process.MainWindowHandle;
                    mainWindowTitle = process.MainWindowTitle ?? string.Empty;
                    startTime = process.StartTime;
                    isActive = process.MainWindowHandle != IntPtr.Zero;
                    isResponding = process.Responding;
                    memoryUsageMB = process.WorkingSet64 / 1024 / 1024;
                }
            }
            catch (InvalidOperationException)
            {
                // Процесс завершился между проверкой HasExited и обращением к свойствам
                // Используем fallback значения
            }

            return new RunningApplication
            {
                ApplicationId = app.Id,
                Name = app.Name,
                Description = app.Description,
                Category = app.Category,
                IconText = app.IconText,
                Type = app.Type,
                ProcessId = process.Id,
                ProcessName = processName,
                MainWindowHandle = mainWindowHandle,
                MainWindowTitle = mainWindowTitle,
                StartTime = startTime,
                LaunchedBy = launchedBy,
                ExecutablePath = app.ExecutablePath,
                Arguments = app.Arguments ?? string.Empty,
                WorkingDirectory = app.WorkingDirectory ?? string.Empty,
                IsActive = isActive,
                IsMinimized = false, // Будет определено позже через Windows API
                IsResponding = isResponding,
                MemoryUsageMB = memoryUsageMB,
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