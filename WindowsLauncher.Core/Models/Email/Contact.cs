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
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string? Phone { get; set; }
        
        [MaxLength(100)]
        public string? Company { get; set; }
        
        [MaxLength(50)]
        public string? Department { get; set; }
        
        [MaxLength(50)]
        public string? Group { get; set; }
        
        [MaxLength(500)]
        public string? Notes { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime? UpdatedAt { get; set; }
        
        public string? CreatedBy { get; set; }
        
        /// <summary>
        /// Полное имя (Имя Фамилия)
        /// </summary>
        public string FullName => $"{FirstName} {LastName}".Trim();
        
        /// <summary>
        /// Отображаемое имя для UI (Полное имя <email@domain.com>)
        /// </summary>
        public string DisplayName => string.IsNullOrEmpty(FullName) ? Email : $"{FullName} <{Email}>";
        
        /// <summary>
        /// Инициалы для аватара
        /// </summary>
        public string Initials 
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
        
        /// <summary>
        /// Имеет ли контакт номер телефона
        /// </summary>
        public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
        
        /// <summary>
        /// Имеет ли контакт отдел
        /// </summary>
        public bool HasDepartment => !string.IsNullOrWhiteSpace(Department);
    }
}