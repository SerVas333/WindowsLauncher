using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для работы с Active Directory через LDAP и System.DirectoryServices
    /// </summary>
    public class ActiveDirectoryService : IActiveDirectoryService
    {
        private readonly ActiveDirectoryConfiguration _config;
        private readonly ILogger<ActiveDirectoryService> _logger;

        public ActiveDirectoryService(
            IOptions<ActiveDirectoryConfiguration> config,
            ILogger<ActiveDirectoryService> logger)
        {
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Получение пользователя из AD по имени
        /// </summary>
        public async Task<User> GetUserAsync(string username, string domain = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            domain ??= _config.Domain;

            _logger.LogDebug("Getting user {Username} from domain {Domain}", username, domain);

            try
            {
                // Пробуем сначала через PrincipalContext (для доменных компьютеров)
                var user = await GetUserViaPrincipalContextAsync(username, domain);
                if (user != null)
                    return user;

                // Если не получилось, пробуем через LDAP
                return await GetUserViaLdapAsync(username, domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user {Username} from domain {Domain}", username, domain);
                throw;
            }
        }

        /// <summary>
        /// Получение групп пользователя
        /// </summary>
        public async Task<string[]> GetUserGroupsAsync(string username, string domain = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            domain ??= _config.Domain;

            _logger.LogDebug("Getting groups for user {Username} from domain {Domain}", username, domain);

            try
            {
                // Пробуем сначала через PrincipalContext
                var groups = await GetGroupsViaPrincipalContextAsync(username, domain);
                if (groups?.Length > 0)
                    return groups;

                // Если не получилось, пробуем через LDAP
                return await GetGroupsViaLdapAsync(username, domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get groups for user {Username} from domain {Domain}", username, domain);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Проверка учетных данных в AD
        /// </summary>
        public async Task<bool> ValidateCredentialsAsync(string username, string password, string domain = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            domain ??= _config.Domain;

            _logger.LogDebug("Validating credentials for user {Username} in domain {Domain}", username, domain);

            try
            {
                // Пробуем сначала через PrincipalContext
                var isValid = await ValidateViaPrincipalContextAsync(username, password, domain);
                if (isValid)
                    return true;

                // Если не получилось, пробуем через LDAP
                return await ValidateViaLdapAsync(username, password, domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate credentials for user {Username} in domain {Domain}", username, domain);
                return false;
            }
        }

        /// <summary>
        /// Поиск пользователей в AD
        /// </summary>
        public async Task<User[]> SearchUsersAsync(string searchTerm, string domain = null)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Array.Empty<User>();

            domain ??= _config.Domain;

            _logger.LogDebug("Searching users with term '{SearchTerm}' in domain {Domain}", searchTerm, domain);

            try
            {
                return await SearchUsersViaLdapAsync(searchTerm, domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search users with term '{SearchTerm}' in domain {Domain}", searchTerm, domain);
                return Array.Empty<User>();
            }
        }

        /// <summary>
        /// Проверка подключения к серверу AD
        /// </summary>
        public async Task<bool> TestConnectionAsync(string server, int port = 389)
        {
            try
            {
                _logger.LogDebug("Testing connection to {Server}:{Port}", server, port);

                using var connection = new LdapConnection(new LdapDirectoryIdentifier(server, port));
                connection.SessionOptions.ProtocolVersion = 3;
                connection.Timeout = TimeSpan.FromSeconds(10);

                if (_config.UseTLS)
                {
                    connection.SessionOptions.SecureSocketLayer = true;
                }

                // Пробуем анонимное подключение для проверки доступности
                await Task.Run(() => connection.Bind());

                _logger.LogDebug("Connection test successful for {Server}:{Port}", server, port);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Connection test failed for {Server}:{Port}: {Error}", server, port, ex.Message);
                return false;
            }
        }

        #region Principal Context Methods (для доменных компьютеров)

        /// <summary>
        /// Получение пользователя через PrincipalContext
        /// </summary>
        private async Task<User> GetUserViaPrincipalContextAsync(string username, string domain)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var context = CreatePrincipalContext(domain);
                    using var userPrincipal = UserPrincipal.FindByIdentity(context, username);

                    if (userPrincipal == null)
                        return null;

                    return MapUserPrincipalToUser(userPrincipal);
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug("PrincipalContext method failed for user {Username}: {Error}", username, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Получение групп через PrincipalContext
        /// </summary>
        private async Task<string[]> GetGroupsViaPrincipalContextAsync(string username, string domain)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var context = CreatePrincipalContext(domain);
                    using var userPrincipal = UserPrincipal.FindByIdentity(context, username);

                    if (userPrincipal == null)
                        return Array.Empty<string>();

                    var groups = userPrincipal.GetAuthorizationGroups()
                        .OfType<GroupPrincipal>()
                        .Select(g => g.Name)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToArray();

                    return groups;
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug("PrincipalContext groups method failed for user {Username}: {Error}", username, ex.Message);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Проверка учетных данных через PrincipalContext
        /// </summary>
        private async Task<bool> ValidateViaPrincipalContextAsync(string username, string password, string domain)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var context = CreatePrincipalContext(domain);
                    return context.ValidateCredentials(username, password);
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug("PrincipalContext validation failed for user {Username}: {Error}", username, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Создание PrincipalContext
        /// </summary>
        private PrincipalContext CreatePrincipalContext(string domain)
        {
            if (!string.IsNullOrEmpty(_config.ServiceUser) && !string.IsNullOrEmpty(_config.ServicePassword))
            {
                return new PrincipalContext(
                    ContextType.Domain,
                    domain,
                    _config.ServiceUser,
                    _config.ServicePassword
                );
            }
            else
            {
                return new PrincipalContext(ContextType.Domain, domain);
            }
        }

        #endregion

        #region LDAP Methods (для недоменных компьютеров)

        /// <summary>
        /// Получение пользователя через LDAP
        /// </summary>
        private async Task<User> GetUserViaLdapAsync(string username, string domain)
        {
            try
            {
                using var connection = CreateLdapConnection();

                var searchBase = GetDomainDistinguishedName(domain);
                var filter = $"(&(objectClass=user)(sAMAccountName={username}))";
                var attributes = new[] { "sAMAccountName", "displayName", "mail", "memberOf", "userAccountControl" };

                var searchRequest = new SearchRequest(searchBase, filter, SearchScope.Subtree, attributes);

                var response = await Task.Run(() => (SearchResponse)connection.SendRequest(searchRequest));

                if (response.Entries.Count == 0)
                    return null;

                var entry = response.Entries[0];
                return MapLdapEntryToUser(entry);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("LDAP user lookup failed for {Username}: {Error}", username, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Получение групп через LDAP
        /// </summary>
        private async Task<string[]> GetGroupsViaLdapAsync(string username, string domain)
        {
            try
            {
                using var connection = CreateLdapConnection();

                var searchBase = GetDomainDistinguishedName(domain);
                var filter = $"(&(objectClass=user)(sAMAccountName={username}))";
                var attributes = new[] { "memberOf" };

                var searchRequest = new SearchRequest(searchBase, filter, SearchScope.Subtree, attributes);

                var response = await Task.Run(() => (SearchResponse)connection.SendRequest(searchRequest));

                if (response.Entries.Count == 0)
                    return Array.Empty<string>();

                var entry = response.Entries[0];
                var memberOf = entry.Attributes["memberOf"];

                if (memberOf == null)
                    return Array.Empty<string>();

                var groups = new List<string>();
                for (int i = 0; i < memberOf.Count; i++)
                {
                    var dn = (string)memberOf[i];
                    var groupName = ExtractCnFromDn(dn);
                    if (!string.IsNullOrEmpty(groupName))
                        groups.Add(groupName);
                }

                return groups.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogDebug("LDAP groups lookup failed for {Username}: {Error}", username, ex.Message);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Проверка учетных данных через LDAP
        /// </summary>
        private async Task<bool> ValidateViaLdapAsync(string username, string password, string domain)
        {
            try
            {
                var server = _config.LdapServer ?? domain;
                using var connection = new LdapConnection(new LdapDirectoryIdentifier(server, _config.Port));

                connection.SessionOptions.ProtocolVersion = 3;
                connection.AuthType = AuthType.Basic;

                if (_config.UseTLS)
                {
                    connection.SessionOptions.SecureSocketLayer = true;
                }

                var userDn = $"{username}@{domain}";
                var credentials = new NetworkCredential(userDn, password);

                await Task.Run(() => connection.Bind(credentials));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("LDAP credential validation failed for {Username}: {Error}", username, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Поиск пользователей через LDAP
        /// </summary>
        private async Task<User[]> SearchUsersViaLdapAsync(string searchTerm, string domain)
        {
            try
            {
                using var connection = CreateLdapConnection();

                var searchBase = GetDomainDistinguishedName(domain);
                var filter = $"(&(objectClass=user)(|(sAMAccountName=*{searchTerm}*)(displayName=*{searchTerm}*)(mail=*{searchTerm}*)))";
                var attributes = new[] { "sAMAccountName", "displayName", "mail", "userAccountControl" };

                var searchRequest = new SearchRequest(searchBase, filter, SearchScope.Subtree, attributes);
                searchRequest.SizeLimit = 100; // Ограничиваем результаты поиска

                var response = await Task.Run(() => (SearchResponse)connection.SendRequest(searchRequest));

                var users = new List<User>();
                foreach (SearchResultEntry entry in response.Entries)
                {
                    var user = MapLdapEntryToUser(entry);
                    if (user != null)
                        users.Add(user);
                }

                return users.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogDebug("LDAP user search failed for term '{SearchTerm}': {Error}", searchTerm, ex.Message);
                return Array.Empty<User>();
            }
        }

        /// <summary>
        /// Создание LDAP подключения
        /// </summary>
        private LdapConnection CreateLdapConnection()
        {
            var server = _config.LdapServer ?? _config.Domain;
            var connection = new LdapConnection(new LdapDirectoryIdentifier(server, _config.Port));

            connection.SessionOptions.ProtocolVersion = 3;
            connection.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

            if (_config.UseTLS)
            {
                connection.SessionOptions.SecureSocketLayer = true;
            }

            // Аутентификация сервисным аккаунтом, если настроен
            if (!string.IsNullOrEmpty(_config.ServiceUser) && !string.IsNullOrEmpty(_config.ServicePassword))
            {
                connection.AuthType = AuthType.Basic;
                var credentials = new NetworkCredential(_config.ServiceUser, _config.ServicePassword);
                connection.Bind(credentials);
            }

            return connection;
        }

        #endregion

        #region Mapping Methods

        /// <summary>
        /// Преобразование UserPrincipal в User
        /// </summary>
        private User MapUserPrincipalToUser(UserPrincipal userPrincipal)
        {
            var groups = userPrincipal.GetAuthorizationGroups()
                .OfType<GroupPrincipal>()
                .Select(g => g.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();

            var role = DetermineUserRole(groups);

            return new User
            {
                Id = userPrincipal.Guid ?? Guid.NewGuid(),
                Username = userPrincipal.SamAccountName,
                DisplayName = userPrincipal.DisplayName ?? userPrincipal.SamAccountName,
                Email = userPrincipal.EmailAddress ?? $"{userPrincipal.SamAccountName}@{_config.Domain}",
                Role = role,
                IsActive = userPrincipal.Enabled ?? true,
                LastLoginDate = userPrincipal.LastLogon,
                CreatedDate = userPrincipal.LastPasswordSet ?? DateTime.Now,
                Groups = groups
            };
        }

        /// <summary>
        /// Преобразование LDAP записи в User
        /// </summary>
        private User MapLdapEntryToUser(SearchResultEntry entry)
        {
            var username = GetAttributeValue(entry, "sAMAccountName");
            if (string.IsNullOrEmpty(username))
                return null;

            var displayName = GetAttributeValue(entry, "displayName") ?? username;
            var email = GetAttributeValue(entry, "mail") ?? $"{username}@{_config.Domain}";

            // Проверяем, активен ли аккаунт
            var userAccountControl = GetAttributeValue(entry, "userAccountControl");
            var isActive = true;
            if (int.TryParse(userAccountControl, out var uac))
            {
                // Бит 2 (0x0002) означает, что аккаунт отключен
                isActive = (uac & 0x0002) == 0;
            }

            // Получаем группы из memberOf
            var memberOf = entry.Attributes["memberOf"];
            var groups = new List<string>();
            if (memberOf != null)
            {
                for (int i = 0; i < memberOf.Count; i++)
                {
                    var dn = (string)memberOf[i];
                    var groupName = ExtractCnFromDn(dn);
                    if (!string.IsNullOrEmpty(groupName))
                        groups.Add(groupName);
                }
            }

            var role = DetermineUserRole(groups.ToArray());

            return new User
            {
                Id = Guid.NewGuid(), // LDAP может не иметь GUID, генерируем новый
                Username = username,
                DisplayName = displayName,
                Email = email,
                Role = role,
                IsActive = isActive,
                CreatedDate = DateTime.Now,
                Groups = groups.ToArray()
            };
        }

        /// <summary>
        /// Получение значения атрибута из LDAP записи
        /// </summary>
        private static string GetAttributeValue(SearchResultEntry entry, string attributeName)
        {
            if (entry.Attributes.TryGetValue(attributeName, out var attribute) && attribute.Count > 0)
            {
                return attribute[0]?.ToString();
            }
            return null;
        }

        /// <summary>
        /// Извлечение CN из Distinguished Name
        /// </summary>
        private static string ExtractCnFromDn(string dn)
        {
            if (string.IsNullOrEmpty(dn))
                return null;

            var cnPrefix = "CN=";
            var startIndex = dn.IndexOf(cnPrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
                return null;

            startIndex += cnPrefix.Length;
            var endIndex = dn.IndexOf(',', startIndex);

            return endIndex > startIndex
                ? dn.Substring(startIndex, endIndex - startIndex)
                : dn.Substring(startIndex);
        }

        /// <summary>
        /// Определение роли пользователя на основе групп AD
        /// </summary>
        private UserRole DetermineUserRole(string[] groups)
        {
            if (groups == null || groups.Length == 0)
                return UserRole.Standard;

            // Проверяем группы администраторов
            var adminGroups = new[] { "LauncherAdmins", "Domain Admins", "Administrators" };
            if (groups.Any(g => adminGroups.Contains(g, StringComparer.OrdinalIgnoreCase)))
                return UserRole.Administrator;

            // Проверяем группы продвинутых пользователей
            var powerUserGroups = new[] { "LauncherPowerUsers", "Power Users" };
            if (groups.Any(g => powerUserGroups.Contains(g, StringComparer.OrdinalIgnoreCase)))
                return UserRole.PowerUser;

            return UserRole.Standard;
        }

        /// <summary>
        /// Получение Distinguished Name домена
        /// </summary>
        private static string GetDomainDistinguishedName(string domain)
        {
            if (string.IsNullOrEmpty(domain))
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));

            var parts = domain.Split('.');
            return string.Join(",", parts.Select(part => $"DC={part}"));
        }

        #endregion
    }
}