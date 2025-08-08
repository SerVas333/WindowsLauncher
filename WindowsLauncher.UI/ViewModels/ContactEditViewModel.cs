using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces.Email;
using WindowsLauncher.Core.Models.Email;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Views;
using WindowsLauncher.UI.Infrastructure.Commands;
using WindowsLauncher.UI.Infrastructure.Services;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для редактирования/создания контакта
    /// </summary>
    public class ContactEditViewModel : ViewModelBase, IDataErrorInfo
    {
        #region Fields
        
        private readonly IAddressBookService _addressBookService;
        private readonly ILogger<ContactEditViewModel> _logger;
        
        private Contact _contact = new();
        private bool _isNew = true;
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private string _email = string.Empty;
        private string _phone = string.Empty;
        private string _company = string.Empty;
        private string _department = string.Empty;
        private string _group = string.Empty;
        private string _notes = string.Empty;
        private bool _hasValidationErrors = false;
        
        #endregion
        
        #region Properties
        
        public string FirstName
        {
            get => _firstName;
            set
            {
                if (SetProperty(ref _firstName, value))
                {
                    OnPropertyChanged(nameof(PreviewFullName));
                    OnPropertyChanged(nameof(PreviewInitials));
                    OnPropertyChanged(nameof(CanSave));
                    ValidateProperty(nameof(FirstName));
                }
            }
        }
        
        public string LastName
        {
            get => _lastName;
            set
            {
                if (SetProperty(ref _lastName, value))
                {
                    OnPropertyChanged(nameof(PreviewFullName));
                    OnPropertyChanged(nameof(PreviewInitials));
                    OnPropertyChanged(nameof(CanSave));
                    ValidateProperty(nameof(LastName));
                }
            }
        }
        
        public string Email
        {
            get => _email;
            set
            {
                if (SetProperty(ref _email, value))
                {
                    OnPropertyChanged(nameof(CanSave));
                    ValidateProperty(nameof(Email));
                }
            }
        }
        
        public string Phone
        {
            get => _phone;
            set
            {
                if (SetProperty(ref _phone, value))
                {
                    OnPropertyChanged(nameof(HasPhone));
                }
            }
        }
        
        public string Company
        {
            get => _company;
            set => SetProperty(ref _company, value);
        }
        
        public string Department
        {
            get => _department;
            set
            {
                if (SetProperty(ref _department, value))
                {
                    OnPropertyChanged(nameof(HasDepartment));
                }
            }
        }
        
        public string Group
        {
            get => _group;
            set => SetProperty(ref _group, value);
        }
        
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }
        
        public bool HasValidationErrors
        {
            get => _hasValidationErrors;
            set => SetProperty(ref _hasValidationErrors, value);
        }
        
        public ObservableCollection<string> AvailableGroups { get; } = new();
        
        // UI Properties
        public string WindowTitle => _isNew ? "Добавить контакт" : "Редактировать контакт";
        public string HeaderTitle => _isNew ? "Новый контакт" : "Редактирование контакта";
        public string HeaderSubtitle => _isNew ? "Заполните информацию о контакте" : $"Изменение данных контакта";
        
        public string PreviewFullName => $"{FirstName} {LastName}".Trim();
        
        public string PreviewInitials
        {
            get
            {
                if (!string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName))
                    return $"{FirstName[0]}{LastName[0]}".ToUpper();
                
                if (!string.IsNullOrEmpty(FirstName))
                    return FirstName[0].ToString().ToUpper();
                
                if (!string.IsNullOrEmpty(LastName))
                    return LastName[0].ToString().ToUpper();
                
                return Email.Length > 0 ? Email[0].ToString().ToUpper() : "?";
            }
        }
        
        public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
        public bool HasDepartment => !string.IsNullOrWhiteSpace(Department);
        
        public bool CanSave => !string.IsNullOrWhiteSpace(FirstName) && 
                              !string.IsNullOrWhiteSpace(LastName) && 
                              !string.IsNullOrWhiteSpace(Email) && 
                              IsValidEmail(Email) &&
                              !HasValidationErrors;
        
        public string ValidationErrorMessage => this.Error;
        
        #endregion
        
        #region Commands
        
        public AsyncRelayCommand SaveContactCommand { get; }
        public ICommand CancelCommand { get; }
        
        #endregion
        
        #region Constructor
        
        public ContactEditViewModel(
            IAddressBookService addressBookService,
            ILogger<ContactEditViewModel> logger,
            IDialogService dialogService) : base(logger, dialogService)
        {
            _addressBookService = addressBookService ?? throw new ArgumentNullException(nameof(addressBookService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize commands
            SaveContactCommand = new AsyncRelayCommand(SaveContactAsync, () => CanSave);
            CancelCommand = new RelayCommand(() => CloseWindow(false));
            
            // Subscribe to property changes for command updates
            PropertyChanged += OnPropertyChanged;
            
            // Load available groups
            _ = LoadAvailableGroupsAsync();
        }
        
        #endregion
        
        #region Methods
        
        public void SetContact(Contact contact, bool isNew)
        {
            _contact = contact ?? throw new ArgumentNullException(nameof(contact));
            _isNew = isNew;
            
            // Load contact data into properties
            FirstName = contact.FirstName;
            LastName = contact.LastName;
            Email = contact.Email;
            Phone = contact.Phone ?? string.Empty;
            Company = contact.Company ?? string.Empty;
            Department = contact.Department ?? string.Empty;
            Group = contact.Group ?? string.Empty;
            Notes = contact.Notes ?? string.Empty;
            
            // Update UI properties
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
        }
        
        private async Task LoadAvailableGroupsAsync()
        {
            try
            {
                var contacts = await _addressBookService.GetAllContactsAsync();
                var groups = contacts
                    .Where(c => !string.IsNullOrWhiteSpace(c.Group))
                    .Select(c => c.Group!)
                    .Distinct()
                    .OrderBy(g => g)
                    .ToList();
                
                AvailableGroups.Clear();
                foreach (var group in groups)
                {
                    AvailableGroups.Add(group);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load available groups");
                // Not critical error, continue without groups
            }
        }
        
        private async Task SaveContactAsync()
        {
            try
            {
                // Update contact with current data
                _contact.FirstName = FirstName;
                _contact.LastName = LastName;
                _contact.Email = Email;
                _contact.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone;
                _contact.Company = string.IsNullOrWhiteSpace(Company) ? null : Company;
                _contact.Department = string.IsNullOrWhiteSpace(Department) ? null : Department;
                _contact.Group = string.IsNullOrWhiteSpace(Group) ? null : Group;
                _contact.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes;
                _contact.UpdatedAt = DateTime.Now;
                
                if (_isNew)
                {
                    await _addressBookService.CreateContactAsync(_contact);
                    _logger.LogInformation("Created new contact: {ContactName} ({Email})", 
                        _contact.FullName, _contact.Email);
                }
                else
                {
                    await _addressBookService.UpdateContactAsync(_contact);
                    _logger.LogInformation("Updated contact: {ContactName} ({Email})", 
                        _contact.FullName, _contact.Email);
                }
                
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving contact {ContactId}", _contact.Id);
                MessageBox.Show($"Ошибка при сохранении контакта: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CloseWindow(bool dialogResult = false)
        {
            if (Application.Current.Windows.OfType<ContactEditWindow>().FirstOrDefault() is ContactEditWindow window)
            {
                // ContactEditWindow всегда открывается как диалог через ShowDialog()
                // Безопасно устанавливаем DialogResult
                window.DialogResult = dialogResult;
                window.Close();
            }
        }
        
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Update command can execute status
            if (e.PropertyName is nameof(FirstName) or nameof(LastName) or nameof(Email))
            {
                SaveContactCommand.RaiseCanExecuteChanged();
            }
        }
        
        private void ValidateProperty(string propertyName)
        {
            var error = GetPropertyError(propertyName);
            HasValidationErrors = !string.IsNullOrEmpty(error);
        }
        
        private string GetPropertyError(string propertyName)
        {
            return propertyName switch
            {
                nameof(FirstName) when string.IsNullOrWhiteSpace(FirstName) => "Имя обязательно для заполнения",
                nameof(LastName) when string.IsNullOrWhiteSpace(LastName) => "Фамилия обязательна для заполнения",
                nameof(Email) when string.IsNullOrWhiteSpace(Email) => "Email обязателен для заполнения",
                nameof(Email) when !IsValidEmail(Email) => "Некорректный формат email адреса",
                _ => string.Empty
            };
        }
        
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
                
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        
        #endregion
        
        #region IDataErrorInfo Implementation
        
        public string Error
        {
            get
            {
                var errors = new List<string>();
                
                if (string.IsNullOrWhiteSpace(FirstName))
                    errors.Add("Имя");
                if (string.IsNullOrWhiteSpace(LastName))
                    errors.Add("Фамилия");
                if (string.IsNullOrWhiteSpace(Email))
                    errors.Add("Email");
                else if (!IsValidEmail(Email))
                    errors.Add("корректный Email");
                
                return errors.Count > 0 ? $"Обязательные поля: {string.Join(", ", errors)}" : string.Empty;
            }
        }
        
        public string this[string columnName] => GetPropertyError(columnName);
        
        #endregion
    }
}