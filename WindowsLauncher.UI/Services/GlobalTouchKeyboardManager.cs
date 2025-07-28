using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.UI.Helpers;

namespace WindowsLauncher.UI.Services
{
    /// <summary>
    /// Глобальный менеджер для автоматического подключения сенсорной клавиатуры ко всем окнам приложения
    /// </summary>
    public class GlobalTouchKeyboardManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GlobalTouchKeyboardManager> _logger;
        private readonly HashSet<Window> _attachedWindows;
        private bool _isInitialized = false;
        private DispatcherTimer? _windowCheckTimer;

        public GlobalTouchKeyboardManager(IServiceProvider serviceProvider, ILogger<GlobalTouchKeyboardManager> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _attachedWindows = new HashSet<Window>();
        }

        /// <summary>
        /// Инициализация глобального менеджера
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                // Инициализируем TouchKeyboardHelper
                TouchKeyboardHelper.Initialize(_serviceProvider);

                // Подписываемся на события создания новых окон
                if (Application.Current != null)
                {
                    // Обрабатываем уже существующие окна
                    foreach (Window window in Application.Current.Windows)
                    {
                        AttachToWindowSafely(window);
                    }

                    // Подписываемся на создание новых окон через несколько событий для надежности
                    Application.Current.Activated += OnApplicationActivated;
                    
                    // Дополнительный таймер для периодической проверки новых окон
                    StartPeriodicWindowCheck();
                }

                _isInitialized = true;
                _logger.LogInformation("GlobalTouchKeyboardManager инициализирован для {WindowCount} окон", _attachedWindows.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка инициализации GlobalTouchKeyboardManager");
            }
        }

        private void StartPeriodicWindowCheck()
        {
            try
            {
                _windowCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5) // Уменьшили частоту до 5 секунд
                };
                _windowCheckTimer.Tick += OnWindowCheckTimer;
                _windowCheckTimer.Start();
                
                _logger.LogDebug("Запущена периодическая проверка новых окон");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка запуска таймера проверки окон");
            }
        }

        private void OnWindowCheckTimer(object? sender, EventArgs e)
        {
            CheckForNewWindows();
        }

        private void OnApplicationActivated(object? sender, EventArgs e)
        {
            CheckForNewWindows();
        }

        private void CheckForNewWindows()
        {
            try
            {
                // Проверяем все окна приложения на предмет новых
                if (Application.Current?.Windows != null)
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        AttachToWindowSafely(window);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке новых окон");
            }
        }

        /// <summary>
        /// Безопасное подключение к окну с обработкой ошибок
        /// </summary>
        private void AttachToWindowSafely(Window window)
        {
            if (window == null || _attachedWindows.Contains(window))
                return;

            try
            {
                // Подключаем сенсорную клавиатуру к окну
                TouchKeyboardHelper.AttachToWindow(window);
                _attachedWindows.Add(window);

                // Подписываемся на закрытие окна для корректной очистки
                window.Closed += (s, e) => OnWindowClosed(window);

                _logger.LogDebug("TouchKeyboard подключен к окну {WindowType}", window.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось подключить TouchKeyboard к окну {WindowType}", window.GetType().Name);
            }
        }

        private void OnWindowClosed(Window window)
        {
            try
            {
                if (_attachedWindows.Contains(window))
                {
                    TouchKeyboardHelper.DetachFromWindow(window);
                    _attachedWindows.Remove(window);
                    _logger.LogDebug("TouchKeyboard отключен от закрытого окна {WindowType}", window.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка отключения TouchKeyboard от окна {WindowType}", window.GetType().Name);
            }
        }

        /// <summary>
        /// Принудительное подключение к конкретному окну (для особых случаев)
        /// </summary>
        public void AttachToWindow(Window window)
        {
            AttachToWindowSafely(window);
        }

        /// <summary>
        /// Получить количество подключенных окон
        /// </summary>
        public int AttachedWindowsCount => _attachedWindows.Count;

        /// <summary>
        /// Очистка ресурсов при завершении приложения
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Останавливаем таймер
                if (_windowCheckTimer != null)
                {
                    _windowCheckTimer.Stop();
                    _windowCheckTimer.Tick -= OnWindowCheckTimer;
                    _windowCheckTimer = null;
                }

                // Отписываемся от событий
                if (Application.Current != null)
                {
                    Application.Current.Activated -= OnApplicationActivated;
                }

                // Отключаем от всех окон
                foreach (var window in _attachedWindows)
                {
                    try
                    {
                        TouchKeyboardHelper.DetachFromWindow(window);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ошибка отключения TouchKeyboard при dispose");
                    }
                }

                _attachedWindows.Clear();
                _logger.LogInformation("GlobalTouchKeyboardManager dispose завершен");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при dispose GlobalTouchKeyboardManager");
            }
        }
    }
}