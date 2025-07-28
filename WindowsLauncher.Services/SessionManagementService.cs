using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис управления сессиями пользователей
    /// </summary>
    public class SessionManagementService : ISessionManagementService
    {
        private readonly ILogger<SessionManagementService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuditService _auditService;
        
        private User? _currentUser;
        private bool _isSessionActive;
        private SessionConfiguration _sessionConfig;

        public event EventHandler<SessionEventArgs>? SessionEvent;

        public SessionManagementService(
            ILogger<SessionManagementService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            IAuditService auditService)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _auditService = auditService;
            _sessionConfig = new SessionConfiguration();
        }

        public User? CurrentUser => _currentUser;
        public bool IsSessionActive => _isSessionActive;
        public bool IsRunningAsShell => _sessionConfig.RunAsShell;
        public SessionConfiguration Configuration => _sessionConfig;

        public async Task LoadConfigurationAsync()
        {
            try
            {
                _sessionConfig = _configuration.GetSection("SessionManagement").Get<SessionConfiguration>() 
                                ?? new SessionConfiguration();

                var validation = _sessionConfig.Validate();
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Session configuration validation failed: {Errors}", 
                        string.Join(", ", validation.Errors));
                }

                _logger.LogInformation("Session configuration loaded: RunAsShell={RunAsShell}, AutoRestart={AutoRestart}", 
                    _sessionConfig.RunAsShell, _sessionConfig.AutoRestartOnClose);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session configuration, using defaults");
                _sessionConfig = new SessionConfiguration();
            }
        }

        public async Task<bool> StartSessionAsync(User user)
        {
            try
            {
                _logger.LogInformation("Starting session for user: {Username} ({Role})", user.Username, user.Role);
                _logger.LogDebug("Current session state: _isSessionActive={IsActive}, _currentUser={CurrentUser}", 
                    _isSessionActive, _currentUser?.Username ?? "null");

                // Проверяем множественные сессии
                if (_isSessionActive && !_sessionConfig.AllowMultipleSessions)
                {
                    var currentUserName = _currentUser?.Username ?? "unknown";
                    _logger.LogWarning("Multiple sessions not allowed, ending current session for: {CurrentUser}, new user: {NewUser}", 
                        currentUserName, user.Username);
                    
                    // Если это тот же пользователь, просто обновляем сессию без завершения
                    if (_currentUser?.Username == user.Username)
                    {
                        _logger.LogInformation("Same user re-login, updating existing session instead of ending");
                        _currentUser = user; // Обновляем данные пользователя
                        
                        // Логируем аудит для повторного входа
                        await _auditService.LogEventAsync(user.Username, "SessionRestart", 
                            $"Session restarted for user {user.Username} with role {user.Role}");
                            
                        return true;
                    }
                    
                    _logger.LogWarning("Different user login detected, ending current session");
                    await EndSessionAsync("Multiple sessions not allowed");
                }

                _currentUser = user;
                _isSessionActive = true;

                // Уведомляем о начале сессии
                await RaiseSessionEventAsync(new SessionEventArgs
                {
                    EventType = SessionEventType.Login,
                    User = user,
                    Reason = "User login"
                });

                // Логируем в аудит
                await _auditService.LogEventAsync(user.Username, "SessionStart", 
                    $"Session started for user {user.Username} with role {user.Role}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start session for user {Username}", user.Username);
                return false;
            }
        }

        public async Task<bool> EndSessionAsync(string? reason = null)
        {
            try
            {
                _logger.LogDebug("EndSessionAsync called - _isSessionActive={IsActive}, _currentUser={User}, reason={Reason}", 
                    _isSessionActive, _currentUser?.Username ?? "null", reason ?? "User request");
                    
                if (!_isSessionActive || _currentUser == null)
                {
                    _logger.LogDebug("No active session to end");
                    return true;
                }

                _logger.LogInformation("Ending session for user: {Username}, reason: {Reason}", 
                    _currentUser.Username, reason ?? "User request");

                var user = _currentUser;
                
                // Уведомляем о завершении сессии
                await RaiseSessionEventAsync(new SessionEventArgs
                {
                    EventType = SessionEventType.Logout,
                    User = user,
                    Reason = reason ?? "User logout"
                });

                // Логируем в аудит
                await _auditService.LogEventAsync(user.Username, "SessionEnd", 
                    $"Session ended for user {user.Username}, reason: {reason ?? "User request"}");

                _currentUser = null;
                _isSessionActive = false;
                
                _logger.LogInformation("Session ended successfully for user: {Username}", user.Username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to end session");
                return false;
            }
        }

        public async Task<bool> HandleMainWindowClosingAsync()
        {
            try
            {
                _logger.LogInformation("Handling main window closing, RunAsShell: {RunAsShell}", _sessionConfig.RunAsShell);

                // Уведомляем о закрытии главного окна
                var eventArgs = new SessionEventArgs
                {
                    EventType = SessionEventType.MainWindowClosing,
                    User = _currentUser,
                    Reason = "Main window closing",
                    CanCancel = true
                };

                await RaiseSessionEventAsync(eventArgs);

                // Если событие отменено
                if (eventArgs.Cancel)
                {
                    _logger.LogInformation("Main window closing cancelled by event handler");
                    return false;
                }

                // В режиме Shell - предупреждаем о перезапуске
                if (_sessionConfig.RunAsShell)
                {
                    _logger.LogInformation("Running as Shell - will restart application");
                    
                    if (_sessionConfig.LogoutOnMainWindowClose && _isSessionActive)
                    {
                        await EndSessionAsync("Shell restart");
                    }
                    
                    // Планируем перезапуск
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000); // Даем время для корректного закрытия
                        await RestartApplicationAsync();
                    });
                    
                    return true;
                }

                // Обычный режим - разлогиниваемся и возвращаемся к окну входа
                if (_sessionConfig.LogoutOnMainWindowClose && _isSessionActive)
                {
                    await EndSessionAsync("Main window closed");
                    
                    if (_sessionConfig.ReturnToLoginOnLogout)
                    {
                        _logger.LogInformation("Returning to login window");
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(100);
                            await ShowLoginWindowAsync();
                        });
                        return false; // Не закрываем приложение
                    }
                }

                return true; // Разрешаем закрытие
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling main window closing");
                return true; // В случае ошибки разрешаем закрытие
            }
        }

        public async Task<bool> HandleLogoutRequestAsync()
        {
            try
            {
                _logger.LogInformation("Handling logout request");

                if (!_isSessionActive)
                {
                    _logger.LogDebug("No active session for logout");
                    return true;
                }

                // Завершаем сессию
                await EndSessionAsync("User logout request");

                // Возвращаемся к окну входа (отключено - этим занимается App.xaml.cs)
                if (_sessionConfig.ReturnToLoginOnLogout)
                {
                    _logger.LogInformation("Login window will be shown by App.xaml.cs HandleMainWindowClosedAsync");
                    // await ShowLoginWindowAsync(); // ОТКЛЮЧЕНО - предотвращает конфликт с App.xaml.cs
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling logout request");
                return false;
            }
        }

        public async Task RestartApplicationAsync()
        {
            try
            {
                _logger.LogInformation("Restarting application");

                // Уведомляем о перезапуске
                await RaiseSessionEventAsync(new SessionEventArgs
                {
                    EventType = SessionEventType.ShellRestart,
                    User = _currentUser,
                    Reason = "Shell restart"
                });

                // Логируем в аудит
                if (_currentUser != null)
                {
                    await _auditService.LogEventAsync(_currentUser.Username, "ApplicationRestart", 
                        "Application restarted in Shell mode");
                }

                // Получаем путь к текущему исполняемому файлу
                var currentProcess = Process.GetCurrentProcess();
                var exePath = currentProcess.MainModule?.FileName ?? 
                             System.Reflection.Assembly.GetEntryAssembly()?.Location ??
                             Environment.ProcessPath;

                if (string.IsNullOrEmpty(exePath))
                {
                    _logger.LogError("Could not determine executable path for restart");
                    return;
                }

                _logger.LogInformation("Restarting application: {ExePath}", exePath);

                // Запускаем новый процесс
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory
                };

                Process.Start(startInfo);

                // Закрываем текущий процесс
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart application");
            }
        }

        public async Task<User?> ShowLoginWindowAsync()
        {
            try
            {
                _logger.LogInformation("Showing login window");

                return await Task.Run(() =>
                {
                    return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // Скрываем главное окно если оно открыто
                            var mainWindow = System.Windows.Application.Current.MainWindow;
                            if (mainWindow != null && mainWindow.IsVisible)
                            {
                                mainWindow.Hide();
                            }

                            // Создаем и показываем окно входа через рефлексию
                            var uiAssembly = System.Windows.Application.Current.GetType().Assembly;
                            var loginWindowType = uiAssembly.GetTypes()
                                .FirstOrDefault(t => t.Name == "LoginWindow");

                            if (loginWindowType != null)
                            {
                                var loginWindow = Activator.CreateInstance(loginWindowType) as System.Windows.Window;
                                if (loginWindow != null)
                                {
                                    var result = loginWindow.ShowDialog();
                                    
                                    // Получаем результат аутентификации через рефлексию
                                    var authUserProperty = loginWindowType.GetProperty("AuthenticatedUser");
                                    if (result == true && authUserProperty != null)
                                    {
                                        var user = authUserProperty.GetValue(loginWindow) as User;
                                        return user;
                                    }
                                }
                            }

                            return null;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in UI thread while showing login window");
                            return null;
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show login window");
                return null;
            }
        }

        public async Task<bool> CanCloseApplicationAsync()
        {
            try
            {
                // В режиме Shell приложение всегда перезапускается
                if (_sessionConfig.RunAsShell)
                {
                    return false; // Не разрешаем полное закрытие
                }

                // Если настроено сворачивание вместо закрытия
                if (_sessionConfig.MinimizeInsteadOfClose)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if application can close");
                return true; // В случае ошибки разрешаем закрытие
            }
        }

        private async Task RaiseSessionEventAsync(SessionEventArgs eventArgs)
        {
            try
            {
                SessionEvent?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising session event: {EventType}", eventArgs.EventType);
            }
        }
    }
}