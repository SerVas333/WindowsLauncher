using Microsoft.Extensions.DependencyInjection;

namespace WindowsLauncher.UI.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods для упрощения работы с IServiceScopeFactory
    /// Помогает избежать captive dependency проблем
    /// </summary>
    public static class ServiceScopeExtensions
    {
        /// <summary>
        /// Создает scoped service через временный scope
        /// Использовать для получения Scoped сервисов из Singleton/Transient компонентов
        /// </summary>
        /// <typeparam name="T">Тип сервиса для создания</typeparam>
        /// <param name="scopeFactory">Фабрика scope'ов</param>
        /// <returns>Экземпляр сервиса</returns>
        /// <example>
        /// // Вместо:
        /// using var scope = _scopeFactory.CreateScope();
        /// var service = scope.ServiceProvider.GetRequiredService&lt;IMyService&gt;();
        /// 
        /// // Используйте:
        /// var service = _scopeFactory.CreateScopedService&lt;IMyService&gt;();
        /// </example>
        public static T CreateScopedService<T>(this IServiceScopeFactory scopeFactory) where T : class
        {
            using var scope = scopeFactory.CreateScope();
            return scope.ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Создает scoped service через временный scope с возможностью получения null
        /// </summary>
        /// <typeparam name="T">Тип сервиса для создания</typeparam>
        /// <param name="scopeFactory">Фабрика scope'ов</param>
        /// <returns>Экземпляр сервиса или null если не найден</returns>
        public static T? GetScopedService<T>(this IServiceScopeFactory scopeFactory) where T : class
        {
            using var scope = scopeFactory.CreateScope();
            return scope.ServiceProvider.GetService<T>();
        }

        /// <summary>
        /// Выполняет действие с scoped service
        /// Автоматически управляет жизненным циклом scope
        /// </summary>
        /// <typeparam name="T">Тип сервиса</typeparam>
        /// <param name="scopeFactory">Фабрика scope'ов</param>
        /// <param name="action">Действие для выполнения с сервисом</param>
        /// <example>
        /// _scopeFactory.WithScopedService&lt;IEmailService&gt;(emailService => 
        /// {
        ///     emailService.SendEmail(...);
        /// });
        /// </example>
        public static void WithScopedService<T>(this IServiceScopeFactory scopeFactory, System.Action<T> action) where T : class
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<T>();
            action(service);
        }

        /// <summary>
        /// Выполняет асинхронное действие с scoped service
        /// </summary>
        /// <typeparam name="T">Тип сервиса</typeparam>
        /// <param name="scopeFactory">Фабрика scope'ов</param>
        /// <param name="action">Асинхронное действие для выполнения</param>
        public static async System.Threading.Tasks.Task WithScopedServiceAsync<T>(this IServiceScopeFactory scopeFactory, System.Func<T, System.Threading.Tasks.Task> action) where T : class
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<T>();
            await action(service);
        }

        /// <summary>
        /// Выполняет функцию с scoped service и возвращает результат
        /// </summary>
        /// <typeparam name="TService">Тип сервиса</typeparam>
        /// <typeparam name="TResult">Тип результата</typeparam>
        /// <param name="scopeFactory">Фабрика scope'ов</param>
        /// <param name="func">Функция для выполнения</param>
        /// <returns>Результат функции</returns>
        public static TResult WithScopedService<TService, TResult>(this IServiceScopeFactory scopeFactory, System.Func<TService, TResult> func) where TService : class
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<TService>();
            return func(service);
        }

        /// <summary>
        /// Выполняет асинхронную функцию с scoped service и возвращает результат
        /// </summary>
        /// <typeparam name="TService">Тип сервиса</typeparam>
        /// <typeparam name="TResult">Тип результата</typeparam>
        /// <param name="scopeFactory">Фабрика scope'ов</param>
        /// <param name="func">Асинхронная функция для выполнения</param>
        /// <returns>Результат функции</returns>
        public static async System.Threading.Tasks.Task<TResult> WithScopedServiceAsync<TService, TResult>(this IServiceScopeFactory scopeFactory, System.Func<TService, System.Threading.Tasks.Task<TResult>> func) where TService : class
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<TService>();
            return await func(service);
        }
    }
}