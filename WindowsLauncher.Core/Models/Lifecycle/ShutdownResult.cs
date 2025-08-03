using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsLauncher.Core.Models.Lifecycle
{
    /// <summary>
    /// Результат операции массового закрытия приложений
    /// </summary>
    public class ShutdownResult
    {
        /// <summary>
        /// Общий результат операции
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Общее количество приложений для закрытия
        /// </summary>
        public int TotalApplications { get; set; }
        
        /// <summary>
        /// Количество успешно закрытых приложений
        /// </summary>
        public int ClosedSuccessfully { get; set; }
        
        /// <summary>
        /// Количество приложений, закрытых принудительно (Kill)
        /// </summary>
        public int ForcedClosed { get; set; }
        
        /// <summary>
        /// Количество приложений, которые не удалось закрыть
        /// </summary>
        public int FailedToClose { get; set; }
        
        /// <summary>
        /// Общее время выполнения операции
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Детальная информация по каждому приложению
        /// </summary>
        public List<ApplicationShutdownInfo> Applications { get; set; } = new();
        
        /// <summary>
        /// Сообщения об ошибках
        /// </summary>
        public List<string> Errors { get; set; } = new();
        
        /// <summary>
        /// Создать результат успешного закрытия всех приложений
        /// </summary>
        public static ShutdownResult AllClosed(TimeSpan duration, List<ApplicationShutdownInfo> applications)
        {
            return new ShutdownResult
            {
                Success = true,
                TotalApplications = applications.Count,
                ClosedSuccessfully = applications.Count(a => a.Method == ShutdownMethod.Graceful),
                ForcedClosed = applications.Count(a => a.Method == ShutdownMethod.Forced),
                FailedToClose = applications.Count(a => !a.Success),
                Duration = duration,
                Applications = applications
            };
        }
        
        /// <summary>
        /// Создать результат с частичными неудачами
        /// </summary>
        public static ShutdownResult PartialFailure(TimeSpan duration, List<ApplicationShutdownInfo> applications, List<string> errors)
        {
            return new ShutdownResult
            {
                Success = false,
                TotalApplications = applications.Count,
                ClosedSuccessfully = applications.Count(a => a.Success && a.Method == ShutdownMethod.Graceful),
                ForcedClosed = applications.Count(a => a.Success && a.Method == ShutdownMethod.Forced),
                FailedToClose = applications.Count(a => !a.Success),
                Duration = duration,
                Applications = applications,
                Errors = errors
            };
        }
        
        /// <summary>
        /// Создать результат полной неудачи
        /// </summary>
        public static ShutdownResult Failed(string error, TimeSpan duration = default)
        {
            return new ShutdownResult
            {
                Success = false,
                TotalApplications = 0,
                Duration = duration,
                Errors = new List<string> { error }
            };
        }
    }
    
    /// <summary>
    /// Детальная информация о закрытии одного приложения
    /// </summary>
    public class ApplicationShutdownInfo
    {
        /// <summary>
        /// ID экземпляра приложения
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Название приложения
        /// </summary>
        public string ApplicationName { get; set; } = string.Empty;
        
        /// <summary>
        /// ID процесса
        /// </summary>
        public int ProcessId { get; set; }
        
        /// <summary>
        /// Успешность закрытия
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Метод закрытия
        /// </summary>
        public ShutdownMethod Method { get; set; }
        
        /// <summary>
        /// Время, затраченное на закрытие
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Сообщение об ошибке (если есть)
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Метод закрытия приложения
    /// </summary>
    public enum ShutdownMethod
    {
        /// <summary>
        /// Корректное закрытие (WM_CLOSE)
        /// </summary>
        Graceful,
        
        /// <summary>
        /// Принудительное завершение (Kill Process)
        /// </summary>
        Forced,
        
        /// <summary>
        /// Не удалось закрыть
        /// </summary>
        Failed
    }
}