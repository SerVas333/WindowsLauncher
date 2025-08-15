using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
    /// ViewModel для окна управления адресной книгой
    /// </summary>
    public class AddressBookViewModel : ViewModelBase
    {
        #region Fields
        
        private readonly IAddressBookService _addressBookService;
        private readonly ILogger<AddressBookViewModel> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        
        private string _searchText = string.Empty;
        private bool _isLoading = false;
        private bool _isSelectionMode = false;
        private bool _isAdminMode = false;
        private bool _allowMultipleSelection = false;
        private SelectionMode _selectionMode = SelectionMode.Single;
        
        private ObservableCollection<Contact> _allContacts = new();
        private ObservableCollection<Contact> _filteredContacts = new();
        private List<Contact>? _selectedContacts = null;
        private Contact? _selectedContact = null;
        
        #endregion
        
        #region Properties
        
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterContacts();
                }
            }
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        public bool IsSelectionMode
        {
            get => _isSelectionMode;
            set => SetProperty(ref _isSelectionMode, value);
        }
        
        public bool IsAdminMode
        {
            get => _isAdminMode;
            set => SetProperty(ref _isAdminMode, value);
        }
        
        public bool AllowMultipleSelection
        {
            get => _allowMultipleSelection;
            set
            {
                if (SetProperty(ref _allowMultipleSelection, value))
                {
                    SelectionMode = value ? SelectionMode.Multiple : SelectionMode.Single;
                }
            }
        }
        
        public SelectionMode SelectionMode
        {
            get => _selectionMode;
            set => SetProperty(ref _selectionMode, value);
        }
        
        public ObservableCollection<Contact> FilteredContacts
        {
            get => _filteredContacts;
            private set => SetProperty(ref _filteredContacts, value);
        }
        
        public List<Contact>? SelectedContacts
        {
            get => _selectedContacts;
            set => SetProperty(ref _selectedContacts, value);
        }

        public Contact? SelectedContact
        {
            get => _selectedContact;
            set => SetProperty(ref _selectedContact, value);
        }
        
        // Коллекция для привязки к ListView.SelectedItems
        public INotifyCollectionChanged? SelectedContactsCollection { get; set; }
        
        public bool HasNoContacts => !IsLoading && FilteredContacts.Count == 0;
        public bool HasSelectedContacts => SelectedContacts?.Count > 0;
        public int SelectedContactsCount => SelectedContacts?.Count ?? 0;
        public string SelectAllButtonText => HasSelectedContacts ? "Снять выделение" : "Выделить все";
        
        #endregion
        
        #region Commands
        
        public AsyncRelayCommand AddContactCommand { get; }
        public AsyncRelayCommand<Contact> EditContactCommand { get; }
        public AsyncRelayCommand<Contact> DeleteContactCommand { get; }
        public RelayCommand ImportExportCommand { get; }
        public RelayCommand ToggleSelectAllCommand { get; }
        public RelayCommand ConfirmSelectionCommand { get; }
        public RelayCommand CancelCommand { get; }
        
        #endregion
        
        #region Constructor
        
        public AddressBookViewModel(
            IAddressBookService addressBookService,
            ILogger<AddressBookViewModel> logger,
            IDialogService dialogService,
            IServiceScopeFactory serviceScopeFactory) : base(logger, dialogService)
        {
            _addressBookService = addressBookService ?? throw new ArgumentNullException(nameof(addressBookService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            
            // Initialize commands
            AddContactCommand = new AsyncRelayCommand(AddContactAsync);
            EditContactCommand = new AsyncRelayCommand<Contact>(EditContactAsync);
            DeleteContactCommand = new AsyncRelayCommand<Contact>(DeleteContactAsync);
            ImportExportCommand = new RelayCommand(ShowImportExportMenu);
            ToggleSelectAllCommand = new RelayCommand(ToggleSelectAll);
            ConfirmSelectionCommand = new RelayCommand(ConfirmSelection, () => HasSelectedContacts);
            CancelCommand = new RelayCommand(CloseWindow);
            
            // Subscribe to property changes
            PropertyChanged += OnPropertyChanged;
            
            // Load contacts on initialization
            _ = LoadContactsAsync();
        }
        
        #endregion
        
        #region Methods
        
        public async Task LoadContactsAsync()
        {
            try
            {
                IsLoading = true;
                
                var contacts = await _addressBookService.GetAllContactsAsync();
                
                _allContacts.Clear();
                foreach (var contact in contacts.OrderBy(c => c.FirstName).ThenBy(c => c.LastName))
                {
                    _allContacts.Add(contact);
                }
                
                FilterContacts();
                
                _logger.LogInformation("Loaded {ContactCount} contacts", contacts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contacts");
                MessageBox.Show($"Ошибка загрузки контактов: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void FilterContacts()
        {
            var filtered = _allContacts.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchTerm = SearchText.ToLowerInvariant();
                filtered = filtered.Where(c =>
                    c.FirstName.ToLowerInvariant().Contains(searchTerm) ||
                    c.LastName.ToLowerInvariant().Contains(searchTerm) ||
                    c.Email.ToLowerInvariant().Contains(searchTerm) ||
                    (c.Phone?.ToLowerInvariant().Contains(searchTerm) ?? false) ||
                    (c.Department?.ToLowerInvariant().Contains(searchTerm) ?? false));
            }
            
            FilteredContacts = new ObservableCollection<Contact>(filtered);
            OnPropertyChanged(nameof(HasNoContacts));
        }
        
        private async Task AddContactAsync()
        {
            try
            {
                var contact = new Contact();
                
                var contactEditViewModel = _serviceScopeFactory.CreateScopedService<ContactEditViewModel>();
                
                contactEditViewModel.SetContact(contact, isNew: true);
                
                var contactEditWindow = new ContactEditWindow(contactEditViewModel)
                {
                    Owner = Application.Current.MainWindow,
                    Title = "Добавить контакт"
                };
                
                if (contactEditWindow.ShowDialog() == true)
                {
                    await LoadContactsAsync(); // Перезагружаем список
                    _logger.LogInformation("Added new contact: {ContactName}", contact.FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding contact");
                MessageBox.Show($"Ошибка при добавлении контакта: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async Task EditContactAsync(Contact? contact)
        {
            if (contact == null) return;
            
            try
            {
                var contactEditViewModel = _serviceScopeFactory.CreateScopedService<ContactEditViewModel>();
                
                // Создаем копию контакта для редактирования
                var contactCopy = new Contact
                {
                    Id = contact.Id,
                    FirstName = contact.FirstName,
                    LastName = contact.LastName,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    Department = contact.Department,
                    Company = contact.Company,
                    Notes = contact.Notes,
                    CreatedAt = contact.CreatedAt,
                    UpdatedAt = contact.UpdatedAt
                };
                
                contactEditViewModel.SetContact(contactCopy, isNew: false);
                
                var contactEditWindow = new ContactEditWindow(contactEditViewModel)
                {
                    Owner = Application.Current.MainWindow,
                    Title = $"Редактировать контакт - {contact.FullName}"
                };
                
                if (contactEditWindow.ShowDialog() == true)
                {
                    await LoadContactsAsync(); // Перезагружаем список
                    _logger.LogInformation("Edited contact: {ContactName}", contact.FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing contact {ContactId}", contact.Id);
                MessageBox.Show($"Ошибка при редактировании контакта: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async Task DeleteContactAsync(Contact? contact)
        {
            if (contact == null) return;
            
            try
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить контакт '{contact.FullName}'?\n\nЭто действие нельзя отменить.", 
                    "Подтверждение удаления", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    await _addressBookService.DeleteContactAsync(contact.Id);
                    await LoadContactsAsync(); // Перезагружаем список
                    
                    _logger.LogInformation("Deleted contact: {ContactName} (ID: {ContactId})", 
                        contact.FullName, contact.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact {ContactId}", contact.Id);
                MessageBox.Show($"Ошибка при удалении контакта: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ShowImportExportMenu()
        {
            try
            {
                // TODO: Реализовать меню импорта/экспорта
                var contextMenu = new ContextMenu();
                
                var importCsvItem = new MenuItem { Header = "Импорт из CSV..." };
                importCsvItem.Click += async (s, e) => await ImportFromCsvAsync();
                
                var exportCsvItem = new MenuItem { Header = "Экспорт в CSV..." };
                exportCsvItem.Click += async (s, e) => await ExportToCsvAsync();
                
                contextMenu.Items.Add(importCsvItem);
                contextMenu.Items.Add(exportCsvItem);
                
                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing import/export menu");
            }
        }
        
        private async Task ImportFromCsvAsync()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Выберите CSV файл для импорта",
                    Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
                    CheckFileExists = true
                };
                
                if (openFileDialog.ShowDialog() == true)
                {
                    var csvContent = await File.ReadAllTextAsync(openFileDialog.FileName);
                    var importResult = await _addressBookService.ImportContactsFromCsvAsync(csvContent, "System");
                    
                    MessageBox.Show($"Импорт завершен:\n" +
                        $"Успешно: {importResult.SuccessfullyImported}\n" +
                        $"Ошибки: {importResult.Errors}", 
                        "Результат импорта", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    await LoadContactsAsync(); // Перезагружаем список
                    _logger.LogInformation("Imported {SuccessfullyImported} contacts from CSV", 
                        importResult.SuccessfullyImported);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing contacts from CSV");
                MessageBox.Show($"Ошибка при импорте: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async Task ExportToCsvAsync()
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Сохранить контакты в CSV",
                    Filter = "CSV файлы (*.csv)|*.csv",
                    DefaultExt = "csv",
                    FileName = $"contacts_{DateTime.Now:yyyy-MM-dd}.csv"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    var csvContent = await _addressBookService.ExportContactsToCsvAsync();
                    await File.WriteAllTextAsync(saveFileDialog.FileName, csvContent);
                    
                    MessageBox.Show($"Контакты успешно экспортированы в файл:\n{saveFileDialog.FileName}", 
                        "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    _logger.LogInformation("Exported {ContactCount} contacts to CSV: {FilePath}", 
                        _allContacts.Count, saveFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting contacts to CSV");
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ToggleSelectAll()
        {
            if (HasSelectedContacts)
            {
                SelectedContacts?.Clear();
            }
            else
            {
                SelectedContacts = FilteredContacts.ToList();
            }
            
            OnPropertyChanged(nameof(HasSelectedContacts));
            OnPropertyChanged(nameof(SelectedContactsCount));
            OnPropertyChanged(nameof(SelectAllButtonText));
            
            // Обновляем команду подтверждения
            ConfirmSelectionCommand.RaiseCanExecuteChanged();
        }
        
        private void ConfirmSelection()
        {
            if (Application.Current.Windows.OfType<AddressBookWindow>().FirstOrDefault() is AddressBookWindow window)
            {
                // AddressBookWindow может открываться как диалог (из ComposeEmail) 
                // или как обычное окно (из MainWindow). Проверяем режим.
                try
                {
                    window.DialogResult = true;
                }
                catch (System.InvalidOperationException)
                {
                    // Окно открыто как обычное окно, DialogResult недоступен
                }
                window.Close();
            }
        }
        
        private void CloseWindow()
        {
            if (Application.Current.Windows.OfType<AddressBookWindow>().FirstOrDefault() is AddressBookWindow window)
            {
                // AddressBookWindow может открываться как диалог (из ComposeEmail) 
                // или как обычное окно (из MainWindow). Проверяем режим.
                try
                {
                    window.DialogResult = false;
                }
                catch (System.InvalidOperationException)
                {
                    // Окно открыто как обычное окно, DialogResult недоступен
                }
                window.Close();
            }
        }
        
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SelectedContacts):
                    OnPropertyChanged(nameof(HasSelectedContacts));
                    OnPropertyChanged(nameof(SelectedContactsCount));
                    OnPropertyChanged(nameof(SelectAllButtonText));
                    // Обновляем команду подтверждения при изменении выбранных контактов
                    ConfirmSelectionCommand.RaiseCanExecuteChanged();
                    break;
            }
        }
        
        #endregion
    }
}