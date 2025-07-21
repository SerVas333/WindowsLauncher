using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для работы с локальными пользователями
    /// </summary>
    public class LocalUserService : ILocalUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuditService _auditService;
        private readonly ILogger<LocalUserService> _logger;
        private readonly LocalUserConfiguration _config;
        private readonly PasswordPolicyConfiguration _passwordPolicy;

        public LocalUserService(
            IUserRepository userRepository,
            IAuditService auditService,
            ILogger<LocalUserService> logger,
            IOptions<LocalUserConfiguration> config)
        {
            _userRepository = userRepository;
            _auditService = auditService;
            _logger = logger;
            _config = config.Value;
            _passwordPolicy = _config.PasswordPolicy;
        }

        #region CRUD операции

        public async Task<User> CreateLocalUserAsync(string username, string password, string displayName = "", string email = "", UserRole role = UserRole.Standard)
        {
            _logger.LogInformation("Creating local user: {Username}", username);

            // Валидация входных данных
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            // Проверка существования пользователя
            var existingUser = await _userRepository.GetByUsernameAsync(username);
            if (existingUser != null)
                throw new InvalidOperationException($"User with username '{username}' already exists");

            // Валидация пароля
            var passwordValidation = await ValidatePasswordAsync(password);
            if (!passwordValidation.IsValid)
                throw new ArgumentException($"Password validation failed: {string.Join(", ", passwordValidation.Errors)}");

            // Создание пользователя
            var (passwordHash, salt) = HashPassword(password);
            
            var user = new User
            {
                Username = username,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
                Email = email,
                Role = role,
                AuthenticationType = AuthenticationType.LocalUsers,
                IsLocalUser = true,
                IsServiceAccount = false,
                AllowLocalLogin = true,
                PasswordHash = passwordHash,
                Salt = salt,
                IsActive = _config.AutoActivateNewUsers,
                CreatedAt = DateTime.UtcNow,
                LastPasswordChange = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();

            // Аудит
            await _auditService.LogEventAsync(
                username,
                "LocalUser.Create",
                $"Created local user: {username} with role: {role}",
                true
            );

            _logger.LogInformation("Successfully created local user: {Username} with ID: {UserId}", username, user.Id);
            return user;
        }

        public async Task<List<User>> GetLocalUsersAsync()
        {
            return await _userRepository.GetLocalUsersAsync();
        }

        public async Task<User?> GetLocalUserByUsernameAsync(string username)
        {
            var users = await _userRepository.GetLocalUsersAsync();
            return users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<User?> GetLocalUserByIdAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            // Возвращаем пользователя если он локальный или гостевой
            return (user?.IsLocalUser == true || user?.AuthenticationType == AuthenticationType.Guest) ? user : null;
        }

        public async Task<User> UpdateLocalUserAsync(User user)
        {
            if (!user.IsLocalUser && user.AuthenticationType != AuthenticationType.Guest)
                throw new InvalidOperationException("User is not a local or guest user");

            var existingUser = await _userRepository.GetByIdAsync(user.Id);
            if (existingUser == null)
                throw new InvalidOperationException("User not found");

            // Обновляем разрешенные поля
            existingUser.DisplayName = user.DisplayName;
            existingUser.Email = user.Email;
            existingUser.IsActive = user.IsActive;
            
            await _userRepository.UpdateAsync(existingUser);
            await _userRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                user.Username,
                "LocalUser.Update",
                $"Updated local user: {user.Username}",
                true
            );

            return existingUser;
        }

        public async Task<bool> DeleteLocalUserAsync(int userId)
        {
            var user = await GetLocalUserByIdAsync(userId);
            if (user == null)
                return false;

            await _userRepository.DeleteAsync(userId);
            await _userRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                user.Username,
                "LocalUser.Delete",
                $"Deleted local user: {user.Username}",
                true
            );

            _logger.LogInformation("Deleted local user: {Username} (ID: {UserId})", user.Username, userId);
            return true;
        }

        #endregion

        #region Аутентификация

        public async Task<bool> ValidateLocalUserAsync(string username, string password)
        {
            var user = await GetLocalUserByUsernameAsync(username);
            if (user == null || !user.IsActive || user.IsLocked)
                return false;

            return VerifyPassword(password, user.PasswordHash, user.Salt);
        }

        public async Task<AuthenticationResult> AuthenticateLocalUserAsync(string username, string password)
        {
            try
            {
                var user = await GetLocalUserByUsernameAsync(username);
                if (user == null)
                {
                    await _auditService.LogEventAsync(
                        username,
                        "LocalUser.LoginFailed",
                        $"Login failed - user not found: {username}",
                        false,
                        "User not found"
                    );
                    return AuthenticationResult.Failure(AuthenticationStatus.UserNotFound, "User not found");
                }

                // Проверка блокировки
                if (user.IsLocked && user.LockoutEnd > DateTime.UtcNow)
                {
                    await _auditService.LogEventAsync(
                        username,
                        "LocalUser.LoginFailed",
                        $"Login failed - account locked: {username} until {user.LockoutEnd}",
                        false,
                        "Account is locked"
                    );
                    return AuthenticationResult.Failure(AuthenticationStatus.AccountLocked, "Account is locked");
                }

                // Проверка активности
                if (!user.IsActive)
                {
                    await _auditService.LogEventAsync(
                        username,
                        "LocalUser.LoginFailed", 
                        $"Login failed - account inactive: {username}",
                        false,
                        "Account is inactive"
                    );
                    return AuthenticationResult.Failure(AuthenticationStatus.UserNotFound, "Account is inactive");
                }

                // Проверка пароля
                if (!VerifyPassword(password, user.PasswordHash, user.Salt))
                {
                    // Увеличиваем счетчик неудачных попыток
                    user.FailedLoginAttempts++;
                    
                    // Блокируем аккаунт при превышении лимита
                    if (user.FailedLoginAttempts >= _config.MaxLoginAttempts)
                    {
                        user.IsLocked = true;
                        user.LockoutEnd = DateTime.UtcNow.AddMinutes(_config.LockoutDurationMinutes);
                        
                        await _auditService.LogEventAsync(
                            username,
                            "LocalUser.AccountLocked",
                            $"Account locked due to failed login attempts: {username}, attempts: {user.FailedLoginAttempts}",
                            false,
                            "Too many failed login attempts"
                        );
                    }

                    await _userRepository.UpdateAsync(user);
                    await _userRepository.SaveChangesAsync();

                    await _auditService.LogEventAsync(
                        username,
                        "LocalUser.LoginFailed",
                        $"Login failed - invalid password: {username}, attempts: {user.FailedLoginAttempts}",
                        false,
                        "Invalid password"
                    );

                    return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, "Invalid credentials");
                }

                // Успешная аутентификация
                user.FailedLoginAttempts = 0;
                user.IsLocked = false;
                user.LockoutEnd = null;
                user.LastLoginAt = DateTime.UtcNow;
                user.LastActivityAt = DateTime.UtcNow;

                await _userRepository.UpdateAsync(user);
                await _userRepository.SaveChangesAsync();

                await _auditService.LogEventAsync(
                    username,
                    "LocalUser.LoginSuccess",
                    $"Successful login: {username} (LocalUser)",
                    true
                );

                return AuthenticationResult.Success(user, AuthenticationType.LocalUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during local user authentication for username: {Username}", username);
                return AuthenticationResult.Error("Authentication error occurred", ex);
            }
        }

        #endregion

        #region Управление паролями

        public async Task<bool> UpdateLocalUserPasswordAsync(int userId, string newPassword)
        {
            var user = await GetLocalUserByIdAsync(userId);
            if (user == null)
                return false;

            var validation = await ValidatePasswordAsync(newPassword);
            if (!validation.IsValid)
                throw new ArgumentException($"Password validation failed: {string.Join(", ", validation.Errors)}");

            var (passwordHash, salt) = HashPassword(newPassword);
            user.PasswordHash = passwordHash;
            user.Salt = salt;
            user.LastPasswordChange = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                user.Username,
                "LocalUser.PasswordChanged",
                $"Password changed for user: {user.Username}",
                true
            );

            return true;
        }

        public async Task<bool> ResetLocalUserPasswordAsync(int userId, string newPassword, int adminUserId)
        {
            if (!await IsAdministratorAsync(adminUserId))
                throw new UnauthorizedAccessException("Only administrators can reset passwords");

            var result = await UpdateLocalUserPasswordAsync(userId, newPassword);
            
            if (result)
            {
                var user = await GetLocalUserByIdAsync(userId);
                await _auditService.LogEventAsync(
                    user?.Username ?? "unknown",
                    "LocalUser.PasswordReset",
                    $"Password reset by admin for user: {user?.Username}",
                    true
                );
            }

            return result;
        }

        public Task<PasswordValidationResult> ValidatePasswordAsync(string password)
        {
            var result = new PasswordValidationResult();

            if (string.IsNullOrEmpty(password))
            {
                result.AddError("Password cannot be empty");
                return Task.FromResult(result);
            }

            // Проверка длины
            if (password.Length < _passwordPolicy.MinLength)
                result.AddError($"Password must be at least {_passwordPolicy.MinLength} characters long");

            if (password.Length > _passwordPolicy.MaxLength)
                result.AddError($"Password cannot be longer than {_passwordPolicy.MaxLength} characters");

            // Проверка требований к символам
            if (_passwordPolicy.RequireDigits && !password.Any(char.IsDigit))
                result.AddError("Password must contain at least one digit");

            if (_passwordPolicy.RequireLowercase && !password.Any(char.IsLower))
                result.AddError("Password must contain at least one lowercase letter");

            if (_passwordPolicy.RequireUppercase && !password.Any(char.IsUpper))
                result.AddError("Password must contain at least one uppercase letter");

            if (_passwordPolicy.RequireSpecialChars && !password.Any(c => _passwordPolicy.AllowedSpecialChars.Contains(c)))
                result.AddError($"Password must contain at least one special character from: {_passwordPolicy.AllowedSpecialChars}");

            // Проверка запрещенных паролей
            if (_passwordPolicy.ProhibitCommonPasswords)
            {
                var lowerPassword = password.ToLowerInvariant();
                if (_passwordPolicy.ProhibitedPasswords.Any(p => lowerPassword.Contains(p.ToLowerInvariant())))
                    result.AddError("Password contains prohibited words or patterns");
            }

            // Проверка повторяющихся символов
            if (_passwordPolicy.ProhibitRepeatingChars)
            {
                var repeatingPattern = new Regex($@"(.)\1{{{_passwordPolicy.MaxRepeatingChars},}}");
                if (repeatingPattern.IsMatch(password))
                    result.AddError($"Password cannot contain more than {_passwordPolicy.MaxRepeatingChars} repeating characters in a row");
            }

            // Проверка последовательностей
            if (_passwordPolicy.ProhibitSequentialChars)
            {
                if (ContainsSequentialChars(password))
                    result.AddError("Password cannot contain sequential characters (123, abc, qwerty, etc.)");
            }

            result.IsValid = !result.Errors.Any();
            return Task.FromResult(result);
        }

        #endregion

        #region Управление ролями

        public async Task<bool> ChangeLocalUserRoleAsync(int userId, UserRole newRole, int adminUserId)
        {
            if (!await IsAdministratorAsync(adminUserId))
                throw new UnauthorizedAccessException("Only administrators can change user roles");

            var user = await GetLocalUserByIdAsync(userId);
            if (user == null)
                return false;

            var oldRole = user.Role;
            user.Role = newRole;

            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                user.Username,
                "LocalUser.RoleChanged",
                $"Role changed for user: {user.Username} from {oldRole} to {newRole}",
                true
            );

            return true;
        }

        #endregion

        #region Управление активностью аккаунтов

        public async Task<bool> ActivateLocalUserAsync(int userId, int adminUserId)
        {
            if (!await IsAdministratorAsync(adminUserId))
                throw new UnauthorizedAccessException("Only administrators can activate users");

            var user = await GetLocalUserByIdAsync(userId);
            if (user == null)
                return false;

            user.IsActive = true;
            user.IsLocked = false;
            user.LockoutEnd = null;
            user.FailedLoginAttempts = 0;

            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                user.Username,
                "LocalUser.Activated",
                $"User activated: {user.Username}",
                true
            );

            return true;
        }

        public async Task<bool> DeactivateLocalUserAsync(int userId, int adminUserId)
        {
            if (!await IsAdministratorAsync(adminUserId))
                throw new UnauthorizedAccessException("Only administrators can deactivate users");

            var user = await GetLocalUserByIdAsync(userId);
            if (user == null)
                return false;

            user.IsActive = false;

            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                user.Username,
                "LocalUser.Deactivated",
                $"User deactivated: {user.Username}",
                true
            );

            return true;
        }

        public async Task<bool> LockLocalUserAsync(int userId, TimeSpan lockDuration, string reason)
        {
            var user = await GetLocalUserByIdAsync(userId);
            if (user == null)
                return false;

            user.IsLocked = true;
            user.LockoutEnd = DateTime.UtcNow.Add(lockDuration);

            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                user.Username,
                "LocalUser.Locked",
                $"User locked: {user.Username}. Reason: {reason}",
                true
            );

            return true;
        }

        public async Task<bool> UnlockLocalUserAsync(int userId, int adminUserId)
        {
            if (!await IsAdministratorAsync(adminUserId))
                throw new UnauthorizedAccessException("Only administrators can unlock users");

            var user = await GetLocalUserByIdAsync(userId);
            if (user == null)
                return false;

            user.IsLocked = false;
            user.LockoutEnd = null;
            user.FailedLoginAttempts = 0;

            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                user.Username,
                "LocalUser.Unlocked",
                $"User unlocked: {user.Username}",
                true
            );

            return true;
        }

        #endregion

        #region Аудит и статистика

        public async Task<UserLoginStatistics> GetLocalUserLoginStatisticsAsync(int userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // TODO: Реализовать через AuditService когда он будет готов
            var user = await GetLocalUserByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found", nameof(userId));

            return new UserLoginStatistics
            {
                UserId = userId,
                Username = user.Username,
                LastLogin = user.LastLoginAt,
                // Остальные поля будут заполнены когда будет готов AuditService
                TotalLogins = 0,
                SuccessfulLogins = 0,
                FailedLogins = 0
            };
        }

        public async Task<bool> IsAdministratorAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.Role == UserRole.Administrator;
        }

        #endregion

        #region Утилиты для работы с паролями

        private (string hash, string salt) HashPassword(string password)
        {
            // Генерируем соль
            byte[] saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            string salt = Convert.ToBase64String(saltBytes);

            // Хэшируем пароль с солью
            string hash = HashPasswordWithSalt(password, salt);

            return (hash, salt);
        }

        private string HashPasswordWithSalt(string password, string salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 100000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);
            return Convert.ToBase64String(hash);
        }

        private bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
                return false;

            string computedHash = HashPasswordWithSalt(password, storedSalt);
            return computedHash == storedHash;
        }

        private bool ContainsSequentialChars(string password)
        {
            var sequences = new[]
            {
                "0123456789", "abcdefghijklmnopqrstuvwxyz", "qwertyuiop", "asdfghjkl", "zxcvbnm",
                "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю"
            };

            foreach (var sequence in sequences)
            {
                for (int i = 0; i <= sequence.Length - 3; i++)
                {
                    var subseq = sequence.Substring(i, 3);
                    if (password.ToLowerInvariant().Contains(subseq))
                        return true;
                    
                    // Проверяем обратную последовательность
                    var reversed = new string(subseq.Reverse().ToArray());
                    if (password.ToLowerInvariant().Contains(reversed))
                        return true;
                }
            }

            return false;
        }

        #endregion
    }
}