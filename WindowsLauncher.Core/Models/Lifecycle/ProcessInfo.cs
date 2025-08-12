using System;
using System.Collections.Generic;

namespace WindowsLauncher.Core.Models.Lifecycle
{
    /// <summary>
    /// Подробная информация о процессе, собранная через безопасные методы мониторинга
    /// Используется для передачи данных между компонентами без прямой работы с Process объектами
    /// </summary>
    public class ProcessInfo
    {
        #region Основная информация о процессе
        
        /// <summary>
        /// ID процесса
        /// </summary>
        public int ProcessId { get; set; }
        
        /// <summary>
        /// Имя процесса (без расширения .exe)
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;
        
        /// <summary>
        /// Полный путь к исполняемому файлу процесса
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Аргументы командной строки процесса
        /// </summary>
        public string CommandLine { get; set; } = string.Empty;
        
        /// <summary>
        /// Рабочая директория процесса
        /// </summary>
        public string WorkingDirectory { get; set; } = string.Empty;
        
        #endregion

        #region Состояние процесса
        
        /// <summary>
        /// Процесс существует и не завершен
        /// </summary>
        public bool IsAlive { get; set; }
        
        /// <summary>
        /// Процесс отвечает на запросы
        /// </summary>
        public bool IsResponding { get; set; } = true;
        
        /// <summary>
        /// Процесс работает (алиас для IsAlive для совместимости с тестами)
        /// </summary>
        public bool IsRunning => IsAlive;
        
        /// <summary>
        /// Процесс завершился
        /// </summary>
        public bool HasExited { get; set; }
        
        /// <summary>
        /// Код завершения процесса (если завершен)
        /// </summary>
        public int? ExitCode { get; set; }
        
        /// <summary>
        /// Время запуска процесса
        /// </summary>
        public DateTime? StartTime { get; set; }
        
        /// <summary>
        /// Время завершения процесса (если завершен)
        /// </summary>
        public DateTime? ExitTime { get; set; }
        
        /// <summary>
        /// Общее время работы процесса
        /// </summary>
        public TimeSpan? TotalProcessorTime { get; set; }
        
        #endregion

        #region Использование ресурсов
        
        /// <summary>
        /// Использование оперативной памяти в байтах
        /// </summary>
        public long WorkingSetMemory { get; set; }
        
        /// <summary>
        /// Пиковое использование памяти в байтах
        /// </summary>
        public long PeakWorkingSetMemory { get; set; }
        
        /// <summary>
        /// Виртуальная память в байтах
        /// </summary>
        public long VirtualMemory { get; set; }
        
        /// <summary>
        /// Приватная память в байтах
        /// </summary>
        public long PrivateMemory { get; set; }
        
        /// <summary>
        /// Использование процессора (в процентах, 0-100)
        /// </summary>
        public double CpuUsagePercent { get; set; }
        
        /// <summary>
        /// Количество потоков процесса
        /// </summary>
        public int ThreadCount { get; set; }
        
        /// <summary>
        /// Количество дескрипторов (handles)
        /// </summary>
        public int HandleCount { get; set; }
        
        #endregion

        #region Информация об окнах
        
        /// <summary>
        /// Handle главного окна процесса
        /// </summary>
        public IntPtr MainWindowHandle { get; set; } = IntPtr.Zero;
        
        /// <summary>
        /// Заголовок главного окна
        /// </summary>
        public string MainWindowTitle { get; set; } = string.Empty;
        
        /// <summary>
        /// Список всех окон процесса
        /// </summary>
        public List<WindowInfo> Windows { get; set; } = new();
        
        /// <summary>
        /// Есть ли у процесса видимые окна
        /// </summary>
        public bool HasVisibleWindows => Windows.Any(w => w.IsVisible);
        
        #endregion

        #region Информация о процессе в системе
        
        /// <summary>
        /// ID родительского процесса
        /// </summary>
        public int? ParentProcessId { get; set; }
        
        /// <summary>
        /// Базовый приоритет процесса
        /// </summary>
        public int BasePriority { get; set; }
        
        /// <summary>
        /// Имя пользователя, запустившего процесс
        /// </summary>
        public string UserName { get; set; } = string.Empty;
        
        /// <summary>
        /// Домен пользователя
        /// </summary>
        public string UserDomain { get; set; } = string.Empty;
        
        /// <summary>
        /// Архитектура процесса (x86, x64, ARM)
        /// </summary>
        public string Architecture { get; set; } = string.Empty;
        
        #endregion

        #region Метаданные
        
        /// <summary>
        /// Время сбора информации о процессе
        /// </summary>
        public DateTime CollectedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Источник информации (откуда была собрана)
        /// </summary>
        public string Source { get; set; } = "ProcessMonitor";
        
        /// <summary>
        /// Дополнительные метаданные
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        /// <summary>
        /// Ошибки, возникшие при сборе информации
        /// </summary>
        public List<string> CollectionErrors { get; set; } = new();
        
        #endregion

        #region Статические методы создания
        
        /// <summary>
        /// Создать ProcessInfo для завершенного процесса
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="processName">Имя процесса</param>
        /// <param name="exitTime">Время завершения</param>
        /// <param name="exitCode">Код завершения</param>
        /// <returns>ProcessInfo для завершенного процесса</returns>
        public static ProcessInfo CreateExited(int processId, string processName, DateTime? exitTime = null, int? exitCode = null)
        {
            return new ProcessInfo
            {
                ProcessId = processId,
                ProcessName = processName,
                IsAlive = false,
                HasExited = true,
                ExitTime = exitTime ?? DateTime.Now,
                ExitCode = exitCode,
                CollectedAt = DateTime.Now
            };
        }
        
