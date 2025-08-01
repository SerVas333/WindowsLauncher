using System;
using System.Diagnostics;

namespace WindowsLauncher.Core.Models.Lifecycle
{
    /// <summary>
    /// Результат запуска приложения с подробной информацией об успехе или ошибке
    /// </summary>
    public class LaunchResult
    {
        #region Основная информация о результате
        
        /// <summary>
        /// Успешен ли запуск
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// Сообщение об ошибке (если запуск неуспешен)
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Код ошибки (если есть)
        /// </summary>
        public int? ErrorCode { get; set; }
        
        /// <summary>
        /// Исключение, которое привело к ошибке (если есть)
        /// </summary>
        public Exception? Exception { get; set; }
        
        #endregion

        #region Информация о запущенном приложении
        
        /// <summary>
        /// Созданный экземпляр приложения (при успешном запуске)
        /// </summary>
        public ApplicationInstance? Instance { get; set; }
        
        /// <summary>
        /// Уникальный ID экземпляра приложения (для WebView2 и других без Process)
        /// </summary>
        public string? InstanceId { get; set; }
        
        /// <summary>
        /// Process объект запущенного приложения (при успешном запуске)
        /// </summary>
        public Process? Process { get; set; }
        
        /// <summary>
        /// ID процесса запущенного приложения
        /// </summary>
        public int? ProcessId => Process?.Id ?? Instance?.ProcessId;
        
        /// <summary>
        /// Время, затраченное на запуск
        /// </summary>
        public TimeSpan LaunchDuration { get; set; }
        
        /// <summary>
        /// Время запуска
        /// </summary>
        public DateTime LaunchTime { get; set; } = DateTime.Now;
        
        #endregion

        #region Диагностическая информация
        
        /// <summary>
        /// Тип запуска, который был выполнен
        /// </summary>
        public LaunchType LaunchType { get; set; } = LaunchType.New;
        
        /// <summary>
        /// Дополнительная диагностическая информация
        /// </summary>
        public string? DiagnosticInfo { get; set; }
        
        /// <summary>
        /// Были ли попытки восстановления после ошибок
        /// </summary>
        public bool HasRetries { get; set; }
        
        /// <summary>
        /// Количество попыток запуска
        /// </summary>
        public int RetryCount { get; set; }
        
        #endregion

        #region Статические методы создания результатов
        
        /// <summary>
        /// Создать результат успешного запуска
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="process">Process объект</param>
        /// <param name="launchDuration">Время затраченное на запуск</param>
        /// <returns>Успешный результат запуска</returns>
        public static LaunchResult Success(ApplicationInstance instance, Process process, TimeSpan? launchDuration = null)
        {
            return new LaunchResult
            {
                IsSuccess = true,
                Instance = instance,
                Process = process,
                InstanceId = instance?.InstanceId,
                LaunchDuration = launchDuration ?? TimeSpan.Zero,
                LaunchTime = DateTime.Now,
                LaunchType = LaunchType.New
            };
        }
        
        /// <summary>
        /// Создать результат успешного запуска для WebView2 приложения (без Process)
        /// </summary>
        /// <param name="instanceId">ID экземпляра</param>
        /// <param name="launchDuration">Время затраченное на запуск</param>
        /// <returns>Успешный результат запуска</returns>
        public static LaunchResult Success(string instanceId, TimeSpan? launchDuration = null)
        {
            return new LaunchResult
            {
                IsSuccess = true,
                InstanceId = instanceId,
                LaunchDuration = launchDuration ?? TimeSpan.Zero,
                LaunchTime = DateTime.Now,
                LaunchType = LaunchType.New
            };
        }

