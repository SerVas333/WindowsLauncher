using System;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Информация о базе данных
    /// </summary>
    public class DatabaseInfo
    {
        /// <summary>
        /// Существует ли база данных
        /// </summary>
        public bool Exists { get; set; }
        
        /// <summary>
        /// Версия сервера базы данных
        /// </summary>
        public string? Version { get; set; }
        
        /// <summary>
        /// Размер базы данных (отформатированная строка)
        /// </summary>
        public string? Size { get; set; }
        
        /// <summary>
        /// Количество таблиц
        /// </summary>
        public int TableCount { get; set; }
        
        /// <summary>
        /// Дата создания (для файловых БД)
        /// </summary>
        public DateTime? Created { get; set; }
        
        /// <summary>
        /// Дата последнего изменения (для файловых БД)
        /// </summary>
        public DateTime? LastModified { get; set; }
        
        /// <summary>
        /// Сообщение об ошибке (если есть)
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Есть ли ошибки
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    }
}