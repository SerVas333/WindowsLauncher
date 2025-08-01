using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle.Events;

namespace WindowsLauncher.Services.Lifecycle.Management
{
    /// <summary>
    /// Менеджер экземпляров приложений для управления коллекцией запущенных приложений
    /// Предоставляет потокобезопасные операции с коллекцией экземпляров
    /// </summary>
    public class ApplicationInstanceManager : IApplicationInstanceManager
    {
        private readonly ILogger<ApplicationInstanceManager> _logger;
        private readonly ConcurrentDictionary<string, ApplicationInstance> _instances;
        private readonly SemaphoreSlim _semaphore;
        private readonly object _eventLock = new object();
        
        // События
        public event EventHandler<ApplicationInstanceEventArgs>? InstanceAdded;
        public event EventHandler<ApplicationInstanceEventArgs>? InstanceRemoved;  
        public event EventHandler<ApplicationInstanceEventArgs>? InstanceUpdated;
        public event EventHandler<EventArgs>? CollectionCleared;
        
        public ApplicationInstanceManager(ILogger<ApplicationInstanceManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _instances = new ConcurrentDictionary<string, ApplicationInstance>();
            _semaphore = new SemaphoreSlim(1, 1);
        }
        
        #region Управление экземплярами
        
        public async Task<bool> AddInstanceAsync(ApplicationInstance instance)
        {
            if (instance == null)
            {
                _logger.LogWarning("Attempted to add null instance");
                return false;
            }
            
            if (string.IsNullOrEmpty(instance.InstanceId))
            {
                _logger.LogWarning("Attempted to add instance with empty InstanceId");
                return false;
            }
            
            await _semaphore.WaitAsync();
            try
            {
                bool added = _instances.TryAdd(instance.InstanceId, instance);
                
                if (added)
                {
                    _logger.LogDebug("Added instance {InstanceId} ({AppName}, PID: {ProcessId})", 
                        instance.InstanceId, instance.Application?.Name, instance.ProcessId);
                    
                    // Генерируем событие
                    RaiseInstanceAdded(instance);
                }
                else
                {
                    _logger.LogWarning("Failed to add instance {InstanceId} - already exists", instance.InstanceId);
                }
                
                return added;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<ApplicationInstance?> RemoveInstanceAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                _logger.LogWarning("Attempted to remove instance with empty instanceId");
                return null;
            }
            
            await _semaphore.WaitAsync();
            try
            {
                bool removed = _instances.TryRemove(instanceId, out var instance);
                
                if (removed && instance != null)
                {
                    _logger.LogDebug("Removed instance {InstanceId} ({AppName}, PID: {ProcessId})", 
                        instanceId, instance.Application?.Name, instance.ProcessId);
                    
                    // Генерируем событие
                    RaiseInstanceRemoved(instance);
                    
                    return instance;
                }
                else
                {
                    _logger.LogDebug("Instance {InstanceId} not found for removal", instanceId);
                    return null;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<bool> UpdateInstanceAsync(ApplicationInstance instance)
        {
            if (instance == null || string.IsNullOrEmpty(instance.InstanceId))
            {
                _logger.LogWarning("Attempted to update invalid instance");
                return false;
            }
            
            await _semaphore.WaitAsync();
            try
            {
                if (_instances.ContainsKey(instance.InstanceId))
                {
                    // Обновляем время последнего обновления
                    instance.LastUpdate = DateTime.Now;
                    
                    // Заменяем экземпляр
                    _instances[instance.InstanceId] = instance;
                    
                    _logger.LogTrace("Updated instance {InstanceId} ({AppName})", 
                        instance.InstanceId, instance.Application?.Name);
                    
                    // Генерируем событие
                    RaiseInstanceUpdated(instance);
                    
                    return true;
                }
                else
                {
                    _logger.LogWarning("Cannot update instance {InstanceId} - not found", instance.InstanceId);
                    return false;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<bool> ContainsInstanceAsync(string instanceId)
        {
            await Task.CompletedTask;
            
            if (string.IsNullOrEmpty(instanceId))
                return false;
            
            return _instances.ContainsKey(instanceId);
        }
        
        #endregion
        
        #region Получение экземпляров
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetAllInstancesAsync()
        {
            await Task.CompletedTask;
            
            // ConcurrentDictionary.Values уже потокобезопасен для чтения
            return _instances.Values.ToList().AsReadOnly();
        }
        
        public async Task<ApplicationInstance?> GetInstanceAsync(string instanceId)
        {
            await Task.CompletedTask;
            
            if (string.IsNullOrEmpty(instanceId))
                return null;
            
            return _instances.TryGetValue(instanceId, out var instance) ? instance : null;
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetInstancesByApplicationIdAsync(int applicationId)
        {
            await Task.CompletedTask;
            
            var matchingInstances = _instances.Values
                .Where(instance => instance.Application?.Id == applicationId)
                .ToList();
            
            _logger.LogTrace("Found {Count} instances for application {ApplicationId}", 
                matchingInstances.Count, applicationId);
            
            return matchingInstances.AsReadOnly();
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetInstancesByProcessIdAsync(int processId)
        {
            await Task.CompletedTask;
            
            var matchingInstances = _instances.Values
                .Where(instance => instance.ProcessId == processId)
                .ToList();
            
            return matchingInstances.AsReadOnly();
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetInstancesByUserAsync(string username)
        {
            await Task.CompletedTask;
            
            if (string.IsNullOrEmpty(username))
                return new List<ApplicationInstance>().AsReadOnly();
            
            var matchingInstances = _instances.Values
                .Where(instance => string.Equals(instance.LaunchedBy, username, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            return matchingInstances.AsReadOnly();
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetInstancesByStateAsync(ApplicationState state)
        {
            await Task.CompletedTask;
            
            var matchingInstances = _instances.Values
                .Where(instance => instance.State == state)
                .ToList();
            
            return matchingInstances.AsReadOnly();
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetInstancesByTypeAsync(ApplicationType applicationType)
        {
            await Task.CompletedTask;
            
            var matchingInstances = _instances.Values
                .Where(instance => instance.Application?.Type == applicationType)
                .ToList();
            
            return matchingInstances.AsReadOnly();
        }
        
        #endregion
        
        #region Фильтрация и поиск
        
        public async Task<IReadOnlyList<ApplicationInstance>> FindInstancesAsync(Func<ApplicationInstance, bool> predicate)
        {
            await Task.CompletedTask;
            
            try
            {
                var matchingInstances = _instances.Values
                    .Where(predicate)
                    .ToList();
                
                return matchingInstances.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing predicate in FindInstancesAsync");
                return new List<ApplicationInstance>().AsReadOnly();
            }
        }
        
        public async Task<ApplicationInstance?> FindFirstInstanceAsync(Func<ApplicationInstance, bool> predicate)
        {
            await Task.CompletedTask;
            
            try
            {
                return _instances.Values.FirstOrDefault(predicate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing predicate in FindFirstInstanceAsync");
                return null;
            }
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync()
        {
            await Task.CompletedTask;
            
            var activeInstances = _instances.Values
                .Where(instance => instance.IsActiveInstance())
                .ToList();
            
            return activeInstances.AsReadOnly();
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetTerminatedInstancesAsync()
        {
            await Task.CompletedTask;
            
            var terminatedInstances = _instances.Values
                .Where(instance => instance.State.IsTerminated())
                .ToList();
            
            return terminatedInstances.AsReadOnly();
        }
        
        #endregion
        
        #region Статистика и мониторинг
        
        public async Task<int> GetTotalCountAsync()
        {
            await Task.CompletedTask;
            return _instances.Count;
        }
        
        public async Task<int> GetActiveCountAsync()
        {
            await Task.CompletedTask;
            
            return _instances.Values.Count(instance => instance.IsActiveInstance());
        }
        
        public async Task<long> GetTotalMemoryUsageAsync()
        {
            await Task.CompletedTask;
            
            return _instances.Values
                .Where(instance => instance.IsActiveInstance())
                .Sum(instance => instance.MemoryUsageMB);
        }
        
        public async Task<Dictionary<ApplicationType, int>> GetTypeStatisticsAsync()
        {
            await Task.CompletedTask;
            
            var statistics = new Dictionary<ApplicationType, int>();
            
            var groupedByType = _instances.Values
                .Where(instance => instance.Application != null)
                .GroupBy(instance => instance.Application.Type);
            
            foreach (var group in groupedByType)
            {
                statistics[group.Key] = group.Count();
            }
            
            return statistics;
        }
        
        public async Task<Dictionary<ApplicationState, int>> GetStateStatisticsAsync()
        {
            await Task.CompletedTask;
            
            var statistics = new Dictionary<ApplicationState, int>();
            
            var groupedByState = _instances.Values.GroupBy(instance => instance.State);
            
            foreach (var group in groupedByState)
            {
                statistics[group.Key] = group.Count();
            }
            
            return statistics;
        }
        
        #endregion
        
        #region Очистка и обслуживание
        
        public async Task<int> CleanupTerminatedInstancesAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var terminatedInstanceIds = _instances.Values
                    .Where(instance => instance.State.IsTerminated())
                    .Select(instance => instance.InstanceId)
                    .ToList();
                
                int removedCount = 0;
                
                foreach (var instanceId in terminatedInstanceIds)
                {
                    if (_instances.TryRemove(instanceId, out var removedInstance))
                    {
                        removedCount++;
                        
                        _logger.LogDebug("Cleaned up terminated instance {InstanceId} ({AppName})", 
                            instanceId, removedInstance.Application?.Name);
                        
                        // Генерируем событие удаления
                        RaiseInstanceRemoved(removedInstance);
                    }
                }
                
                if (removedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {RemovedCount} terminated instances", removedCount);
                }
                
                return removedCount;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<int> CleanupOldInstancesAsync(TimeSpan maxAge)
        {
            await _semaphore.WaitAsync();
            try
            {
                var cutoffTime = DateTime.Now - maxAge;
                
                var oldInstanceIds = _instances.Values
                    .Where(instance => instance.StartTime < cutoffTime)
                    .Select(instance => instance.InstanceId)
                    .ToList();
                
                int removedCount = 0;
                
                foreach (var instanceId in oldInstanceIds)
                {
                    if (_instances.TryRemove(instanceId, out var removedInstance))
                    {
                        removedCount++;
                        
                        _logger.LogDebug("Cleaned up old instance {InstanceId} ({AppName}, Age: {Age})", 
                            instanceId, removedInstance.Application?.Name, DateTime.Now - removedInstance.StartTime);
                        
                        // Генерируем событие удаления
                        RaiseInstanceRemoved(removedInstance);
                    }
                }
                
                if (removedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {RemovedCount} old instances (older than {MaxAge})", 
                        removedCount, maxAge);
                }
                
                return removedCount;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<int> ClearAllInstancesAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                int count = _instances.Count;
                
                // Получаем копию всех экземпляров для событий
                var allInstances = _instances.Values.ToList();
                
                _instances.Clear();
                
                _logger.LogInformation("Cleared all {Count} instances from collection", count);
                
                // Генерируем события удаления для всех экземпляров
                foreach (var instance in allInstances)
                {
                    RaiseInstanceRemoved(instance);
                }
                
                // Генерируем событие очистки коллекции
                RaiseCollectionCleared();
                
                return count;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> ValidateInstancesAsync()
        {
            await Task.CompletedTask;
            
            var problematicInstances = new List<ApplicationInstance>();
            
            foreach (var instance in _instances.Values)
            {
                try
                {
                    // Проверяем базовую целостность
                    bool hasIssues = false;
                    
                    if (string.IsNullOrEmpty(instance.InstanceId))
                    {
                        hasIssues = true;
                        _logger.LogWarning("Instance has empty InstanceId: {AppName}", instance.Application?.Name);
                    }
                    
                    if (instance.Application == null)
                    {
                        hasIssues = true;
                        _logger.LogWarning("Instance {InstanceId} has null Application", instance.InstanceId);
                    }
                    
                    if (instance.ProcessId <= 0)
                    {
                        hasIssues = true;
                        _logger.LogWarning("Instance {InstanceId} has invalid ProcessId: {ProcessId}", 
                            instance.InstanceId, instance.ProcessId);
                    }
                    
                    if (string.IsNullOrEmpty(instance.LaunchedBy))
                    {
                        hasIssues = true;
                        _logger.LogWarning("Instance {InstanceId} has empty LaunchedBy", instance.InstanceId);
                    }
                    
                    // Проверяем логику состояний
                    if (instance.State.IsTerminated() && instance.IsActiveInstance())
                    {
                        hasIssues = true;
                        _logger.LogWarning("Instance {InstanceId} has inconsistent state: {State} but IsActiveInstance=true", 
                            instance.InstanceId, instance.State);
                    }
                    
                    // Проверяем устаревшие данные
                    if (instance.LastUpdate < DateTime.Now.AddHours(-1))
                    {
                        hasIssues = true;
                        _logger.LogWarning("Instance {InstanceId} has stale data (last update: {LastUpdate})", 
                            instance.InstanceId, instance.LastUpdate);
                    }
                    
                    if (hasIssues)
                    {
                        problematicInstances.Add(instance);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating instance {InstanceId}", instance.InstanceId);
                    problematicInstances.Add(instance);
                }
            }
            
            if (problematicInstances.Count > 0)
            {
                _logger.LogWarning("Found {Count} problematic instances during validation", problematicInstances.Count);
            }
            
            return problematicInstances.AsReadOnly();
        }
        
        #endregion
        
        #region События
        
        private void RaiseInstanceAdded(ApplicationInstance instance)
        {
            try
            {
                lock (_eventLock)
                {
                    var args = ApplicationInstanceEventArgs.Started(instance, "ApplicationInstanceManager");
                    InstanceAdded?.Invoke(this, args);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising InstanceAdded event for {InstanceId}", instance.InstanceId);
            }
        }
        
        private void RaiseInstanceRemoved(ApplicationInstance instance)
        {
            try
            {
                lock (_eventLock)
                {
                    var args = ApplicationInstanceEventArgs.Stopped(instance, "Removed from collection", "ApplicationInstanceManager");
                    InstanceRemoved?.Invoke(this, args);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising InstanceRemoved event for {InstanceId}", instance.InstanceId);
            }
        }
        
        private void RaiseInstanceUpdated(ApplicationInstance instance)
        {
            try
            {
                lock (_eventLock)
                {
                    var args = ApplicationInstanceEventArgs.Updated(instance, "Instance data updated", "ApplicationInstanceManager");
                    InstanceUpdated?.Invoke(this, args);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising InstanceUpdated event for {InstanceId}", instance.InstanceId);
            }
        }
        
        private void RaiseCollectionCleared()
        {
            try
            {
                lock (_eventLock)
                {
                    CollectionCleared?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising CollectionCleared event");
            }
        }
        
        #endregion
        
        #region Отладочные методы
        
        /// <summary>
        /// Получить отладочную информацию о состоянии коллекции
        /// </summary>
        /// <returns>Строка с отладочной информацией</returns>
        public string GetDebugInfo()
        {
            var totalCount = _instances.Count;
            var activeCount = _instances.Values.Count(i => i.IsActiveInstance());
            var terminatedCount = _instances.Values.Count(i => i.State.IsTerminated());
            
            var typeStats = _instances.Values
                .Where(i => i.Application != null)
                .GroupBy(i => i.Application.Type)
                .ToDictionary(g => g.Key, g => g.Count());
            
            var stateStats = _instances.Values
                .GroupBy(i => i.State)
                .ToDictionary(g => g.Key, g => g.Count());
            
            return $"ApplicationInstanceManager Debug Info:\n" +
                   $"  Total Instances: {totalCount}\n" +
                   $"  Active: {activeCount}, Terminated: {terminatedCount}\n" +
                   $"  By Type: {string.Join(", ", typeStats.Select(kvp => $"{kvp.Key}={kvp.Value}"))}\n" +
                   $"  By State: {string.Join(", ", stateStats.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
        }
        
        #endregion
    }
}