// WindowsLauncher.Services/ActiveDirectory/ActiveDirectoryService.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services.ActiveDirectory
{
    /// <summary>
    /// Унифицированный сервис для работы с Active Directory
    /// </summary>
    public class ActiveDirectoryService : IActiveDirectoryService
    {
        private readonly IAuthenticationConfigurationService _configService;
        private readonly ILogger<ActiveDirectoryService> _logger;

        public ActiveDirectoryService(
            IAuthenticationConfigurationService configService,
            ILogger<ActiveDirectoryService> logger)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Тестировать подключение к AD серверу
        /// </summary>
        public async Task<bool> TestConnectionAsync(string server, int port)
        {
            try
            {
                _logger.LogInformation("Testing connection to {Server}:{Port}", server, port);

                // Проверяем доступность сервера через ping
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(server, 5000);

                _logger.LogInformation("Ping result for {Server}: Status={Status}, RoundtripTime={Time}ms", 
                    server, reply.Status, reply.RoundtripTime);

                if (reply.Status != IPStatus.Success)
                {
                    _logger.LogWarning("Server {Server} is not reachable via ping: {Status}", server, reply.Status);
                    return false;
                }

                _logger.LogInformation("Ping successful, now testing TCP connection to {Server}:{Port}", server, port);

                // Проверяем доступность TCP порта вместо LDAP bind
                using var tcpClient = new System.Net.Sockets.TcpClient();
                var connectTask = tcpClient.ConnectAsync(server, port);
                var timeoutTask = Task.Delay(5000); // 5 секунд таймаут

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("TCP connection to {Server}:{Port} timed out", server, port);
                    return false;
                }

                if (connectTask.IsFaulted)
                {
                    _logger.LogWarning("TCP connection to {Server}:{Port} failed: {Error}", server, port, connectTask.Exception?.GetBaseException().Message);
                    return false;
                }

                _logger.LogInformation("TCP connection to {Server}:{Port} successful", server, port);

                _logger.LogInformation("Connection test successful for {Server}:{Port}", server, port);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test failed for {Server}:{Port}: {Error}", server, port, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Аутентифицировать пользователя в AD
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            try
            {
                var config = _configService.GetConfiguration();

                // Проверяем учетные данные в зависимости от доступности домена
                bool isValid;

                if (IsComputerInDomain())
                {
                    // Используем PrincipalContext для доменных компьютеров
                    isValid = await ValidateViaPrincipalContextAsync(username, password, config.Domain);
                }
                else
                {
                    // Используем LDAP для недоменных компьютеров
                    isValid = await ValidateViaLdapAsync(username, password, config.Domain);
                }

                if (isValid)
                {
                    var userInfo = await GetUserInfoAsync(username);
                    if (userInfo != null)
                    {
                        var user = new User
                        {
                            Username = username,
                            DisplayName = userInfo.DisplayName,
                            Email = userInfo.Email,
                            Groups = userInfo.Groups,
                            Role = DetermineUserRole(userInfo.Groups, config),
                            IsActive = userInfo.IsEnabled
                        };

                        _logger.LogInformation("User {Username} authenticated successfully", username);
                        return AuthenticationResult.Success(user, AuthenticationType.DomainLDAP, config.Domain);
                    }
                }

                _logger.LogWarning("Authentication failed for user {Username}", username);
                return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, "Неверные учетные данные");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating user {Username}", username);
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        /// <summary>
        /// Получить информацию о пользователе из AD
        /// </summary>
        public async Task<AdUserInfo?> GetUserInfoAsync(string username)
        {
            try
            {
                var config = _configService.GetConfiguration();

                // Пробуем сначала через PrincipalContext (для доменных компьютеров)
                if (IsComputerInDomain())
                {
                    var userInfo = await GetUserViaPrincipalContextAsync(username, config.Domain);
                    if (userInfo != null)
                        return userInfo;
                }

                // Если не получилось, пробуем через LDAP
                return await GetUserViaLdapAsync(username, config.Domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info for {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// Получить группы пользователя
        /// </summary>
        public async Task<List<string>> GetUserGroupsAsync(string username)
        {
            try
            {
                var userInfo = await GetUserInfoAsync(username);
                return userInfo?.Groups ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups for user {Username}", username);
                return new List<string>();
            }
        }

        /// <summary>
        /// Проверить принадлежность к группе
        /// </summary>
        public async Task<bool> IsUserInGroupAsync(string username, string groupName)
        {
            try
            {
                var userGroups = await GetUserGroupsAsync(username);
                return userGroups.Any(g => g.Equals(groupName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking group membership for user {Username}, group {GroupName}", username, groupName);
                return false;
            }
        }

        /// <summary>
        /// Найти пользователей в AD
        /// </summary>
        public async Task<List<AdUserInfo>> SearchUsersAsync(string searchTerm, int maxResults = 50)
        {
            try
            {
                var config = _configService.GetConfiguration();
                return await SearchUsersViaLdapAsync(searchTerm, config.Domain, maxResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search users with term '{SearchTerm}'", searchTerm);
                return new List<AdUserInfo>();
            }
        }

        /// <summary>
        /// Получить список групп в AD
        /// </summary>
        public async Task<List<string>> GetGroupsAsync()
        {
            var groups = new List<string>();

            try
            {
                var config = _configService.GetConfiguration();

                if (IsComputerInDomain())
                {
                    using var context = CreatePrincipalContext(config.Domain);
                    using var groupSearcher = new GroupPrincipal(context);
                    using var searcher = new PrincipalSearcher(groupSearcher);

                    var results = searcher.FindAll();
                    groups.AddRange(results.OfType<GroupPrincipal>().Select(g => g.Name).Where(name => !string.IsNullOrEmpty(name))!);
                }
                else
                {
                    // LDAP поиск групп для недоменных компьютеров
                    groups = await GetGroupsViaLdapAsync(config.Domain);
                }

                _logger.LogDebug("Retrieved {GroupCount} groups from AD", groups.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups from AD");
            }

            return groups;
        }

        /// <summary>
        /// Проверить доступность домена
        /// </summary>
        public async Task<bool> IsDomainAvailableAsync()
        {
            try
            {
                var config = _configService.GetConfiguration();
                
                // В тестовом режиме всегда возвращаем true
                if (config.TestMode)
                {
                    _logger.LogInformation("Domain availability check skipped (Test Mode enabled)");
                    return true;
                }
                
                return await TestConnectionAsync(config.LdapServer, config.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking domain availability");
                return false;
            }
        }

        #region Principal Context Methods (для доменных компьютеров)

        /// <summary>
        /// Получение пользователя через PrincipalContext
        /// </summary>
        private async Task<AdUserInfo?> GetUserViaPrincipalContextAsync(string username, string domain)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var context = CreatePrincipalContext(domain);
                    using var userPrincipal = UserPrincipal.FindByIdentity(context, username);

                    if (userPrincipal == null)
                        return null;

                    var groups = userPrincipal.GetAuthorizationGroups()
                        .OfType<GroupPrincipal>()
                        .Select(g => g.Name)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToList()!;

                    var userInfo = new AdUserInfo
                    {
                        Username = userPrincipal.SamAccountName ?? username,
                        DisplayName = userPrincipal.DisplayName ?? userPrincipal.SamAccountName ?? username,
                        FirstName = userPrincipal.GivenName ?? string.Empty,
                        LastName = userPrincipal.Surname ?? string.Empty,
                        Email = userPrincipal.EmailAddress ?? string.Empty,
                        IsEnabled = userPrincipal.Enabled ?? false,
                        LastLogon = userPrincipal.LastLogon,
                        Groups = groups
                    };

                    // Получаем дополнительную информацию через DirectoryEntry
                    if (userPrincipal.GetUnderlyingObject() is DirectoryEntry directoryEntry)
                    {
                        userInfo.Department = GetPropertyValue(directoryEntry, "department");
                        userInfo.Title = GetPropertyValue(directoryEntry, "title");
                        userInfo.Phone = GetPropertyValue(directoryEntry, "telephoneNumber");

                        // Безопасное извлечение даты последней смены пароля
                        userInfo.PasswordLastSet = GetFileTimeProperty(directoryEntry, "pwdLastSet");
                    }

                    return userInfo;
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug("PrincipalContext method failed for user {Username}: {Error}", username, ex.Message);
                return null;
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
            return new PrincipalContext(ContextType.Domain, domain);
        }

        #endregion

        #region LDAP Methods (для недоменных компьютеров)

        /// <summary>
        /// Получение пользователя через LDAP
        /// </summary>
        private async Task<AdUserInfo?> GetUserViaLdapAsync(string username, string domain)
        {
            try
            {
                using var connection = CreateLdapConnection();

                var searchBase = GetDomainDistinguishedName(domain);
                var filter = $"(&(objectClass=user)(sAMAccountName={username}))";
                var attributes = new[] { "sAMAccountName", "displayName", "givenName", "sn", "mail", "memberOf", "userAccountControl", "department", "title", "telephoneNumber" };

                var searchRequest = new SearchRequest(searchBase, filter, System.DirectoryServices.Protocols.SearchScope.Subtree, attributes);

                var response = await Task.Run(() => (SearchResponse)connection.SendRequest(searchRequest));

                if (response.Entries.Count == 0)
                    return null;

                var entry = response.Entries[0];
                return MapLdapEntryToAdUserInfo(entry);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("LDAP user lookup failed for {Username}: {Error}", username, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Проверка учетных данных через LDAP
        /// </summary>
        private async Task<bool> ValidateViaLdapAsync(string username, string password, string domain)
        {
            try
            {
                var config = _configService.GetConfiguration();
                var server = config.LdapServer ?? domain;

                using var connection = new LdapConnection(new LdapDirectoryIdentifier(server, config.Port));

                connection.SessionOptions.ProtocolVersion = 3;
                connection.AuthType = AuthType.Basic;

                if (config.UseTLS)
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
        private async Task<List<AdUserInfo>> SearchUsersViaLdapAsync(string searchTerm, string domain, int maxResults)
        {
            try
            {
                using var connection = CreateLdapConnection();

                var searchBase = GetDomainDistinguishedName(domain);
                var filter = $"(&(objectClass=user)(|(sAMAccountName=*{searchTerm}*)(displayName=*{searchTerm}*)(mail=*{searchTerm}*)))";
                var attributes = new[] { "sAMAccountName", "displayName", "givenName", "sn", "mail", "userAccountControl" };

                var searchRequest = new SearchRequest(searchBase, filter, System.DirectoryServices.Protocols.SearchScope.Subtree, attributes);
                searchRequest.SizeLimit = maxResults;

                var response = await Task.Run(() => (SearchResponse)connection.SendRequest(searchRequest));

                var users = new List<AdUserInfo>();
                foreach (SearchResultEntry entry in response.Entries)
                {
                    var user = MapLdapEntryToAdUserInfo(entry);
                    if (user != null)
                        users.Add(user);
                }

                return users;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("LDAP user search failed for term '{SearchTerm}': {Error}", searchTerm, ex.Message);
                return new List<AdUserInfo>();
            }
        }

        /// <summary>
        /// Получение групп через LDAP
        /// </summary>
        private async Task<List<string>> GetGroupsViaLdapAsync(string domain)
        {
            try
            {
                using var connection = CreateLdapConnection();

                var searchBase = GetDomainDistinguishedName(domain);
                var filter = "(objectClass=group)";
                var attributes = new[] { "cn" };

                var searchRequest = new SearchRequest(searchBase, filter, System.DirectoryServices.Protocols.SearchScope.Subtree, attributes);
                searchRequest.SizeLimit = 1000; // Ограничиваем количество групп

                var response = await Task.Run(() => (SearchResponse)connection.SendRequest(searchRequest));

                var groups = new List<string>();
                foreach (SearchResultEntry entry in response.Entries)
                {
                    var groupName = GetAttributeValue(entry, "cn");
                    if (!string.IsNullOrEmpty(groupName))
                        groups.Add(groupName);
                }

                return groups;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("LDAP groups lookup failed: {Error}", ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Создание LDAP подключения
        /// </summary>
        private LdapConnection CreateLdapConnection()
        {
            var config = _configService.GetConfiguration();
            var server = config.LdapServer ?? config.Domain;
            var connection = new LdapConnection(new LdapDirectoryIdentifier(server, config.Port));

            connection.SessionOptions.ProtocolVersion = 3;
            connection.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

            if (config.UseTLS)
            {
                connection.SessionOptions.SecureSocketLayer = true;
            }

            return connection;
        }

        #endregion

        #region Mapping Methods

        /// <summary>
        /// Преобразование LDAP записи в AdUserInfo
        /// </summary>
        private AdUserInfo? MapLdapEntryToAdUserInfo(SearchResultEntry entry)
        {
            var username = GetAttributeValue(entry, "sAMAccountName");
            if (string.IsNullOrEmpty(username))
                return null;

            var displayName = GetAttributeValue(entry, "displayName") ?? username;
            var firstName = GetAttributeValue(entry, "givenName") ?? string.Empty;
            var lastName = GetAttributeValue(entry, "sn") ?? string.Empty;
            var email = GetAttributeValue(entry, "mail") ?? string.Empty;
            var department = GetAttributeValue(entry, "department") ?? string.Empty;
            var title = GetAttributeValue(entry, "title") ?? string.Empty;
            var phone = GetAttributeValue(entry, "telephoneNumber") ?? string.Empty;

            // Проверяем, активен ли аккаунт
            var userAccountControl = GetAttributeValue(entry, "userAccountControl");
            var isActive = true;
            if (int.TryParse(userAccountControl, out var uac))
            {
                // Бит 2 (0x0002) означает, что аккаунт отключен
                isActive = (uac & 0x0002) == 0;
            }

            // Получаем группы из memberOf
            var groups = new List<string>();
            if (entry.Attributes.Contains("memberOf"))
            {
                var memberOf = entry.Attributes["memberOf"];
                for (int i = 0; i < memberOf.Count; i++)
                {
                    var dn = (string)memberOf[i]!;
                    var groupName = ExtractCnFromDn(dn);
                    if (!string.IsNullOrEmpty(groupName))
                        groups.Add(groupName);
                }
            }

            return new AdUserInfo
            {
                Username = username,
                DisplayName = displayName,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Department = department,
                Title = title,
                Phone = phone,
                Groups = groups,
                IsEnabled = isActive,
                LastLogon = null // LDAP не всегда предоставляет эту информацию
            };
        }

        /// <summary>
        /// Получение значения атрибута из LDAP записи
        /// </summary>
        private static string? GetAttributeValue(SearchResultEntry entry, string attributeName)
        {
            if (entry.Attributes.Contains(attributeName) && entry.Attributes[attributeName].Count > 0)
            {
                return entry.Attributes[attributeName][0]?.ToString();
            }
            return null;
        }

        /// <summary>
        /// Извлечение CN из Distinguished Name
        /// </summary>
        private static string? ExtractCnFromDn(string dn)
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
        /// Получение значения свойства из DirectoryEntry
        /// </summary>
        private string GetPropertyValue(DirectoryEntry entry, string propertyName)
        {
            try
            {
                if (entry.Properties[propertyName].Value != null)
                {
                    return entry.Properties[propertyName].Value?.ToString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting property {PropertyName}", propertyName);
            }

            return string.Empty;
        }

        /// <summary>
        /// Определение роли пользователя на основе групп AD
        /// </summary>
        private UserRole DetermineUserRole(List<string> groups, ActiveDirectoryConfiguration config)
        {
            if (groups == null || groups.Count == 0)
                return UserRole.Standard;

            // Используем базовые значения если конфигурация пуста
            var adminGroups = !string.IsNullOrEmpty(config.AdminGroups)
                ? config.AdminGroups.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string> { "LauncherAdmins", "Domain Admins" };

            var powerUserGroups = !string.IsNullOrEmpty(config.PowerUserGroups)
                ? config.PowerUserGroups.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string> { "LauncherPowerUsers" };

            if (groups.Any(g => adminGroups.Any(ag => ag.Trim().Equals(g, StringComparison.OrdinalIgnoreCase))))
            {
                return UserRole.Administrator;
            }

            if (groups.Any(g => powerUserGroups.Any(pg => pg.Trim().Equals(g, StringComparison.OrdinalIgnoreCase))))
            {
                return UserRole.PowerUser;
            }

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

        /// <summary>
        /// Проверка, находится ли компьютер в домене
        /// </summary>
        private bool IsComputerInDomain()
        {
            try
            {
                var domain = Environment.UserDomainName;
                var computerName = Environment.MachineName;

                // Если имя домена совпадает с именем компьютера, то компьютер не в домене
                return !string.Equals(domain, computerName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Безопасное извлечение long значения из AD Property
        /// </summary>
        private long? GetLongProperty(DirectoryEntry entry, string propertyName)
        {
            try
            {
                if (entry.Properties[propertyName].Value == null)
                    return null;

                var rawValue = entry.Properties[propertyName].Value;
                
                // Проверка на COM объект через тип
                if (rawValue?.GetType().IsCOMObject == true)
                {
                    if (long.TryParse(rawValue.ToString(), out var parsedValue))
                        return parsedValue;
                }
                else if (rawValue is long longValue)
                {
                    return longValue;
                }
                else if (long.TryParse(rawValue?.ToString(), out var stringParsedValue))
                {
                    return stringParsedValue;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Безопасное извлечение DateTime из AD FileTime Property
        /// </summary>
        private DateTime? GetFileTimeProperty(DirectoryEntry entry, string propertyName)
        {
            try
            {
                var longValue = GetLongProperty(entry, propertyName);
                if (longValue.HasValue && longValue.Value > 0)
                {
                    return DateTime.FromFileTime(longValue.Value);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}