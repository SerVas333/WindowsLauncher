using System;
using System.Security.Cryptography;

public class PasswordHasher
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PasswordHasher <password>");
            Console.WriteLine("Example: PasswordHasher \"vfc11Nrh!\"");
            return;
        }

        string password = args[0];
        
        // Используем ту же соль что и в коде (32 байта нулей)
        var saltBytes = new byte[32]; // все нули
        var salt = Convert.ToBase64String(saltBytes);

        // Генерируем хэш по тому же алгоритму что в коде
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100000, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(32);
        var passwordHash = Convert.ToBase64String(hashBytes);

        Console.WriteLine($"Password: {password}");
        Console.WriteLine($"Salt: {salt}");
        Console.WriteLine($"Hash: {passwordHash}");
        Console.WriteLine();
        Console.WriteLine("Update your appsettings.json or auth-config.json with:");
        Console.WriteLine($"\"passwordHash\": \"{passwordHash}\",");
        Console.WriteLine($"\"salt\": \"{salt}\",");
    }
}