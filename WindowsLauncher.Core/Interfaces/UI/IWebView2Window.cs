using System;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models.Lifecycle;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.Core.Interfaces.UI
{
    /// <summary>
    /// Интерфейс для WebView2 окна
    /// Разрывает циклическую зависимость между Services и UI
    /// </summary>
    public interface IWebView2Window
    {
        string InstanceId { get; }
        DateTime StartTime { get; }
        string LaunchedBy { get; }
        CoreApplication Application { get; }
        bool IsClosed { get; }
        
        // Свойства окна
        string WindowTitle { get; }
        bool IsVisible { get; }

        // События для интеграции с ApplicationLifecycleService
        event EventHandler<ApplicationInstance>? WindowActivated;
        event EventHandler<ApplicationInstance>? WindowDeactivated;
        event EventHandler<ApplicationInstance>? WindowClosed;
        event EventHandler<ApplicationInstance>? WindowStateChanged;

        // Методы управления окном
        Task<bool> SwitchToAsync();
        Task NavigateAsync(string url);
        string GetCurrentUrl();
        void Show();
        void Close();
        bool Activate();
    }
}