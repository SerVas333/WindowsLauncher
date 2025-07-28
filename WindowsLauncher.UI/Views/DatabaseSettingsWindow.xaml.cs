using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Services;
using WpfApplication = System.Windows.Application;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Окно настроек базы данных
    /// </summary>
    public partial class DatabaseSettingsWindow : Window
    {
        private readonly IDatabaseConfigurationService? _dbConfigService;
        private readonly IApplicationVersionService? _versionService;
        private readonly IDatabaseMigrationService? _migrationService;
        private readonly ILogger<DatabaseSettingsWindow>? _logger;
        private DatabaseConfiguration _currentConfig;
        private bool _isLoading = false;

        public DatabaseConfiguration? ResultConfiguration { get; private set; }

        public DatabaseSettingsWindow()
        {
            InitializeComponent();

            // Получаем сервисы
            var app = WpfApplication.Current as App;
            if (app?.ServiceProvider != null)
            {
                _dbConfigService = app.ServiceProvider.GetService<IDatabaseConfigurationService>();
                _versionService = app.ServiceProvider.GetService<IApplicationVersionService>();
                _migrationService = app.ServiceProvider.GetService<IDatabaseMigrationService>();
                _logger = app.ServiceProvider.GetService<ILogger<DatabaseSettingsWindow>>();
            }

            // Инициализация
            _currentConfig = new DatabaseConfiguration();
            
            // Загружаем конфигурацию после того как окно загрузится
            Loaded += DatabaseSettingsWindow_Loaded;
        }

        private async void DatabaseSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadCurrentConfigurationAsync();
                await LoadDatabaseVersionInfoAsync();
                InitializeUI();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during window initialization");
                UpdateStatus($"Ошибка инициализации: {ex.Message}", StatusType.Error);
            }
        }

        private async Task LoadCurrentConfigurationAsync()
        {
            try
            {
                _isLoading = true;
                
                if (_dbConfigService != null)
                {
                    _currentConfig = await _dbConfigService.GetConfigurationAsync() ?? new DatabaseConfiguration();
                }
                else
                {
                    _currentConfig = new DatabaseConfiguration(); // Default SQLite
                }

                UpdateStatus("Текущие настройки загружены", StatusType.Info);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading database configuration");
                _currentConfig = new DatabaseConfiguration(); // Fallback to default
                UpdateStatus($"Ошибка загрузки настроек: {ex.Message}", StatusType.Error);
            }
            finally
            {
                _isLoading = false;
                // Обновляем UI после сброса флага _isLoading
                UpdateUIFromConfiguration();
            }
        }

        private async Task LoadDatabaseVersionInfoAsync()
        {
            if (_versionService == null) 
            {
                UpdateVersionUI("Неизвестно", "Сервис версий недоступен", "Проверка невозможна", false);
                return;
            }

            try
            {
                var appVersion = _versionService.GetApplicationVersion();
                var isInitialized = await _versionService.IsDatabaseInitializedAsync();
                
                if (isInitialized)
                {
                    var dbVersion = await _versionService.GetDatabaseVersionAsync();
                    var isCompatible = await _versionService.IsDatabaseCompatibleAsync();

                    var compatibilityText = isCompatible ? "✅ Совместимо" : "⚠️ Несовместимо - требуется миграция";
                    this.Title = $"Настройки базы данных - KDV Corporate Portal (БД: v{dbVersion}, Приложение: v{appVersion}) - {compatibilityText}";
                    
                    UpdateVersionUI(appVersion, $"v{dbVersion}", $"Совместимость: {compatibilityText}", false);
                }
                else
                {
                    this.Title = $"Настройки базы данных - KDV Corporate Portal (Приложение: v{appVersion}) - БД не инициализирована";
                    UpdateVersionUI(appVersion, "Не инициализирована", "База данных будет создана при запуске", true);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading database version info");
                this.Title = "Настройки базы данных - KDV Corporate Portal";
                UpdateVersionUI("Ошибка", "Ошибка", $"Ошибка проверки: {ex.Message}", false);
            }
        }

        private void UpdateVersionUI(string appVersion, string dbVersion, string compatibility, bool showInitButton)
        {
            try
            {
                if (AppVersionText != null) AppVersionText.Text = appVersion;
                if (DbVersionText != null) DbVersionText.Text = dbVersion;
                if (CompatibilityText != null) CompatibilityText.Text = compatibility;
                if (InitializeDatabaseButton != null) 
                {
                    InitializeDatabaseButton.Visibility = showInitButton ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating version UI");
            }
        }

        private void InitializeUI()
        {
            try
            {
                // Устанавливаем надежный пароль по умолчанию для Firebird (если контрол доступен)
                if (PasswordBox != null && string.IsNullOrEmpty(PasswordBox.Password))
                {
                    PasswordBox.Password = "KDV_Launcher_2025!"; // Secure default password
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing UI");
            }
        }

        private void UpdateUIFromConfiguration()
        {
            if (_isLoading) return;

            try
            {
                // Тип БД
                SQLiteRadio.IsChecked = _currentConfig.DatabaseType == DatabaseType.SQLite;
                FirebirdRadio.IsChecked = _currentConfig.DatabaseType == DatabaseType.Firebird;

                // SQLite настройки
                SQLitePathTextBox.Text = _currentConfig.DatabasePath;

                // Firebird настройки
                FirebirdPathTextBox.Text = _currentConfig.DatabasePath;
                EmbeddedRadio.IsChecked = _currentConfig.ConnectionMode == FirebirdConnectionMode.Embedded;
                ClientServerRadio.IsChecked = _currentConfig.ConnectionMode == FirebirdConnectionMode.ClientServer;
                
                ServerTextBox.Text = _currentConfig.Server ?? "localhost";
                PortTextBox.Text = _currentConfig.Port.ToString();
                DatabaseNameTextBox.Text = _currentConfig.DatabasePath;
                
                UsernameTextBox.Text = _currentConfig.Username;
                PasswordBox.Password = _currentConfig.Password;
                
                CharsetComboBox.Text = _currentConfig.Charset;
                DialectComboBox.Text = _currentConfig.Dialect.ToString();
                TimeoutTextBox.Text = _currentConfig.ConnectionTimeout.ToString();

                // Обновляем видимость панелей
                UpdatePanelVisibility();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating UI from configuration");
            }
        }

        private void UpdatePanelVisibility()
        {
            // Проверяем, что все контролы инициализированы
            if (SQLiteSettings == null || FirebirdSettings == null || 
                EmbeddedPanel == null || ClientServerPanel == null ||
                SQLiteRadio == null || FirebirdRadio == null ||
                EmbeddedRadio == null || ClientServerRadio == null)
            {
                _logger?.LogDebug("UI controls not yet initialized, skipping panel visibility update");
                return;
            }

            try
            {
                // Показываем/скрываем панели в зависимости от выбранного типа БД
                SQLiteSettings.Visibility = SQLiteRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                FirebirdSettings.Visibility = FirebirdRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

                // Для Firebird показываем/скрываем панели режимов
                if (FirebirdRadio.IsChecked == true)
                {
                    EmbeddedPanel.Visibility = EmbeddedRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                    ClientServerPanel.Visibility = ClientServerRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating panel visibility");
            }
        }

        private DatabaseConfiguration CreateConfigurationFromUI()
        {
            var config = new DatabaseConfiguration();

            // Тип БД
            config.DatabaseType = SQLiteRadio.IsChecked == true ? DatabaseType.SQLite : DatabaseType.Firebird;

            if (config.DatabaseType == DatabaseType.SQLite)
            {
                // SQLite настройки
                config.DatabasePath = SQLitePathTextBox.Text?.Trim() ?? "launcher.db";
            }
            else
            {
                // Firebird настройки
                config.ConnectionMode = EmbeddedRadio.IsChecked == true 
                    ? FirebirdConnectionMode.Embedded 
                    : FirebirdConnectionMode.ClientServer;

                if (config.ConnectionMode == FirebirdConnectionMode.Embedded)
                {
                    config.DatabasePath = FirebirdPathTextBox.Text?.Trim() ?? "launcher.fdb";
                }
                else
                {
                    config.Server = ServerTextBox.Text?.Trim() ?? "localhost";
                    config.DatabasePath = DatabaseNameTextBox.Text?.Trim() ?? "launcher.fdb";
                    
                    if (int.TryParse(PortTextBox.Text, out int port))
                        config.Port = port;
                }

                config.Username = UsernameTextBox.Text?.Trim() ?? "SYSDBA";
                config.Password = PasswordBox.Password ?? "KDV_Launcher_2025!";
                config.Charset = CharsetComboBox.Text?.Trim() ?? "UTF8";
                
                if (int.TryParse(DialectComboBox.Text, out int dialect))
                    config.Dialect = dialect;
                
                if (int.TryParse(TimeoutTextBox.Text, out int timeout))
                    config.ConnectionTimeout = timeout;
            }

            return config;
        }

        private void UpdateStatus(string message, StatusType type)
        {
            try
            {
                // Проверяем что контролы инициализированы
                if (StatusText == null || StatusIndicator == null)
                {
                    _logger?.LogDebug("Status controls not yet initialized, message: {Message}", message);
                    return;
                }

                StatusText.Text = message;
                
                var brush = type switch
                {
                    StatusType.Success => (Brush)FindResource("SuccessBrush"),
                    StatusType.Warning => (Brush)FindResource("WarningBrush"),
                    StatusType.Error => (Brush)FindResource("ErrorBrush"),
                    StatusType.Info => (Brush)FindResource("InfoBrush"),
                    _ => (Brush)FindResource("CorporateTextMuted")
                };
                
                StatusIndicator.Fill = brush;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating status: {Message}", message);
            }
        }

        #region Event Handlers

        private void DatabaseType_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            
            UpdatePanelVisibility();
            UpdateStatus("Настройки изменены", StatusType.Warning);
        }

        private void FirebirdMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            
            UpdatePanelVisibility();
            UpdateStatus("Настройки изменены", StatusType.Warning);
        }

        private void BrowseSQLite_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Выберите файл базы данных SQLite",
                Filter = "SQLite Database|*.db;*.sqlite;*.sqlite3|All Files|*.*",
                DefaultExt = "db",
                FileName = SQLitePathTextBox.Text
            };

            if (dialog.ShowDialog() == true)
            {
                SQLitePathTextBox.Text = dialog.FileName;
                UpdateStatus("Настройки изменены", StatusType.Warning);
            }
        }

        private void BrowseFirebird_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Выберите файл базы данных Firebird",
                Filter = "Firebird Database|*.fdb;*.gdb|All Files|*.*",
                DefaultExt = "fdb",
                FileName = FirebirdPathTextBox.Text
            };

            if (dialog.ShowDialog() == true)
            {
                FirebirdPathTextBox.Text = dialog.FileName;
                UpdateStatus("Настройки изменены", StatusType.Warning);
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestConnectionButton.IsEnabled = false;
                TestConnectionButton.Content = "⏳ Тестирование...";
                UpdateStatus("Проверка подключения...", StatusType.Info);

                var config = CreateConfigurationFromUI();
                
                // Валидация
                var validation = config.ValidateConfiguration();
                if (!validation.IsValid)
                {
                    var errors = string.Join("; ", validation.Errors);
                    UpdateStatus($"Ошибка конфигурации: {errors}", StatusType.Error);
                    return;
                }

                // Тест подключения и версионности
                if (_dbConfigService != null)
                {
                    var testResult = await _dbConfigService.TestConnectionAsync(config);
                    if (testResult)
                    {
                        // Проверяем совместимость версий
                        if (_versionService != null)
                        {
                            var isInitialized = await _versionService.IsDatabaseInitializedAsync();
                            if (isInitialized)
                            {
                                var isCompatible = await _versionService.IsDatabaseCompatibleAsync();
                                if (isCompatible)
                                {
                                    var dbVersion = await _versionService.GetDatabaseVersionAsync();
                                    var appVersion = _versionService.GetApplicationVersion();
                                    UpdateStatus($"✅ Подключение успешно! БД: v{dbVersion}, Приложение: v{appVersion}", StatusType.Success);
                                }
                                else
                                {
                                    UpdateStatus("⚠️ Подключение установлено, но версии БД и приложения несовместимы", StatusType.Warning);
                                }
                            }
                            else
                            {
                                UpdateStatus("✅ Подключение успешно! БД не инициализирована (будет создана при запуске)", StatusType.Success);
                            }
                        }
                        else
                        {
                            UpdateStatus("✅ Подключение успешно!", StatusType.Success);
                        }
                    }
                    else
                    {
                        UpdateStatus("❌ Ошибка подключения", StatusType.Error);
                    }
                }
                else
                {
                    UpdateStatus("✅ Конфигурация корректна (тест подключения недоступен)", StatusType.Success);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error testing connection");
                UpdateStatus($"❌ Ошибка тестирования: {ex.Message}", StatusType.Error);
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
                TestConnectionButton.Content = "🔍 Тест подключения";
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "💾 Сохранение...";

                var config = CreateConfigurationFromUI();
                
                // Валидация
                var validation = config.ValidateConfiguration();
                if (!validation.IsValid)
                {
                    var errors = string.Join("\\n", validation.Errors);
                    MessageBox.Show($"Ошибки в конфигурации:\\n{errors}", "Ошибка валидации", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Сохранение
                if (_dbConfigService != null)
                {
                    await _dbConfigService.SaveConfigurationAsync(config);
                    UpdateStatus("✅ Настройки сохранены", StatusType.Success);
                }

                ResultConfiguration = config;
                
                // Проверяем нужна ли инициализация БД
                var needsInitialization = false;
                if (_versionService != null)
                {
                    try
                    {
                        needsInitialization = !await _versionService.IsDatabaseInitializedAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Unable to check database initialization status");
                        needsInitialization = true; // Assume initialization needed if check fails
                    }
                }

                string message;
                if (needsInitialization)
                {
                    message = "Настройки базы данных сохранены.\\n\\nБаза данных будет инициализирована при следующем запуске приложения с применением миграций v1.0.0.001.\\n\\nПерезапустить сейчас?";
                }
                else
                {
                    message = "Настройки базы данных сохранены.\\n\\nДля применения изменений необходимо перезапустить приложение.\\n\\nПерезапустить сейчас?";
                }
                
                // Показываем сообщение о необходимости перезапуска
                var result = MessageBox.Show(
                    message,
                    "Настройки сохранены",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Перезапуск приложения
                    System.Diagnostics.Process.Start(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                    WpfApplication.Current.Shutdown();
                }
                else
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving configuration");
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus($"❌ Ошибка сохранения: {ex.Message}", StatusType.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "Сохранить";
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Сбросить все настройки к значениям по умолчанию?",
                "Подтверждение сброса",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _currentConfig = new DatabaseConfiguration(); // Default SQLite
                UpdateUIFromConfiguration();
                UpdateStatus("Настройки сброшены к значениям по умолчанию", StatusType.Info);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void InitializeDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InitializeDatabaseButton.IsEnabled = false;
                InitializeDatabaseButton.Content = "⏳ Инициализация...";
                UpdateStatus("Инициализация базы данных...", StatusType.Info);

                var config = CreateConfigurationFromUI();
                
                // Валидация
                var validation = config.ValidateConfiguration();
                if (!validation.IsValid)
                {
                    var errors = string.Join("; ", validation.Errors);
                    UpdateStatus($"Ошибка конфигурации: {errors}", StatusType.Error);
                    return;
                }

                // Сохраняем конфигурацию сначала
                if (_dbConfigService != null)
                {
                    await _dbConfigService.SaveConfigurationAsync(config);
                }

                // Выполняем миграции
                if (_migrationService != null)
                {
                    await _migrationService.MigrateAsync();
                    UpdateStatus("✅ База данных инициализирована успешно!", StatusType.Success);
                    
                    // Обновляем информацию о версии
                    await LoadDatabaseVersionInfoAsync();
                }
                else
                {
                    UpdateStatus("❌ Сервис миграций недоступен", StatusType.Error);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing database");
                UpdateStatus($"❌ Ошибка инициализации: {ex.Message}", StatusType.Error);
                
                // Показываем подробное сообщение
                MessageBox.Show(
                    $"Ошибка инициализации базы данных:\\n\\n{ex.Message}\\n\\nПроверьте настройки подключения и права доступа.",
                    "Ошибка инициализации",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                InitializeDatabaseButton.IsEnabled = true;
                InitializeDatabaseButton.Content = "🔧 Инициализировать БД";
            }
        }

        #endregion

        private enum StatusType
        {
            Info,
            Success,
            Warning,
            Error
        }
    }
}