using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WindowsLauncher.UI.Infrastructure.Extensions;
using Microsoft.Win32;
using WindowsLauncher.Core.Interfaces.Email;
using WindowsLauncher.Core.Models.Email;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Views;
using WindowsLauncher.UI.Infrastructure.Commands;
using WindowsLauncher.UI.Infrastructure.Services;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для окна создания email сообщения
    /// </summary>
    public class ComposeEmailViewModel : ViewModelBase
    {
        #region Fields
        
        private readonly IEmailService _emailService;
        private readonly IAddressBookService _addressBookService;
        private readonly ILogger<ComposeEmailViewModel> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        
        private string _subject = string.Empty;
        private string _messageBody = string.Empty;
        private bool _showCcBcc = false;
        private bool _isSending = false;
        private string _sendingStatusMessage = string.Empty;
        
        #endregion
        
        #region Properties
        
        public string Subject
        {
            get => _subject;
            set => SetProperty(ref _subject, value);
        }
        
        public string MessageBody
        {
            get => _messageBody;
            set => SetProperty(ref _messageBody, value);
        }
        
        public bool ShowCcBcc
        {
            get => _showCcBcc;
            set => SetProperty(ref _showCcBcc, value);
        }
        
        public bool IsSending
        {
            get => _isSending;
            set => SetProperty(ref _isSending, value);
        }
        
        public string SendingStatusMessage
        {
            get => _sendingStatusMessage;
            set => SetProperty(ref _sendingStatusMessage, value);
        }
        
        public ObservableCollection<Contact> ToRecipients { get; } = new();
        public ObservableCollection<Contact> CcRecipients { get; } = new();
        public ObservableCollection<Contact> BccRecipients { get; } = new();
        public ObservableCollection<EmailAttachment> Attachments { get; } = new();
        
        public bool HasAttachments => Attachments.Count > 0;
        public bool CanSend => ToRecipients.Count > 0 && !string.IsNullOrWhiteSpace(Subject) && !IsSending;
        
        #endregion
        
        #region Commands
        
        public AsyncRelayCommand SelectToRecipientsCommand { get; }
        public AsyncRelayCommand SelectCcRecipientsCommand { get; }
        public AsyncRelayCommand SelectBccRecipientsCommand { get; }
        public RelayCommand<Contact> RemoveToRecipientCommand { get; }
        public RelayCommand<Contact> RemoveCcRecipientCommand { get; }
        public RelayCommand<Contact> RemoveBccRecipientCommand { get; }
        public RelayCommand ToggleCcBccCommand { get; }
        public AsyncRelayCommand AddAttachmentCommand { get; }
        public RelayCommand<EmailAttachment> RemoveAttachmentCommand { get; }
        public AsyncRelayCommand SendEmailCommand { get; }
        public AsyncRelayCommand SaveDraftCommand { get; }
        public RelayCommand CancelCommand { get; }
        
        #endregion
        
        #region Constructor
        
        public ComposeEmailViewModel(
            IEmailService emailService,
            IAddressBookService addressBookService,
            ILogger<ComposeEmailViewModel> logger,
            IDialogService dialogService,
            IServiceScopeFactory serviceScopeFactory) : base(logger, dialogService)
        {
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _addressBookService = addressBookService ?? throw new ArgumentNullException(nameof(addressBookService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            
            // Initialize commands
            SelectToRecipientsCommand = new AsyncRelayCommand(() => SelectRecipientsAsync(RecipientType.To));
            SelectCcRecipientsCommand = new AsyncRelayCommand(() => SelectRecipientsAsync(RecipientType.Cc));
            SelectBccRecipientsCommand = new AsyncRelayCommand(() => SelectRecipientsAsync(RecipientType.Bcc));
            RemoveToRecipientCommand = new RelayCommand<Contact>(RemoveToRecipient);
            RemoveCcRecipientCommand = new RelayCommand<Contact>(RemoveCcRecipient);
            RemoveBccRecipientCommand = new RelayCommand<Contact>(RemoveBccRecipient);
            ToggleCcBccCommand = new RelayCommand(() => ShowCcBcc = !ShowCcBcc);
            AddAttachmentCommand = new AsyncRelayCommand(AddAttachmentAsync);
            RemoveAttachmentCommand = new RelayCommand<EmailAttachment>(RemoveAttachment);
            SendEmailCommand = new AsyncRelayCommand(SendEmailAsync, () => CanSend);
            SaveDraftCommand = new AsyncRelayCommand(SaveDraftAsync);
            CancelCommand = new RelayCommand(CloseWindow);
            
            // Subscribe to property changes
            ToRecipients.CollectionChanged += (s, e) => OnPropertyChanged(nameof(CanSend));
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Subject) || e.PropertyName == nameof(IsSending))
                    OnPropertyChanged(nameof(CanSend));
                if (e.PropertyName == nameof(Attachments))
                    OnPropertyChanged(nameof(HasAttachments));
            };
            Attachments.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasAttachments));
        }
        
        #endregion
        
        #region Methods
        
        private async Task SelectRecipientsAsync(RecipientType recipientType)
        {
            try
            {
                // Создаем AddressBookViewModel через scoped scope для доступа к Scoped сервисам
                var addressBookViewModel = _serviceScopeFactory.CreateScopedService<AddressBookViewModel>();
                
                // Настраиваем режим выбора
                addressBookViewModel.IsSelectionMode = true;
                addressBookViewModel.AllowMultipleSelection = true;
                
                // Исключаем уже выбранных получателей
                var excludeContacts = new List<Contact>();
                excludeContacts.AddRange(ToRecipients);
                excludeContacts.AddRange(CcRecipients);
                excludeContacts.AddRange(BccRecipients);
                
                var addressBookWindow = new AddressBookWindow(addressBookViewModel)
                {
                    Owner = Application.Current.MainWindow,
                    Title = recipientType switch
                    {
                        RecipientType.To => "Выбрать получателей",
                        RecipientType.Cc => "Выбрать получателей копии",
                        RecipientType.Bcc => "Выбрать получателей скрытой копии",
                        _ => "Выбрать контакты"
                    }
                };
                
                if (addressBookWindow.ShowDialog() == true)
                {
                    var selectedContacts = addressBookViewModel.SelectedContacts ?? new List<Contact>();
                    
                    foreach (var contact in selectedContacts)
                    {
                        // Проверяем, что контакт не добавлен в другие списки
                        if (!excludeContacts.Any(c => c.Id == contact.Id))
                        {
                            switch (recipientType)
                            {
                                case RecipientType.To:
                                    ToRecipients.Add(contact);
                                    break;
                                case RecipientType.Cc:
                                    CcRecipients.Add(contact);
                                    break;
                                case RecipientType.Bcc:
                                    BccRecipients.Add(contact);
                                    break;
                            }
                        }
                    }
                    
                    _logger.LogInformation("Selected {Count} {RecipientType} recipients", 
                        selectedContacts.Count, recipientType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting {RecipientType} recipients", recipientType);
                MessageBox.Show($"Ошибка при выборе получателей: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void RemoveToRecipient(Contact contact)
        {
            if (contact != null)
                ToRecipients.Remove(contact);
        }
        
        private void RemoveCcRecipient(Contact contact)
        {
            if (contact != null)
                CcRecipients.Remove(contact);
        }
        
        private void RemoveBccRecipient(Contact contact)
        {
            if (contact != null)
                BccRecipients.Remove(contact);
        }
        
        private async Task AddAttachmentAsync()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Выберите файлы для прикрепления",
                    Multiselect = true,
                    Filter = "Все файлы (*.*)|*.*|" +
                           "Документы (*.pdf;*.doc;*.docx)|*.pdf;*.doc;*.docx|" +
                           "Изображения (*.jpg;*.jpeg;*.png;*.gif)|*.jpg;*.jpeg;*.png;*.gif|" +
                           "Архивы (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z"
                };
                
                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (var filePath in openFileDialog.FileNames)
                    {
                        if (File.Exists(filePath))
                        {
                            var fileInfo = new FileInfo(filePath);
                            
                            // Проверяем размер файла (максимум 25 МБ)
                            const long maxFileSize = 25 * 1024 * 1024; // 25 MB
                            if (fileInfo.Length > maxFileSize)
                            {
                                MessageBox.Show($"Файл '{fileInfo.Name}' слишком большой. " +
                                    "Максимальный размер файла: 25 МБ", "Предупреждение", 
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                continue;
                            }
                            
                            // Проверяем, что файл еще не добавлен
                            if (Attachments.Any(a => string.Equals(a.FilePath, filePath, 
                                StringComparison.OrdinalIgnoreCase)))
                            {
                                continue; // Файл уже добавлен
                            }
                            
                            var attachment = new EmailAttachment
                            {
                                FileName = fileInfo.Name,
                                FilePath = filePath,
                                Size = fileInfo.Length,
                                ContentType = GetContentType(fileInfo.Extension)
                            };
                            
                            Attachments.Add(attachment);
                            _logger.LogInformation("Added attachment: {FileName} ({Size} bytes)", 
                                fileInfo.Name, fileInfo.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding attachment");
                MessageBox.Show($"Ошибка при добавлении вложения: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void RemoveAttachment(EmailAttachment attachment)
        {
            if (attachment != null)
            {
                Attachments.Remove(attachment);
                _logger.LogInformation("Removed attachment: {FileName}", attachment.FileName);
            }
        }
        
        private async Task SendEmailAsync()
        {
            if (!CanSend) return;
            
            try
            {
                IsSending = true;
                SendingStatusMessage = "Отправка сообщения...";
                
                // Создаем email сообщение
                var emailMessage = new EmailMessage
                {
                    Subject = Subject,
                    Body = MessageBody,
                    IsHtml = false, // Пока только текстовые сообщения
                    To = ToRecipients.Select(c => c.Email).ToList(),
                    Cc = CcRecipients.Select(c => c.Email).ToList(),
                    Bcc = BccRecipients.Select(c => c.Email).ToList(),
                    Attachments = Attachments.ToList()
                };
                
                // Отправляем email
                var result = await _emailService.SendEmailAsync(emailMessage);
                
                if (result.IsSuccess)
                {
                    _logger.LogInformation("Email sent successfully to {ToCount} recipients via {UsedServer}", 
                        emailMessage.To.Count, result.UsedServer);
                    
                    MessageBox.Show("Сообщение успешно отправлено!", "Отправка завершена", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    CloseWindow();
                }
                else
                {
                    _logger.LogWarning("Failed to send email: {Error}", result.ErrorMessage);
                    
                    MessageBox.Show($"Не удалось отправить сообщение:\n{result.ErrorMessage}", 
                        "Ошибка отправки", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending email");
                MessageBox.Show($"Неожиданная ошибка при отправке: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSending = false;
                SendingStatusMessage = string.Empty;
            }
        }
        
        private Task SaveDraftAsync()
        {
            try
            {
                // TODO: Реализовать сохранение черновиков в будущих версиях
                MessageBox.Show("Функция сохранения черновиков будет добавлена в следующих версиях.", 
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving draft");
                MessageBox.Show($"Ошибка при сохранении черновика: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return Task.CompletedTask;
            }
        }
        
        private void CloseWindow()
        {
            if (Application.Current.Windows.OfType<ComposeEmailWindow>().FirstOrDefault() is ComposeEmailWindow window)
            {
                // ComposeEmailWindow всегда открывается как обычное окно через Show(), 
                // поэтому не пытаемся установить DialogResult
                _logger?.LogDebug("Closing ComposeEmailWindow (regular window, not dialog)");
                window.Close();
            }
        }
        
        private string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",
                _ => "application/octet-stream"
            };
        }
        
        #endregion
    }
    
    public enum RecipientType
    {
        To,
        Cc,
        Bcc
    }
}