using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.UI.ViewModels.Base;

namespace WindowsLauncher.UI.Infrastructure.Services
{
    /// <summary>
    /// WPF реализация INavigationService
    /// </summary>
    public class WpfNavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WpfNavigationService> _logger;

        // Маппинг ViewModels к Views
        private static readonly Dictionary<Type, Type> _viewModelToViewMap = new();

        public WpfNavigationService(IServiceProvider serviceProvider, ILogger<WpfNavigationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Инициализируем маппинг
            InitializeViewMapping();
        }

        public bool? ShowDialog<TViewModel>() where TViewModel : ViewModelBase
        {
            return ShowDialog<TViewModel>(null);
        }

        public bool? ShowDialog<TViewModel>(object parameter) where TViewModel : ViewModelBase
        {
            try
            {
                var viewModelType = typeof(TViewModel);
                _logger.LogInformation("Opening dialog for ViewModel: {ViewModel}", viewModelType.Name);

                if (!_viewModelToViewMap.TryGetValue(viewModelType, out var viewType))
                {
                    _logger.LogError("No view registered for ViewModel: {ViewModel}", viewModelType.Name);
                    throw new InvalidOperationException($"No view registered for ViewModel: {viewModelType.Name}");
                }

                // Создаем ViewModel через DI
                var viewModel = _serviceProvider.GetRequiredService<TViewModel>();

                // Передаем параметр если есть
                if (parameter != null && viewModel is IParameterReceiver parameterReceiver)
                {
                    parameterReceiver.ReceiveParameter(parameter);
                }

                // Создаем View
                var view = (Window)Activator.CreateInstance(viewType)!;
                view.DataContext = viewModel;

                // Устанавливаем владельца
                if (Application.Current.MainWindow?.IsVisible == true)
                {
                    view.Owner = Application.Current.MainWindow;
                }

                var result = view.ShowDialog();
                _logger.LogInformation("Dialog closed with result: {Result}", result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening dialog for ViewModel: {ViewModel}", typeof(TViewModel).Name);
                throw;
            }
        }

        public void Show<TViewModel>() where TViewModel : ViewModelBase
        {
            try
            {
                var viewModelType = typeof(TViewModel);
                _logger.LogInformation("Opening window for ViewModel: {ViewModel}", viewModelType.Name);

                if (!_viewModelToViewMap.TryGetValue(viewModelType, out var viewType))
                {
                    _logger.LogError("No view registered for ViewModel: {ViewModel}", viewModelType.Name);
                    throw new InvalidOperationException($"No view registered for ViewModel: {viewModelType.Name}");
                }

                var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
                var view = (Window)Activator.CreateInstance(viewType)!;
                view.DataContext = viewModel;

                view.Show();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening window for ViewModel: {ViewModel}", typeof(TViewModel).Name);
                throw;
            }
        }

        public void Close()
        {
            Close(null);
        }

        public void Close(bool? result)
        {
            if (Application.Current.MainWindow?.IsActive == true)
            {
                Application.Current.MainWindow.DialogResult = result;
                Application.Current.MainWindow.Close();
            }
        }

        private static void InitializeViewMapping()
        {
            // TODO: Автоматическая регистрация или через конфигурацию
            // Пока ручная регистрация

            // _viewModelToViewMap[typeof(LoginViewModel)] = typeof(LoginWindow);
            // _viewModelToViewMap[typeof(SettingsViewModel)] = typeof(SettingsWindow);
        }

        /// <summary>
        /// Регистрация маппинга ViewModel -> View
        /// </summary>
        public static void RegisterView<TViewModel, TView>()
            where TViewModel : ViewModelBase
            where TView : Window
        {
            _viewModelToViewMap[typeof(TViewModel)] = typeof(TView);
        }
    }
}