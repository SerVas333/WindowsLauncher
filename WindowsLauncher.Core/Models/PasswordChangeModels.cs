using System;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Результат операции смены пароля
    /// </summary>
    public class PasswordChangeResult
    {
        /// <summary>
        /// Успешность операции
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Сообщение об успехе
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Сообщение об ошибке
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Дополнительная информация
        /// </summary>
        public object? AdditionalData { get; set; }

        /// <summary>
        /// Создать успешный результат
        /// </summary>
        public static PasswordChangeResult Success(string message = "Пароль успешно изменен")
        {
            return new PasswordChangeResult { IsSuccess = true, Message = message };
        }

        /// <summary>
        /// Создать результат с ошибкой
        /// </summary>
        public static PasswordChangeResult Failure(string errorMessage)
        {
            return new PasswordChangeResult { IsSuccess = false, ErrorMessage = errorMessage };
        }
    }

    /// <summary>
    /// Информация о пароле пользователя
    /// </summary>
    public class PasswordInfo
    {
        /// <summary>
        /// ID пользователя
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Имя пользователя
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Дата последней смены пароля
        /// </summary>
        public DateTime? LastPasswordChange { get; set; }

        /// <summary>
        /// Истек ли срок действия пароля
        /// </summary>
        public bool IsExpired { get; set; }

        /// <summary>
        /// Количество дней до истечения срока действия (null если не ограничено)
        /// </summary>
        public int? DaysUntilExpiry { get; set; }

        /// <summary>
        /// Требуется ли смена пароля
        /// </summary>
        public bool RequiresChange => IsExpired || (DaysUntilExpiry.HasValue && DaysUntilExpiry.Value <= 0);
    }

    /// <summary>
    /// Запрос на смену пароля
    /// </summary>
    public class PasswordChangeRequest
    {
        /// <summary>
        /// ID пользователя
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Текущий пароль
        /// </summary>
        public string CurrentPassword { get; set; } = string.Empty;

        /// <summary>
        /// Новый пароль
        /// </summary>
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// Подтверждение нового пароля
        /// </summary>
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>
        /// Валидация запроса
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            if (string.IsNullOrEmpty(CurrentPassword))
            {
                errorMessage = "Текущий пароль не может быть пустым";
                return false;
            }

            if (string.IsNullOrEmpty(NewPassword))
            {
                errorMessage = "Новый пароль не может быть пустым";
                return false;
            }

            if (NewPassword != ConfirmPassword)
            {
                errorMessage = "Подтверждение пароля не совпадает с новым паролем";
                return false;
            }

            if (CurrentPassword == NewPassword)
            {
                errorMessage = "Новый пароль должен отличаться от текущего";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}