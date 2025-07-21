using System;
using System.IO;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        var dbPath = @"WindowsLauncher.UI/bin/Debug/net8.0-windows/launcher.db";
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Database file not found: {dbPath}");
            return;
        }
        
        var connectionString = $"Data Source={dbPath}";
        
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            
            // Проверяем пользователя serviceadmin
            var command = new SqliteCommand("SELECT Username, DisplayName, AuthenticationType, IsLocalUser, IsServiceAccount FROM Users WHERE Username = 'serviceadmin'", connection);
            using var reader = command.ExecuteReader();
            
            if (reader.Read())
            {
                Console.WriteLine("Found serviceadmin user:");
                Console.WriteLine($"Username: {reader["Username"]}");
                Console.WriteLine($"DisplayName: {reader["DisplayName"]}");
                Console.WriteLine($"AuthenticationType: {reader["AuthenticationType"]}");
                Console.WriteLine($"IsLocalUser: {reader["IsLocalUser"]}");
                Console.WriteLine($"IsServiceAccount: {reader["IsServiceAccount"]}");
            }
            else
            {
                Console.WriteLine("serviceadmin user not found in database");
            }
            
            // Проверяем все пользователи
            var allUsersCommand = new SqliteCommand("SELECT Username, DisplayName, AuthenticationType, IsLocalUser, IsServiceAccount FROM Users", connection);
            using var allUsersReader = allUsersCommand.ExecuteReader();
            
            Console.WriteLine("\nAll users in database:");
            while (allUsersReader.Read())
            {
                Console.WriteLine($"- {allUsersReader["Username"]} ({allUsersReader["DisplayName"]}) - AuthType: {allUsersReader["AuthenticationType"]}, IsLocal: {allUsersReader["IsLocalUser"]}, IsService: {allUsersReader["IsServiceAccount"]}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}