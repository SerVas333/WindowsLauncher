namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Конфигурация локальных пользователей
    /// </summary>
    public class LocalUserConfiguration
    {
        /// <summary>
        /// Разрешить регистрацию пользователей самостоятельно
        /// </summary>
        public bool AllowUserRegistration { get; set; } = false;

        /// <summary>
        /// Требовать сильные пароли
        /// </summary>
        public bool RequireStrongPasswords { get; set; } = true;

        /// <summary>
        /// Максимальное количество попыток входа
        /// </summary>
        public int MaxLoginAttempts { get; set; } = 5;

        /// <summary>
        /// Продолжительность блокировки в минутах
        /// </summary>
        public int LockoutDurationMinutes { get; set; } = 15;

        /// <summary>
        /// Срок действия пароля в днях (0 = без ограничений)
        /// </summary>
        public int PasswordExpiryDays { get; set; } = 0;

        /// <summary>
        /// Количество паролей для запрета повторного использования
        /// </summary>
        public int PasswordHistorySize { get; set; } = 5;

        /// <summary>
        /// Политика паролей
        /// </summary>
        public PasswordPolicyConfiguration PasswordPolicy { get; set; } = new();

        /// <summary>
        /// Требовать подтверждение email при создании пользователя
        /// </summary>
        public bool RequireEmailConfirmation { get; set; } = false;

        /// <summary>
        /// Автоматически активировать новых пользователей
        /// </summary>
        public bool AutoActivateNewUsers { get; set; } = true;

        /// <summary>
        /// Разрешить изменение собственного пароля
        /// </summary>
        public bool AllowPasswordChange { get; set; } = true;

        /// <summary>
        /// Разрешить изменение собственного профиля
        /// </summary>
        public bool AllowProfileChange { get; set; } = true;
    }

    /// <summary>
    /// Политика паролей
    /// </summary>
    public class PasswordPolicyConfiguration
    {
        /// <summary>
        /// Минимальная длина пароля
        /// </summary>
        public int MinLength { get; set; } = 8;

        /// <summary>
        /// Максимальная длина пароля
        /// </summary>
        public int MaxLength { get; set; } = 128;

        /// <summary>
        /// Требовать цифры
        /// </summary>
        public bool RequireDigits { get; set; } = true;

        /// <summary>
        /// Требовать строчные буквы
        /// </summary>
        public bool RequireLowercase { get; set; } = true;

        /// <summary>
        /// Требовать заглавные буквы
        /// </summary>
        public bool RequireUppercase { get; set; } = true;

        /// <summary>
        /// Требовать специальные символы
        /// </summary>
        public bool RequireSpecialChars { get; set; } = true;

        /// <summary>
        /// Список разрешенных специальных символов
        /// </summary>
        public string AllowedSpecialChars { get; set; } = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        /// <summary>
        /// Запретить общие пароли
        /// </summary>
        public bool ProhibitCommonPasswords { get; set; } = true;

        /// <summary>
        /// Запретить использование имени пользователя в пароле
        /// </summary>
        public bool ProhibitUsernameInPassword { get; set; } = true;

        /// <summary>
        /// Запретить последовательности символов (123, abc, qwerty)
        /// </summary>
        public bool ProhibitSequentialChars { get; set; } = true;

        /// <summary>
        /// Запретить повторяющиеся символы (aaa, 111)
        /// </summary>
        public bool ProhibitRepeatingChars { get; set; } = true;

        /// <summary>
        /// Максимальное количество повторяющихся символов подряд
        /// </summary>
        public int MaxRepeatingChars { get; set; } = 2;

        /// <summary>
        /// Список запрещенных паролей
        /// </summary>
        public List<string> ProhibitedPasswords { get; set; } = new()
        {
            "password", "123456", "qwerty", "admin", "user", "guest",
            "пароль", "администратор", "пользователь", "гость"
        };
    }

    /// <summary>
    /// Конфигурация аудита локальных пользователей
    /// </summary>
    public class LocalUserAuditConfiguration
    {
        /// <summary>
        /// Логировать все действия с локальными пользователями
        /// </summary>
        public bool LogAllActions { get; set; } = true;

        /// <summary>
        /// Логировать попытки входа
        /// </summary>
        public bool LogLoginAttempts { get; set; } = true;

        /// <summary>
        /// Логировать изменения паролей
        /// </summary>
        public bool LogPasswordChanges { get; set; } = true;

        /// <summary>
        /// Логировать изменения ролей
        /// </summary>
        public bool LogRoleChanges { get; set; } = true;

        /// <summary>
        /// Логировать блокировки аккаунтов
        /// </summary>
        public bool LogAccountLocks { get; set; } = true;

        /// <summary>
        /// Сохранять подробную информацию о клиенте
        /// </summary>
        public bool LogClientDetails { get; set; } = true;

        /// <summary>
        /// Период хранения логов в днях
        /// </summary>
        public int LogRetentionDays { get; set; } = 90;
    }
}