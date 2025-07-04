﻿using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace WindowsLauncher.UI.Infrastructure.Services
{
    /// <summary>
    /// WPF реализация IDialogService
    /// </summary>
    public class WpfDialogService : IDialogService
    {
        private readonly ILogger<WpfDialogService> _logger;

        public WpfDialogService(ILogger<WpfDialogService> logger)
        {
            _logger = logger;
        }

        public void ShowError(string message, string title = "Error")
        {
            _logger.LogWarning("Showing error dialog: {Message}", message);

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            _logger.LogInformation("Showing warning dialog: {Message}", message);

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        public void ShowInfo(string message, string title = "Information")
        {
            _logger.LogInformation("Showing info dialog: {Message}", message);

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        public bool ShowConfirmation(string message, string title = "Confirmation")
        {
            _logger.LogInformation("Showing confirmation dialog: {Message}", message);

            var result = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                result = MessageBox.Show(message, title, MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes;
            });

            _logger.LogInformation("Confirmation dialog result: {Result}", result);
            return result;
        }

        public string? ShowInputDialog(string message, string title = "Input", string defaultValue = "")
        {
            // Простая реализация через InputBox (можно заменить на custom dialog)
            // Для этого нужно добавить using Microsoft.VisualBasic;
            // Пока используем простой MessageBox
            var result = string.Empty;
            Application.Current.Dispatcher.Invoke(() =>
            {
                // TODO: Реализовать proper input dialog
                // Пока используем заглушку
                result = defaultValue;
            });
            return result;
        }

        public async Task ShowToastAsync(string message, ToastType type = ToastType.Information, int durationMs = 3000)
        {
            _logger.LogInformation("Showing toast: {Message} ({Type})", message, type);

            // TODO: Реализовать toast notifications
            // Пока используем обычное сообщение
            await Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var icon = type switch
                    {
                        ToastType.Success => MessageBoxImage.Information,
                        ToastType.Warning => MessageBoxImage.Warning,
                        ToastType.Error => MessageBoxImage.Error,
                        _ => MessageBoxImage.Information
                    };

                    MessageBox.Show(message, type.ToString(), MessageBoxButton.OK, icon);
                });
            });
        }
    }
}