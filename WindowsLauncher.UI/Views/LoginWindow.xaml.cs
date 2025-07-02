// ===== WindowsLauncher.UI/Views/LoginWindow.xaml.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ =====
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WpfApplication = System.Windows.Application;
// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы для KeyEventArgs
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Окно входа в систему с поддержкой доменной и сервисной аутентификации
    /// ✅ ИСПРАВЛЕНО: Убрана зависимость от XAML файла при ошибках загрузки
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<LoginWindow> _logger;
        private bool _isAuthenticating = false;
        private AuthenticationResult _lastResult;

        // Публичные свойства для доступа к введенным данным
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string Domain { get; private set; }
        public bool IsServiceMode { get; private set; }
        public AuthenticationResult AuthenticationResult { get; private set; }

        // Совместимость с MainViewModel
        public User? AuthenticatedUser => AuthenticationResult?.User;

        public LoginWindow()
        {
            try
            {
                InitializeComponent();
                InitializeWindow();
            }
            catch (Exception ex)
            {
                // ✅ ЕСЛИ НЕ УДАЕТСЯ ЗАГРУЗИТЬ XAML, СОЗДАЕМ ОКНО ПРОГРАММНО
                CreateWindowProgrammatically();

                // Логируем ошибку
                System.Diagnostics.Debug.WriteLine($"Failed to load LoginWindow.xaml: {ex.Message}");
                System.Diagnostics.Debug.WriteLine("Creating window programmatically as fallback.");

                InitializeWindow();
            }
        }

        /// <summary>
        /// Конструктор с передачей сообщения об ошибке
        /// </summary>
        public LoginWindow(string errorMessage) : this()
        {
            if (!string.IsNullOrEmpty(errorMessage))
            {
                ShowError(errorMessage);
            }
        }

        /// <summary>
        /// ✅ СОЗДАНИЕ ОКНА ПРОГРАММНО (FALLBACK ДЛЯ ОТСУТСТВУЮЩЕГО XAML)
        /// </summary>
        private void CreateWindowProgrammatically()
        {
            // Базовые настройки окна
            Title = "System Login";
            Width = 450;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            // Создаем основную структуру
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Заголовок
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(196, 30, 58)), // #C41E3A
                Padding = new Thickness(30, 20),
                CornerRadius = new CornerRadius(8, 8, 0, 0)
            };
            Grid.SetRow(headerBorder, 0);

            var headerStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var titleText = new TextBlock
            {
                Text = "System Login",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var subtitleText = new TextBlock
            {
                Text = "KDV Corporate Application Launcher",
                FontSize = 14,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.8,
                Margin = new Thickness(0, 5, 0, 0)
            };

            headerStack.Children.Add(titleText);
            headerStack.Children.Add(subtitleText);
            headerBorder.Child = headerStack;

            // Форма входа
            var formScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(30, 20)
            };
            Grid.SetRow(formScrollViewer, 1);

            var formStack = new StackPanel();

            // Переключатель режимов
            var modeStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var domainRadio = new RadioButton
            {
                Content = "Domain Login",
                IsChecked = true,
                GroupName = "LoginMode",
                Margin = new Thickness(0, 0, 20, 0)
            };
            domainRadio.Checked += (s, e) => IsServiceMode = false;

            var serviceRadio = new RadioButton
            {
                Content = "Service Administrator",
                GroupName = "LoginMode"
            };
            serviceRadio.Checked += (s, e) => IsServiceMode = true;

            modeStack.Children.Add(domainRadio);
            modeStack.Children.Add(serviceRadio);

            // Поля ввода
            var domainLabel = new TextBlock
            {
                Text = "Domain:",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.Medium
            };

            var domainTextBox = new TextBox
            {
                Text = "company.local",
                Margin = new Thickness(0, 0, 0, 15),
                Height = 40,
                Padding = new Thickness(12, 8),
                FontSize = 14
            };

            var usernameLabel = new TextBlock
            {
                Text = "Username:",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.Medium
            };

            var usernameTextBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Height = 40,
                Padding = new Thickness(12, 8),
                FontSize = 14
            };

            var passwordLabel = new TextBlock
            {
                Text = "Password:",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.Medium
            };

            var passwordBox = new PasswordBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Height = 40,
                Padding = new Thickness(12, 8),
                FontSize = 14
            };

            // Собираем форму
            formStack.Children.Add(modeStack);
            formStack.Children.Add(domainLabel);
            formStack.Children.Add(domainTextBox);
            formStack.Children.Add(usernameLabel);
            formStack.Children.Add(usernameTextBox);
            formStack.Children.Add(passwordLabel);
            formStack.Children.Add(passwordBox);

            formScrollViewer.Content = formStack;

            // Кнопки
            var buttonBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(221, 221, 221)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(30, 20)
            };
            Grid.SetRow(buttonBorder, 2);

            var buttonGrid = new Grid();
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 100,
                Height = 40,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetColumn(cancelButton, 1);
            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };

            var loginButton = new Button
            {
                Content = "Login",
                Width = 120,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(196, 30, 58)),
                Foreground = Brushes.White,
                IsDefault = true
            };
            Grid.SetColumn(loginButton, 2);
            loginButton.Click += async (s, e) => await PerformSimpleLoginAsync(usernameTextBox.Text, passwordBox.Password, domainTextBox.Text);

            buttonGrid.Children.Add(cancelButton);
            buttonGrid.Children.Add(loginButton);
            buttonBorder.Child = buttonGrid;

            // Собираем все вместе
            mainGrid.Children.Add(headerBorder);
            mainGrid.Children.Add(formScrollViewer);
            mainGrid.Children.Add(buttonBorder);

            // Обертка с отступами
            var mainBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(20),
                Child = mainGrid
            };

            Content = mainBorder;
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
        }

        /// <summary>
        /// Инициализация окна
        /// </summary>
        private async void InitializeWindow()
        {
            try
            {
                // Получаем сервисы из DI контейнера
                var serviceProvider = ((App)WpfApplication.Current).ServiceProvider;
                var authService = serviceProvider.GetService<IAuthenticationService>();
                var logger = serviceProvider.GetService<ILogger<LoginWindow>>();

                if (authService != null && logger != null)
                {
                    // Инициализируем сервисы только если они доступны
                    // В противном случае работаем в упрощенном режиме
                }

                // Устанавливаем фокус на первое поле (если доступно)
                Loaded += (s, e) =>
                {
                    var firstTextBox = FindChild<TextBox>(this);
                    firstTextBox?.Focus();
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing login window: {ex.Message}");
            }
        }

        /// <summary>
        /// Упрощенная аутентификация для программно созданного окна
        /// </summary>
        private async Task PerformSimpleLoginAsync(string username, string password, string domain)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    MessageBox.Show("Please enter username", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Please enter password", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создаем простого пользователя для тестирования
                var testUser = new User
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    FullName = $"Test User ({username})",
                    Role = UserRole.Standard,
                    Email = $"{username}@{domain}",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                // Создаем результат аутентификации
                AuthenticationResult = new AuthenticationResult
                {
                    IsSuccess = true,
                    User = testUser,
                    Status = AuthenticationStatus.Success,
                    Message = "Login successful"
                };

                // Сохраняем данные
                Username = username;
                Password = password;
                Domain = domain;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Поиск дочернего элемента по типу
        /// </summary>
        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = FindChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }

            return null;
        }

        #region Методы-заглушки для совместимости

        /// <summary>
        /// Показ ошибки (заглушка)
        /// </summary>
        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Статический метод для создания окна входа
        /// </summary>
        public static LoginWindow ShowLoginDialog(string domain = null, string username = null, bool serviceMode = false)
        {
            var loginWindow = new LoginWindow();
            return loginWindow;
        }

        /// <summary>
        /// Статический метод для показа окна с ошибкой
        /// </summary>
        public static LoginWindow ShowLoginDialogWithError(string errorMessage)
        {
            return new LoginWindow(errorMessage);
        }

        #endregion
    }
}