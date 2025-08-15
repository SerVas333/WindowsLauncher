using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.UI.Infrastructure.Commands;
using WindowsLauncher.UI.Infrastructure.Extensions;
using WindowsLauncher.UI.Infrastructure.Localization;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Views;
using WpfApplication = System.Windows.Application;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для офисных инструментов: email, адресная книга, справка
    /// </summary>
    public class OfficeToolsViewModel : ViewModelBase
    {
        #region Fields

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private Func<UserRole?>? _getCurrentUserRole;

        #endregion

        #region Constructor

        public OfficeToolsViewModel(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<OfficeToolsViewModel> logger,
            IDialogService dialogService)
            : base(logger, dialogService)
        {
            _serviceScopeFactory = serviceScopeFactory;
            InitializeCommands();
        }

        #endregion

        #region Commands

        public RelayCommand ComposeEmailCommand { get; private set; } = null!;
        public RelayCommand OpenAddressBookCommand { get; private set; } = null!;
        public RelayCommand OpenHelpCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            ComposeEmailCommand = new RelayCommand(ComposeEmail);
            OpenAddressBookCommand = new RelayCommand(OpenAddressBook);
            OpenHelpCommand = new RelayCommand(OpenHelp);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Установить функцию получения роли текущего пользователя
        /// </summary>
        public void SetCurrentUserRoleProvider(Func<UserRole?> getCurrentUserRole)
        {
            _getCurrentUserRole = getCurrentUserRole;
        }

        #endregion

        #region Commands Implementation

        /// <summary>
        /// Открыть окно создания email сообщения
        /// </summary>
        private void ComposeEmail()
        {
            try
            {
                // Создаем ComposeEmailViewModel через scoped scope для доступа к Scoped сервисам (IEmailService)
                var composeViewModel = _serviceScopeFactory.CreateScopedService<ComposeEmailViewModel>();

                var composeWindow = new ComposeEmailWindow(composeViewModel)
                {
                    Owner = WpfApplication.Current.MainWindow,
                    Title = LocalizationHelper.Instance.GetString("ComposeEmail_WindowTitle")
                };

                composeWindow.Show();
                Logger.LogInformation("Opened compose email window");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening compose email window");
                MessageBox.Show($"{LocalizationHelper.Instance.GetString("Error_EmailServiceUnavailable")}: {ex.Message}", 
                    LocalizationHelper.Instance.GetString("Error"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Открыть адресную книгу
        /// </summary>
        private void OpenAddressBook()
        {
            try
            {
                // Создаем AddressBookViewModel через scoped scope для доступа к Scoped сервисам (IAddressBookService)
                var addressBookViewModel = _serviceScopeFactory.CreateScopedService<AddressBookViewModel>();

                // Настраиваем режим просмотра (не выбор контактов)
                addressBookViewModel.IsSelectionMode = false;
                
                // Определяем права администратора через функцию получения роли пользователя
                var currentUserRole = _getCurrentUserRole?.Invoke();
                addressBookViewModel.IsAdminMode = currentUserRole >= UserRole.Administrator;

                var addressBookWindow = new AddressBookWindow(addressBookViewModel)
                {
                    Owner = WpfApplication.Current.MainWindow,
                    Title = LocalizationHelper.Instance.GetString("AddressBook_WindowTitle")
                };

                addressBookWindow.Show();
                Logger.LogInformation("Opened address book window in admin mode: {IsAdminMode}", addressBookViewModel.IsAdminMode);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening address book window");
                MessageBox.Show($"{LocalizationHelper.Instance.GetString("Error_AddressBookUnavailable")}: {ex.Message}", 
                    LocalizationHelper.Instance.GetString("Error"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Открыть справочную систему
        /// </summary>
        private void OpenHelp()
        {
            try
            {
                var helpWindow = new HelpWindow()
                {
                    Owner = WpfApplication.Current.MainWindow
                };

                helpWindow.Show();
                Logger.LogInformation("Opened help system window");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening help window");
                MessageBox.Show($"{LocalizationHelper.Instance.GetString("Help_LoadingError")}: {ex.Message}", 
                    LocalizationHelper.Instance.GetString("Error"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}