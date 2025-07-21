using System;
using System.Security.Cryptography;

class TestPassword
{
    static void Main()
    {
        // Данные из appsettings.json
        string storedHash = "K2dhYGw+6/woWybYU601B4CS4mJQjB8f0L44Q06gI3o=";
        string storedSalt = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        
        // Тестируем разные пароли
        string[] testPasswords = { "vfc11Nth!", "admin123", "serviceadmin", "123456", "password" };
        
        Console.WriteLine("Testing password hashes:");
        Console.WriteLine($"Stored Hash: {storedHash}");
        Console.WriteLine($"Stored Salt: {storedSalt}");
        Console.WriteLine();
        
        foreach (var password in testPasswords)
        {
            bool isValid = VerifyPassword(password, storedHash, storedSalt);
            Console.WriteLine($"Password '{password}': {(isValid ? "VALID" : "INVALID")}");
            
            // Показываем какой хэш мы получили
            var saltBytes = Convert.FromBase64String(storedSalt);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100000, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(32);
            var computedHash = Convert.ToBase64String(hashBytes);
            Console.WriteLine($"  Computed Hash: {computedHash}");
            Console.WriteLine();
        }
    }
    
    static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        try
        {
            var saltBytes = Convert.FromBase64String(storedSalt);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100000, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(32);
            var computedHash = Convert.ToBase64String(hashBytes);
            return computedHash == storedHash;
        }
        catch
        {
            return false;
        }
    }
}