        /// <summary>
        /// Создать результат успешного запуска только с экземпляром приложения (без Process)
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="launchDuration">Время затраченное на запуск</param>
        /// <returns>Успешный результат запуска</returns>
        public static LaunchResult Success(ApplicationInstance instance, TimeSpan? launchDuration = null)
        {
            return new LaunchResult
            {
                IsSuccess = true,
                Instance = instance,
                InstanceId = instance?.InstanceId,
                LaunchDuration = launchDuration ?? TimeSpan.Zero,
                LaunchTime = DateTime.Now,
                LaunchType = LaunchType.New
            };
        }
        
        /// <summary>
        /// Создать результат успешного запуска с диагностической информацией
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="process">Process объект</param>
        /// <param name="diagnosticInfo">Диагностическая информация</param>
        /// <param name="launchDuration">Время затраченное на запуск</param>
        /// <returns>Успешный результат запуска с диагностикой</returns>
        public static LaunchResult SuccessWithDiagnostics(
            ApplicationInstance instance, 
            Process process, 
            string diagnosticInfo,
            TimeSpan? launchDuration = null)
        {
            return new LaunchResult
            {
                IsSuccess = true,
                Instance = instance,
                Process = process,
                DiagnosticInfo = diagnosticInfo,
                LaunchDuration = launchDuration ?? TimeSpan.Zero,
                LaunchTime = DateTime.Now,
                LaunchType = LaunchType.New
            };
        }
        
        /// <summary>
        /// Создать результат неудачного запуска
        /// </summary>
        /// <param name="errorMessage">Сообщение об ошибке</param>
        /// <param name="errorCode">Код ошибки (опционально)</param>
        /// <returns>Неуспешный результат запуска</returns>
        public static LaunchResult Failure(string errorMessage, int? errorCode = null)
        {
            return new LaunchResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                LaunchTime = DateTime.Now
            };
        }
        
