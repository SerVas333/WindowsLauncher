// ===== WindowsLauncher.UI/Infrastructure/Services/IDatabaseInitializer.cs =====
using System.Threading.Tasks;

namespace WindowsLauncher.UI.Infrastructure.Services
{
    /// <summary>
    /// Интерфейс для инициализации базы данных
    /// </summary>
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// Инициализация базы данных (миграции, seed данные)
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Проверка готовности базы данных
        /// </summary>
        Task<bool> IsDatabaseReadyAsync();
    }
}