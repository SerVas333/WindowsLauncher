// WindowsLauncher.UI/ViewModels/LocalUserDialogViewModel.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.Infrastructure.Commands;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для диалогового окна управления локальными пользователями
    /// </summary>
    public class LocalUserDialogViewModel : INotifyPropertyChanged
    {
        private readonly ILocalUserService _localUserService;
        private readonly IAuditService _auditService;
        private readonly User _originalUser;
        private int _currentUserId;

        // Backing fields
        private string _username = string.Empty;
        private string _displayName = string.Empty;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private UserRole _role = UserRole.Standard;
        private bool _isActive = true;
        private bool _requirePasswordChange = false;
        private bool _isLocked = false;
        private int _failedLoginAttempts = 0;
        private DateTime _createdAt = DateTime.Now;
        private DateTime? _lastLoginAt = null;
        private DateTime? _modifiedAt = null;
        private DateTime? _lastActivityAt = null;
        
        private bool _isLoading = false;
        private string _loadingMessage = string.Empty;
        private bool _hasError = false;
        private string _errorMessage = string.Empty;
        private bool _isEditMode = false;

        #region Properties

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public UserRole Role
        {
            get => _role;
            set 
            {
                if (SetProperty(ref _role, value))
                {
                    OnPropertyChanged(nameof(SelectedRole));
                }
            }
        }

        public RoleItem SelectedRole
        {
            get => AvailableRoles.FirstOrDefault(r => r.Value == Role) ?? AvailableRoles.First();
            set
            {
                if (value != null)
                {
                    Role = value.Value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool RequirePasswordChange
        {
            get => _requirePasswordChange;
            set => SetProperty(ref _requirePasswordChange, value);
        }

        public bool IsLocked
        {
            get => _isLocked;
            set => SetProperty(ref _isLocked, value);
        }

        public int FailedLoginAttempts
        {
            get => _failedLoginAttempts;
            set => SetProperty(ref _failedLoginAttempts, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public DateTime? LastLoginAt
        {
            get => _lastLoginAt;
            set => SetProperty(ref _lastLoginAt, value);
        }

        public DateTime? ModifiedAt
        {
            get => _modifiedAt;
            set => SetProperty(ref _modifiedAt, value);
        }

        public DateTime? LastActivityAt
        {
            get => _lastActivityAt;
            set => SetProperty(ref _lastActivityAt, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public string DialogTitle => IsEditMode ? "Редактирование пользователя" : "Создание пользователя";
        public string SaveButtonText => IsEditMode ? "Сохранить" : "Создать";

        public List<RoleItem> AvailableRoles { get; } = new List<RoleItem>
        {
            new RoleItem { Value = UserRole.Guest, DisplayName = "Гостевой пользователь" },
            new RoleItem { Value = UserRole.Standard, DisplayName = "Стандартный пользователь" },
            new RoleItem { Value = UserRole.PowerUser, DisplayName = "Опытный пользователь" },
            new RoleItem { Value = UserRole.Administrator, DisplayName = "Администратор" }
        };

        #endregion

        #region Commands

        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand ResetPasswordCommand { get; private set; }
        public ICommand ResetFailedAttemptsCommand { get; private set; }

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<bool> DialogClosed;

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор для создания нового пользователя
        /// </summary>
        public LocalUserDialogViewModel(ILocalUserService localUserService, IAuditService auditService, int currentUserId)
        {
            _localUserService = localUserService ?? throw new ArgumentNullException(nameof(localUserService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _currentUserId = currentUserId;
            _originalUser = null;
            _isEditMode = false;

            InitializeCommands();
        }

        /// <summary>
        /// Конструктор для редактирования существующего пользователя
        /// </summary>
        public LocalUserDialogViewModel(ILocalUserService localUserService, IAuditService auditService, User user, int currentUserId)
        {
            _localUserService = localUserService ?? throw new ArgumentNullException(nameof(localUserService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _originalUser = user ?? throw new ArgumentNullException(nameof(user));
            _currentUserId = currentUserId;
            _isEditMode = true;

            LoadUserData();
            InitializeCommands();
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            SaveCommand = new AsyncRelayCommand(SaveUserAsync, CanSaveUser);
            CancelCommand = new RelayCommand(() => OnDialogClosed(false));
            ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => IsEditMode);
            ResetFailedAttemptsCommand = new AsyncRelayCommand(ResetFailedAttemptsAsync, () => IsEditMode && FailedLoginAttempts > 0);
        }

        private void LoadUserData()
        {
            if (_originalUser == null) return;

            Username = _originalUser.Username;
            DisplayName = _originalUser.DisplayName;
            Email = _originalUser.Email;
            Role = _originalUser.Role;
            IsActive = _originalUser.IsActive;
            RequirePasswordChange = false; // Пока не реализовано в модели
            IsLocked = _originalUser.IsLocked;
            FailedLoginAttempts = _originalUser.FailedLoginAttempts;
            CreatedAt = _originalUser.CreatedAt;
            LastLoginAt = _originalUser.LastLoginAt;
            ModifiedAt = DateTime.Now; // Используем текущую дату
            LastActivityAt = _originalUser.LastActivityAt;
        }

        private bool CanSaveUser()
        {
            var result = true;
            var reasons = new List<string>();

            if (IsLoading) { result = false; reasons.Add("IsLoading"); }
            if (string.IsNullOrWhiteSpace(Username)) { result = false; reasons.Add("Username empty"); }
            if (string.IsNullOrWhiteSpace(DisplayName)) { result = false; reasons.Add("DisplayName empty"); }
            
            if (!IsEditMode)
            {
                if (string.IsNullOrWhiteSpace(Password)) { result = false; reasons.Add("Password empty"); }
                if (Password != ConfirmPassword) { result = false; reasons.Add("Password mismatch"); }
            }

            System.Diagnostics.Debug.WriteLine($"CanSaveUser: {result}, Reasons: {string.Join(", ", reasons)}");
            return result;
        }

        private async Task SaveUserAsync()
        {
            try
            {
                SetLoading(true, IsEditMode ? "Сохранение пользователя..." : "Создание пользователя...");
                ClearError();

                // Детальное логирование
                System.Diagnostics.Debug.WriteLine($"SaveUserAsync: IsEditMode={IsEditMode}, Username='{Username}', Password='{Password?.Length} chars'");

                bool success = false;
                
                if (!IsEditMode)
                {
                    System.Diagnostics.Debug.WriteLine("SaveUserAsync: Calling CreateUserAsync...");
                    success = await CreateUserAsync();
                    System.Diagnostics.Debug.WriteLine($"SaveUserAsync: CreateUserAsync returned: {success}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("SaveUserAsync: Calling UpdateUserAsync...");
                    success = await UpdateUserAsync();
                    System.Diagnostics.Debug.WriteLine($"SaveUserAsync: UpdateUserAsync returned: {success}");
                }

                if (success)
                {
                    System.Diagnostics.Debug.WriteLine("SaveUserAsync: Calling OnDialogClosed(true)...");
                    OnDialogClosed(true);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("SaveUserAsync: Operation failed, dialog remains open");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveUserAsync: Exception caught: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SaveUserAsync: Exception StackTrace: {ex.StackTrace}");
                SetError($"Ошибка при сохранении пользователя: {ex.Message}");
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine("SaveUserAsync: Setting loading to false");
                SetLoading(false);
            }
        }

        private async Task<bool> CreateUserAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("CreateUserAsync: Starting password validation...");
                // Validate password
                var passwordValidation = await _localUserService.ValidatePasswordAsync(Password);
                if (!passwordValidation.IsValid)
                {
                    var errorMsg = $"Пароль не соответствует требованиям: {string.Join(", ", passwordValidation.Errors)}";
                    System.Diagnostics.Debug.WriteLine($"CreateUserAsync: Password validation failed: {errorMsg}");
                    SetError(errorMsg);
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("CreateUserAsync: Password validation passed, creating user...");
                // Create user
                var newUser = await _localUserService.CreateLocalUserAsync(
                    Username, 
                    Password, 
                    DisplayName, 
                    Email, 
                    Role
                );

                if (newUser == null)
                {
                    System.Diagnostics.Debug.WriteLine("CreateUserAsync: CreateLocalUserAsync returned null");
                    SetError("Не удалось создать пользователя");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"CreateUserAsync: User created successfully with ID: {newUser.Id}");

                // Set additional properties if needed
                if (!IsActive)
                {
                    System.Diagnostics.Debug.WriteLine("CreateUserAsync: Deactivating user...");
                    await _localUserService.DeactivateLocalUserAsync(newUser.Id, _currentUserId);
                }

                System.Diagnostics.Debug.WriteLine("CreateUserAsync: Logging audit event...");
                await _auditService.LogEventAsync(
                    Username,
                    "UserCreated",
                    $"Создан локальный пользователь: {Username}",
                    true
                );

                System.Diagnostics.Debug.WriteLine("CreateUserAsync: Completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateUserAsync: Exception caught: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"CreateUserAsync: Exception StackTrace: {ex.StackTrace}");
                SetError($"Ошибка при создании пользователя: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> UpdateUserAsync()
        {
            if (_originalUser == null) return false;

            // Update basic info
            _originalUser.DisplayName = DisplayName;
            _originalUser.Email = Email;
            _originalUser.Role = Role;
            
            var updatedUser = await _localUserService.UpdateLocalUserAsync(_originalUser);

            if (updatedUser == null)
            {
                SetError("Не удалось обновить пользователя");
                return false;
            }

            // Update active status if changed
            if (IsActive != _originalUser.IsActive)
            {
                if (IsActive)
                {
                    await _localUserService.ActivateLocalUserAsync(_originalUser.Id, _currentUserId);
                }
                else
                {
                    await _localUserService.DeactivateLocalUserAsync(_originalUser.Id, _currentUserId);
                }
            }

            // Update lock status if changed
            if (IsLocked != _originalUser.IsLocked)
            {
                if (IsLocked)
                {
                    await _localUserService.LockLocalUserAsync(_originalUser.Id, TimeSpan.FromMinutes(15), "Заблокирован администратором");
                }
                else
                {
                    await _localUserService.UnlockLocalUserAsync(_originalUser.Id, _currentUserId);
                }
            }

            await _auditService.LogEventAsync(
                Username,
                "UserUpdated",
                $"Обновлен локальный пользователь: {Username}",
                true
            );
            
            return true;
        }

        private async Task ResetPasswordAsync()
        {
            if (_originalUser == null) return;

            try
            {
                SetLoading(true, "Сброс пароля...");
                ClearError();

                // Generate new temporary password
                var tempPassword = GenerateTemporaryPassword();
                var resetResult = await _localUserService.ResetLocalUserPasswordAsync(_originalUser.Id, tempPassword, _currentUserId);

                if (!resetResult)
                {
                    SetError("Не удалось сбросить пароль");
                    return;
                }

                // Show new password to admin with copy functionality
                var passwordDialog = new WindowsLauncher.UI.Views.PasswordDisplayDialog(tempPassword);
                passwordDialog.Owner = System.Windows.Application.Current.MainWindow;
                passwordDialog.ShowDialog();

                RequirePasswordChange = true;
            }
            catch (Exception ex)
            {
                SetError($"Ошибка при сбросе пароля: {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task ResetFailedAttemptsAsync()
        {
            if (_originalUser == null) return;

            try
            {
                SetLoading(true, "Сброс неудачных попыток...");
                ClearError();

                // Обновляем пользователя, сбрасывая неудачные попытки
                _originalUser.FailedLoginAttempts = 0;
                _originalUser.IsLocked = false;
                _originalUser.LockoutEnd = null;
                
                var updatedUser = await _localUserService.UpdateLocalUserAsync(_originalUser);
                
                if (updatedUser == null)
                {
                    SetError("Не удалось сбросить неудачные попытки");
                    return;
                }

                FailedLoginAttempts = 0;
                IsLocked = false;
            }
            catch (Exception ex)
            {
                SetError($"Ошибка при сбросе неудачных попыток: {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private string GenerateTemporaryPassword()
        {
            var random = new Random();
            
            // Определяем наборы символов для обеспечения требований безопасности
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "!@#$%^&*()_+-=[]{}|;:,.<>?";
            
            // Начинаем с пустого пароля
            var password = new List<char>();
            
            // Гарантируем наличие хотя бы одного символа каждого типа
            password.Add(uppercase[random.Next(uppercase.Length)]);
            password.Add(lowercase[random.Next(lowercase.Length)]);
            password.Add(digits[random.Next(digits.Length)]);
            password.Add(special[random.Next(special.Length)]);
            
            // Создаем объединенный набор символов
            const string allChars = uppercase + lowercase + digits + special;
            
            // Добавляем оставшиеся символы до длины 12
            for (int i = 4; i < 12; i++)
            {
                password.Add(allChars[random.Next(allChars.Length)]);
            }
            
            // Перемешиваем символы для случайного порядка
            for (int i = password.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = password[i];
                password[i] = password[j];
                password[j] = temp;
            }
            
            return new string(password.ToArray());
        }

        private void SetLoading(bool isLoading, string message = "")
        {
            IsLoading = isLoading;
            LoadingMessage = message;
        }

        private void SetError(string message)
        {
            HasError = true;
            ErrorMessage = message;
        }

        private void ClearError()
        {
            HasError = false;
            ErrorMessage = string.Empty;
        }

        private void OnDialogClosed(bool result)
        {
            DialogClosed?.Invoke(this, result);
        }

        private bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Элемент для выбора роли пользователя
    /// </summary>
    public class RoleItem
    {
        public UserRole Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}