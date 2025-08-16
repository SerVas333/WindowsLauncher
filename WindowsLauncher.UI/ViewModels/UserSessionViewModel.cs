using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.UI.Infrastructure.Commands;
using WindowsLauncher.UI.Infrastructure.Localization;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Components.Dialogs;
using WpfApplication = System.Windows.Application;

namespace WindowsLauncher.UI.ViewModels;

/// <summary>
/// ViewModel для управления пользовательскими сессиями
/// Извлечен из MainViewModel для соблюдения принципа единственной ответственности
/// </summary>
public class UserSessionViewModel : ViewModelBase
{
    #region Fields

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private User? _currentUser;

    #endregion

    #region Constructor

    public UserSessionViewModel(
        IServiceScopeFactory serviceScopeFactory,
        IDialogService dialogService,
        ILogger<UserSessionViewModel> logger)
        : base(logger, dialogService)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

        // Инициализация команд
        ExitApplicationCommand = new RelayCommand(ExitApplication);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Текущий аутентифицированный пользователь
    /// </summary>
    public User? CurrentUser
    {
        get => _currentUser;
        set
        {
            if (SetProperty(ref _currentUser, value))
            {
                Logger.LogDebug("CurrentUser changed to: {Username}", value?.Username ?? "null");
                
                // Уведомляем об изменении связанных свойств
                OnPropertyChanged(nameof(LocalizedRole));
                OnPropertyChanged(nameof(CanManageSettings));
                
                // Генерируем событие для MainViewModel
                UserChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Локализованное название роли текущего пользователя
    /// </summary>
    public string LocalizedRole
    {
        get
        {
            if (CurrentUser == null) return "";

            return CurrentUser.Role switch
            {
                Core.Enums.UserRole.Administrator => LocalizationHelper.Instance.GetString("RoleAdministrator"),
                Core.Enums.UserRole.PowerUser => LocalizationHelper.Instance.GetString("RolePowerUser"),
                Core.Enums.UserRole.Standard => LocalizationHelper.Instance.GetString("RoleStandard"),
                _ => CurrentUser.Role.ToString()
            };
        }
    }

    /// <summary>
    /// Может ли текущий пользователь управлять настройками
    /// </summary>
    public bool CanManageSettings => CurrentUser?.Role >= Core.Enums.UserRole.PowerUser;

    #endregion

    #region Commands

    /// <summary>
    /// Команда выхода из приложения / смены пользователя
    /// </summary>
    public RelayCommand ExitApplicationCommand { get; }

    #endregion

    #region Events

    /// <summary>
    /// Событие изменения текущего пользователя
    /// </summary>
    public event EventHandler<User?>? UserChanged;

    #endregion

    #region Public Methods

    /// <summary>
    /// Аутентификация пользователя
    /// </summary>
    public async Task AuthenticateUserAsync()
    {
        try
        {
            Logger.LogInformation("Starting user authentication");

            using var scope = _serviceScopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            var authResult = await authService.AuthenticateAsync();

            if (authResult.IsSuccess && authResult.User != null)
            {
                CurrentUser = authResult.User;
                Logger.LogInformation("User authenticated: {User} ({Role})",
                    CurrentUser.Username, CurrentUser.Role);
            }
            else
            {
                var errorMessage = LocalizationHelper.Instance.GetFormattedString("AuthenticationFailed",
                    authResult.ErrorMessage ?? "Unknown error");

                Logger.LogError("Authentication failed: {Error}", authResult.ErrorMessage);
                DialogService.ShowError(errorMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Authentication error");
            var errorMessage = LocalizationHelper.Instance.GetFormattedString("AuthenticationFailed", ex.Message);
            DialogService.ShowError(errorMessage);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Выход из приложения / смена пользователя
    /// </summary>
    private async void ExitApplication()
    {
        try
        {
            Logger.LogInformation("User logout/switch requested by {Username}", CurrentUser?.Username);
            
            // Показываем корпоративный диалог подтверждения выхода/смены пользователя
            bool confirmed = CorporateConfirmationDialog.ShowLogoutConfirmation(
                WpfApplication.Current.MainWindow);

            if (!confirmed)
            {
                Logger.LogInformation("User logout/switch cancelled by user");
                return;
            }

            // Завершаем сессию текущего пользователя
            await HandleUserLogoutAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during user logout/switch");
            var errorMessage = LocalizationHelper.Instance.GetFormattedString("LogoutError", ex.Message);
            DialogService.ShowError(errorMessage);
        }
    }

    /// <summary>
    /// Обработка выхода пользователя из системы
    /// </summary>
    private async Task HandleUserLogoutAsync()
    {
        try
        {
            Logger.LogInformation("Handling user switch for {Username}", CurrentUser?.Username);

            // 1. БЕЗОПАСНОСТЬ: Закрываем все приложения текущего пользователя
            if (CurrentUser != null)
            {
                await CloseUserApplicationsAsync(CurrentUser);
            }

            // 2. Завершаем сессию через SessionManagementService
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionManagementService>();
                var success = await sessionService.HandleLogoutRequestAsync();
                
                if (success)
                {
                    Logger.LogInformation("User switch successful for {Username}", CurrentUser?.Username);
                    
                    // 3. Закрываем MainWindow → App создаст новое окно для нового пользователя
                    WpfApplication.Current.Dispatcher.BeginInvoke(() =>
                    {
                        var mainWindow = WpfApplication.Current.MainWindow;
                        mainWindow?.Close();
                    });
                }
                else
                {
                    Logger.LogWarning("User switch failed for {Username}", CurrentUser?.Username);
                    var errorMessage = LocalizationHelper.Instance.GetString("UserSwitchFailed");
                    DialogService.ShowError("Не удалось выполнить смену пользователя");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling user switch for {Username}", CurrentUser?.Username);
            var errorMessage = LocalizationHelper.Instance.GetFormattedString("SwitchUserError", ex.Message);
            DialogService.ShowError(errorMessage);
        }
    }

    /// <summary>
    /// Закрытие всех приложений пользователя при смене пользователя
    /// Критично для предотвращения утечки персональной информации
    /// </summary>
    private async Task CloseUserApplicationsAsync(User user)
    {
        if (user == null) return;

        try
        {
            Logger.LogWarning("SECURITY: Closing all applications for user {Username} during user switch", user.Username);
            
            using var scope = _serviceScopeFactory.CreateScope();
            var lifecycleService = scope.ServiceProvider.GetRequiredService<IApplicationLifecycleService>();
            
            // Получаем все приложения пользователя
            var userApplications = await lifecycleService.GetByUserAsync(user.Username);
            
            if (userApplications.Count > 0)
            {
                Logger.LogWarning("SECURITY: Found {Count} applications to close for user {Username}", 
                    userApplications.Count, user.Username);
                
                // Закрываем все приложения пользователя
                var result = await lifecycleService.CloseAllAsync(timeoutMs: 10000);
                
                if (result.Success)
                {
                    Logger.LogInformation("SECURITY: Successfully closed all applications for user {Username}", user.Username);
                }
                else
                {
                    Logger.LogError("SECURITY: Failed to close some applications for user {Username}: {Errors}", 
                        user.Username, string.Join(", ", result.Errors));
                }
            }
            else
            {
                Logger.LogInformation("SECURITY: No applications to close for user {Username}", user.Username);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SECURITY: Critical error closing applications for user {Username}", user.Username);
            // Не выбрасываем исключение - смена пользователя должна продолжиться даже при ошибке
        }
    }

    #endregion
}