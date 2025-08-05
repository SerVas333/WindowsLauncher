using System;
using System.ComponentModel.DataAnnotations;

namespace WindowsLauncher.Core.Models.Email
{
    /// <summary>
    /// Контакт в локальной адресной книге
    /// </summary>
    public class Contact
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? Company { get; set; }
        
        [MaxLength(50)]
        public string? Group { get; set; }
        
        [MaxLength(500)]
        public string? Notes { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime? UpdatedAt { get; set; }
        
        public string? CreatedBy { get; set; }
        
        /// <summary>
        /// Отображаемое имя для UI (Имя <email@domain.com>)
        /// </summary>
        public string DisplayName => string.IsNullOrEmpty(Name) ? Email : $"{Name} <{Email}>";
        
        /// <summary>
        /// Инициалы для аватара
        /// </summary>
        public string Initials 
        {
            get
            {
                if (string.IsNullOrEmpty(Name))
                    return Email.Length > 0 ? Email[0].ToString().ToUpper() : "?";
                
                var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                    return parts[0][0].ToString().ToUpper();
                
                return $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpper();
            }
        }
    }
}