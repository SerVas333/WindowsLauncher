using System;

namespace WindowsLauncher.Core.Models.Lifecycle.Events
{
    /// <summary>
    /// Аргументы событий мониторинга процессов
    /// Используется для событий IProcessMonitor
    /// </summary>
    public class ProcessExitedEventArgs : EventArgs
    {
        /// <summary>
        /// ID завершившегося процесса
        /// </summary>
        public int ProcessId { get; }
        
        /// <summary>
        /// Имя процесса
        /// </summary>
        public string ProcessName { get; }
        
        /// <summary>
        /// Время завершения процесса
        /// </summary>
        public DateTime ExitTime { get; }
        
        /// <summary>
        /// Код завершения процесса (если доступен)
        /// </summary>
        public int? ExitCode { get; }
        
        /// <summary>
        /// Было ли завершение ожидаемым (корректное закрытие)
        /// </summary>
        public bool IsExpected { get; set; }
        
        /// <summary>
        /// Дополнительная информация о завершении
        /// </summary>
        public string? AdditionalInfo { get; set; }
        
        public ProcessExitedEventArgs(int processId, string processName, DateTime exitTime, int? exitCode = null)
        {
            ProcessId = processId;
            ProcessName = processName;
            ExitTime = exitTime;
            ExitCode = exitCode;
        }
        
        public override string ToString()
        {
            return $"Process exited: {ProcessName} (PID: {ProcessId}, Exit Code: {ExitCode}, Expected: {IsExpected})";
        }
    }
    
    /// <summary>
    /// Аргументы события "процесс не отвечает"
    /// </summary>
    public class ProcessNotRespondingEventArgs : EventArgs
    {
        /// <summary>
        /// ID процесса, который не отвечает
        /// </summary>
        public int ProcessId { get; }
        
        /// <summary>
        /// Имя процесса
        /// </summary>
        public string ProcessName { get; }
        
        /// <summary>
        /// Время обнаружения проблемы
        /// </summary>
        public DateTime DetectedAt { get; }
        
        /// <summary>
        /// Как долго процесс не отвечает
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Использование процессором (в процентах)
        /// </summary>
        public double CpuUsage { get; set; }
        
        /// <summary>
        /// Использование памяти (в байтах)
        /// </summary>
        public long MemoryUsage { get; set; }
        
        /// <summary>
        /// Рекомендуемое действие
        /// </summary>
        public ProcessAction RecommendedAction { get; set; } = ProcessAction.Wait;
        
        public ProcessNotRespondingEventArgs(int processId, string processName)
        {
            ProcessId = processId;
            ProcessName = processName;
            DetectedAt = DateTime.Now;
        }
        
        public override string ToString()
        {
            return $"Process not responding: {ProcessName} (PID: {ProcessId}, Duration: {Duration}, Action: {RecommendedAction})";
        }
    }
    
    /// <summary>
    /// Аргументы события изменения использования памяти процессом
    /// </summary>
    public class ProcessMemoryChangedEventArgs : EventArgs
    {
        /// <summary>
        /// ID процесса
        /// </summary>
        public int ProcessId { get; }
        
        /// <summary>
        /// Имя процесса
        /// </summary>
        public string ProcessName { get; }
        
        /// <summary>
        /// Предыдущее использование памяти (в байтах)
        /// </summary>
        public long PreviousMemoryUsage { get; }
        
        /// <summary>
        /// Текущее использование памяти (в байтах)
        /// </summary>
        public long CurrentMemoryUsage { get; }
        
        /// <summary>
        /// Изменение в байтах
        /// </summary>
        public long MemoryDelta => CurrentMemoryUsage - PreviousMemoryUsage;
        
        /// <summary>
        /// Изменение в процентах
        /// </summary>
        public double PercentageChange => PreviousMemoryUsage > 0 
            ? ((double)MemoryDelta / PreviousMemoryUsage) * 100 
            : 0;
        
        /// <summary>
        /// Время измерения
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Является ли изменение значительным
        /// </summary>
        public bool IsSignificant { get; set; }
        
        public ProcessMemoryChangedEventArgs(int processId, string processName, long previousMemory, long currentMemory)
        {
            ProcessId = processId;
            ProcessName = processName;
            PreviousMemoryUsage = previousMemory;
            CurrentMemoryUsage = currentMemory;
            Timestamp = DateTime.Now;
            
            // Считаем значительным изменение более чем на 50MB или 25%
            IsSignificant = Math.Abs(MemoryDelta) > 50 * 1024 * 1024 || Math.Abs(PercentageChange) > 25;
        }
        
        public override string ToString()
        {
            var direction = MemoryDelta > 0 ? "increased" : "decreased";
            var memoryMB = Math.Abs(MemoryDelta) / 1024 / 1024;
            return $"Process memory {direction}: {ProcessName} (PID: {ProcessId}, {memoryMB:F1}MB, {PercentageChange:F1}%)";
        }
    }
    
    /// <summary>
    /// Рекомендуемые действия для проблемных процессов
    /// </summary>
    public enum ProcessAction
    {
        /// <summary>
        /// Подождать восстановления
        /// </summary>
        Wait,
        
        /// <summary>
        /// Попытаться корректно закрыть
        /// </summary>
        GracefulClose,
        
        /// <summary>
        /// Принудительно завершить
        /// </summary>
        ForceKill,
        
        /// <summary>
        /// Перезапустить приложение
        /// </summary>
        Restart,
        
        /// <summary>
        /// Уведомить пользователя
        /// </summary>
        NotifyUser
    }
}