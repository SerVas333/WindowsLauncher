using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.UI.Infrastructure.Services;

namespace WindowsLauncher.UI.ViewModels.Base
{
    /// <summary>
    /// Базовый класс для всех ViewModels с расширенной функциональностью
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        protected readonly ILogger Logger;
        protected readonly IDialogService DialogService;

        private bool _isLoading;
        private string _title = string.Empty;
        private bool _disposed;

        protected ViewModelBase(ILogger logger, IDialogService dialogService)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            DialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                onChanged?.Invoke();
                return true;
            }
            return false;
        }

        #endregion

        #region Common Properties

        /// <summary>
        /// Индикатор загрузки для UI
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            protected set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Заголовок окна/страницы
        /// </summary>
        public string Title
        {
            get => _title;
            protected set => SetProperty(ref _title, value);
        }

        #endregion

        #region Error Handling

        /// <summary>
        /// Безопасное выполнение операции с обработкой ошибок
        /// </summary>
        protected async Task ExecuteSafelyAsync(Func<Task> operation, string? operationName = null)
        {
            try
            {
                IsLoading = true;
                await operation();
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, operationName);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Безопасное выполнение операции с результатом
        /// </summary>
        protected async Task<T?> ExecuteSafelyAsync<T>(Func<Task<T>> operation, string? operationName = null)
        {
            try
            {
                IsLoading = true;
                return await operation();
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, operationName);
                return default;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Централизованная обработка ошибок
        /// </summary>
        protected virtual async Task HandleErrorAsync(Exception exception, string? operationName = null)
        {
            var operation = !string.IsNullOrEmpty(operationName) ? $" during {operationName}" : "";
            Logger.LogError(exception, "Error in {ViewModel}{Operation}", GetType().Name, operation);

            var message = GetUserFriendlyErrorMessage(exception);

            // Показываем пользователю в зависимости от типа ошибки
            if (exception is UnauthorizedAccessException)
            {
                DialogService.ShowWarning(message, LocalizationManager.GetString("AccessDenied"));
            }
            else if (exception is TimeoutException)
            {
                DialogService.ShowWarning(message, LocalizationManager.GetString("TimeoutError"));
            }
            else
            {
                DialogService.ShowError(message, LocalizationManager.GetString("Error"));
            }

            // Можно добавить дополнительную логику: отправку телеметрии, etc.
            await OnErrorHandledAsync(exception, operationName);
        }

        /// <summary>
        /// Получение понятного пользователю сообщения об ошибке
        /// </summary>
        protected virtual string GetUserFriendlyErrorMessage(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => LocalizationManager.GetString("ErrorAccessDenied"),
                TimeoutException => LocalizationManager.GetString("ErrorTimeout"),
                InvalidOperationException => LocalizationManager.GetString("ErrorInvalidOperation"),
                ArgumentException => LocalizationManager.GetString("ErrorInvalidData"),
                _ => LocalizationManager.GetString("ErrorUnknown") ?? $"Unknown error: {exception.Message}"
            };
        }

        /// <summary>
        /// Дополнительная обработка после показа ошибки пользователю
        /// </summary>
        protected virtual Task OnErrorHandledAsync(Exception exception, string? operationName)
        {
            // Переопределить в наследниках для специфичной логики
            return Task.CompletedTask;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Инициализация ViewModel (вызывается после создания)
        /// </summary>
        public virtual Task InitializeAsync()
        {
            Logger.LogDebug("Initializing {ViewModel}", GetType().Name);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Очистка ресурсов (вызывается перед закрытием)
        /// </summary>
        public virtual Task CleanupAsync()
        {
            Logger.LogDebug("Cleaning up {ViewModel}", GetType().Name);
            return Task.CompletedTask;
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Очистка управляемых ресурсов
                    Logger.LogDebug("Disposing {ViewModel}", GetType().Name);
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}