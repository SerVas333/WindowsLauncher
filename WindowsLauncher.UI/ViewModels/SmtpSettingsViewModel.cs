using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Email;
using WindowsLauncher.Core.Models.Email;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Infrastructure.Commands;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.Views;
using WindowsLauncher.Services.Security;
using Microsoft.Extensions.DependencyInjection;
using WindowsLauncher.UI.Infrastructure.Extensions;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для управления настройками SMTP серверов
    /// Поддерживает Primary/Backup серверы с валидацией и тестированием
    /// </summary>
    public class SmtpSettingsViewModel : ViewModelBase
    {
        #region Fields
        
        private readonly ISmtpSettingsRepository _smtpRepository;
        private readonly IEmailService _emailService;
        private readonly WindowsLauncher.Core.Interfaces.IEncryptionService _encryptionService;
        private readonly ILogger<SmtpSettingsViewModel> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        
        private ObservableCollection<SmtpSettings> _smtpServers = new();
        private SmtpSettings? _selectedServer = null;
        private SmtpSettings? _primaryServer = null;
        private SmtpSettings? _backupServer = null;
        
        private bool _isLoading = false;
        private bool _isTestingConnection = false;
        private string _statusMessage = string.Empty;
        private string _testResult = string.Empty;
        
        // Tracking for unsaved changes detection
        private readonly List<SmtpEditWindow> _openEditWindows = new();
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Все SMTP серверы
        /// </summary>
        public ObservableCollection<SmtpSettings> SmtpServers
        {
            get => _smtpServers;
            set => SetProperty(ref _smtpServers, value);
        }
        
        /// <summary>
        /// Выбранный сервер для редактирования
        /// </summary>
        public SmtpSettings? SelectedServer
        {
            get => _selectedServer;
            set => SetProperty(ref _selectedServer, value);
        }
        
        /// <summary>
        /// Основной SMTP сервер
        /// </summary>
        public SmtpSettings? PrimaryServer
        {
            get => _primaryServer;
            set => SetProperty(ref _primaryServer, value);
        }
        
        /// <summary>
        /// Резервный SMTP сервер
        /// </summary>
        public SmtpSettings? BackupServer
        {
            get => _backupServer;
            set => SetProperty(ref _backupServer, value);
        }
        
        /// <summary>
        /// Идет загрузка данных
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        /// <summary>
        /// Идет тестирование подключения
        /// </summary>
        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            set => SetProperty(ref _isTestingConnection, value);
        }
        
        /// <summary>
        /// Сообщение статуса
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        /// <summary>
        /// Результат тестирования подключения
        /// </summary>
        public string TestResult
        {
            get => _testResult;
            set => SetProperty(ref _testResult, value);
        }
        
        // Computed properties
        public bool HasPrimaryServer => PrimaryServer != null;
        public bool HasBackupServer => BackupServer != null;
        public bool CanAddPrimaryServer => !HasPrimaryServer;
        public bool CanAddBackupServer => !HasBackupServer;
        public string PrimaryServerStatus => HasPrimaryServer ? $"✅ {PrimaryServer!.DisplayName}" : "❌ Не настроен";
        public string BackupServerStatus => HasBackupServer ? $"✅ {BackupServer!.DisplayName}" : "⚠️ Не настроен";
        
        /// <summary>
        /// Проверить наличие несохраненных изменений в открытых окнах редактирования
        /// </summary>
        public bool HasUnsavedChanges
        {
            get
            {
                // Проверяем все открытые окна редактирования на несохраненные изменения
                return _openEditWindows.Any(window => 
                    window.IsVisible && 
                    window.DataContext is SmtpEditViewModel viewModel && 
                    viewModel.HasUnsavedChanges);
            }
        }
        
        #endregion
        
        #region Commands
        
        public AsyncRelayCommand LoadServersCommand { get; }
        public AsyncRelayCommand AddPrimaryServerCommand { get; }
        public AsyncRelayCommand AddBackupServerCommand { get; }
        public AsyncRelayCommand<SmtpSettings> EditServerCommand { get; }
        public AsyncRelayCommand<SmtpSettings> DeleteServerCommand { get; }
        public AsyncRelayCommand<SmtpSettings> TestConnectionCommand { get; }
        public RelayCommand CloseCommand { get; }
        
        #endregion
        
        #region Constructor
        
        public SmtpSettingsViewModel(
            ISmtpSettingsRepository smtpRepository,
            IEmailService emailService,
            WindowsLauncher.Core.Interfaces.IEncryptionService encryptionService,
            ILogger<SmtpSettingsViewModel> logger,
            IDialogService dialogService,
            IServiceScopeFactory serviceScopeFactory) : base(logger, dialogService)
        {
            _smtpRepository = smtpRepository ?? throw new ArgumentNullException(nameof(smtpRepository));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            
            // Initialize commands
            LoadServersCommand = new AsyncRelayCommand(LoadServersAsync);
            AddPrimaryServerCommand = new AsyncRelayCommand(AddPrimaryServerAsync, () => CanAddPrimaryServer);
            AddBackupServerCommand = new AsyncRelayCommand(AddBackupServerAsync, () => CanAddBackupServer);
            EditServerCommand = new AsyncRelayCommand<SmtpSettings>(EditServerAsync);
            DeleteServerCommand = new AsyncRelayCommand<SmtpSettings>(DeleteServerAsync);
            TestConnectionCommand = new AsyncRelayCommand<SmtpSettings>(TestConnectionAsync);
            CloseCommand = new RelayCommand(CloseWindow);
            
            // Subscribe to property changes for command updates
            PropertyChanged += OnPropertyChanged;
            
            // Load servers on initialization
            _ = LoadServersAsync();
        }
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Загрузить все SMTP серверы
        /// </summary>
        public async Task LoadServersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Загрузка SMTP серверов...";
                
                var servers = await _smtpRepository.GetActiveSettingsAsync();
                
                SmtpServers.Clear();
                foreach (var server in servers)
                {
                    SmtpServers.Add(server);
                }
                
                // Разделяем по типам
                PrimaryServer = servers.FirstOrDefault(s => s.ServerType == SmtpServerType.Primary);
                BackupServer = servers.FirstOrDefault(s => s.ServerType == SmtpServerType.Backup);
                
                StatusMessage = $"Загружено {servers.Count} SMTP серверов";
                
                _logger.LogInformation("Loaded {Count} SMTP servers (Primary: {HasPrimary}, Backup: {HasBackup})", 
                    servers.Count, HasPrimaryServer, HasBackupServer);
                
                // Update command availability
                UpdateCommandAvailability();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SMTP servers");
                StatusMessage = $"Ошибка загрузки: {ex.Message}";
                MessageBox.Show($"Ошибка загрузки SMTP серверов: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        /// <summary>
        /// Добавить основной SMTP сервер
        /// </summary>
        private async Task AddPrimaryServerAsync()
        {
            await AddServerAsync(SmtpServerType.Primary);
        }
        
        /// <summary>
        /// Добавить резервный SMTP сервер
        /// </summary>
        private async Task AddBackupServerAsync()
        {
            await AddServerAsync(SmtpServerType.Backup);
        }
        
        /// <summary>
        /// Добавить новый SMTP сервер
        /// </summary>
        private async Task AddServerAsync(SmtpServerType serverType)
        {
            try
            {
                var newServer = new SmtpSettings
                {
                    Name = serverType == SmtpServerType.Primary ? "Основной SMTP" : "Резервный SMTP",
                    ServerType = serverType,
                    Port = 587,
                    UseSSL = true,
                    UseStartTLS = true,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                
                await CreateNewServerAsync(newServer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding {ServerType} SMTP server", serverType);
                StatusMessage = $"Ошибка создания сервера: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Создать новый SMTP сервер
        /// </summary>
        private async Task CreateNewServerAsync(SmtpSettings newServer)
        {
            try
            {
                _logger.LogInformation("Opening SMTP editor for new server: {ServerType}", newServer.ServerType);
                
                // Создаем SmtpEditViewModel через scoped scope для доступа к Scoped сервисам
                var editViewModel = _serviceScopeFactory.CreateScopedService<SmtpEditViewModel>();
                
                // Настраиваем ViewModel для создания нового сервера
                editViewModel.SetServer(newServer, isNew: true);
                
                // Создаем и показываем окно
                var editWindow = new SmtpEditWindow(editViewModel)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Title = $"Добавить {(newServer.ServerType == SmtpServerType.Primary ? "основной" : "резервный")} SMTP сервер"
                };
                
                // Добавляем окно в список отслеживаемых
                _openEditWindows.Add(editWindow);
                
                // Подписываемся на закрытие окна для очистки списка
                editWindow.Closed += (s, e) => 
                {
                    _openEditWindows.Remove(editWindow);
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                };
                
                var result = editWindow.ShowDialog();
                
                if (result == true)
                {
                    // Обновляем список серверов после сохранения
                    await LoadServersAsync();
                    StatusMessage = $"SMTP сервер '{newServer.Name}' успешно создан";
                    _logger.LogInformation("SMTP server created successfully: {ServerName}", newServer.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new SMTP server: {ServerType}", newServer.ServerType);
                StatusMessage = $"Ошибка создания сервера: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Редактировать SMTP сервер
        /// </summary>
        private async Task EditServerAsync(SmtpSettings? server)
        {
            if (server == null) return;
            
            try
            {
                _logger.LogInformation("Opening SMTP editor for server: {ServerName} ({ServerType})", 
                    server.Name, server.ServerType);
                
                // Создаем SmtpEditViewModel через scoped scope для доступа к Scoped сервисам
                var editViewModel = _serviceScopeFactory.CreateScopedService<SmtpEditViewModel>();
                
                // Настраиваем ViewModel для редактирования существующего сервера
                editViewModel.SetServer(server, isNew: false);
                
                // Создаем и показываем окно
                var editWindow = new SmtpEditWindow(editViewModel)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Title = $"Редактировать SMTP сервер: {server.Name}"
                };
                
                // Добавляем окно в список отслеживаемых
                _openEditWindows.Add(editWindow);
                
                // Подписываемся на закрытие окна для очистки списка
                editWindow.Closed += (s, e) => 
                {
                    _openEditWindows.Remove(editWindow);
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                };
                
                var result = editWindow.ShowDialog();
                
                if (result == true)
                {
                    // Обновляем список серверов после сохранения
                    await LoadServersAsync();
                    StatusMessage = $"SMTP сервер '{server.Name}' успешно обновлен";
                    _logger.LogInformation("SMTP server updated successfully: {ServerName}", server.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing SMTP server {ServerId}", server.Id);
                StatusMessage = $"Ошибка редактирования: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Удалить SMTP сервер
        /// </summary>
        private async Task DeleteServerAsync(SmtpSettings? server)
        {
            if (server == null) return;
            
            try
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить SMTP сервер '{server.DisplayName}'?\\n\\n" +
                    $"Тип: {server.ServerType}\\n" +
                    $"После удаления {(server.ServerType == SmtpServerType.Primary ? "основного" : "резервного")} " +
                    $"сервера отправка писем может быть нарушена.\\n\\n" +
                    $"Это действие нельзя отменить.", 
                    "Подтверждение удаления", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    await _smtpRepository.DeleteAsync(server.Id);
                    await LoadServersAsync(); // Перезагружаем список
                    
                    StatusMessage = $"SMTP сервер '{server.Name}' удален";
                    _logger.LogInformation("Deleted SMTP server: {ServerName} (ID: {ServerId}, Type: {ServerType})", 
                        server.Name, server.Id, server.ServerType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting SMTP server {ServerId}", server.Id);
                StatusMessage = $"Ошибка удаления: {ex.Message}";
                MessageBox.Show($"Ошибка при удалении SMTP сервера: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Тестировать подключение к SMTP серверу
        /// </summary>
        private async Task TestConnectionAsync(SmtpSettings? server)
        {
            if (server == null) return;
            
            try
            {
                IsTestingConnection = true;
                TestResult = string.Empty;
                StatusMessage = $"Тестирование подключения к {server.DisplayName}...";
                
                // Используем реальный EmailService для тестирования SMTP подключения
                var testResult = await _emailService.TestSmtpConnectionAsync(server);
                
                if (testResult.IsSuccess)
                {
                    TestResult = $"✅ Подключение к {server.DisplayName} успешно";
                    StatusMessage = $"Тестирование завершено успешно за {testResult.Duration.TotalMilliseconds:F0} мс";
                    
                    if (!string.IsNullOrEmpty(testResult.ServerInfo))
                    {
                        StatusMessage += $" - {testResult.ServerInfo}";
                    }
                    
                    // Обновляем LastSuccessfulSend и сбрасываем счетчик ошибок
                    server.LastSuccessfulSend = DateTime.Now;
                    server.ConsecutiveErrors = 0;
                    await _smtpRepository.UpdateAsync(server);
                }
                else
                {
                    TestResult = $"❌ Не удалось подключиться к {server.DisplayName}";
                    StatusMessage = $"Тестирование не пройдено за {testResult.Duration.TotalMilliseconds:F0} мс";
                    
                    if (!string.IsNullOrEmpty(testResult.ErrorMessage))
                    {
                        TestResult += $": {testResult.ErrorMessage}";
                    }
                    
                    // Увеличиваем счетчик ошибок
                    server.ConsecutiveErrors++;
                    await _smtpRepository.UpdateAsync(server);
                }
                
                _logger.LogInformation("SMTP connection test for {ServerName}: {Result} in {Duration}ms", 
                    server.Name, testResult.IsSuccess ? "SUCCESS" : "FAILED", testResult.Duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing SMTP connection for server {ServerId}", server.Id);
                TestResult = $"❌ Ошибка тестирования: {ex.Message}";
                StatusMessage = "Ошибка при тестировании";
                
                MessageBox.Show($"Ошибка при тестировании SMTP подключения: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsTestingConnection = false;
            }
        }
        
        /// <summary>
        /// Закрыть окно настроек SMTP
        /// </summary>
        private void CloseWindow()
        {
            _logger.LogDebug("Close SMTP settings window requested");
            OnCloseRequested();
        }
        
        /// <summary>
        /// Событие закрытия окна
        /// </summary>
        public event EventHandler? CloseRequested;
        
        /// <summary>
        /// Вызывает событие закрытия окна
        /// </summary>
        private void OnCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Обновить доступность команд
        /// </summary>
        private void UpdateCommandAvailability()
        {
            AddPrimaryServerCommand.RaiseCanExecuteChanged();
            AddBackupServerCommand.RaiseCanExecuteChanged();
            
            // Обновляем computed properties
            OnPropertyChanged(nameof(HasPrimaryServer));
            OnPropertyChanged(nameof(HasBackupServer));
            OnPropertyChanged(nameof(CanAddPrimaryServer));
            OnPropertyChanged(nameof(CanAddBackupServer));
            OnPropertyChanged(nameof(PrimaryServerStatus));
            OnPropertyChanged(nameof(BackupServerStatus));
        }
        
        /// <summary>
        /// Обработчик изменения свойств
        /// </summary>
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PrimaryServer):
                case nameof(BackupServer):
                    UpdateCommandAvailability();
                    break;
            }
        }
        
        /// <summary>
        /// Очистка ресурсов при закрытии окна настроек
        /// </summary>
        public void Cleanup()
        {
            // Закрываем все открытые окна редактирования
            foreach (var window in _openEditWindows.ToList())
            {
                try
                {
                    if (window.IsVisible)
                    {
                        window.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing SMTP edit window during cleanup");
                }
            }
            
            _openEditWindows.Clear();
        }
        
        #endregion
    }
}