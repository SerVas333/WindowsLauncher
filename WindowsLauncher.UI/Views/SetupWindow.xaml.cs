using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

// Явно указываем пространства имен для разрешения конфликтов
using WpfMessageBox = System.Windows.MessageBox;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Окно первоначальной настройки системы
    /// </summary>
    public partial class SetupWindow : Window
    {
        private readonly IAuthenticationConfigurationService _configService;
        private readonly IAuthenticationService _authService;
        private readonly IActiveDirectoryService _adService;
        private readonly IDatabaseConfigurationService _dbConfigService;
        private readonly ILogger<SetupWindow> _logger;
        private readonly IServiceProvider _serviceProvider;

        private bool _isDomainValid = false;
        private bool _isServiceAdminValid = false;
        private bool _isDatabaseValid = true; // По умолчанию SQLite валидна
        private bool _isProcessing = false;

        // Валидация пароля
        private readonly Regex _uppercaseRegex = new(@"[A-Z]");
        private readonly Regex _lowercaseRegex = new(@"[a-z]");
        private readonly Regex _digitRegex = new(@"[0-9]");
        private readonly Regex _specialRegex = new(@"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]");

        public bool SetupCompleted { get; private set; } = false;

        public SetupWindow(
            IAuthenticationConfigurationService configService,
            IAuthenticationService authService,
            IActiveDirectoryService adService,
            ILogger<SetupWindow> logger,
            IServiceProvider serviceProvider)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _dbConfigService = serviceProvider.GetRequiredService<IDatabaseConfigurationService>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            InitializeComponent();
            
            // Переносим инициализацию в событие Loaded
            Loaded += SetupWindow_Loaded;
        }

        /// <summary>
        /// Обработчик события загрузки окна
        /// </summary>
        private void SetupWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeWindow();
        }

        /// <summary>
        /// Инициализация окна
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                // Загружаем текущую конфигурацию
                LoadCurrentConfiguration();

                // Настраиваем валидацию
                UpdateValidationIndicators();
                UpdateSetupProgress();

                _logger.LogInformation("Setup window initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing setup window");
                ShowError("Ошибка инициализации окна настройки");
            }
        }

        #region Event Handlers

        /// <summary>
        /// Обработчик изменения настроек домена
        /// </summary>
        private void DomainSettings_Changed(object sender, EventArgs e)
        {
            ValidateDomainSettings();
            UpdateSetupProgress();
        }

        /// <summary>
        /// Обработчик изменения настроек сервисного администратора
        /// </summary>
        private void ServiceAdmin_Changed(object sender, EventArgs e)
        {
            ValidateServiceAdminSettings();
            UpdateSetupProgress();
        }

        /// <summary>
        /// Обработчик проверки подключения
        /// </summary>
        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            await TestDomainConnectionAsync();
        }

        /// <summary>
        /// Обработчик пропуска настройки
        /// </summary>
        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            var result = WpfMessageBox.Show(
                "Вы уверены, что хотите пропустить настройку?\n\n" +
                "Система будет работать с настройками по умолчанию.\n" +
                "Вы сможете изменить настройки позже в меню администратора.",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                SetupCompleted = false;
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// Обработчик завершения настройки
        /// </summary>
        private async void CompleteSetupButton_Click(object sender, RoutedEventArgs e)
        {
            await CompleteSetupAsync();
        }

        /// <summary>
        /// Обработчик переключения виртуальной клавиатуры
        /// </summary>
        private async void VirtualKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var virtualKeyboardService = _serviceProvider.GetRequiredService<IVirtualKeyboardService>();
                await virtualKeyboardService.ToggleVirtualKeyboardAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling virtual keyboard");
                ShowError($"Ошибка при переключении виртуальной клавиатуры: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик изменения типа базы данных
        /// </summary>
        private void DatabaseTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FirebirdSettingsPanel == null) return;

            var selectedItem = DatabaseTypeComboBox.SelectedItem as ComboBoxItem;
            var databaseType = selectedItem?.Tag?.ToString();

            if (databaseType == "Firebird")
            {
                FirebirdSettingsPanel.Visibility = Visibility.Visible;
                InitializeFirebirdSettings();
            }
            else
            {
                FirebirdSettingsPanel.Visibility = Visibility.Collapsed;
            }

            ValidateDatabaseSettings();
            UpdateSetupProgress();
        }

        /// <summary>
        /// Обработчик изменения режима Firebird
        /// </summary>
        private void FirebirdModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FirebirdServerPanel == null || DatabasePathLabel == null) return;

            var selectedItem = FirebirdModeComboBox.SelectedItem as ComboBoxItem;
            var mode = selectedItem?.Tag?.ToString();

            if (mode == "ClientServer")
            {
                FirebirdServerPanel.Visibility = Visibility.Visible;
                DatabasePathLabel.Text = "Имя базы данных:";
                if (string.IsNullOrEmpty(DatabasePathTextBox.Text) || DatabasePathTextBox.Text == "launcher.fdb")
                {
                    DatabasePathTextBox.Text = "launcher.fdb";
                }
            }
            else
            {
                FirebirdServerPanel.Visibility = Visibility.Collapsed;
                DatabasePathLabel.Text = "Путь к базе данных:";
                if (string.IsNullOrEmpty(DatabasePathTextBox.Text) || !DatabasePathTextBox.Text.Contains("\\"))
                {
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    DatabasePathTextBox.Text = Path.Combine(appDataPath, "WindowsLauncher", "launcher.fdb");
                }
            }

            ValidateDatabaseSettings();
            UpdateSetupProgress();
        }

        /// <summary>
        /// Обработчик изменения настроек базы данных
        /// </summary>
        private void DatabaseSettings_Changed(object sender, EventArgs e)
        {
            ValidateDatabaseSettings();
            UpdateSetupProgress();
        }

        /// <summary>
        /// Обработчик кнопки обзора пути к БД
        /// </summary>
        private void BrowseDatabasePathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = FirebirdModeComboBox?.SelectedItem as ComboBoxItem;
                var mode = selectedItem?.Tag?.ToString();

                if (mode == "ClientServer")
                {
                    // Для клиент-сервер режима просто даем возможность ввести имя БД
                    return;
                }

                // Для Embedded режима выбираем файл
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Выберите расположение базы данных Firebird",
                    Filter = "Firebird Database|*.fdb|All files|*.*",
                    DefaultExt = "fdb",
                    FileName = "launcher.fdb"
                };

                if (dialog.ShowDialog() == true)
                {
                    DatabasePathTextBox.Text = dialog.FileName;
                    ValidateDatabaseSettings();
                    UpdateSetupProgress();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing database path");
                ShowError($"Ошибка при выборе пути к базе данных: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик проверки подключения к БД
        /// </summary>
        private async void TestDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            await TestDatabaseConnectionAsync();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Загрузка текущей конфигурации
        /// </summary>
        private void LoadCurrentConfiguration()
        {
            try
            {
                var config = _configService.GetConfiguration();

                // Настройки домена
                DomainTextBox.Text = config.Domain ?? "company.local";
                LdapServerTextBox.Text = config.LdapServer ?? "dc.company.local";
                PortTextBox.Text = config.Port.ToString();
                UseTlsCheckBox.IsChecked = config.UseTLS;

                // Настройки сервисного администратора
                ServiceUsernameTextBox.Text = config.ServiceAdmin.Username ?? "serviceadmin";
                SessionTimeoutTextBox.Text = config.ServiceAdmin.SessionTimeoutMinutes.ToString();

                _logger.LogDebug("Current configuration loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not load current configuration: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Валидация настроек домена
        /// </summary>
        private void ValidateDomainSettings()
        {
            try
            {
                // Проверяем, что все элементы UI доступны
                if (DomainTextBox == null || LdapServerTextBox == null || PortTextBox == null)
                {
                    _isDomainValid = false;
                    return;
                }
                var domain = DomainTextBox?.Text?.Trim() ?? "";
                var ldapServer = LdapServerTextBox?.Text?.Trim() ?? "";
                var portText = PortTextBox?.Text?.Trim() ?? "389";

                _isDomainValid = !string.IsNullOrEmpty(domain) &&
                                 !string.IsNullOrEmpty(ldapServer) &&
                                 int.TryParse(portText, out var port) &&
                                 port > 0 && port <= 65535;

                _logger.LogDebug("Domain settings validation: {IsValid}", _isDomainValid);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error validating domain settings");
                _isDomainValid = false;
            }
        }

        /// <summary>
        /// Валидация настроек сервисного администратора
        /// </summary>
        private void ValidateServiceAdminSettings()
        {
            try
            {
                // Проверяем, что все элементы UI доступны
                if (ServiceUsernameTextBox == null || ServicePasswordBox == null || 
                    ServicePasswordConfirmBox == null || SessionTimeoutTextBox == null)
                {
                    _isServiceAdminValid = false;
                    return;
                }
                var username = ServiceUsernameTextBox?.Text?.Trim();
                var password = ServicePasswordBox?.Password;
                var confirmPassword = ServicePasswordConfirmBox?.Password;
                var sessionTimeoutText = SessionTimeoutTextBox?.Text?.Trim();

                // Валидация имени пользователя
                var isUsernameValid = !string.IsNullOrEmpty(username) && username.Length >= 3;

                // Валидация времени сессии
                var isSessionTimeoutValid = int.TryParse(sessionTimeoutText, out var timeout) &&
                                           timeout > 0 && timeout <= 1440;

                // Валидация пароля
                var isPasswordValid = ValidatePassword(password);
                var isPasswordMatch = password == confirmPassword;

                _isServiceAdminValid = isUsernameValid && isSessionTimeoutValid &&
                                      isPasswordValid && isPasswordMatch;

                // Обновляем индикаторы валидации пароля
                UpdatePasswordValidationIndicators(password, confirmPassword);

                _logger.LogDebug("Service admin settings validation: {IsValid}", _isServiceAdminValid);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error validating service admin settings: {Error}", ex.Message);
                _isServiceAdminValid = false;
            }
        }

        /// <summary>
        /// Валидация пароля
        /// </summary>
        private bool ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            return password.Length >= 8 &&
                   _uppercaseRegex.IsMatch(password) &&
                   _lowercaseRegex.IsMatch(password) &&
                   _digitRegex.IsMatch(password) &&
                   _specialRegex.IsMatch(password);
        }

        /// <summary>
        /// Обновление индикаторов валидации пароля
        /// </summary>
        private void UpdatePasswordValidationIndicators(string password, string confirmPassword)
        {
            try
            {
                var successBrush = TryFindResource("SuccessBrush") as WpfBrush ?? Brushes.Green;
                var errorBrush = TryFindResource("ErrorBrush") as WpfBrush ?? Brushes.Red;
                var defaultBrush = TryFindResource("OnSurfaceVariantBrush") as WpfBrush ?? Brushes.Gray;

                // Длина пароля
                PasswordLengthValidation.Foreground = (password?.Length >= 8) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Заглавные буквы
                PasswordUppercaseValidation.Foreground = (!string.IsNullOrEmpty(password) && _uppercaseRegex.IsMatch(password)) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Строчные буквы
                PasswordLowercaseValidation.Foreground = (!string.IsNullOrEmpty(password) && _lowercaseRegex.IsMatch(password)) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Цифры
                PasswordDigitValidation.Foreground = (!string.IsNullOrEmpty(password) && _digitRegex.IsMatch(password)) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Специальные символы
                PasswordSpecialValidation.Foreground = (!string.IsNullOrEmpty(password) && _specialRegex.IsMatch(password)) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Совпадение паролей
                PasswordMatchValidation.Foreground = (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(confirmPassword) && password == confirmPassword) ? successBrush :
                    (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(confirmPassword) ? defaultBrush : errorBrush);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error updating password validation indicators: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Обновление индикаторов валидации
        /// </summary>
        private void UpdateValidationIndicators()
        {
            ValidateDomainSettings();
            ValidateServiceAdminSettings();
            ValidateDatabaseSettings();
        }

        /// <summary>
        /// Инициализация настроек Firebird
        /// </summary>
        private void InitializeFirebirdSettings()
        {
            try
            {
                if (DatabasePathTextBox != null)
                {
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    DatabasePathTextBox.Text = Path.Combine(appDataPath, "WindowsLauncher", "launcher.fdb");
                }

                if (FirebirdPasswordBox != null && string.IsNullOrEmpty(FirebirdPasswordBox.Password))
                {
                    FirebirdPasswordBox.Password = "masterkey";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error initializing Firebird settings");
            }
        }

        /// <summary>
        /// Валидация настроек базы данных
        /// </summary>
        private void ValidateDatabaseSettings()
        {
            try
            {
                if (DatabaseTypeComboBox == null)
                {
                    _isDatabaseValid = true; // По умолчанию SQLite валидна
                    return;
                }

                var selectedItem = DatabaseTypeComboBox.SelectedItem as ComboBoxItem;
                var databaseType = selectedItem?.Tag?.ToString();

                if (databaseType == "SQLite")
                {
                    _isDatabaseValid = true;
                }
                else if (databaseType == "Firebird")
                {
                    _isDatabaseValid = ValidateFirebirdSettings();
                }
                else
                {
                    _isDatabaseValid = true; // Fallback к SQLite
                }

                _logger.LogDebug("Database settings validation: {IsValid}", _isDatabaseValid);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error validating database settings");
                _isDatabaseValid = false;
            }
        }

        /// <summary>
        /// Валидация настроек Firebird
        /// </summary>
        private bool ValidateFirebirdSettings()
        {
            try
            {
                if (DatabasePathTextBox == null || FirebirdModeComboBox == null)
                    return false;

                var databasePath = DatabasePathTextBox.Text?.Trim();
                if (string.IsNullOrEmpty(databasePath))
                    return false;

                var modeItem = FirebirdModeComboBox.SelectedItem as ComboBoxItem;
                var mode = modeItem?.Tag?.ToString();

                if (mode == "ClientServer")
                {
                    // Для клиент-сервер режима проверяем настройки сервера
                    if (FirebirdServerTextBox == null || FirebirdPortTextBox == null ||
                        FirebirdUserTextBox == null || FirebirdPasswordBox == null)
                        return false;

                    var server = FirebirdServerTextBox.Text?.Trim();
                    var portText = FirebirdPortTextBox.Text?.Trim();
                    var user = FirebirdUserTextBox.Text?.Trim();
                    var password = FirebirdPasswordBox.Password;

                    return !string.IsNullOrEmpty(server) &&
                           int.TryParse(portText, out var port) && port > 0 && port <= 65535 &&
                           !string.IsNullOrEmpty(user) &&
                           !string.IsNullOrEmpty(password);
                }

                // Для Embedded режима достаточно указать путь
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error validating Firebird settings");
                return false;
            }
        }

        /// <summary>
        /// Создание конфигурации базы данных из UI
        /// </summary>
        private DatabaseConfiguration CreateDatabaseConfigurationFromUI()
        {
            var selectedItem = DatabaseTypeComboBox?.SelectedItem as ComboBoxItem;
            var databaseTypeTag = selectedItem?.Tag?.ToString();

            var config = new DatabaseConfiguration();

            if (databaseTypeTag == "Firebird")
            {
                config.DatabaseType = DatabaseType.Firebird;
                config.DatabasePath = DatabasePathTextBox?.Text?.Trim() ?? "launcher.fdb";

                var modeItem = FirebirdModeComboBox?.SelectedItem as ComboBoxItem;
                var mode = modeItem?.Tag?.ToString();

                if (mode == "ClientServer")
                {
                    config.ConnectionMode = FirebirdConnectionMode.ClientServer;
                    config.Server = FirebirdServerTextBox?.Text?.Trim() ?? "localhost";
                    if (int.TryParse(FirebirdPortTextBox?.Text?.Trim(), out var port))
                        config.Port = port;
                    config.Username = FirebirdUserTextBox?.Text?.Trim() ?? "SYSDBA";
                    config.Password = FirebirdPasswordBox?.Password ?? "masterkey";
                }
                else
                {
                    config.ConnectionMode = FirebirdConnectionMode.Embedded;
                }
            }
            else
            {
                config.DatabaseType = DatabaseType.SQLite;
            }

            return config;
        }

        /// <summary>
        /// Тестирование подключения к базе данных
        /// </summary>
        private async Task TestDatabaseConnectionAsync()
        {
            if (_isProcessing) return;

            try
            {
                _isProcessing = true;
                TestDatabaseButton.IsEnabled = false;
                DatabaseStatusPanel.Visibility = Visibility.Collapsed;

                var config = CreateDatabaseConfigurationFromUI();
                var result = await _dbConfigService.TestConnectionAsync(config);

                DatabaseStatusPanel.Visibility = Visibility.Visible;
                if (result)
                {
                    DatabaseStatusIndicator.Fill = TryFindResource("SuccessBrush") as WpfBrush ?? Brushes.Green;
                    DatabaseStatusText.Text = "Подключение к базе данных успешно";
                    DatabaseStatusText.Foreground = TryFindResource("SuccessBrush") as WpfBrush ?? Brushes.Green;
                }
                else
                {
                    DatabaseStatusIndicator.Fill = TryFindResource("ErrorBrush") as WpfBrush ?? Brushes.Red;
                    DatabaseStatusText.Text = "Ошибка подключения к базе данных";
                    DatabaseStatusText.Foreground = TryFindResource("ErrorBrush") as WpfBrush ?? Brushes.Red;
                }

                _logger.LogInformation("Database connection test result: {Result}", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing database connection");
                
                DatabaseStatusPanel.Visibility = Visibility.Visible;
                DatabaseStatusIndicator.Fill = TryFindResource("ErrorBrush") as WpfBrush ?? Brushes.Red;
                DatabaseStatusText.Text = $"Ошибка: {ex.Message}";
                DatabaseStatusText.Foreground = TryFindResource("ErrorBrush") as WpfBrush ?? Brushes.Red;
            }
            finally
            {
                _isProcessing = false;
                TestDatabaseButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Обновление прогресса настройки
        /// </summary>
        private void UpdateSetupProgress()
        {
            try
            {
                var completedSteps = 0;
                var totalSteps = 4;

                if (_isDomainValid) completedSteps++;
                if (_isDatabaseValid) completedSteps++;
                if (_isServiceAdminValid) completedSteps++;
                // Четвертый шаг (дополнительные настройки) всегда считается выполненным
                completedSteps++; // Дополнительные настройки

                if (SetupProgressText != null)
                {
                    SetupProgressText.Text = $"Настройка: {completedSteps}/{totalSteps} шагов завершено";
                }

                // Кнопка "Завершить настройку" доступна только если выполнены критически важные шаги
                if (CompleteSetupButton != null)
                {
                    CompleteSetupButton.IsEnabled = _isDomainValid && _isDatabaseValid && _isServiceAdminValid && !_isProcessing;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error updating setup progress: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Тестирование подключения к домену
        /// </summary>
        private async Task TestDomainConnectionAsync()
        {
            if (_isProcessing)
                return;

            try
            {
                SetLoadingState(true, "Проверка подключения к домену...");
                ConnectionStatusPanel.Visibility = Visibility.Collapsed;

                var server = LdapServerTextBox.Text?.Trim();
                var portText = PortTextBox.Text?.Trim();

                if (string.IsNullOrEmpty(server) || !int.TryParse(portText, out var port))
                {
                    ShowConnectionStatus(false, "Некорректные параметры подключения");
                    return;
                }

                var isConnected = await _adService.TestConnectionAsync(server, port);

                ShowConnectionStatus(isConnected,
                    isConnected ? "Подключение успешно" : "Не удалось подключиться к серверу");

                _logger.LogInformation("Domain connection test result: {IsConnected}", isConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing domain connection");
                ShowConnectionStatus(false, $"Ошибка подключения: {ex.Message}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        /// <summary>
        /// Отображение статуса подключения
        /// </summary>
        private void ShowConnectionStatus(bool isSuccess, string message)
        {
            try
            {
                ConnectionStatusPanel.Visibility = Visibility.Visible;
                ConnectionStatusText.Text = message;

                var brush = TryFindResource(isSuccess ? "SuccessBrush" : "ErrorBrush") as WpfBrush ??
                           (isSuccess ? Brushes.Green : Brushes.Red);
                ConnectionStatusIndicator.Fill = brush;
                ConnectionStatusText.Foreground = brush;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error showing connection status: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Завершение настройки
        /// </summary>
        private async Task CompleteSetupAsync()
        {
            if (_isProcessing)
                return;

            try
            {
                SetLoadingState(true, "Сохранение настроек...");

                // Создаем конфигурацию аутентификации
                var authConfig = CreateConfigurationFromForm();
                await _configService.SaveConfigurationAsync(authConfig);

                // Создаем конфигурацию базы данных
                var dbConfig = CreateDatabaseConfigurationFromUI();
                await _dbConfigService.SaveConfigurationAsync(dbConfig);

                // Убеждаемся что база данных существует
                await _dbConfigService.EnsureDatabaseExistsAsync(dbConfig);

                // Создаем сервисного администратора
                await CreateServiceAdminAsync();

                // Применяем дополнительные настройки
                await ApplyAdditionalSettingsAsync();

                SetupCompleted = true;
                DialogResult = true;

                _logger.LogInformation("Setup completed successfully");
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing setup");
                ShowError($"Ошибка завершения настройки: {ex.Message}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        /// <summary>
        /// Создание конфигурации из формы
        /// </summary>
        private AuthenticationConfiguration CreateConfigurationFromForm()
        {
            return new AuthenticationConfiguration
            {
                Domain = DomainTextBox.Text?.Trim(),
                LdapServer = LdapServerTextBox.Text?.Trim(),
                Port = int.Parse(PortTextBox.Text?.Trim() ?? "389"),
                UseTLS = UseTlsCheckBox.IsChecked == true,
                ServiceAdmin = new ServiceAdminConfiguration
                {
                    Username = ServiceUsernameTextBox.Text?.Trim(),
                    SessionTimeoutMinutes = int.Parse(SessionTimeoutTextBox.Text?.Trim() ?? "60")
                },
                IsConfigured = true,
                LastModified = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Создание сервисного администратора
        /// </summary>
        private async Task CreateServiceAdminAsync()
        {
            try
            {
                var username = ServiceUsernameTextBox.Text?.Trim();
                var password = ServicePasswordBox.Password;

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    await _authService.CreateServiceAdminAsync(username, password);
                    _logger.LogInformation("Service admin created: {Username}", username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error creating service admin: {Error}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Применение дополнительных настроек
        /// </summary>
        private async Task ApplyAdditionalSettingsAsync()
        {
            try
            {
                // Настройка аудита
                if (EnableAuditingCheckBox.IsChecked == true)
                {
                    // Включаем аудит через соответствующий сервис
                    _logger.LogInformation("Auditing enabled");
                }

                // Автозагрузка
                if (AutoStartCheckBox.IsChecked == true)
                {
                    // Добавляем в автозагрузку
                    await SetAutoStartAsync(true);
                    _logger.LogInformation("Auto-start enabled");
                }

                // Язык интерфейса
                var selectedLanguage = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(selectedLanguage))
                {
                    await SetLanguageAsync(selectedLanguage);
                    _logger.LogInformation("Language set to: {Language}", selectedLanguage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error applying additional settings: {Error}", ex.Message);
                // Не прерываем процесс настройки из-за дополнительных настроек
            }
        }

        /// <summary>
        /// Настройка автозагрузки
        /// </summary>
        private async Task SetAutoStartAsync(bool enable)
        {
            try
            {
                // Реализация настройки автозагрузки через реестр Windows
                await Task.Run(() =>
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                    if (enable)
                    {
                        var executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key?.SetValue("KDV Launcher", executablePath);
                    }
                    else
                    {
                        key?.DeleteValue("KDV Launcher", false);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error setting auto-start: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Настройка языка интерфейса
        /// </summary>
        private async Task SetLanguageAsync(string languageCode)
        {
            try
            {
                await Task.Run(() =>
                {
                    var culture = new CultureInfo(languageCode);
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;

                    // Сохраняем настройку языка в конфигурации
                    // TODO: Использовать конфигурационный сервис вместо Settings
                    _logger.LogInformation("Language preference saved: {Language}", languageCode);
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error setting language: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Установка состояния загрузки
        /// </summary>
        private void SetLoadingState(bool isLoading, string message = null)
        {
            try
            {
                _isProcessing = isLoading;
                LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

                if (!string.IsNullOrEmpty(message))
                {
                    LoadingText.Text = message;
                }

                // Обновляем доступность кнопок
                TestConnectionButton.IsEnabled = !isLoading;
                CompleteSetupButton.IsEnabled = !isLoading && _isDomainValid && _isServiceAdminValid;
                SkipButton.IsEnabled = !isLoading;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error setting loading state: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Отображение ошибки
        /// </summary>
        private void ShowError(string message)
        {
            try
            {
                ErrorTextBlock.Text = message;
                ErrorPanel.Visibility = Visibility.Visible;

                // Автоматически скрываем ошибку через 5 секунд
                Task.Delay(5000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ErrorPanel.Visibility = Visibility.Collapsed;
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error showing error message: {Error}", ex.Message);
            }
        }

        #endregion

        #region Window Events

        /// <summary>
        /// Обработчик закрытия окна
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Предупреждаем о несохраненных изменениях
                if (!SetupCompleted && (_isDomainValid || _isServiceAdminValid))
                {
                    var result = WpfMessageBox.Show(
                        "У вас есть несохраненные изменения.\n\n" +
                        "Вы уверены, что хотите выйти без сохранения настроек?",
                        "Несохраненные изменения",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                base.OnClosing(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during window closing");
            }
        }

        #endregion
    }
}