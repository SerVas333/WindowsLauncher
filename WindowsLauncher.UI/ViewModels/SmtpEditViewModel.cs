using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using FontAwesome.WPF;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Email;
using WindowsLauncher.Core.Models.Email;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Infrastructure.Commands;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.Services.Security;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для редактирования SMTP сервера
    /// </summary>
    public class SmtpEditViewModel : ViewModelBase
    {
        #region Fields
        
        private readonly ISmtpSettingsRepository _smtpRepository;
        private readonly IEmailService _emailService;
        private readonly WindowsLauncher.Core.Interfaces.IEncryptionService _encryptionService;
        private readonly ILogger<SmtpEditViewModel> _logger;
        
        private SmtpSettings _originalSettings;
        private bool _isNewServer;
        private bool _isTestingConnection = false;
        private bool _isPasswordVisible = false;
        
        // Form fields
        private string _serverName = string.Empty;
        private SmtpServerType _selectedServerType = SmtpServerType.Primary;
        private string _host = string.Empty;
        private int _port = 587;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private bool _useSSL = true;
        private bool _useStartTLS = true;
        private string _defaultFromEmail = string.Empty;
        private string _defaultFromName = string.Empty;
        
        // UI state
        private string _validationMessage = string.Empty;
        private string _testResult = string.Empty;
        private string _testStatusMessage = string.Empty;
        private Brush _testResultBackground = Brushes.Transparent;
        private Brush _testResultBorderBrush = Brushes.Transparent;
        private Brush _testResultForeground = Brushes.Black;
        
        #endregion
        
        #region Properties
        
        // Window properties
        public string WindowTitle => _isNewServer ? "Добавить SMTP сервер" : $"Редактировать SMTP сервер - {_originalSettings.Name}";
        public string HeaderTitle => _isNewServer ? "Новый SMTP сервер" : "Редактирование SMTP сервера";
        public string HeaderSubtitle => _isNewServer ? "Настройка параметров нового сервера" : "Изменение параметров существующего сервера";
        
        // Form properties
        public string ServerName
        {
            get => _serverName;
            set
            {
                if (SetProperty(ref _serverName, value))
                {
                    ValidateForm();
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        public SmtpServerType SelectedServerType
        {
            get => _selectedServerType;
            set
            {
                if (SetProperty(ref _selectedServerType, value))
                {
                    ValidateForm();
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        public string Host
        {
            get => _host;
            set
            {
                if (SetProperty(ref _host, value))
                {
                    ValidateForm();
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        public int Port
        {
            get => _port;
            set
            {
                if (SetProperty(ref _port, value))
                {
                    ValidateForm();
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    ValidateForm();
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    ValidateForm();
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        public bool UseSSL
        {
            get => _useSSL;
            set
            {
                if (SetProperty(ref _useSSL, value))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        public bool UseStartTLS
        {
            get => _useStartTLS;
            set
            {
                if (SetProperty(ref _useStartTLS, value))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        public string DefaultFromEmail
        {
            get => _defaultFromEmail;
            set
            {
                if (SetProperty(ref _defaultFromEmail, value))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        public string DefaultFromName
        {
            get => _defaultFromName;
            set
            {
                if (SetProperty(ref _defaultFromName, value))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }
        
        // UI state properties
        public string ValidationMessage
        {
            get => _validationMessage;
            set => SetProperty(ref _validationMessage, value);
        }
        
        public string TestResult
        {
            get => _testResult;
            set => SetProperty(ref _testResult, value);
        }
        
        public string TestStatusMessage
        {
            get => _testStatusMessage;
            set => SetProperty(ref _testStatusMessage, value);
        }
        
        public Brush TestResultBackground
        {
            get => _testResultBackground;
            set => SetProperty(ref _testResultBackground, value);
        }
        
        public Brush TestResultBorderBrush
        {
            get => _testResultBorderBrush;
            set => SetProperty(ref _testResultBorderBrush, value);
        }
        
        public Brush TestResultForeground
        {
            get => _testResultForeground;
            set => SetProperty(ref _testResultForeground, value);
        }
        
        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            set => SetProperty(ref _isTestingConnection, value);
        }
        
        // Computed properties
        public bool CanSave => !string.IsNullOrEmpty(ValidationMessage) == false;
        public bool HasUnsavedChanges => HasFieldChanges();
        
        // Password visibility
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set => SetProperty(ref _isPasswordVisible, value);
        }
        
        public FontAwesomeIcon PasswordVisibilityIcon => IsPasswordVisible ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye;
        public FontAwesomeIcon TestButtonIcon => IsTestingConnection ? FontAwesomeIcon.Spinner : FontAwesomeIcon.Plug;
        public string TestButtonText => IsTestingConnection ? "Тестирование..." : "Тест";
        
        // Available options
        public List<ServerTypeOption> AvailableServerTypes { get; } = new()
        {
            new ServerTypeOption { Value = SmtpServerType.Primary, DisplayName = "Основной сервер" },
            new ServerTypeOption { Value = SmtpServerType.Backup, DisplayName = "Резервный сервер" }
        };
        
        #endregion
        
        #region Commands
        
        public AsyncRelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public AsyncRelayCommand TestConnectionCommand { get; }
        public RelayCommand TogglePasswordVisibilityCommand { get; }
        
        #endregion
        
        #region Constructor
        
        public SmtpEditViewModel(
            ISmtpSettingsRepository smtpRepository,
            IEmailService emailService,
            WindowsLauncher.Core.Interfaces.IEncryptionService encryptionService,
            ILogger<SmtpEditViewModel> logger,
            IDialogService dialogService) : base(logger, dialogService)
        {
            _smtpRepository = smtpRepository ?? throw new ArgumentNullException(nameof(smtpRepository));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _originalSettings = new SmtpSettings();
            _isNewServer = true;
            
            // Initialize commands
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            CancelCommand = new RelayCommand(Cancel);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
            TogglePasswordVisibilityCommand = new RelayCommand(TogglePasswordVisibility);
            
            // Subscribe to property changes
            PropertyChanged += OnPropertyChanged;
        }
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Установить SMTP сервер для редактирования
        /// </summary>
        public void SetServer(SmtpSettings server, bool isNew = false)
        {
            _originalSettings = server ?? throw new ArgumentNullException(nameof(server));
            _isNewServer = isNew;
            
            // Копируем значения в форму
            ServerName = server.Name;
            SelectedServerType = server.ServerType;
            Host = server.Host;
            Port = server.Port;
            Username = server.Username;
            UseSSL = server.UseSSL;
            UseStartTLS = server.UseStartTLS;
            DefaultFromEmail = server.DefaultFromEmail ?? string.Empty;
            DefaultFromName = server.DefaultFromName ?? string.Empty;
            
            // Расшифровываем пароль, если он есть
            if (!string.IsNullOrEmpty(server.EncryptedPassword))
            {
                try
                {
                    Password = _encryptionService.DecryptSecure(server.EncryptedPassword);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt password for server {ServerId}", server.Id);
                    Password = string.Empty;
                }
            }
            else
            {
                Password = string.Empty;
            }
            
            // Обновляем заголовки окна
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            
            ValidateForm();
            
            _logger.LogDebug("Set SMTP server for editing: {ServerName} (ID: {ServerId}, IsNew: {IsNew})", 
                server.Name, server.Id, isNew);
        }
        
        /// <summary>
        /// Сохранить изменения
        /// </summary>
        private async Task SaveAsync()
        {
            try
            {
                if (!ValidateForm())
                {
                    return;
                }
                
                // Шифруем пароль
                var encryptedPassword = !string.IsNullOrEmpty(Password) 
                    ? _encryptionService.EncryptSecure(Password) 
                    : string.Empty;
                
                // Обновляем настройки
                _originalSettings.Name = ServerName;
                _originalSettings.ServerType = SelectedServerType;
                _originalSettings.Host = Host;
                _originalSettings.Port = Port;
                _originalSettings.Username = Username;
                _originalSettings.EncryptedPassword = encryptedPassword;
                _originalSettings.UseSSL = UseSSL;
                _originalSettings.UseStartTLS = UseStartTLS;
                _originalSettings.DefaultFromEmail = string.IsNullOrWhiteSpace(DefaultFromEmail) ? null : DefaultFromEmail;
                _originalSettings.DefaultFromName = string.IsNullOrWhiteSpace(DefaultFromName) ? null : DefaultFromName;
                _originalSettings.IsActive = true;
                _originalSettings.UpdatedAt = DateTime.Now;
                
                // Сохраняем в БД
                if (_isNewServer)
                {
                    _originalSettings.CreatedAt = DateTime.Now;
                    _originalSettings.CreatedBy = "System"; // TODO: Получать текущего пользователя
                    await _smtpRepository.CreateAsync(_originalSettings);
                    _logger.LogInformation("Created new SMTP server: {ServerName} ({ServerType})", 
                        _originalSettings.Name, _originalSettings.ServerType);
                }
                else
                {
                    await _smtpRepository.UpdateAsync(_originalSettings);
                    _logger.LogInformation("Updated SMTP server: {ServerName} (ID: {ServerId})", 
                        _originalSettings.Name, _originalSettings.Id);
                }
                
                // Сигнализируем о завершении сохранения
                OnSaveCompleted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving SMTP server: {ServerName}", ServerName);
                ValidationMessage = $"Ошибка сохранения: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Отменить изменения
        /// </summary>
        private void Cancel()
        {
            OnCancelRequested();
        }
        
        /// <summary>
        /// Тестировать подключение к SMTP серверу
        /// </summary>
        private async Task TestConnectionAsync()
        {
            try
            {
                if (!ValidateBasicFields())
                {
                    return;
                }
                
                IsTestingConnection = true;
                TestStatusMessage = "Подключение к серверу...";
                TestResult = string.Empty;
                
                // Создаем временные настройки для тестирования
                var testSettings = new SmtpSettings
                {
                    Host = Host,
                    Port = Port,
                    Username = Username,
                    EncryptedPassword = !string.IsNullOrEmpty(Password) 
                        ? _encryptionService.EncryptSecure(Password) 
                        : string.Empty,
                    UseSSL = UseSSL,
                    UseStartTLS = UseStartTLS,
                    Name = "Test Connection",
                    DefaultFromEmail = DefaultFromEmail
                };
                
                // Используем реальный EmailService для тестирования SMTP подключения
                var testResult = await _emailService.TestSmtpConnectionAsync(testSettings);
                
                if (testResult.IsSuccess)
                {
                    TestResult = "✅ Подключение установлено успешно!";
                    TestStatusMessage = $"Тестирование завершено за {testResult.Duration.TotalMilliseconds:F0} мс";
                    TestResultBackground = new SolidColorBrush(Color.FromRgb(220, 252, 231));
                    TestResultBorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    TestResultForeground = new SolidColorBrush(Color.FromRgb(21, 128, 61));
                    
                    if (!string.IsNullOrEmpty(testResult.ServerInfo))
                    {
                        TestStatusMessage += $" - {testResult.ServerInfo}";
                    }
                }
                else
                {
                    TestResult = "❌ Не удалось подключиться к серверу";
                    TestStatusMessage = $"Тестирование не пройдено за {testResult.Duration.TotalMilliseconds:F0} мс";
                    TestResultBackground = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                    TestResultBorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    TestResultForeground = new SolidColorBrush(Color.FromRgb(153, 27, 27));
                    
                    if (!string.IsNullOrEmpty(testResult.ErrorMessage))
                    {
                        TestResult += $": {testResult.ErrorMessage}";
                    }
                }
                
                _logger.LogInformation("SMTP connection test for {Host}:{Port} - Result: {Result} in {Duration}ms", 
                    Host, Port, testResult.IsSuccess ? "SUCCESS" : "FAILED", testResult.Duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing SMTP connection: {Host}:{Port}", Host, Port);
                TestResult = $"❌ Ошибка тестирования: {ex.Message}";
                TestStatusMessage = "Ошибка при тестировании";
                TestResultBackground = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                TestResultBorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                TestResultForeground = new SolidColorBrush(Color.FromRgb(153, 27, 27));
            }
            finally
            {
                IsTestingConnection = false;
                OnPropertyChanged(nameof(TestButtonIcon));
                OnPropertyChanged(nameof(TestButtonText));
            }
        }
        
        /// <summary>
        /// Переключить видимость пароля
        /// </summary>
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
            OnPropertyChanged(nameof(PasswordVisibilityIcon));
        }
        
        /// <summary>
        /// Валидация формы
        /// </summary>
        private bool ValidateForm()
        {
            var errors = new List<string>();
            
            // Проверяем обязательные поля
            if (string.IsNullOrWhiteSpace(ServerName))
                errors.Add("Введите название сервера");
                
            if (string.IsNullOrWhiteSpace(Host))
                errors.Add("Введите адрес хоста");
                
            if (Port <= 0 || Port > 65535)
                errors.Add("Порт должен быть от 1 до 65535");
                
            if (string.IsNullOrWhiteSpace(Username))
                errors.Add("Введите имя пользователя");
                
            if (string.IsNullOrWhiteSpace(Password))
                errors.Add("Введите пароль");
            
            // Проверяем email адрес
            if (!string.IsNullOrWhiteSpace(DefaultFromEmail))
            {
                var emailAttribute = new EmailAddressAttribute();
                if (!emailAttribute.IsValid(DefaultFromEmail))
                    errors.Add("Некорректный email адрес отправителя");
            }
            
            ValidationMessage = errors.Count > 0 ? string.Join("; ", errors) : string.Empty;
            
            // Обновляем доступность команды сохранения
            SaveCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanSave));
            
            return errors.Count == 0;
        }
        
        /// <summary>
        /// Валидация основных полей для тестирования
        /// </summary>
        private bool ValidateBasicFields()
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                TestResult = "❌ Введите адрес хоста для тестирования";
                return false;
            }
            
            if (Port <= 0 || Port > 65535)
            {
                TestResult = "❌ Введите корректный порт (1-65535)";
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(Username))
            {
                TestResult = "❌ Введите имя пользователя";
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(Password))
            {
                TestResult = "❌ Введите пароль";
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Проверить наличие несохраненных изменений
        /// </summary>
        private bool HasFieldChanges()
        {
            if (_originalSettings == null) return true;
            
            return ServerName != _originalSettings.Name ||
                   SelectedServerType != _originalSettings.ServerType ||
                   Host != _originalSettings.Host ||
                   Port != _originalSettings.Port ||
                   Username != _originalSettings.Username ||
                   UseSSL != _originalSettings.UseSSL ||
                   UseStartTLS != _originalSettings.UseStartTLS ||
                   DefaultFromEmail != (_originalSettings.DefaultFromEmail ?? string.Empty) ||
                   DefaultFromName != (_originalSettings.DefaultFromName ?? string.Empty) ||
                   // Пароль сравниваем по расшифрованному значению
                   HasPasswordChanged();
        }
        
        /// <summary>
        /// Проверить изменение пароля
        /// </summary>
        private bool HasPasswordChanged()
        {
            if (string.IsNullOrEmpty(_originalSettings.EncryptedPassword))
            {
                return !string.IsNullOrEmpty(Password);
            }
            
            try
            {
                var decryptedOriginal = _encryptionService.DecryptSecure(_originalSettings.EncryptedPassword);
                return Password != decryptedOriginal;
            }
            catch
            {
                // Если не удалось расшифровать, считаем что пароль изменился
                return true;
            }
        }
        
        /// <summary>
        /// Обработчик изменения свойств
        /// </summary>
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IsTestingConnection):
                    OnPropertyChanged(nameof(TestButtonIcon));
                    OnPropertyChanged(nameof(TestButtonText));
                    break;
                case nameof(IsPasswordVisible):
                    OnPropertyChanged(nameof(PasswordVisibilityIcon));
                    break;
            }
        }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Событие завершения сохранения
        /// </summary>
        public event EventHandler? SaveCompleted;
        
        /// <summary>
        /// Событие отмены
        /// </summary>
        public event EventHandler? CancelRequested;
        
        /// <summary>
        /// Вызывает событие завершения сохранения
        /// </summary>
        private void OnSaveCompleted()
        {
            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Вызывает событие отмены
        /// </summary>
        private void OnCancelRequested()
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Опция для типа сервера
    /// </summary>
    public class ServerTypeOption
    {
        public SmtpServerType Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}