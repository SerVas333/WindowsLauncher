using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace WindowsLauncher.Core.Services.Android
{
    /// <summary>
    /// Парсер аргументов для Android приложений
    /// Извлекает Android-специфические параметры из строки аргументов
    /// </summary>
    public class AndroidArgumentsParser
    {
        private readonly ILogger? _logger;
        
        // Регулярное выражение для парсинга аргументов вида --param='value' или --param="value"
        private static readonly Regex ArgumentRegex = new Regex(
            @"--(?<name>\w+)=['""](?<value>[^'""]*)['""]|--(?<name>\w+)=(?<value>\S+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public AndroidArgumentsParser(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Парсит строку аргументов и извлекает Android-специфические параметры
        /// </summary>
        /// <param name="arguments">Строка аргументов для парсинга</param>
        /// <returns>Объект с извлеченными Android параметрами</returns>
        public AndroidArguments Parse(string? arguments)
        {
            var result = new AndroidArguments();
            
            if (string.IsNullOrWhiteSpace(arguments))
            {
                _logger?.LogDebug("No arguments provided for Android parsing");
                return result;
            }

            _logger?.LogDebug("Parsing Android arguments: {Arguments}", arguments);
            
            try
            {
                var matches = ArgumentRegex.Matches(arguments);
                
                foreach (Match match in matches)
                {
                    if (!match.Success) continue;
                    
                    var name = match.Groups["name"].Value.ToLowerInvariant();
                    var value = match.Groups["value"].Value;
                    
                    switch (name)
                    {
                        case "window_name":
                            result.WindowName = value;
                            _logger?.LogDebug("Extracted window_name: {WindowName}", value);
                            break;
                            
                        case "activity_name":
                            result.ActivityName = value;
                            _logger?.LogDebug("Extracted activity_name: {ActivityName}", value);
                            break;
                            
                        case "launch_timeout":
                            if (int.TryParse(value, out int timeout))
                            {
                                result.LaunchTimeout = TimeSpan.FromSeconds(timeout);
                                _logger?.LogDebug("Extracted launch_timeout: {Timeout} seconds", timeout);
                            }
                            else
                            {
                                _logger?.LogWarning("Invalid launch_timeout value: {Value}", value);
                            }
                            break;
                            
                        case "wait_for_window":
                            if (bool.TryParse(value, out bool waitForWindow))
                            {
                                result.WaitForWindow = waitForWindow;
                                _logger?.LogDebug("Extracted wait_for_window: {WaitForWindow}", waitForWindow);
                            }
                            else
                            {
                                _logger?.LogWarning("Invalid wait_for_window value: {Value}", value);
                            }
                            break;
                            
                        case "virtual_fallback":
                            if (bool.TryParse(value, out bool virtualFallback))
                            {
                                result.VirtualFallback = virtualFallback;
                                _logger?.LogDebug("Extracted virtual_fallback: {VirtualFallback}", virtualFallback);
                            }
                            else
                            {
                                _logger?.LogWarning("Invalid virtual_fallback value: {Value}", value);
                            }
                            break;
                            
                        default:
                            // Сохраняем неизвестные параметры для будущего использования
                            result.CustomParameters[name] = value;
                            _logger?.LogDebug("Extracted custom parameter: {Name} = {Value}", name, value);
                            break;
                    }
                }
                
                _logger?.LogInformation("Successfully parsed Android arguments: {ParsedCount} parameters", 
                    result.GetParametersCount());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error parsing Android arguments: {Arguments}", arguments);
            }
            
            return result;
        }

        /// <summary>
        /// Создает строку аргументов из объекта AndroidArguments
        /// Полезно для обратного преобразования и тестирования
        /// </summary>
        /// <param name="arguments">Объект Android аргументов</param>
        /// <returns>Строка аргументов</returns>
        public string Build(AndroidArguments arguments)
        {
            if (arguments == null)
                return string.Empty;

            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(arguments.WindowName))
                parts.Add($"--window_name='{arguments.WindowName}'");
            
            if (!string.IsNullOrEmpty(arguments.ActivityName))
                parts.Add($"--activity_name='{arguments.ActivityName}'");
                
            if (arguments.LaunchTimeout.HasValue)
                parts.Add($"--launch_timeout={arguments.LaunchTimeout.Value.TotalSeconds:F0}");
                
            if (arguments.WaitForWindow.HasValue)
                parts.Add($"--wait_for_window={arguments.WaitForWindow.Value.ToString().ToLowerInvariant()}");
                
            if (arguments.VirtualFallback.HasValue)
                parts.Add($"--virtual_fallback={arguments.VirtualFallback.Value.ToString().ToLowerInvariant()}");
            
            // Добавляем пользовательские параметры
            foreach (var kvp in arguments.CustomParameters)
            {
                parts.Add($"--{kvp.Key}='{kvp.Value}'");
            }
            
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Проверяет, содержит ли строка аргументов Android-специфические параметры
        /// </summary>
        /// <param name="arguments">Строка аргументов</param>
        /// <returns>true если найдены Android параметры</returns>
        public bool HasAndroidArguments(string? arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return false;
                
            var parsed = Parse(arguments);
            return parsed.GetParametersCount() > 0;
        }

        /// <summary>
        /// Извлекает только Android-специфические аргументы из общей строки аргументов
        /// Остальные аргументы остаются нетронутыми
        /// </summary>
        /// <param name="arguments">Полная строка аргументов</param>
        /// <param name="androidArguments">Извлеченные Android аргументы</param>
        /// <returns>Строка аргументов без Android-специфических параметров</returns>
        public string ExtractAndroidArguments(string? arguments, out AndroidArguments androidArguments)
        {
            androidArguments = new AndroidArguments();
            
            if (string.IsNullOrWhiteSpace(arguments))
                return string.Empty;

            // Парсим Android аргументы
            androidArguments = Parse(arguments);
            
            // Удаляем Android-специфические аргументы из строки
            var result = arguments;
            var matches = ArgumentRegex.Matches(arguments);
            
            // Удаляем совпадения в обратном порядке чтобы не нарушить позиции
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                var name = match.Groups["name"].Value.ToLowerInvariant();
                
                // Проверяем, является ли это Android-специфическим параметром
                if (IsAndroidSpecificParameter(name))
                {
                    result = result.Remove(match.Index, match.Length).Trim();
                }
            }
            
            // Очищаем множественные пробелы
            result = Regex.Replace(result, @"\s+", " ").Trim();
            
            return result;
        }

        /// <summary>
        /// Проверяет, является ли параметр Android-специфическим
        /// </summary>
        /// <param name="parameterName">Имя параметра (в нижнем регистре)</param>
        /// <returns>true если параметр Android-специфический</returns>
        private static bool IsAndroidSpecificParameter(string parameterName)
        {
            return parameterName switch
            {
                "window_name" => true,
                "activity_name" => true,
                "launch_timeout" => true,
                "wait_for_window" => true,
                "virtual_fallback" => true,
                _ => false
            };
        }
    }

    /// <summary>
    /// Класс для хранения извлеченных Android-специфических аргументов
    /// </summary>
    public class AndroidArguments
    {
        /// <summary>
        /// Имя WSA окна для точного поиска (параметр --window_name)
        /// </summary>
        public string? WindowName { get; set; }

        /// <summary>
        /// Имя Android активности (параметр --activity_name)
        /// </summary>
        public string? ActivityName { get; set; }

        /// <summary>
        /// Тайм-аут запуска приложения (параметр --launch_timeout)
        /// </summary>
        public TimeSpan? LaunchTimeout { get; set; }

        /// <summary>
        /// Ожидать появления окна после запуска (параметр --wait_for_window)
        /// </summary>
        public bool? WaitForWindow { get; set; }

        /// <summary>
        /// Использовать виртуальное окно если реальное не найдено (параметр --virtual_fallback)
        /// По умолчанию true для обратной совместимости
        /// </summary>
        public bool? VirtualFallback { get; set; }

        /// <summary>
        /// Пользовательские параметры для будущего расширения
        /// </summary>
        public Dictionary<string, string> CustomParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Возвращает общее количество извлеченных параметров
        /// </summary>
        /// <returns>Количество параметров</returns>
        public int GetParametersCount()
        {
            int count = 0;
            
            if (!string.IsNullOrEmpty(WindowName)) count++;
            if (!string.IsNullOrEmpty(ActivityName)) count++;
            if (LaunchTimeout.HasValue) count++;
            if (WaitForWindow.HasValue) count++;
            if (VirtualFallback.HasValue) count++;
            
            count += CustomParameters.Count;
            
            return count;
        }

        /// <summary>
        /// Возвращает строковое представление аргументов для отладки
        /// </summary>
        /// <returns>Строка с параметрами</returns>
        public override string ToString()
        {
            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(WindowName))
                parts.Add($"WindowName: '{WindowName}'");
                
            if (!string.IsNullOrEmpty(ActivityName))
                parts.Add($"ActivityName: '{ActivityName}'");
                
            if (LaunchTimeout.HasValue)
                parts.Add($"LaunchTimeout: {LaunchTimeout.Value.TotalSeconds}s");
                
            if (WaitForWindow.HasValue)
                parts.Add($"WaitForWindow: {WaitForWindow.Value}");
                
            if (VirtualFallback.HasValue)
                parts.Add($"VirtualFallback: {VirtualFallback.Value}");
            
            if (CustomParameters.Count > 0)
                parts.Add($"CustomParams: {CustomParameters.Count}");
            
            return parts.Count > 0 ? $"AndroidArguments[{string.Join(", ", parts)}]" : "AndroidArguments[empty]";
        }

        /// <summary>
        /// Создает экземпляр AndroidArguments с настройками по умолчанию
        /// </summary>
        /// <returns>Новый экземпляр с значениями по умолчанию</returns>
        public static AndroidArguments CreateDefault()
        {
            return new AndroidArguments
            {
                VirtualFallback = true, // Включено для обратной совместимости
                WaitForWindow = false,
                LaunchTimeout = TimeSpan.FromSeconds(15)
            };
        }
    }
}