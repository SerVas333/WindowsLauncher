using System;
using WindowsLauncher.UI.ViewModels.Base;  // 🔄 ТОЧНО указываем какой ViewModelBase использовать

namespace WindowsLauncher.UI.Infrastructure.Services
{
    /// <summary>
    /// Сервис навигации между окнами и страницами
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Открыть окно с указанной ViewModel
        /// </summary>
        bool? ShowDialog<TViewModel>() where TViewModel : ViewModels.Base.ViewModelBase;  // 🔄 Полный путь

        /// <summary>
        /// Открыть окно с указанной ViewModel и параметрами
        /// </summary>
        bool? ShowDialog<TViewModel>(object parameter) where TViewModel : ViewModels.Base.ViewModelBase;  // 🔄 Полный путь

        /// <summary>
        /// Показать обычное окно
        /// </summary>
        void Show<TViewModel>() where TViewModel : ViewModels.Base.ViewModelBase;  // 🔄 Полный путь

        /// <summary>
        /// Закрыть текущее окно
        /// </summary>
        void Close();

        /// <summary>
        /// Закрыть окно с результатом
        /// </summary>
        void Close(bool? result);
    }

    /// <summary>
    /// Интерфейс для ViewModels, которые могут принимать параметры
    /// </summary>
    public interface IParameterReceiver
    {
        void ReceiveParameter(object parameter);
    }
}