using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Services
{
    /// <summary>
    /// Контекст для выполнения миграций
    /// </summary>
    public class DatabaseMigrationContext : IDatabaseMigrationContext
    {
        private readonly LauncherDbContext _context;
        
        public DatabaseMigrationContext(LauncherDbContext context, DatabaseType databaseType)
        {
            _context = context;
            DatabaseType = databaseType;
        }
        
        public DatabaseType DatabaseType { get; }
        
        public async Task ExecuteSqlAsync(string sql)
        {
            await _context.Database.ExecuteSqlRawAsync(sql);
        }
        
        public async Task<T> ExecuteScalarAsync<T>(string sql)
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            if (command.Connection?.State != System.Data.ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }
            
            var result = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result, typeof(T));
        }
        
        public async Task<bool> TableExistsAsync(string tableName)
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            
            switch (DatabaseType)
            {
                case DatabaseType.SQLite:
                    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = @tableName";
                    var param1 = command.CreateParameter();
                    param1.ParameterName = "@tableName";
                    param1.Value = tableName;
                    command.Parameters.Add(param1);
                    break;
                    
                case DatabaseType.Firebird:
                    command.CommandText = "SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = @tableName";
                    var param2 = command.CreateParameter();
                    param2.ParameterName = "@tableName";
                    param2.Value = tableName.ToUpper();
                    command.Parameters.Add(param2);
                    break;
                    
                default:
                    throw new NotSupportedException($"Database type {DatabaseType} not supported");
            }
            
            if (command.Connection?.State != System.Data.ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }
            
            var result = await command.ExecuteScalarAsync();
            var count = Convert.ToInt32(result);
            return count > 0;
        }
        
        public async Task<bool> ColumnExistsAsync(string tableName, string columnName)
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            
            switch (DatabaseType)
            {
                case DatabaseType.SQLite:
                    // SQLite не поддерживает параметры в pragma_table_info, поэтому используем безопасную подстановку
                    command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName.Replace("'", "''")}') WHERE name = @columnName";
                    var param2 = command.CreateParameter();
                    param2.ParameterName = "@columnName";
                    param2.Value = columnName;
                    command.Parameters.Add(param2);
                    break;
                    
                case DatabaseType.Firebird:
                    command.CommandText = "SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = @tableName AND RDB$FIELD_NAME = @columnName";
                    var param3 = command.CreateParameter();
                    param3.ParameterName = "@tableName";
                    param3.Value = tableName.ToUpper();
                    command.Parameters.Add(param3);
                    var param4 = command.CreateParameter();
                    param4.ParameterName = "@columnName";
                    param4.Value = columnName.ToUpper();
                    command.Parameters.Add(param4);
                    break;
                    
                default:
                    throw new NotSupportedException($"Database type {DatabaseType} not supported");
            }
            
            if (command.Connection?.State != System.Data.ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }
            
            var result = await command.ExecuteScalarAsync();
            var count = Convert.ToInt32(result);
            return count > 0;
        }
        
        public async Task<bool> IndexExistsAsync(string indexName)
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            
            switch (DatabaseType)
            {
                case DatabaseType.SQLite:
                    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name = @indexName";
                    var param1 = command.CreateParameter();
                    param1.ParameterName = "@indexName";
                    param1.Value = indexName;
                    command.Parameters.Add(param1);
                    break;
                    
                case DatabaseType.Firebird:
                    command.CommandText = "SELECT COUNT(*) FROM RDB$INDICES WHERE RDB$INDEX_NAME = @indexName";
                    var param2 = command.CreateParameter();
                    param2.ParameterName = "@indexName";
                    param2.Value = indexName.ToUpper();
                    command.Parameters.Add(param2);
                    break;
                    
                default:
                    throw new NotSupportedException($"Database type {DatabaseType} not supported");
            }
            
            if (command.Connection?.State != System.Data.ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }
            
            var result = await command.ExecuteScalarAsync();
            var count = Convert.ToInt32(result);
            return count > 0;
        }
    }
}