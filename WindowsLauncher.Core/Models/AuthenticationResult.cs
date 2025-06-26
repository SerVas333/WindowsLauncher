using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models
{
    public class AuthenticationResult
    {
        public bool IsSuccess { get; set; }
        public User? User { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime AuthenticationTime { get; set; } = DateTime.Now;

        public static AuthenticationResult Success(User user)
        {
            return new AuthenticationResult
            {
                IsSuccess = true,
                User = user
            };
        }

        public static AuthenticationResult Failure(string errorMessage)
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
}