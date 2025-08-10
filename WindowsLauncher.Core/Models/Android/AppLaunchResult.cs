namespace WindowsLauncher.Core.Models.Android
{
    /// <summary>
    /// Результат запуска Android приложения через WSA
    /// </summary>
    public class AppLaunchResult
    {
        /// <summary>
        /// Успешно ли прошел запуск приложения
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// ID процесса запущенного приложения (может быть WSA процессом)
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Package name запущенного приложения
        /// </summary>
        public string? PackageName { get; set; }

        /// <summary>
        /// Activity name основного экрана приложения
        /// </summary>
        public string? ActivityName { get; set; }

        /// <summary>
        /// Сообщение об ошибке, если запуск не удался
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Время запуска приложения
        /// </summary>
        public DateTime LaunchedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Время, потребовавшееся для запуска (в миллисекундах)
        /// </summary>
        public int? LaunchTimeMs { get; set; }

        /// <summary>
        /// Дополнительная информация от ADB команды
        /// </summary>
        public string? AdditionalInfo { get; set; }

        /// <summary>
        /// Создать результат успешного запуска
        /// </summary>
        public static AppLaunchResult CreateSuccess(string packageName, int? processId = null, string? activityName = null)
        {
            return new AppLaunchResult
            {
                Success = true,
                PackageName = packageName,
                ProcessId = processId,
                ActivityName = activityName,
                LaunchedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Создать результат неудачного запуска
        /// </summary>
        public static AppLaunchResult CreateFailure(string packageName, string errorMessage)
        {
            return new AppLaunchResult
            {
                Success = false,
                PackageName = packageName,
                ErrorMessage = errorMessage,
                LaunchedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Получить детальное описание результата
        /// </summary>
        public string GetDescription()
        {
            if (Success)
            {
                var description = $"Successfully launched {PackageName}";
                if (ProcessId.HasValue)
                {
                    description += $" (PID: {ProcessId})";
                }
                if (!string.IsNullOrEmpty(ActivityName))
                {
                    description += $" [{ActivityName}]";
                }
                if (LaunchTimeMs.HasValue)
                {
                    description += $" in {LaunchTimeMs}ms";
                }
                return description;
            }
            else
            {
                return $"Failed to launch {PackageName}: {ErrorMessage}";
            }
        }
    }
}