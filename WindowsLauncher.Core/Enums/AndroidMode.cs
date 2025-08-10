namespace WindowsLauncher.Core.Enums
{
    /// <summary>
    /// Режимы работы Android подсистемы (WSA)
    /// </summary>
    public enum AndroidMode
    {
        /// <summary>
        /// Полное отключение Android функций - экономия ресурсов на слабых системах
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Запуск WSA только при необходимости - оптимальный баланс производительности и ресурсов
        /// </summary>
        OnDemand = 1,

        /// <summary>
        /// Предварительный запуск WSA в фоне - максимальная производительность за счет потребления ресурсов
        /// </summary>
        Preload = 2
    }
}