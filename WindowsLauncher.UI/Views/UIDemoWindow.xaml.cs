// ===== WindowsLauncher.UI/Views/UIDemoWindow.xaml.cs =====
using System;
using System.Windows;
using System.Windows.Controls;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Демонстрационное окно для показа всех UI элементов с FontAwesome иконками
    /// </summary>
    public partial class UIDemoWindow : Window
    {
        private bool _isTouchModeEnabled = false;

        public UIDemoWindow()
        {
            InitializeComponent();
            
            // Инициализация окна
            InitializeWindow();
        }

        /// <summary>
        /// Инициализация настроек окна
        /// </summary>
        private void InitializeWindow()
        {
            // Устанавливаем начальный статус
            UpdateStatus("UI Demo загружено успешно. Все FontAwesome иконки активны.");
            
            // Проверяем текущий Touch Mode из настроек приложения
            CheckCurrentTouchMode();
        }

        /// <summary>
        /// Проверка текущего состояния Touch Mode
        /// </summary>
        private void CheckCurrentTouchMode()
        {
            try
            {
                // Проверяем есть ли уже Touch Mode стили в ресурсах
                var touchModeScale = this.FindResource("TouchModeScale");
                if (touchModeScale != null)
                {
                    // Touch Mode уже активен
                    TouchModeCheckBox.IsChecked = true;
                    _isTouchModeEnabled = true;
                    UpdateStatus("Touch Mode активен - элементы увеличены для сенсорного управления");
                }
            }
            catch
            {
                // Touch Mode не активен
                _isTouchModeEnabled = false;
                UpdateStatus("Обычный режим - стандартные размеры элементов");
            }
        }

        /// <summary>
        /// Обработчик включения Touch Mode
        /// </summary>
        private void TouchModeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isTouchModeEnabled)
            {
                EnableTouchMode();
            }
        }

        /// <summary>
        /// Обработчик отключения Touch Mode
        /// </summary>
        private void TouchModeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isTouchModeEnabled)
            {
                DisableTouchMode();
            }
        }

        /// <summary>
        /// Включение Touch Mode
        /// </summary>
        private void EnableTouchMode()
        {
            try
            {
                _isTouchModeEnabled = true;
                
                // Применяем Touch Mode стили к демонстрационным кнопкам
                ApplyTouchModeToElements();
                
                UpdateStatus("Touch Mode включен - размеры элементов увеличены на 20% для сенсорного управления");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка включения Touch Mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Отключение Touch Mode
        /// </summary>
        private void DisableTouchMode()
        {
            try
            {
                _isTouchModeEnabled = false;
                
                // Возвращаем обычные стили
                ApplyNormalModeToElements();
                
                UpdateStatus("Touch Mode отключен - восстановлены стандартные размеры элементов");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка отключения Touch Mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Применение Touch Mode стилей к элементам
        /// </summary>
        private void ApplyTouchModeToElements()
        {
            // Находим Touch Mode кнопки и применяем увеличенные стили
            if (TouchButton1 != null)
            {
                var touchStyle = FindResource("FontAwesomeButtonTouch") as Style;
                if (touchStyle != null)
                {
                    TouchButton1.Style = touchStyle;
                }
            }

            if (TouchButton2 != null)
            {
                var touchStyle = FindResource("FontAwesomeButtonTouch") as Style;
                if (touchStyle != null)
                {
                    TouchButton2.Style = touchStyle;
                }
            }

            // Можно добавить применение Touch Mode к другим элементам
            ApplyTouchModeToContainer(this);
        }

        /// <summary>
        /// Применение обычных стилей к элементам
        /// </summary>
        private void ApplyNormalModeToElements()
        {
            // Возвращаем обычные стили для Touch Mode кнопок
            if (TouchButton1 != null)
            {
                var normalStyle = FindResource("FontAwesomeButtonPrimary") as Style;
                if (normalStyle != null)
                {
                    TouchButton1.Style = normalStyle;
                }
            }

            if (TouchButton2 != null)
            {
                var normalStyle = FindResource("FontAwesomeButtonPrimary") as Style;
                if (normalStyle != null)
                {
                    TouchButton2.Style = normalStyle;
                }
            }

            // Возвращаем обычные размеры для всех элементов
            ApplyNormalModeToContainer(this);
        }

        /// <summary>
        /// Рекурсивное применение Touch Mode к контейнеру
        /// </summary>
        private void ApplyTouchModeToContainer(DependencyObject container)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(container); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(container, i);
                
                // Применяем Touch Mode к кнопкам
                if (child is Button button && !ReferenceEquals(button, TouchButton1) && !ReferenceEquals(button, TouchButton2))
                {
                    // Увеличиваем MinHeight и MinWidth для Touch Mode
                    button.MinHeight = Math.Max(button.MinHeight, 48);
                    button.MinWidth = Math.Max(button.MinWidth, 48);
                }
                
                // Применяем Touch Mode к текстовым полям
                if (child is TextBox textBox)
                {
                    textBox.MinHeight = Math.Max(textBox.MinHeight, 48);
                }
                
                // Рекурсивно применяем к дочерним элементам
                ApplyTouchModeToContainer(child);
            }
        }

        /// <summary>
        /// Рекурсивное применение обычного режима к контейнеру
        /// </summary>
        private void ApplyNormalModeToContainer(DependencyObject container)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(container); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(container, i);
                
                // Возвращаем обычные размеры для кнопок
                if (child is Button button && !ReferenceEquals(button, TouchButton1) && !ReferenceEquals(button, TouchButton2))
                {
                    // Возвращаем стандартные размеры
                    button.ClearValue(Button.MinHeightProperty);
                    button.ClearValue(Button.MinWidthProperty);
                }
                
                // Возвращаем обычные размеры для текстовых полей
                if (child is TextBox textBox)
                {
                    textBox.ClearValue(TextBox.MinHeightProperty);
                }
                
                // Рекурсивно применяем к дочерним элементам
                ApplyNormalModeToContainer(child);
            }
        }

        /// <summary>
        /// Обновление статуса в статус баре
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (StatusText != null)
            {
                StatusText.Text = message;
            }
        }

        /// <summary>
        /// Обработчик кнопки "Закрыть"
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при закрытии окна: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обработчик закрытия окна
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Очистка ресурсов если необходимо
                base.OnClosed(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при очистке ресурсов UIDemoWindow: {ex.Message}");
            }
        }

        /// <summary>
        /// Показать окно как диалог
        /// </summary>
        public static void ShowDemo(Window? owner = null)
        {
            try
            {
                var demoWindow = new UIDemoWindow();
                
                if (owner != null)
                {
                    demoWindow.Owner = owner;
                    demoWindow.ShowDialog();
                }
                else
                {
                    demoWindow.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия UI Demo: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}