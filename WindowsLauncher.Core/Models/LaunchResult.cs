using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;

// WindowsLauncher.Core/Models/LaunchResult.cs
namespace WindowsLauncher.Core.Models
{
    public class LaunchResult
    {
        public bool IsSuccess { get; set; }
        public int ProcessId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime LaunchTime { get; set; } = DateTime.Now;

        public static LaunchResult Success(int processId)
        {
            return new LaunchResult
            {
                IsSuccess = true,
                ProcessId = processId
            };
        }

        public static LaunchResult Failure(string errorMessage)
        {
            return new LaunchResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
}