        /// <summary>
        /// Создать результат неудачного запуска с исключением
        /// </summary>
        /// <param name="exception">Исключение</param>
        /// <param name="errorMessage">Дополнительное сообщение об ошибке</param>
        /// <returns>Неуспешный результат запуска</returns>
        public static LaunchResult Failure(Exception exception, string? errorMessage = null)
        {
            return new LaunchResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage ?? exception.Message,
                Exception = exception,
                LaunchTime = DateTime.Now
            };
        }
        
        /// <summary>
        /// Создать результат для случая, когда приложение уже запущено
        /// </summary>
        /// <param name="existingInstance">Существующий экземпляр</param>
        /// <param name="wasActivated">Было ли приложение активировано</param>
        /// <returns>Результат с информацией о существующем экземпляре</returns>
        public static LaunchResult AlreadyRunning(ApplicationInstance existingInstance, bool wasActivated = false)
        {
            return new LaunchResult
            {
                IsSuccess = true,
                Instance = existingInstance,
                LaunchType = wasActivated ? LaunchType.ActivatedExisting : LaunchType.FoundExisting,
                DiagnosticInfo = wasActivated ? "Application was activated" : "Application already running",
                LaunchTime = DateTime.Now
            };
        }
        
        /// <summary>
        /// Создать результат с информацией о повторных попытках
        /// </summary>
        /// <param name="baseResult">Базовый результат</param>
        /// <param name="retryCount">Количество попыток</param>
        /// <param name="retryInfo">Информация о попытках</param>
        /// <returns>Результат с информацией о повторах</returns>
        public static LaunchResult WithRetries(LaunchResult baseResult, int retryCount, string retryInfo)
        {
            baseResult.HasRetries = true;
            baseResult.RetryCount = retryCount;
            baseResult.DiagnosticInfo = $"{baseResult.DiagnosticInfo} | Retries: {retryInfo}";
            return baseResult;
        }
        
        #endregion

        #region Методы анализа
        
        /// <summary>
        /// Проверить, был ли запуск полностью успешным (с созданием экземпляра)
        /// </summary>
        /// <returns>true если запуск успешен и экземпляр создан</returns>
        public bool IsCompleteSuccess()
        {
            return IsSuccess && Instance != null;
        }
        
        /// <summary>
        /// Проверить, была ли ошибка критической (требует вмешательства пользователя)
        /// </summary>
        /// <returns>true если ошибка критическая</returns>
        public bool IsCriticalError()
        {
            if (IsSuccess) return false;
            
            // Определяем критические ошибки по коду или типу исключения
            if (ErrorCode.HasValue)
            {
                return ErrorCode.Value switch
                {
                    2 => true,    // File not found
                    5 => true,    // Access denied
                    1223 => false, // User cancelled (not critical)
                    _ => false
                };
            }
            
            if (Exception != null)
            {
                return Exception is UnauthorizedAccessException or 
                                   System.IO.FileNotFoundException or 
                                   System.IO.DirectoryNotFoundException;
            }
            
            return false;
        }
        
        /// <summary>
        /// Получить категорию ошибки для анализа
        /// </summary>
        /// <returns>Категория ошибки</returns>
        public ErrorCategory GetErrorCategory()
        {
            if (IsSuccess) return ErrorCategory.None;
            
            if (Exception != null)
            {
                return Exception switch
                {
                    UnauthorizedAccessException => ErrorCategory.Permissions,
                    System.IO.FileNotFoundException => ErrorCategory.FileNotFound,
                    System.IO.DirectoryNotFoundException => ErrorCategory.PathNotFound,
                    TimeoutException => ErrorCategory.Timeout,
                    System.ComponentModel.Win32Exception => ErrorCategory.System,
                    _ => ErrorCategory.Unknown
                };
            }
            
            if (ErrorCode.HasValue)
            {
                return ErrorCode.Value switch
                {
                    2 => ErrorCategory.FileNotFound,
                    3 => ErrorCategory.PathNotFound,
                    5 => ErrorCategory.Permissions,
                    1223 => ErrorCategory.UserCancelled,
                    _ => ErrorCategory.System
                };
            }
            
            return ErrorCategory.Unknown;
        }
        
        #endregion

        #region Переопределенные методы
        
        /// <summary>
        /// Строковое представление результата
        /// </summary>
        /// <returns>Описание результата</returns>
        public override string ToString()
        {
            if (IsSuccess)
            {
                var appName = Instance?.Application?.Name ?? "Unknown";
                var processId = Instance?.ProcessId ?? Process?.Id ?? 0;
                return $"Success: {appName} (PID: {processId}, Type: {LaunchType})";
            }
            else
            {
                return $"Failure: {ErrorMessage} (Code: {ErrorCode}, Category: {GetErrorCategory()})";
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Тип запуска приложения
    /// </summary>
    public enum LaunchType
    {
        /// <summary>
        /// Новый запуск приложения
        /// </summary>
        New,
        
        /// <summary>
        /// Найден существующий экземпляр (без активации)
        /// </summary>
        FoundExisting,
        
        /// <summary>
        /// Найден и активирован существующий экземпляр
        /// </summary>
        ActivatedExisting,
        
        /// <summary>
        /// Перезапуск после ошибки
        /// </summary>
        Restart,
        
        /// <summary>
        /// Запуск через регистрацию внешнего процесса
        /// </summary>
        ExternalRegistration
    }
    
    /// <summary>
    /// Категория ошибки запуска
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>
        /// Нет ошибки
        /// </summary>
        None,
        
        /// <summary>
        /// Файл не найден
        /// </summary>
        FileNotFound,
        
        /// <summary>
        /// Путь не найден
        /// </summary>
        PathNotFound,
        
        /// <summary>
        /// Недостаточно прав доступа
        /// </summary>
        Permissions,
        
        /// <summary>
        /// Пользователь отменил операцию
        /// </summary>
        UserCancelled,
        
        /// <summary>
        /// Таймаут операции
        /// </summary>
        Timeout,
        
        /// <summary>
        /// Системная ошибка Windows
        /// </summary>
        System,
        
        /// <summary>
        /// Неизвестная ошибка
        /// </summary>
        Unknown
    }
}