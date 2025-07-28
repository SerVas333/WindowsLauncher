using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.UI.Helpers
{
    /// <summary>
    /// Помощник для автоматического показа сенсорной клавиатуры при фокусе на текстовых полях
    /// </summary>
    public static class TouchKeyboardHelper
    {
        private static IVirtualKeyboardService? _keyboardService;
        private static ILogger? _logger;
        private static bool _isInitialized = false;

        /// <summary>
        /// Инициализация помощника сенсорной клавиатуры
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            if (_isInitialized) return;

            try
            {
                _keyboardService = serviceProvider.GetService<IVirtualKeyboardService>();
                _logger = serviceProvider.GetService<ILogger<object>>();
                _isInitialized = true;

                _logger?.LogInformation("TouchKeyboardHelper инициализирован");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка инициализации TouchKeyboardHelper");
            }
        }

        /// <summary>
        /// Подключить автоматический показ клавиатуры к окну
        /// </summary>
        public static void AttachToWindow(Window window)
        {
            if (!_isInitialized || _keyboardService == null) return;

            try
            {
                // Обрабатываем все текстовые элементы в окне
                AttachToElement(window);
                
                _logger?.LogDebug("TouchKeyboardHelper подключен к окну {WindowType}", window.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка подключения TouchKeyboardHelper к окну");
            }
        }

        /// <summary>
        /// Подключить автоматический показ клавиатуры к элементу
        /// </summary>
        public static void AttachToElement(DependencyObject element)
        {
            if (!_isInitialized || _keyboardService == null || element == null) return;

            try
            {
                // Если элемент поддерживает ввод текста, подключаем обработчики
                if (element is TextBox textBox)
                {
                    AttachToTextBox(textBox);
                }
                else if (element is PasswordBox passwordBox)
                {
                    AttachToPasswordBox(passwordBox);
                }
                else if (element is ComboBox comboBox && comboBox.IsEditable)
                {
                    AttachToComboBox(comboBox);
                }

                // Рекурсивно обрабатываем дочерние элементы
                if (element is FrameworkElement frameworkElement && frameworkElement.IsLoaded)
                {
                    ProcessChildElements(frameworkElement);
                }
                else if (element is FrameworkElement notLoadedElement)
                {
                    // Ждем загрузки элемента
                    notLoadedElement.Loaded += (sender, e) => ProcessChildElements(notLoadedElement);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка подключения TouchKeyboardHelper к элементу");
            }
        }

        private static void ProcessChildElements(FrameworkElement parent)
        {
            try
            {
                var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    if (child is DependencyObject dependencyChild)
                    {
                        AttachToElement(dependencyChild);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка обработки дочерних элементов");
            }
        }

        private static void AttachToTextBox(System.Windows.Controls.TextBox textBox)
        {
            try
            {
                textBox.GotFocus += OnTextElementGotFocus;
                textBox.LostFocus += OnTextElementLostFocus;
                textBox.PreviewMouseDown += OnTextElementPreviewMouseDown;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка подключения к TextBox");
            }
        }

        private static void AttachToPasswordBox(PasswordBox passwordBox)
        {
            try
            {
                passwordBox.GotFocus += OnTextElementGotFocus;
                passwordBox.LostFocus += OnTextElementLostFocus;
                passwordBox.PreviewMouseDown += OnTextElementPreviewMouseDown;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка подключения к PasswordBox");
            }
        }

        private static void AttachToComboBox(System.Windows.Controls.ComboBox comboBox)
        {
            try
            {
                comboBox.GotFocus += OnTextElementGotFocus;
                comboBox.LostFocus += OnTextElementLostFocus;
                comboBox.PreviewMouseDown += OnTextElementPreviewMouseDown;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка подключения к ComboBox");
            }
        }

        private static async void OnTextElementGotFocus(object sender, RoutedEventArgs e)
        {
            if (_keyboardService == null) return;

            try
            {
                // Добавляем небольшую задержку чтобы не мешать установке фокуса
                await Task.Delay(200);
                
                // Показываем сенсорную клавиатуру при фокусе
                var success = await _keyboardService.ShowVirtualKeyboardAsync();
                if (success)
                {
                    _logger?.LogDebug("Сенсорная клавиатура показана для элемента {ElementType}", sender.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка показа сенсорной клавиатуры при получении фокуса");
            }
        }

        private static async void OnTextElementLostFocus(object sender, RoutedEventArgs e)
        {
            if (_keyboardService == null) return;

            try
            {
                // Увеличиваем задержку чтобы дать время фокусу установиться на новом элементе
                await Task.Delay(500);

                var focusedElement = Keyboard.FocusedElement;
                bool shouldHideKeyboard = focusedElement == null ||
                                        (!(focusedElement is TextBox) &&
                                         !(focusedElement is PasswordBox) &&
                                         !(focusedElement is ComboBox comboBox && comboBox.IsEditable));

                if (shouldHideKeyboard)
                {
                    // НЕ скрываем клавиатуру агрессивно - пусть пользователь сам решает
                    _logger?.LogDebug("Фокус потерян, но клавиатуру оставляем видимой для удобства пользователя");
                    
                    // Опционально: можно добавить настройку автоскрытия
                    // var success = await _keyboardService.HideVirtualKeyboardAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка обработки потери фокуса");
            }
        }

        private static async void OnTextElementPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_keyboardService == null) return;

            try
            {
                // Добавляем задержку чтобы не мешать обработке клика
                await Task.Delay(150);
                
                // Показываем клавиатуру при клике на текстовое поле
                var success = await _keyboardService.ShowVirtualKeyboardAsync();
                if (success)
                {
                    _logger?.LogDebug("Сенсорная клавиатура показана по клику на элемент {ElementType}", sender.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка показа сенсорной клавиатуры по клику");
            }
        }

        /// <summary>
        /// Отключить автоматический показ клавиатуры от окна
        /// </summary>
        public static void DetachFromWindow(Window window)
        {
            if (!_isInitialized || window == null) return;

            try
            {
                DetachFromElement(window);
                _logger?.LogDebug("TouchKeyboardHelper отключен от окна {WindowType}", window.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка отключения TouchKeyboardHelper от окна");
            }
        }

        private static void DetachFromElement(DependencyObject element)
        {
            if (element == null) return;

            try
            {
                // Отключаем обработчики событий
                if (element is TextBox textBox)
                {
                    textBox.GotFocus -= OnTextElementGotFocus;
                    textBox.LostFocus -= OnTextElementLostFocus;
                    textBox.PreviewMouseDown -= OnTextElementPreviewMouseDown;
                }
                else if (element is PasswordBox passwordBox)
                {
                    passwordBox.GotFocus -= OnTextElementGotFocus;
                    passwordBox.LostFocus -= OnTextElementLostFocus;
                    passwordBox.PreviewMouseDown -= OnTextElementPreviewMouseDown;
                }
                else if (element is ComboBox comboBox)
                {
                    comboBox.GotFocus -= OnTextElementGotFocus;
                    comboBox.LostFocus -= OnTextElementLostFocus;
                    comboBox.PreviewMouseDown -= OnTextElementPreviewMouseDown;
                }

                // Рекурсивно отключаем от дочерних элементов
                var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < childCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                    if (child is DependencyObject dependencyChild)
                    {
                        DetachFromElement(dependencyChild);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка отключения TouchKeyboardHelper от элемента");
            }
        }
    }
}