        /// <summary>
        /// Создать ProcessInfo с минимальной информацией
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="processName">Имя процесса</param>
        /// <param name="isAlive">Жив ли процесс</param>
        /// <returns>Базовый ProcessInfo</returns>
        public static ProcessInfo CreateBasic(int processId, string processName, bool isAlive = true)
        {
            return new ProcessInfo
            {
                ProcessId = processId,
                ProcessName = processName,
                IsAlive = isAlive,
                HasExited = !isAlive,
                CollectedAt = DateTime.Now
            };
        }
        
        /// <summary>
        /// Создать ProcessInfo с ошибкой доступа
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="error">Описание ошибки</param>
        /// <returns>ProcessInfo с информацией об ошибке</returns>
        public static ProcessInfo CreateWithError(int processId, string error)
        {
            return new ProcessInfo
            {
                ProcessId = processId,
                ProcessName = $"Process_{processId}",
                IsAlive = false,
                HasExited = true,
                CollectionErrors = { error },
                CollectedAt = DateTime.Now
            };
        }
        
        #endregion

        #region Методы расчетов
        
        /// <summary>
        /// Получить использование памяти в МБ
        /// </summary>
        /// <returns>Память в мегабайтах</returns>
        public double GetMemoryUsageMB()
        {
            return WorkingSetMemory / 1024.0 / 1024.0;
        }
        
        /// <summary>
        /// Получить использование виртуальной памяти в МБ
        /// </summary>
        /// <returns>Виртуальная память в мегабайтах</returns>
        public double GetVirtualMemoryUsageMB()
        {
            return VirtualMemory / 1024.0 / 1024.0;
        }
        
        /// <summary>
        /// Вычислить время работы процесса
        /// </summary>
        /// <returns>Время работы или null если недоступно</returns>
        public TimeSpan? GetUptime()
        {
            if (!StartTime.HasValue) return null;
            
            var endTime = HasExited && ExitTime.HasValue 
                ? ExitTime.Value 
                : DateTime.Now;
                
            return endTime - StartTime.Value;
        }
        
        /// <summary>
        /// Проверить, является ли процесс ресурсоемким
        /// </summary>
        /// <param name="memoryThresholdMB">Порог памяти в МБ</param>
        /// <param name="cpuThresholdPercent">Порог CPU в процентах</param>
        /// <returns>true если процесс ресурсоемкий</returns>
        public bool IsResourceIntensive(double memoryThresholdMB = 500, double cpuThresholdPercent = 25)
        {
            return GetMemoryUsageMB() > memoryThresholdMB || CpuUsagePercent > cpuThresholdPercent;
        }
        
        /// <summary>
        /// Проверить, долго ли работает процесс
        /// </summary>
        /// <param name="threshold">Пороговое время работы</param>
        /// <returns>true если процесс работает дольше порога</returns>
        public bool IsLongRunning(TimeSpan threshold)
        {
            var uptime = GetUptime();
            return uptime.HasValue && uptime.Value > threshold;
        }
        
        #endregion

        #region Методы проверки состояния
        
        /// <summary>
        /// Проверить, есть ли проблемы с процессом
        /// </summary>
        /// <returns>true если есть проблемы</returns>
        public bool HasIssues()
        {
            return !IsResponding || CollectionErrors.Count > 0 || 
                   (IsAlive && MainWindowHandle == IntPtr.Zero && HasVisibleWindows);
        }
        
        /// <summary>
        /// Получить сводку состояния процесса
        /// </summary>
        /// <returns>Строка с описанием состояния</returns>
        public string GetStatusSummary()
        {
            if (!IsAlive)
                return HasExited ? "Exited" : "Not found";
                
            if (!IsResponding)
                return "Not responding";
                
            if (HasIssues())
                return "Has issues";
                
            return "Running";
        }
        
        /// <summary>
        /// Проверить, устарела ли информация
        /// </summary>
        /// <param name="maxAge">Максимальный возраст информации</param>
        /// <returns>true если информация устарела</returns>
        public bool IsStale(TimeSpan maxAge)
        {
            return DateTime.Now - CollectedAt > maxAge;
        }
        
        #endregion

        #region Переопределенные методы
        
        /// <summary>
        /// Строковое представление процесса
        /// </summary>
        /// <returns>Описание процесса</returns>
        public override string ToString()
        {
            var memoryMB = GetMemoryUsageMB();
            var status = GetStatusSummary();
            return $"{ProcessName} (PID: {ProcessId}, Status: {status}, Memory: {memoryMB:F1}MB)";
        }
        
        /// <summary>
        /// Проверка равенства по ID процесса
        /// </summary>
        /// <param name="obj">Объект для сравнения</param>
        /// <returns>true если процессы равны</returns>
        public override bool Equals(object? obj)
        {
            if (obj is ProcessInfo other)
            {
                return ProcessId == other.ProcessId;
            }
            return false;
        }
        
        /// <summary>
        /// Хэш-код на основе ID процесса
        /// </summary>
        /// <returns>Хэш-код</returns>
        public override int GetHashCode()
        {
            return ProcessId.GetHashCode();
        }
        
        #endregion
    }
}